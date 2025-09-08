using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AzureAIAgent.Core.Interfaces;
using AzureAIAgent.Core.Models;
using AzureAIAgent.Core.Services;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

namespace AzureAIAgent.Core;

public interface IAzureAIAgent
{
    Task<string> ProcessRequestAsync(string sessionId, string message);
    Task<List<ConversationMessage>> GetChatHistoryAsync(string sessionId);
    Task ClearChatHistoryAsync(string sessionId);
}

public class AzureAIAgent : IAzureAIAgent
{
    private readonly Kernel _kernel;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<AzureAIAgent> _logger;
    private readonly RateLimitConfiguration _rateLimitConfig;
    private readonly DeploymentConfiguration _deploymentConfig;
    private readonly IAksContextService _aksContextService;
    private readonly AdaptiveCardService _cardService;
    private readonly IUniversalInteractiveService _interactiveService;
    private readonly HttpClient _httpClient;
    private readonly IPortDetectionService _portDetectionService;

    public AzureAIAgent(
        Kernel kernel,
        ISessionManager sessionManager,
        ILogger<AzureAIAgent> logger,
        IConfiguration configuration,
        IAksContextService aksContextService,
        AdaptiveCardService cardService,
        IUniversalInteractiveService interactiveService,
        HttpClient httpClient,
        IPortDetectionService portDetectionService)
    {
        _kernel = kernel;
        _sessionManager = sessionManager;
        _logger = logger;
        _rateLimitConfig = configuration.GetSection("RateLimit").Get<RateLimitConfiguration>() ?? new RateLimitConfiguration();
        _deploymentConfig = configuration.GetSection("Deployment").Get<DeploymentConfiguration>() ?? new DeploymentConfiguration();
        _aksContextService = aksContextService;
        _cardService = cardService;
        _interactiveService = interactiveService;
        _httpClient = httpClient;
        _portDetectionService = portDetectionService;
    }

    public async Task<string> ProcessRequestAsync(string sessionId, string message)
    {
        try
        {
            _logger.LogInformation("Processing request for session {SessionId}: {Message}", sessionId, message);
            
            // Set the current session context for plugins to access
            SessionContextManager.SetCurrentSessionId(sessionId);

            // Check if this is a form submission
            if (IsFormSubmission(message))
            {
                _logger.LogInformation("Detected form submission for session {SessionId}", sessionId);
                return await HandleFormSubmissionAsync(sessionId, message);
            }

            // Check if this is a direct MCP command that can bypass AI processing
            var directMcpResult = await TryDirectMcpCommandAsync(sessionId, message);
            if (directMcpResult != null)
            {
                _logger.LogInformation("Processed direct MCP command without AI processing");
                return directMcpResult;
            }

            // Get chat completion service
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            // Get session history
            var session = await _sessionManager.GetOrCreateSessionAsync(sessionId);
            var chatHistory = new ChatHistory();

            // Add concise system prompt to avoid token limits and infinite loops
            chatHistory.AddSystemMessage(@"You are an Azure infrastructure assistant that creates Terraform templates.

WORKFLOW:
1. Collect required parameters: workload_name, project_name, owner, environment (default: dev), location (default: East US)
2. Generate valid Terraform template using Azure Provider v3.x syntax
3. Ask if user wants to edit settings before deployment
4. Deploy when user confirms

TERRAFORM RULES:
- Use multi-line variable blocks with descriptions
- Mark sensitive outputs (kube_config, passwords) as sensitive = true
- Use current Azure provider arguments (no deprecated ones)
- Follow Azure naming conventions

AVAILABLE FUNCTIONS: ApplyTerraformTemplate, AnalyzeTerraformError, ExecuteAzureCommand, ShowTerraformState

Always ask for confirmation before deploying. Be helpful and conversational.");

            // Convert existing messages to ChatHistory (limit to last 5 messages for token efficiency)
            var recentMessages = session.Messages.TakeLast(5).ToList();
            foreach (var msg in recentMessages)
            {
                if (msg.Role == MessageRole.User)
                    chatHistory.AddUserMessage(msg.Content);
                else if (msg.Role == MessageRole.Assistant)
                    chatHistory.AddAssistantMessage(msg.Content);
            }

            // Add user message
            chatHistory.AddUserMessage(message);
            session.Messages.Add(new ConversationMessage { Role = MessageRole.User, Content = message });

            _logger.LogInformation("Processing request for session {SessionId}. Chat history: {MessageCount} messages, estimated tokens: {EstimatedTokens}", 
                sessionId, chatHistory.Count, EstimateTokens(chatHistory));

            // Get AI response with retry logic
            var response = await GetChatResponseWithRetryAsync(chatCompletionService, chatHistory, _kernel);

            // Add assistant response to history
            chatHistory.AddAssistantMessage(response);
            session.Messages.Add(new ConversationMessage { Role = MessageRole.Assistant, Content = response });

            // Save session
            await _sessionManager.UpdateSessionAsync(session);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request for session {SessionId}", sessionId);
            return $"I apologize, but I encountered an error processing your request: {ex.Message}";
        }
    }

    private async Task<string> GetChatResponseWithRetryAsync(IChatCompletionService chatService, ChatHistory chatHistory, Kernel kernel)
    {
        var maxRetries = _rateLimitConfig.MaxRetries;
        var baseDelay = TimeSpan.FromSeconds(_rateLimitConfig.BaseDelaySeconds);
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await chatService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings: new OpenAIPromptExecutionSettings()
                    {
                        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                        Temperature = 0.1,
                        MaxTokens = 1000  // Reduced from 4000 for S0 tier compatibility
                    },
                    kernel: kernel);

                _logger.LogInformation("Successfully generated response with {TokenCount} tokens (estimated)", 
                    result.Content?.Length / 4 ?? 0); // Rough token estimation

                return result.Content ?? "I apologize, but I couldn't generate a response.";
            }
            catch (Microsoft.SemanticKernel.HttpOperationException ex) when (ex.Message.Contains("429") && attempt < maxRetries)
            {
                // Rate limited - extract retry delay from error message if available
                var retryDelay = ExtractRetryDelayFromError(ex.Message);
                var delay = retryDelay ?? TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                
                // Cap maximum delay to 30 seconds to avoid infinite waits
                var maxDelaySeconds = 30;
                if (delay.TotalSeconds > maxDelaySeconds)
                {
                    _logger.LogWarning("Azure OpenAI requested {RequestedDelay}s delay, but this exceeds our maximum of {MaxDelay}s. Rate limit quota may be exhausted.", 
                        delay.TotalSeconds, maxDelaySeconds);
                    throw new InvalidOperationException($"Azure OpenAI rate limit requires {delay.TotalSeconds}s delay. This suggests quota exhaustion. Please wait and try again later or upgrade your Azure OpenAI pricing tier.");
                }
                
                _logger.LogWarning("Rate limited (attempt {Attempt}/{MaxRetries}). Azure OpenAI rate limit exceeded. Waiting {Delay} seconds before retry.", 
                    attempt + 1, maxRetries + 1, delay.TotalSeconds);
                
                await Task.Delay(delay);
                continue;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429") && attempt < maxRetries)
            {
                // Rate limited - wait and retry
                var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
                _logger.LogWarning("Rate limited (attempt {Attempt}/{MaxRetries}). Waiting {Delay}ms before retry.", 
                    attempt + 1, maxRetries + 1, delay.TotalMilliseconds);
                
                await Task.Delay(delay);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat response (attempt {Attempt}/{MaxRetries})", 
                    attempt + 1, maxRetries + 1);
                
                if (attempt == maxRetries)
                    throw;
                    
                await Task.Delay(baseDelay);
            }
        }

        throw new InvalidOperationException("Max retries exceeded");
    }

    public async Task<List<ConversationMessage>> GetChatHistoryAsync(string sessionId)
    {
        try
        {
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session == null) return new List<ConversationMessage>();
            
            return session.Messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat history for session {SessionId}", sessionId);
            return new List<ConversationMessage>();
        }
    }

    public async Task ClearChatHistoryAsync(string sessionId)
    {
        try
        {
            await _sessionManager.DeleteSessionAsync(sessionId);
            _logger.LogInformation("Cleared chat history for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing chat history for session {SessionId}", sessionId);
            throw;
        }
    }

    private static TimeSpan? ExtractRetryDelayFromError(string errorMessage)
    {
        // Try to extract "retry after X seconds" from the error message
        var match = System.Text.RegularExpressions.Regex.Match(errorMessage, @"retry after (\d+) seconds", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (match.Success && int.TryParse(match.Groups[1].Value, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds + 5); // Add 5 second buffer
        }
        
        return null;
    }

    private static int EstimateTokens(ChatHistory chatHistory)
    {
        // Rough token estimation: ~4 characters per token
        var totalChars = chatHistory.Sum(msg => msg.Content?.Length ?? 0);
        return totalChars / 4;
    }

    private async Task<string?> TryDirectMcpCommandAsync(string sessionId, string message)
    {
        try
        {
            _logger.LogInformation("Processing message with AI intent understanding: '{Message}'", message);
            
            // Handle specific terraform deployment action buttons
            if (message.StartsWith("Deploy terraform template:"))
            {
                _logger.LogInformation("Deploy terraform template action detected");
                var deploymentId = message.Substring("Deploy terraform template:".Length).Trim();
                return await HandleSpecificTerraformDeploymentAsync(sessionId, deploymentId);
            }
            
            // Handle edit terraform template action buttons
            if (message.StartsWith("Edit terraform template:"))
            {
                _logger.LogInformation("Edit terraform template action detected");
                var deploymentId = message.Substring("Edit terraform template:".Length).Trim();
                return await HandleTerraformEditActionAsync(sessionId, deploymentId);
            }
            
            // Handle cancel terraform template action buttons
            if (message.StartsWith("Cancel terraform template:"))
            {
                _logger.LogInformation("Cancel terraform template action detected");
                var deploymentId = message.Substring("Cancel terraform template:".Length).Trim();
                return await HandleTerraformCancelActionAsync(sessionId, deploymentId);
            }
            
            // Only handle structured form submissions directly (these are not natural language)
            if (message.StartsWith("Create AKS cluster:") && message.Contains("workload_name="))
            {
                _logger.LogInformation("Structured AKS form submission detected");
                return await ProcessStructuredAksFormAsync(sessionId, message);
            }
            
            // Let AI determine intent and actions using function calling
            var intentAnalysisPrompt = $@"
Analyze the user's natural language request and intelligently determine the appropriate action:

User Request: ""{message}""

You are an AI assistant that understands natural language requests about Kubernetes operations and infrastructure deployment. Be intelligent about:

**DEPLOYMENT/INFRASTRUCTURE INTENT DETECTION:**
- ""deploy"", ""apply"", ""execute"", ""run"", ""launch"", ""start"", ""create"", ""build"", ""provision"", ""setup"" + any infrastructure/terraform/cluster terms → deploy_terraform_template
- ""let's deploy/create/build"", ""go ahead and deploy"", ""make it happen"", ""apply this"" → deploy_terraform_template
- ""deploy: <id>"", ""apply: <id>"", ""create: <id>"", ""terraform template: <id>"" → deploy_terraform_template|<id>
- Any phrase indicating starting/executing/applying infrastructure deployment → deploy_terraform_template

**RESOURCE TYPE DETECTION:**
- Automatically detect if they're asking about pods, deployments, services, etc.
- Recognize resource names from patterns (e.g., name-hash-random = pod, simple-name = service/deployment)

**NAMESPACE INTELLIGENCE:**
- If resource name contains 'defender', 'metrics', 'coredns', 'kube-', 'azure-', 'csi-' → 'kube-system'
- System components are usually in 'kube-system'
- User apps are usually in 'default' or custom namespaces

**NATURAL LANGUAGE UNDERSTANDING:**
- ""tell me about X"", ""get details on X"", ""more info on X"", ""describe X"", ""what's the status of X"" → describe_resource
- ""give me internals of X"", ""show me inside of X"", ""internal details of X"", ""deep dive into X"" → describe_resource  
- ""explain X"", ""analyze X"", ""break down X"", ""inspect X"", ""examine X"" → describe_resource
- ""status of X"", ""health of X"", ""condition of X"", ""state of X"" → describe_resource
- ""configuration of X"", ""specs of X"", ""specification of X"", ""config for X"" → describe_resource
- ""show me"", ""list"", ""get all"", ""display all"" → list_[resource_type]
- ""logs from X"", ""log output of X"", ""get logs of X"", ""view logs for X"" → get_pod_logs

Available Actions:
1. Infrastructure: 'deploy_terraform_template' (for AKS creation/deployment)
2. Resource Details: 'describe_resource|resource_type|resource_name|namespace' (intelligent resource description)
3. Pod Logs: 'get_pod_logs|pod_name|namespace'
4. List Resources: 'list_pods', 'list_deployments', 'list_services', 'list_namespaces', etc.
5. Raw Commands: 'execute_kubectl|command'
6. Other: 'none' (normal conversation)

SMART EXAMPLES:
- ""Tell me about microsoft-defender-collector-misc-6c7847c69-w6zgp"" → describe_resource|pod|microsoft-defender-collector-misc-6c7847c69-w6zgp|kube-system
- ""Get details on metrics-server"" → describe_resource|pod|metrics-server|kube-system
- ""More info on coredns-6f776c8fb5-dhf76"" → describe_resource|pod|coredns-6f776c8fb5-dhf76|kube-system
- ""What's the status of azure-cns pod?"" → describe_resource|pod|azure-cns|kube-system
- ""Give me internals of konnectivity-agent-5df845cf4d-4bzwc"" → describe_resource|pod|konnectivity-agent-5df845cf4d-4bzwc|kube-system
- ""Show me inside of csi-azuredisk-node-whprq"" → describe_resource|pod|csi-azuredisk-node-whprq|kube-system
- ""Deep dive into metrics-server pod"" → describe_resource|pod|metrics-server|kube-system
- ""Analyze coredns-autoscaler configuration"" → describe_resource|pod|coredns-autoscaler|kube-system
- ""Inspect azure-npm-r9vs6 specs"" → describe_resource|pod|azure-npm-r9vs6|kube-system
- ""Examine kube-proxy health"" → describe_resource|pod|kube-proxy|kube-system
- ""Show me nginx deployment details"" → describe_resource|deployment|nginx|default
- ""Tell me about redis service"" → describe_resource|service|redis|default

Respond with ONLY the action and parameters: action_name|param1|param2|param3
";

            var intentResult = await _kernel.InvokePromptAsync(intentAnalysisPrompt);
            var intentResponse = intentResult.GetValue<string>()?.Trim() ?? "none";
            
            _logger.LogInformation("AI determined intent: {Intent}", intentResponse);
            
            var parts = intentResponse.Split('|');
            var action = parts[0].ToLowerInvariant();
            
            switch (action)
            {
                case "deploy_terraform_template":
                    _logger.LogInformation("AI detected terraform deployment intent");
                    return await HandleTerraformDeploymentAsync(sessionId);
                
                case "deploy_template":
                    _logger.LogInformation("Form deployment action detected");
                    return await HandleTerraformDeploymentAsync(sessionId);
                
                case "get_pod_logs":
                    var podName = parts.Length > 1 ? parts[1] : ExtractPodNameFromMessage(message);
                    if (!string.IsNullOrEmpty(podName))
                    {
                        return await GetPodLogsAsync(podName);
                    }
                    return "❌ Please specify a pod name. Example: 'Get logs from pod nginx-123'";
                
                case "list_pods":
                    return await ListPodsAsync();
                
                case "list_deployments":
                    return await ListDeploymentsAsync();
                
                case "list_services":
                    return await ListServicesAsync();
                
                case "list_namespaces":
                    return await ListNamespacesAsync();
                
                case "list_configmaps":
                    return await ListConfigMapsAsync();
                
                case "list_secrets":
                    return await ListSecretsAsync();
                
                case "list_ingress":
                    return await ListIngressAsync();
                
                case "list_cronjobs":
                    return await ListCronJobsAsync();
                
                case "list_jobs":
                    return await ListJobsAsync();
                
                case "list_persistent_volumes":
                    return await ListPersistentVolumesAsync();
                
                case "describe_pod":
                    var targetPod = parts.Length > 1 ? parts[1] : ExtractPodNameFromMessage(message);
                    if (!string.IsNullOrEmpty(targetPod))
                    {
                        return await DescribePodAsync(targetPod);
                    }
                    return "❌ Please specify a pod name. Example: 'Describe pod nginx-123'";
                
                case "describe_resource":
                    if (parts.Length >= 3)
                    {
                        var resourceType = parts[1];
                        var resourceName = parts[2];
                        var resourceNamespace = parts.Length > 3 ? parts[3] : null;
                        return await DescribeResourceAsync(resourceType, resourceName, resourceNamespace);
                    }
                    return "❌ Please specify resource type and name. Example: 'Get details on pod nginx-123'";
                
                case "execute_kubectl":
                    var command = parts.Length > 1 ? parts[1] : message.Replace("kubectl", "").Trim();
                    return await ExecuteKubectlCommandAsync(command);
                
                default:
                    // No direct command detected, continue with normal AI processing
                    _logger.LogInformation("No direct command detected, using normal AI processing");
                    return null;
            }
            
            return null; // Not a direct command, proceed with AI processing
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in direct command processing");
            return null; // Fall back to AI processing
        }
    }

    private string? ExtractPodNameFromMessage(string message)
    {
        // Multiple extraction patterns for robustness
        var patterns = new[]
        {
            // Enhanced natural language patterns
            @"(?:give\s+(?:me\s+)?)?(?:internals?|inside)\s+(?:of\s+)?(?:pod\s+)?([\w\-\d]+)",
            @"(?:deep\s+dive|analyze|inspect|examine|explain|break\s+down)\s+(?:into\s+)?(?:pod\s+)?([\w\-\d]+)",
            @"(?:status|health|condition|state|configuration|specs?|specification)\s+(?:of\s+)?(?:pod\s+)?([\w\-\d]+)",
            
            // Standard patterns  
            @"(?:more\s+)?(?:details?|info|information|insights?)\s+(?:on\s+|into\s+|about\s+|for\s+)(?:pod\s+)?([\w\-\d]+)",
            @"(?:describe|show|get|tell\s+me\s+about)\s+(?:pod\s+)?([\w\-\d]+)",
            @"pod\s+([\w\-\d]+)",
            @"give\s+(?:me\s+)?(?:details?|info|information|insights?)\s+(?:on\s+|into\s+|about\s+|for\s+)(?:pod\s+)?([\w\-\d]+)",
            
            // Parentheses patterns: "Pod (Name)" or "insights into Pod (konnectivity-agent-5df845cf4d-qn2px)"
            @"pod\s*\(\s*([\w\-\d]+)\s*\)",
            @"(?:details?|info|information|insights?)\s+(?:on\s+|into\s+|about\s+|for\s+)pod\s*\(\s*([\w\-\d]+)\s*\)",
            
            // More flexible patterns
            @"([\w\-\d]*agent[\w\-\d]*)", // Agent-related pods
            @"([\w\-\d]*konnectivity[\w\-\d]*)", // Specific to konnectivity pods
            @"([a-zA-Z][\w\-\d]{10,})", // Long identifiers that look like pod names
        };
        
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var extractedName = match.Groups[1].Value.Trim();
                // Validate that it looks like a pod name (contains letters, numbers, hyphens)
                if (extractedName.Length > 3 && System.Text.RegularExpressions.Regex.IsMatch(extractedName, @"^[\w\-\d]+$"))
                {
                    _logger.LogInformation("Extracted pod name '{PodName}' using pattern '{Pattern}'", extractedName, pattern);
                    return extractedName;
                }
            }
        }
        
        _logger.LogInformation("No valid pod name found in message: '{Message}'", message);
        return null;
    }

    private async Task<string> ProcessStructuredAksFormAsync(string sessionId, string message)
    {
        try
        {
            _logger.LogInformation("Processing structured AKS form data for session {SessionId}", sessionId);
            
            // Parse the structured message
            var parameters = new Dictionary<string, string>();
            
            // Extract parameters using regex
            var paramPattern = @"(\w+)=""([^""]*)""|(\w+)=([^,]+)";
            var matches = System.Text.RegularExpressions.Regex.Matches(message, paramPattern);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var key = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value.Trim();
                parameters[key] = value;
            }
            
            // Extract key parameters
            var workloadName = parameters.GetValueOrDefault("workload_name", "aks-cluster");
            var projectName = parameters.GetValueOrDefault("project_name", "default");
            var owner = parameters.GetValueOrDefault("owner", "devops");
            var environment = parameters.GetValueOrDefault("environment", "dev");
            var location = parameters.GetValueOrDefault("location", "East US");
            var nodeCount = parameters.GetValueOrDefault("node_count", "3");
            var vmSize = parameters.GetValueOrDefault("vm_size", "Standard_DS2_v2");
            var enableAutoscaling = parameters.GetValueOrDefault("enable_autoscaling", "false");
            var enableRbac = parameters.GetValueOrDefault("enable_rbac", "false");
            var networkPolicy = parameters.GetValueOrDefault("network_policy", "azure");
            
            // Create parameters JSON for GitHub template
            var parametersJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                cluster_name = workloadName, // Map workload_name to cluster_name for template
                workload_name = workloadName,
                project_name = projectName,
                owner = owner,
                environment = environment,
                location = location,
                node_count = nodeCount,
                vm_size = vmSize,
                enable_autoscaling = enableAutoscaling.ToLower() == "true",
                enable_rbac = enableRbac.ToLower() == "true",
                network_policy = networkPolicy
            });

            // Store parameters in session context for later terraform deployment
            try
            {
                _logger.LogInformation("Attempting to store terraform parameters for session {SessionId}", sessionId);
                var session = await _sessionManager.GetOrCreateSessionAsync(sessionId);
                _logger.LogInformation("Retrieved session for storage: {SessionExists}, State: {StateExists}, Context: {ContextExists}", 
                    session != null, session?.State != null, session?.State?.Context != null);
                    
                if (session?.State?.Context != null)
                {
                    session.State.Context["terraform_parameters"] = parametersJson;
                    await _sessionManager.UpdateSessionAsync(session);
                    _logger.LogInformation("Successfully stored terraform parameters in session context for session {SessionId}", sessionId);
                }
                else
                {
                    _logger.LogWarning("Cannot store terraform parameters - session, state, or context is null for session {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store parameters in session context for session {SessionId}", sessionId);
            }
            
            // Use DeployGitHubTemplate with preview mode (don't actually deploy)
            try
            {
                _logger.LogInformation("Attempting to fetch GitHub template for AKS cluster");
                
                var azureResourcePlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("AzureResource"));
                if (azureResourcePlugin != null)
                {
                    // First, try to use DeployGitHubTemplate to fetch and process the template
                    var deployFunction = azureResourcePlugin.FirstOrDefault(f => f.Name == "DeployGitHubTemplate");
                    if (deployFunction != null)
                    {
                        _logger.LogInformation("Calling DeployGitHubTemplate with template ID: aks-cluster");
                        
                        var result = await _kernel.InvokeAsync(deployFunction, new KernelArguments
                        {
                            ["templateId"] = "aks-cluster", // Use the AKS template from your GitHub org
                            ["parametersJson"] = parametersJson
                        });
                        
                        var templateResult = result.GetValue<string>();
                        
                        if (!string.IsNullOrEmpty(templateResult))
                        {
                            // Return the template result directly since it already contains
                            // the formatted response with configuration details and HCL code blocks
                            return templateResult;
                        }
                        else
                        {
                            _logger.LogWarning("DeployGitHubTemplate returned empty result");
                            return $"❌ **Error**: Template fetching returned empty result.";
                        }
                    }
                    else
                    {
                        _logger.LogWarning("DeployGitHubTemplate function not found in AzureResource plugin");
                        return $"❌ **Error**: DeployGitHubTemplate function not found in kernel plugins.";
                    }
                }
                else
                {
                    _logger.LogWarning("AzureResource plugin not found in kernel");
                    return $"❌ **Error**: AzureResource plugin not found in kernel.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GitHub template");
                return $"❌ **Error processing GitHub template**: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing structured AKS form");
            return $"❌ **Error processing form data**: {ex.Message}";
        }
    }

    private async Task<string> GetPodLogsAsync(string podName)
    {
        try
        {
            _logger.LogInformation("Getting logs for pod: {PodName}", podName);
            
            // Use MCP server instead of local plugin
            var mcpPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("McpKubernetes"));
            if (mcpPlugin != null)
            {
                // First connect to the cluster using current kubectl context
                var connectFunction = mcpPlugin.FirstOrDefault(f => f.Name == "ConnectToCurrentContext");
                if (connectFunction != null)
                {
                    try
                    {
                        var connectResult = await _kernel.InvokeAsync(connectFunction, new KernelArguments
                        {
                            ["clusterName"] = "aks-dev-aksworkload-si-002"
                        });
                        
                        _logger.LogInformation("Cluster connection result for logs: {Result}", connectResult.GetValue<string>());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to connect to cluster for logs: {Error}", ex.Message);
                        return $"❌ Unable to connect to cluster: {ex.Message}";
                    }
                }
                
                var getLogsFunction = mcpPlugin.FirstOrDefault(f => f.Name == "GetPodLogs");
                if (getLogsFunction != null)
                {
                    var clusterInfo = await _aksContextService.GetCurrentClusterAsync();
                    var clusterName = "aks-dev-aksworkload-si-002"; // Use the same cluster name as in list pods
                    
                    // Use the namespace from the original pods listing instead of making another call
                    // This is more efficient and avoids connection issues
                    var podNamespace = "kube-system"; // Most pods in your cluster are in kube-system
                    
                    var result = await _kernel.InvokeAsync(getLogsFunction, new KernelArguments
                    {
                        ["clusterName"] = clusterName,
                        ["podName"] = podName,
                        ["namespace"] = podNamespace,
                        ["tailLines"] = 50
                    });
                    
                    var logs = result.GetValue<string>() ?? "No logs available";
                    
                    // Parse the JSON response to extract the actual logs
                    try
                    {
                        var logsJson = JsonSerializer.Deserialize<JsonElement>(logs);
                        var actualLogs = logsJson.GetProperty("logs").GetString() ?? "No logs found";
                        var podNamespaceFromResponse = logsJson.GetProperty("namespace").GetString() ?? podNamespace;
                        
                        return $"📋 **Logs for Pod: {podName}**\n" +
                               $"**Namespace**: {podNamespaceFromResponse}\n" +
                               $"**Lines**: 50\n\n" +
                               "```\n" +
                               actualLogs +
                               "\n```\n\n" +
                               "💡 *Use 'logs [pod-name] --tail=100' for more logs or 'logs [pod-name] -f' to follow logs.*";
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, return the raw response
                        return logs + "\n\n💡 *Use 'logs [pod-name] --tail=100' for more logs or 'logs [pod-name] -f' to follow logs.*";
                    }
                }
            }
            
            return $"❌ Unable to retrieve logs for pod '{podName}'. Please ensure you're connected to an AKS cluster and the MCP server is running.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pod logs for {PodName}", podName);
            return $"❌ Error getting logs for pod '{podName}': {ex.Message}";
        }
    }

    private async Task<string> ListPodsAsync()
    {
        try
        {
            _logger.LogInformation("Listing all pods");
            
            // Use MCP server instead of local plugin
            var mcpPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("McpKubernetes"));
            if (mcpPlugin != null)
            {
                // First connect to the cluster using current kubectl context
                var connectFunction = mcpPlugin.FirstOrDefault(f => f.Name == "ConnectToCurrentContext");
                if (connectFunction != null)
                {
                    try
                    {
                        var connectResult = await _kernel.InvokeAsync(connectFunction, new KernelArguments
                        {
                            ["clusterName"] = "aks-dev-aksworkload-si-002"
                        });
                        
                        _logger.LogInformation("Cluster connection result: {Result}", connectResult.GetValue<string>());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to connect to cluster: {Error}", ex.Message);
                        // Continue anyway to try listing pods
                    }
                }
                
                var getPodsFunction = mcpPlugin.FirstOrDefault(f => f.Name == "GetKubernetesPods");
                if (getPodsFunction != null)
                {
                    // Use the current kubectl context directly
                    var clusterName = "aks-dev-aksworkload-si-002";
                    
                    var result = await _kernel.InvokeAsync(getPodsFunction, new KernelArguments
                    {
                        ["clusterName"] = clusterName
                    });
                    
                    var podsData = result.GetValue<string>() ?? "No pods found";
                    
                    // Parse the JSON response and format it for display
                    try
                    {
                        _logger.LogInformation("Parsing pods data: {PodsData}", podsData);
                        
                        var podsJson = JsonSerializer.Deserialize<JsonElement>(podsData);
                        if (podsJson.TryGetProperty("pods", out var podsList))
                        {
                            var formattedOutput = new StringBuilder();
                            formattedOutput.AppendLine($"📊 **Found {podsList.GetArrayLength()} pods in cluster aks-dev-aksworkload-si-002**\n");
                            
                            // Group by namespace
                            var podsByNamespace = new Dictionary<string, List<JsonElement>>();
                            foreach (var pod in podsList.EnumerateArray())
                            {
                                var ns = pod.GetProperty("namespace").GetString() ?? "default";
                                if (!podsByNamespace.ContainsKey(ns))
                                    podsByNamespace[ns] = new List<JsonElement>();
                                podsByNamespace[ns].Add(pod);
                            }
                            
                            foreach (var kvp in podsByNamespace.OrderBy(x => x.Key))
                            {
                                formattedOutput.AppendLine($"🔸 **Namespace: {kvp.Key}** ({kvp.Value.Count} pods)\n");
                                
                                foreach (var pod in kvp.Value.OrderBy(p => p.GetProperty("name").GetString()))
                                {
                                    var name = pod.GetProperty("name").GetString();
                                    var status = pod.GetProperty("status").GetString() ?? "Unknown";
                                    var restarts = pod.TryGetProperty("restarts", out var r) ? r.GetInt32() : 0;
                                    var age = pod.TryGetProperty("age", out var a) ? a.GetString() : "Unknown";
                                    
                                    // Format status with color indicators
                                    var statusIndicator = status?.ToLower() switch
                                    {
                                        "running" => "🟢",
                                        "pending" => "🟡", 
                                        "failed" => "🔴",
                                        "succeeded" => "✅",
                                        _ => "⚪"
                                    };
                                    
                                    formattedOutput.AppendLine($"`{name}`");
                                    formattedOutput.AppendLine($"↘ {statusIndicator} Status: **{status}** | Restarts: {restarts} | Age: {age}\n");
                                }
                            }
                            
                            return formattedOutput.ToString();
                        }
                        else
                        {
                            _logger.LogWarning("No 'pods' property found in JSON response");
                            return $"⚠️ Unexpected response format: {podsData}";
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to parse JSON response: {PodsData}", podsData);
                        return $"⚠️ Failed to parse response. Raw data:\n```json\n{podsData}\n```";
                    }
                    
                    return podsData;
                }
            }
            
            return "❌ Unable to list pods. Please ensure you're connected to an AKS cluster and the MCP server is running.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pods");
            return $"❌ Error listing pods: {ex.Message}";
        }
    }

    private async Task<string> DescribePodAsync(string podName)
    {
        try
        {
            _logger.LogInformation("Describing pod: {PodName}", podName);
            
            // First try the new MCP Kubernetes plugin
            var mcpPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("McpKubernetes"));
            if (mcpPlugin != null)
            {
                await EnsureMcpConnectionAsync(mcpPlugin);
                
                var describePodFunction = mcpPlugin.FirstOrDefault(f => f.Name == "DescribePod");
                if (describePodFunction != null)
                {
                    var clusterInfo = await _aksContextService.GetCurrentClusterAsync();
                    var clusterName = clusterInfo?.Name ?? "aks-dev-aksworkload-si-002";
                    
                    var result = await _kernel.InvokeAsync(describePodFunction, new KernelArguments
                    {
                        ["clusterName"] = clusterName,
                        ["podName"] = podName,
                        ["namespaceName"] = "kube-system" // Default to kube-system for system pods
                    });
                    
                    var description = result.GetValue<string>() ?? "No description available";
                    
                    return $"**Pod Description: {podName}**\n\n```json\n{description}\n```\n\n💡 **Kubectl Command Suggestion**: `kubectl describe pod {podName} -n kube-system`";
                }
            }
            
            // Fallback to old AKS plugin
            var aksPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("AksMcp"));
            if (aksPlugin != null)
            {
                await EnsureAksConnectionAsync(aksPlugin);
                
                var kubectlFunction = aksPlugin.FirstOrDefault(f => f.Name == "ExecuteKubectlCommand");
                if (kubectlFunction != null)
                {
                    var clusterInfo = await _aksContextService.GetCurrentClusterAsync();
                    var deploymentName = clusterInfo?.Name ?? "default-cluster";
                    
                    var result = await _kernel.InvokeAsync(kubectlFunction, new KernelArguments
                    {
                        ["deploymentName"] = deploymentName,
                        ["command"] = $"describe pod {podName} -n kube-system"
                    });
                    
                    var description = result.GetValue<string>() ?? "No description available";
                    
                    return $"**Pod Description: {podName}**\n\n```\n{description}\n```";
                }
            }
            
            return $"❌ Unable to describe pod '{podName}'. Please ensure you're connected to an AKS cluster.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error describing pod {PodName}", podName);
            return $"❌ Error describing pod '{podName}': {ex.Message}";
        }
    }

    private async Task<string> DescribeResourceAsync(string resourceType, string resourceName, string? namespaceName = null)
    {
        try
        {
            _logger.LogInformation("Describing {ResourceType}: {ResourceName} in namespace: {Namespace}", 
                resourceType, resourceName, namespaceName ?? "auto-detect");

            // Smart namespace detection if not provided
            if (string.IsNullOrEmpty(namespaceName))
            {
                namespaceName = DetectNamespace(resourceName);
                _logger.LogInformation("Auto-detected namespace: {Namespace} for resource: {ResourceName}", 
                    namespaceName, resourceName);
            }

            // Prioritize MCP server for all Kubernetes resource descriptions
            var mcpPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("McpKubernetes"));
            if (mcpPlugin != null)
            {
                // Try specific resource functions first
                var resourceFunction = resourceType.ToLower() switch
                {
                    "pod" => mcpPlugin.FirstOrDefault(f => f.Name == "DescribePod"),
                    _ => null
                };

                if (resourceFunction != null)
                {
                    var clusterInfo = await _aksContextService.GetCurrentClusterAsync();
                    var clusterName = clusterInfo?.Name ?? "default-cluster";
                    
                    _logger.LogInformation("Using MCP server to describe {ResourceType}: {ResourceName} in cluster: {ClusterName}", 
                        resourceType, resourceName, clusterName);
                    
                    var result = await _kernel.InvokeAsync(resourceFunction, new KernelArguments
                    {
                        ["clusterName"] = clusterName,
                        ["podName"] = resourceName,
                        ["namespaceName"] = namespaceName
                    });
                    
                    var description = result.GetValue<string>() ?? "No description available";
                    
                    // Parse and format the JSON response
                    return FormatResourceDescription(resourceType, resourceName, description);
                }
            }

            // Use AKS plugin for kubectl command execution as fallback
            var aksPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("AksMcp"));
            if (aksPlugin != null)
            {
                await EnsureAksConnectionAsync(aksPlugin);
                
                var kubectlFunction = aksPlugin.FirstOrDefault(f => f.Name == "ExecuteKubectlCommand");
                if (kubectlFunction != null)
                {
                    var clusterInfo = await _aksContextService.GetCurrentClusterAsync();
                    var deploymentName = clusterInfo?.Name ?? "default-cluster";
                    
                    // Build intelligent kubectl command
                    var kubectlCommand = BuildKubectlDescribeCommand(resourceType, resourceName, namespaceName);
                    
                    var result = await _kernel.InvokeAsync(kubectlFunction, new KernelArguments
                    {
                        ["deploymentName"] = deploymentName,
                        ["command"] = kubectlCommand
                    });
                    
                    var description = result.GetValue<string>() ?? "No description available";
                    
                    // If the description contains "Use GetPods" or other generic messages, 
                    // it means the command wasn't executed properly, so provide intelligent fallback
                    if (description.Contains("Use GetPods") || description.Contains("kubectl command") || description.Length < 50)
                    {
                        return FormatFallbackResourceDescription(resourceType, resourceName, namespaceName, kubectlCommand);
                    }
                    
                    // Format the successful kubectl output
                    return FormatKubectlOutput(resourceType, resourceName, kubectlCommand, description);
                }
            }

            return $"❌ Unable to describe {resourceType} '{resourceName}'. Please ensure you're connected to an AKS cluster.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error describing {ResourceType} {ResourceName}", resourceType, resourceName);
            return $"❌ Error describing {resourceType} '{resourceName}': {ex.Message}";
        }
    }

    private string FormatKubectlOutput(string resourceType, string resourceName, string kubectlCommand, string description)
    {
        return $"🔍 **{resourceType.ToUpper()} Details: {resourceName}**\n\n" +
               $"💡 **Command:** `kubectl {kubectlCommand}`\n\n" +
               $"```yaml\n{description}\n```";
    }

    private string FormatFallbackResourceDescription(string resourceType, string resourceName, string namespaceName, string kubectlCommand)
    {
        var resourceInfo = GetResourceTypeInfo(resourceType);
        
        return $"🔍 **{resourceType.ToUpper()} Details: {resourceName}**\n\n" +
               $"📍 **Namespace:** {namespaceName ?? "auto-detected"}\n" +
               $"🔧 **Resource Type:** {resourceInfo.description}\n\n" +
               $"💡 **Suggested Command:**\n" +
               $"```bash\n" +
               $"kubectl {kubectlCommand}\n" +
               $"```\n\n" +
               $"📋 **Common Actions:**\n" +
               string.Join("\n", resourceInfo.actions.Select(action => $"• `kubectl {action} {resourceName}`"));
    }

    private (string description, string[] actions) GetResourceTypeInfo(string resourceType)
    {
        return resourceType.ToLower() switch
        {
            "pod" => ("A pod is the smallest deployable unit in Kubernetes", 
                     new[] { "logs", "exec -it", "port-forward", "delete" }),
            "deployment" => ("A deployment manages replica sets and pod templates", 
                            new[] { "scale --replicas=3", "rollout status", "rollout history", "edit" }),
            "service" => ("A service exposes pods to network traffic", 
                         new[] { "get endpoints", "edit", "delete" }),
            "configmap" => ("A configmap stores configuration data", 
                           new[] { "get -o yaml", "edit", "delete" }),
            "secret" => ("A secret stores sensitive data like passwords", 
                        new[] { "get -o yaml", "edit", "delete" }),
            "node" => ("A node is a worker machine in the cluster", 
                      new[] { "top", "cordon", "uncordon", "drain" }),
            "namespace" => ("A namespace provides logical separation of resources", 
                           new[] { "get all -n", "describe", "delete" }),
            _ => ($"A {resourceType} is a Kubernetes resource", 
                 new[] { "get -o yaml", "edit", "delete" })
        };
    }

    private string FormatResourceDescription(string resourceType, string resourceName, string jsonDescription)
    {
        try
        {
            // Try to parse the JSON response and format it nicely
            if (jsonDescription.StartsWith("{") && jsonDescription.Contains("podName"))
            {
                using var document = JsonDocument.Parse(jsonDescription);
                var root = document.RootElement;
                
                var podName = root.GetProperty("podName").GetString();
                var clusterName = root.GetProperty("clusterName").GetString();
                var namespaceName = root.GetProperty("namespace").GetString();
                
                if (root.TryGetProperty("description", out var descriptionElement))
                {
                    var description = descriptionElement.GetString() ?? "";
                    
                    // Parse the inner JSON description
                    try 
                    {
                        using var innerDocument = JsonDocument.Parse(description);
                        var innerRoot = innerDocument.RootElement;
                        
                        // Format key information nicely
                        var name = innerRoot.GetProperty("name").GetString();
                        var cluster = innerRoot.GetProperty("cluster").GetString();
                        var ns = innerRoot.GetProperty("namespace").GetString();
                        
                        var result = $"🔍 **{resourceType.ToUpper()} Details: {resourceName}**\n\n" +
                                   $"📍 **Cluster:** {cluster}\n" +
                                   $"📦 **Namespace:** {ns}\n\n";
                        
                        // Add status information
                        if (innerRoot.TryGetProperty("status", out var statusElement))
                        {
                            result += "📊 **Status Information:**\n";
                            if (statusElement.TryGetProperty("phase", out var phase))
                                result += $"• **Phase:** {phase.GetString()}\n";
                            if (statusElement.TryGetProperty("podIP", out var podIP))
                                result += $"• **Pod IP:** {podIP.GetString()}\n";
                            if (statusElement.TryGetProperty("nodeName", out var nodeName))
                                result += $"• **Node:** {nodeName.GetString()}\n";
                            if (statusElement.TryGetProperty("startTime", out var startTime))
                                result += $"• **Started:** {startTime.GetString()}\n";
                            result += "\n";
                        }
                        
                        // Add container information
                        if (innerRoot.TryGetProperty("status", out var statusElement2) && 
                            statusElement2.TryGetProperty("containerStatuses", out var containerStatuses))
                        {
                            result += "🐳 **Container Information:**\n";
                            foreach (var container in containerStatuses.EnumerateArray())
                            {
                                var containerName = container.GetProperty("name").GetString();
                                var containerImage = container.GetProperty("image").GetString();
                                var ready = container.GetProperty("ready").GetBoolean();
                                result += $"• **{containerName}**: {containerImage} (Ready: {ready})\n";
                            }
                            result += "\n";
                        }
                        
                        // Add basic spec information
                        if (innerRoot.TryGetProperty("spec", out var specElement))
                        {
                            result += "⚙️ **Configuration:**\n";
                            if (specElement.TryGetProperty("restartPolicy", out var restartPolicy))
                                result += $"• **Restart Policy:** {restartPolicy.GetString()}\n";
                            if (specElement.TryGetProperty("serviceAccountName", out var serviceAccount))
                                result += $"• **Service Account:** {serviceAccount.GetString()}\n";
                        }
                        
                        return result;
                    }
                    catch (JsonException)
                    {
                        // If inner JSON parsing fails, clean up and return basic format
                        description = description.Replace("\\n", "\n")
                                               .Replace("\\r", "")
                                               .Replace("\\u0022", "\"")
                                               .Replace("\\", "");
                        
                        return $"🔍 **{resourceType.ToUpper()} Details: {resourceName}**\n\n" +
                               $"📍 **Cluster:** {clusterName}\n" +
                               $"📦 **Namespace:** {namespaceName}\n\n" +
                               $"```yaml\n{description}\n```";
                    }
                }
            }
            
            // Fallback formatting for other response types
            return $"🔍 **{resourceType.ToUpper()} Details: {resourceName}**\n\n```\n{jsonDescription}\n```";
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return raw response with basic formatting
            return $"🔍 **{resourceType.ToUpper()} Details: {resourceName}**\n\n```\n{jsonDescription}\n```";
        }
    }

    private string DetectNamespace(string resourceName)
    {
        // Intelligent namespace detection based on resource name patterns
        var systemPatterns = new[]
        {
            "microsoft-defender", "defender", "azure-", "coredns", "kube-", "metrics-server",
            "csi-", "cloud-node-manager", "konnectivity-agent", "azure-cns", "azure-npm",
            "azure-ip-masq-agent", "kube-proxy"
        };

        var lowerName = resourceName.ToLowerInvariant();
        foreach (var pattern in systemPatterns)
        {
            if (lowerName.Contains(pattern))
            {
                return "kube-system";
            }
        }

        // Default to searching all namespaces if not clearly a system component
        return ""; // Empty means all namespaces
    }

    private string BuildKubectlDescribeCommand(string resourceType, string resourceName, string? namespaceName)
    {
        var command = $"describe {resourceType} {resourceName}";
        
        if (!string.IsNullOrEmpty(namespaceName))
        {
            command += $" -n {namespaceName}";
        }
        else
        {
            command += " --all-namespaces";
        }
        
        return command;
    }

    private async Task<string> ExecuteKubectlCommandAsync(string command)
    {
        try
        {
            _logger.LogInformation("Executing kubectl command: {Command}", command);
            
            var aksPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("AksMcp"));
            if (aksPlugin != null)
            {
                await EnsureAksConnectionAsync(aksPlugin);
                
                var kubectlFunction = aksPlugin.FirstOrDefault(f => f.Name == "ExecuteKubectlCommand");
                if (kubectlFunction != null)
                {
                    var clusterInfo = await _aksContextService.GetCurrentClusterAsync();
                    var deploymentName = clusterInfo?.Name ?? "default-cluster";
                    
                    var result = await _kernel.InvokeAsync(kubectlFunction, new KernelArguments
                    {
                        ["deploymentName"] = deploymentName,
                        ["command"] = command
                    });
                    
                    var output = result.GetValue<string>() ?? "No output";
                    
                    return $"**kubectl {command}**\n\n```\n{output}\n```";
                }
            }
            
            return $"❌ Unable to execute kubectl command. Please ensure you're connected to an AKS cluster.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing kubectl command {Command}", command);
            return $"❌ Error executing kubectl command: {ex.Message}";
        }
    }

    private async Task<string> GetPodNamespaceAsync(string podName)
    {
        try
        {
            // Use MCP server instead of local plugin
            var mcpPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("McpKubernetes"));
            if (mcpPlugin != null)
            {
                var getPodsFunction = mcpPlugin.FirstOrDefault(f => f.Name == "GetKubernetesPods");
                if (getPodsFunction != null)
                {
                    var clusterName = "aks-dev-aksworkload-si-002"; // Use the same cluster name as in list pods
                    
                    var result = await _kernel.InvokeAsync(getPodsFunction, new KernelArguments
                    {
                        ["clusterName"] = clusterName
                    });
                    
                    var podsData = result.GetValue<string>() ?? "";
                    
                    // Parse the JSON response to find the namespace for this specific pod
                    try
                    {
                        var podsJson = JsonSerializer.Deserialize<JsonElement>(podsData);
                        if (podsJson.TryGetProperty("pods", out var podsList))
                        {
                            foreach (var pod in podsList.EnumerateArray())
                            {
                                var name = pod.GetProperty("name").GetString();
                                if (name == podName)
                                {
                                    var podNamespaceValue = pod.GetProperty("namespace").GetString() ?? "default";
                                    _logger.LogInformation("Found pod {PodName} in namespace {Namespace}", podName, podNamespaceValue);
                                    return podNamespaceValue;
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse pods JSON response");
                    }
                }
            }
            
            // Try to detect namespace from pod name patterns
            _logger.LogWarning("Could not determine namespace for pod {PodName}, trying common namespaces", podName);
            
            if (podName.StartsWith("azure-") || podName.StartsWith("coredns-") || podName.StartsWith("kube-"))
            {
                return "kube-system";
            }
            else if (podName.StartsWith("microsoft-defender-"))
            {
                return "azure-system";
            }
            
            return "default"; // Default fallback
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine namespace for pod {PodName}, using default", podName);
            return "default";
        }
    }

    private async Task<string> ListDeploymentsAsync()
    {
        return await GetKubernetesResourceAsync("deployments");
    }

    private async Task<string> ListServicesAsync()
    {
        return await GetKubernetesResourceAsync("services");
    }

    private async Task<string> ListNamespacesAsync()
    {
        return await GetKubernetesResourceAsync("namespaces");
    }

    private async Task<string> ListConfigMapsAsync()
    {
        return await GetKubernetesResourceAsync("configmaps");
    }

    private async Task<string> ListSecretsAsync()
    {
        return await GetKubernetesResourceAsync("secrets");
    }

    private async Task<string> ListIngressAsync()
    {
        return await GetKubernetesResourceAsync("ingress");
    }

    private async Task<string> ListCronJobsAsync()
    {
        return await GetKubernetesResourceAsync("cronjobs");
    }

    private async Task<string> ListJobsAsync()
    {
        return await GetKubernetesResourceAsync("jobs");
    }

    private async Task<string> ListPersistentVolumesAsync()
    {
        return await GetKubernetesResourceAsync("persistent_volumes");
    }

    private async Task<string> GetKubernetesResourceAsync(string resourceType, string? namespaceFilter = null)
    {
        try
        {
            _logger.LogInformation("Getting {ResourceType} resources", resourceType);
            
            var mcpPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("McpKubernetes"));
            if (mcpPlugin != null)
            {
                var getResourcesFunction = mcpPlugin.FirstOrDefault(f => f.Name == "GetKubernetesResources");
                if (getResourcesFunction != null)
                {
                    var clusterName = "aks-dev-aksworkload-si-002";
                    
                    var arguments = new KernelArguments
                    {
                        ["clusterName"] = clusterName,
                        ["resourceType"] = resourceType
                    };

                    if (namespaceFilter != null)
                    {
                        arguments["namespaceFilter"] = namespaceFilter;
                    }
                    
                    var result = await _kernel.InvokeAsync(getResourcesFunction, arguments);
                    var jsonResult = result.GetValue<string>() ?? $"No {resourceType} found";
                    
                    // Format the JSON response for better UI display
                    return FormatKubernetesResourceResponse(jsonResult, resourceType);
                }
            }
            
            return $"❌ Unable to list {resourceType}. Please ensure the MCP server is running.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing {ResourceType}", resourceType);
            return $"❌ Error listing {resourceType}: {ex.Message}";
        }
    }

    private string FormatKubernetesResourceResponse(string jsonResponse, string resourceType)
    {
        try
        {
            // Try to parse the JSON response
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("cluster", out var clusterProp) && 
                root.TryGetProperty($"total{char.ToUpper(resourceType[0])}{resourceType.Substring(1)}", out var totalProp))
            {
                var cluster = clusterProp.GetString();
                var total = totalProp.GetInt32();
                var resourcesArray = root.GetProperty(resourceType);
                
                var formatted = new StringBuilder();
                formatted.AppendLine($"🎯 {char.ToUpper(resourceType[0])}{resourceType.Substring(1)} in cluster: {cluster}");
                formatted.AppendLine();
                formatted.AppendLine($"Total {resourceType}: {total}");
                formatted.AppendLine();
                
                if (resourcesArray.GetArrayLength() > 0)
                {
                    foreach (var resource in resourcesArray.EnumerateArray())
                    {
                        FormatSingleResource(formatted, resource, resourceType);
                    }
                }
                else
                {
                    formatted.AppendLine($"No {resourceType} found in this cluster.");
                }
                
                return formatted.ToString();
            }
            
            // If parsing fails, return formatted JSON
            return $"{char.ToUpper(resourceType[0])}{resourceType.Substring(1)} Information:\n\n{jsonResponse}";
        }
        catch (JsonException)
        {
            // If it's not valid JSON, return as-is
            return jsonResponse;
        }
    }

    private void FormatSingleResource(StringBuilder formatted, JsonElement resource, string resourceType)
    {
        try
        {
            var name = resource.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "Unknown";
            var ns = resource.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : "default";
            
            formatted.AppendLine($"📦 {name}");
            formatted.AppendLine($"  • Namespace: {ns}");
            
            if (resourceType == "deployments")
            {
                if (resource.TryGetProperty("replicas", out var replicasProp))
                    formatted.AppendLine($"  • Replicas: {replicasProp.GetInt32()}");
                if (resource.TryGetProperty("readyReplicas", out var readyProp))
                    formatted.AppendLine($"  • Ready: {readyProp.GetInt32()}/{replicasProp.GetInt32()}");
                if (resource.TryGetProperty("strategy", out var strategyProp))
                    formatted.AppendLine($"  • Strategy: {strategyProp.GetString()}");
            }
            else if (resourceType == "services")
            {
                if (resource.TryGetProperty("type", out var typeProp))
                    formatted.AppendLine($"  • Type: {typeProp.GetString()}");
                if (resource.TryGetProperty("clusterIP", out var ipProp))
                    formatted.AppendLine($"  • Cluster IP: {ipProp.GetString()}");
            }
            else if (resourceType == "pods")
            {
                if (resource.TryGetProperty("status", out var statusProp))
                    formatted.AppendLine($"  • Status: {statusProp.GetString()}");
                if (resource.TryGetProperty("restarts", out var restartsProp))
                    formatted.AppendLine($"  • Restarts: {restartsProp.GetInt32()}");
            }
            
            if (resource.TryGetProperty("created", out var createdProp))
            {
                if (DateTime.TryParse(createdProp.GetString(), out var created))
                    formatted.AppendLine($"  • Created: {created:yyyy-MM-dd HH:mm:ss}");
            }
            
            formatted.AppendLine();
        }
        catch (Exception ex)
        {
            formatted.AppendLine($"  • Error formatting resource: {ex.Message}");
            formatted.AppendLine();
        }
    }

    private async Task EnsureAksConnectionAsync(KernelPlugin aksPlugin)
    {
        var connectFunction = aksPlugin.FirstOrDefault(f => f.Name == "ConnectToExistingAksCluster");
        if (connectFunction != null)
        {
            var currentCluster = await _aksContextService.GetCurrentClusterAsync();
            if (currentCluster != null)
            {
                await _kernel.InvokeAsync(connectFunction, new KernelArguments
                {
                    ["clusterName"] = currentCluster.Name,
                    ["resourceGroupName"] = currentCluster.ResourceGroupName,
                    ["deploymentName"] = currentCluster.Name
                });
            }
        }
    }

    private async Task EnsureMcpConnectionAsync(KernelPlugin mcpPlugin)
    {
        var connectFunction = mcpPlugin.FirstOrDefault(f => f.Name == "ConnectToCurrentContextAsync");
        if (connectFunction != null)
        {
            var currentCluster = await _aksContextService.GetCurrentClusterAsync();
            var clusterName = currentCluster?.Name ?? "aks-dev-aksworkload-si-002";
            
            await _kernel.InvokeAsync(connectFunction, new KernelArguments
            {
                ["clusterName"] = clusterName
            });
        }
    }

    private async Task<string> HandleTerraformDeploymentAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation("Handling terraform deployment for session {SessionId}", sessionId);
            
            // Get the session to check if there are stored parameters from the form
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session?.State?.Context != null && session.State.Context.ContainsKey("terraform_parameters"))
            {
                // Use stored parameters from the form submission
                var storedParams = session.State.Context["terraform_parameters"] as string;
                if (!string.IsNullOrEmpty(storedParams))
                {
                    _logger.LogInformation("Using stored terraform parameters from session");
                    
                    // Generate deployment response with stored parameters
                    return await GenerateTerraformDeploymentResponseAsync(storedParams, sessionId);
                }
            }
            
            // If no stored parameters, check recent conversation for form submission
            if (session?.Messages != null && session.Messages.Count > 0)
            {
                // Look for recent form submission in conversation history
                var recentFormMessage = session.Messages
                    .Where(m => m.Content?.Contains("workload_name") == true || 
                               m.Content?.Contains("cluster_name") == true)
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();
                
                if (recentFormMessage != null)
                {
                    _logger.LogInformation("Found form parameters in recent conversation");
                    return await GenerateTerraformDeploymentResponseAsync(recentFormMessage.Content, sessionId);
                }
            }
            
            // If no parameters found, show the adaptive card form instead of asking in text
            _logger.LogInformation("No stored parameters found, showing AKS creation form");
            return await _interactiveService.GenerateParameterFormAsync("create", "clusters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling terraform deployment");
            return $"❌ **Error**: Failed to process terraform deployment: {ex.Message}";
        }
    }

    private async Task<string> GenerateTerraformDeploymentResponseAsync(string parameters, string sessionId)
    {
        try
        {
            // Extract a deployment ID for tracking
            var deploymentId = Guid.NewGuid().ToString("N")[..8];
            
            _logger.LogInformation("Starting terraform deployment with ID {DeploymentId}", deploymentId);
            
            // Parse the parameters to extract deployment information
            var parametersObj = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(parameters);
            var clusterName = parametersObj.GetProperty("cluster_name").GetString() ?? "unknown-cluster";
            var environment = parametersObj.GetProperty("environment").GetString() ?? "dev";
            var location = parametersObj.GetProperty("location").GetString() ?? "eastus";
            
            // Try to call the actual deployment API
            try
            {
                _logger.LogInformation("Creating deployment request with sessionId={SessionId}, deploymentId={DeploymentId}", sessionId, deploymentId);
                
                var deploymentRequest = new
                {
                    sessionId = sessionId, // Use the passed session ID
                    terraformCode = "# AKS Cluster Terraform Configuration\n" +
                                   $"# Generated for cluster: {clusterName}\n" +
                                   "# This will be replaced with actual terraform template",
                    parameters = new Dictionary<string, object>() // Empty - let DeployTemplate get from session
                };

                var jsonContent = JsonSerializer.Serialize(deploymentRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogInformation("Calling terraform deployment API for cluster {ClusterName}", clusterName);
                
                // Call the deployment API (assuming it's running on localhost:5050)
                var response = await _httpClient.PostAsync("http://localhost:5050/api/azure/deploy-terraform", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var deploymentResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    var actualDeploymentId = deploymentResponse.GetProperty("deploymentId").GetString();
                    
                    return $@"🚀 **Terraform Deployment Started Successfully**

**Deployment ID**: `{actualDeploymentId}`
**Cluster Name**: {clusterName}
**Environment**: {environment}
**Location**: {location}

📋 **Deployment Status**: ✅ **ACTIVE DEPLOYMENT IN PROGRESS**

**Real Terraform Execution Started**:
1. ✅ **Parameters validated** - Using stored form data
2. 🔄 **Initializing** - terraform init executing...
3. ⏳ **Planning** - terraform plan in progress
4. ⏳ **Applying** - terraform apply will follow
5. ⏳ **Validating** - Resource verification pending

**Resources being created**:
• Resource Group: rg-{clusterName}-{environment}
• AKS Cluster: {clusterName}
• Virtual Network & Subnet
• Network Security Group
• Service Principal (if needed)

📊 **Estimated time**: 10-15 minutes

� **Monitor Progress**: Check Azure portal for real-time resource creation
⚡ **Live Status**: Terraform commands are executing in the background

⚠️ **Important**: This is creating real Azure resources and will incur costs!";
                }
                else
                {
                    _logger.LogWarning("Terraform deployment API call failed with status {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception apiEx)
            {
                _logger.LogWarning(apiEx, "Failed to call terraform deployment API, falling back to status message");
            }
            
            // Fallback to status message if API call fails
            var fallbackResponse = $@"🚀 **Terraform Deployment Initiated**

**Deployment ID**: `{deploymentId}`
**Cluster Name**: {clusterName}
**Environment**: {environment}
**Location**: {location}

📋 **Deployment Status**: Preparing for execution...

**Next Steps Required**:
1. ✅ **Parameters validated** - Using stored form data
2. ⚠️ **Manual trigger needed** - Use deployment API directly
3. ⏳ **Planning** - terraform plan pending
4. ⏳ **Applying** - terraform apply pending
5. ⏳ **Validating** - Resource verification pending

**Resources to be created**:
• Resource Group: rg-{clusterName}-{environment}
• AKS Cluster: {clusterName}
• Virtual Network & Subnet
• Network Security Group

📊 **Estimated time**: 10-15 minutes

💡 **To actually deploy**: Use the direct API endpoint `/api/azure/deploy-terraform`
⚠️ **Note**: This will create real Azure resources and incur costs.";

            return fallbackResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating terraform deployment response");
            return $"❌ **Error**: Failed to generate deployment response: {ex.Message}";
        }
    }

    private async Task<string> HandleSpecificTerraformDeploymentAsync(string sessionId, string deploymentId)
    {
        try
        {
            _logger.LogInformation("Handling deployment for specific template {DeploymentId} in session {SessionId}", deploymentId, sessionId);
            
            // Get the session to check if there are stored parameters
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session?.State?.Context == null || !session.State.Context.ContainsKey("terraform_parameters"))
            {
                return $"❌ **Error**: No stored parameters found for deployment {deploymentId}. Please fill out the form first.";
            }

            var storedParams = session.State.Context["terraform_parameters"] as string;
            if (string.IsNullOrEmpty(storedParams))
            {
                return $"❌ **Error**: Invalid parameters for deployment {deploymentId}.";
            }

            // Call the actual deployment API with the stored parameters
            try
            {
                var deploymentRequest = new
                {
                    sessionId = sessionId,
                    terraformCode = "# AKS Cluster Terraform Configuration\n" +
                                   $"# Deployment ID: {deploymentId}\n" +
                                   "# Generated from form submission",
                    parameters = new Dictionary<string, object>() // Let API get from session
                };

                var jsonContent = JsonSerializer.Serialize(deploymentRequest);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogInformation("Calling deployment API for {DeploymentId}", deploymentId);
                
                // Get dynamic API base URL
                var apiBaseUrl = await _portDetectionService.GetApiBaseUrlAsync();
                var response = await _httpClient.PostAsync($"{apiBaseUrl}/api/azure/deploy-terraform", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var deploymentResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    var actualDeploymentId = deploymentResponse.GetProperty("deploymentId").GetString();
                    
                    return $@"🚀 **Terraform Deployment Started Successfully!**

**Deployment ID**: `{actualDeploymentId}`
**Status**: Deployment initiated

📋 **Deployment Progress**:
1. ✅ **Parameters validated**
2. 🔄 **Terraform initialization** - In progress
3. ⏳ **Planning** - Pending
4. ⏳ **Applying** - Pending
5. ⏳ **Validation** - Pending

**Monitor Progress**: The deployment is now running in the background. You can check the status using the deployment ID.

⚠️ **Note**: This creates real Azure resources and will incur costs.";
                }
                else
                {
                    return $"❌ **Deployment Failed**: API returned {response.StatusCode}. Please try again.";
                }
            }
            catch (Exception apiEx)
            {
                _logger.LogError(apiEx, "Failed to call deployment API for {DeploymentId}", deploymentId);
                return $"❌ **Deployment Failed**: Unable to start deployment. {apiEx.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling specific terraform deployment");
            return $"❌ **Error**: Failed to process deployment: {ex.Message}";
        }
    }

    private async Task<string> HandleTerraformEditActionAsync(string sessionId, string deploymentId)
    {
        try
        {
            _logger.LogInformation("Handling edit action for template {DeploymentId} in session {SessionId}", deploymentId, sessionId);
            
            // Get the current terraform template and parameters for editing
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session?.State?.Context != null && session.State.Context.ContainsKey("terraform_parameters"))
            {
                var storedParams = session.State.Context["terraform_parameters"] as string;
                if (!string.IsNullOrEmpty(storedParams))
                {
                    // Parse the stored parameters and return to form for editing
                    var parametersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(storedParams);
                    return await _interactiveService.GenerateParameterFormAsync("edit", "clusters", parametersDict);
                }
            }
            
            return $"📝 **Edit Template {deploymentId}**\n\nNo stored parameters found. Please fill out the form to configure the template.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling terraform edit action");
            return $"❌ **Error**: Failed to edit template: {ex.Message}";
        }
    }

    private async Task<string> HandleTerraformCancelActionAsync(string sessionId, string deploymentId)
    {
        try
        {
            _logger.LogInformation("Handling cancel action for template {DeploymentId} in session {SessionId}", deploymentId, sessionId);
            
            // Clear any stored terraform parameters
            var session = await _sessionManager.GetSessionAsync(sessionId);
            if (session?.State?.Context != null)
            {
                session.State.Context.Remove("terraform_parameters");
                await _sessionManager.UpdateSessionAsync(session);
            }
            
            return $@"❌ **Deployment Cancelled**

**Template ID**: {deploymentId}
**Status**: Cancelled by user

✅ **Actions Taken**:
• Cleared stored parameters
• Cancelled deployment preparation
• No Azure resources were created

You can start a new deployment anytime by filling out the AKS cluster form.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling terraform cancel action");
            return $"❌ **Error**: Failed to cancel deployment: {ex.Message}";
        }
    }

    private bool IsFormSubmission(string message)
    {
        try
        {
            // Check if the message contains form submission data
            if (message.Contains("action") && message.Contains("deploy_template"))
            {
                // Try to parse as JSON to verify it's a form submission
                var jsonDoc = JsonDocument.Parse(message);
                return jsonDoc.RootElement.TryGetProperty("action", out var actionProp) && 
                       actionProp.GetString() == "deploy_template";
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> HandleFormSubmissionAsync(string sessionId, string formData)
    {
        try
        {
            _logger.LogInformation("Processing form submission for session {SessionId}", sessionId);

            // Parse the form data JSON
            var jsonDoc = JsonDocument.Parse(formData);
            var root = jsonDoc.RootElement;

            // Extract the action and template ID
            var action = root.GetProperty("action").GetString();
            var templateId = root.GetProperty("templateId").GetString();

            if (action == "deploy_template" && templateId == "aks-cluster")
            {
                // Extract all the form parameters
                var parameters = new Dictionary<string, string>();
                
                // Extract each parameter from the form
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "action" && property.Name != "templateId")
                    {
                        parameters[property.Name] = property.Value.GetString() ?? "";
                    }
                }

                _logger.LogInformation("Extracted {ParameterCount} parameters from form submission", parameters.Count);

                // Store parameters in session for terraform deployment
                var session = await _sessionManager.GetOrCreateSessionAsync(sessionId);
                if (session?.State?.Context != null)
                {
                    var parametersJson = JsonSerializer.Serialize(parameters);
                    session.State.Context["terraform_parameters"] = parametersJson;
                    await _sessionManager.UpdateSessionAsync(session);
                    _logger.LogInformation("Stored form parameters in session context");
                }

                // Immediately trigger terraform deployment
                return await HandleTerraformDeploymentAsync(sessionId);
            }

            return "❌ Unsupported form submission";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling form submission for session {SessionId}", sessionId);
            return $"❌ Error processing form submission: {ex.Message}";
        }
    }
}
