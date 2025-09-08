using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace AzureAIAgent.Core.Models;

/// <summary>
/// Represents a conversation session with the AI agent
/// </summary>
public class ConversationSession
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public ConversationState State { get; set; } = new();
    public List<ConversationMessage> Messages { get; set; } = [];
    
    // Lightweight deployment tracking - let Terraform handle actual state
    public List<DeploymentReference> Deployments { get; set; } = [];
}

/// <summary>
/// Reference to a Terraform deployment - actual state is managed by Terraform
/// </summary>
public class DeploymentReference
{
    public string DeploymentId { get; set; } = string.Empty;
    public string TerraformDirectory { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
    
    // Let Terraform provide actual resource information
    public async Task<List<string>> GetDeployedResourcesAsync()
    {
        // Delegate to Terraform state
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "terraform";
            process.StartInfo.Arguments = "state list";
            process.StartInfo.WorkingDirectory = TerraformDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    // Check if Terraform state exists and is valid
    public async Task<bool> HasValidStateAsync()
    {
        try
        {
            var stateFile = Path.Combine(TerraformDirectory, "terraform.tfstate");
            if (!File.Exists(stateFile)) return false;

            var resources = await GetDeployedResourcesAsync();
            return resources.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    // Get the current Terraform state in JSON format
    public async Task<string> GetTerraformStateJsonAsync()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "terraform";
            process.StartInfo.Arguments = "show -json";
            process.StartInfo.WorkingDirectory = TerraformDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
        catch
        {
            return "{}";
        }
    }

    // Check if deployment failed and can be recovered
    public async Task<(bool canRecover, string reason)> CanRecoverFromFailureAsync()
    {
        try
        {
            var hasState = await HasValidStateAsync();
            if (!hasState)
            {
                return (false, "No valid Terraform state found");
            }

            // Check if there are resources that need to be cleaned up or continued
            var resources = await GetDeployedResourcesAsync();
            if (resources.Count == 0)
            {
                return (false, "No resources found in state");
            }

            // Check for failed resources or incomplete deployment
            var stateJson = await GetTerraformStateJsonAsync();
            if (string.IsNullOrEmpty(stateJson) || stateJson == "{}")
            {
                return (false, "Unable to read Terraform state");
            }

            return (true, $"Found {resources.Count} resources in state - can continue from last known state");
        }
        catch (Exception ex)
        {
            return (false, $"Recovery check failed: {ex.Message}");
        }
    }

    // Sync deployment status with actual Terraform state
    public async Task SyncStatusWithTerraformAsync()
    {
        try
        {
            var hasValidState = await HasValidStateAsync();
            var resources = await GetDeployedResourcesAsync();

            if (hasValidState && resources.Count > 0)
            {
                Status = DeploymentStatus.Applied;
            }
            else if (Directory.Exists(TerraformDirectory))
            {
                Status = DeploymentStatus.Failed;
            }
            else
            {
                Status = DeploymentStatus.Pending;
            }
        }
        catch
        {
            Status = DeploymentStatus.Failed;
        }
    }
}

/// <summary>
/// Simple deployment status - Terraform manages actual resource states
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeploymentStatus
{
    Pending,
    Preparing,
    Applying,
    InProgress,
    Applied,
    Succeeded,
    Failed,
    Cancelled,
    Destroyed
}

/// <summary>
/// A message in the conversation history
/// </summary>
public class ConversationMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Role of the message sender
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageRole
{
    User,
    Assistant,
    System,
    Tool
}

/// <summary>
/// Result of an agent operation
/// </summary>
public class OperationResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static OperationResult<T> Success(T data) => 
        new() { IsSuccess = true, Data = data };

    public static OperationResult<T> Failure(string error) => 
        new() { IsSuccess = false, ErrorMessage = error };

    public static OperationResult<T> SuccessWithWarnings(T data, List<string> warnings) => 
        new() { IsSuccess = true, Data = data, Warnings = warnings };
}

/// <summary>
/// Represents the state of a command execution
/// </summary>
public class CommandExecutionState
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string SessionId { get; set; } = string.Empty;
    
    public string Command { get; set; } = string.Empty;
    
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    
    public CommandStatus Status { get; set; }
    
    public string Output { get; set; } = string.Empty;
    
    public string? ResourceId { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public bool IsRecoverable { get; set; } = true;
}

/// <summary>
/// Represents the state of a template deployment
/// </summary>
public class DeploymentState
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string SessionId { get; set; } = string.Empty;
    
    public string DeploymentName { get; set; } = string.Empty;
    
    public string TemplateContent { get; set; } = string.Empty;
    
    public string? ParametersContent { get; set; }
    
    public string ResourceGroupName { get; set; } = string.Empty;
    
    public string SubscriptionId { get; set; } = string.Empty;
    
    public DeploymentStatus Status { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    public List<AzureResource> CreatedResources { get; set; } = new();
    
    public string? CorrelationId { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public bool CanRollback { get; set; } = true;
}

/// <summary>
/// Represents conversation state and context
/// </summary>
public class ConversationState
{
    [Key]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    
    public List<UserIntent> IntentHistory { get; set; } = new();
    
    public Dictionary<string, object> Context { get; set; } = new();
    
    public List<ExecutedAction> ActionHistory { get; set; } = new();
    
    public string? CurrentResourceGroup { get; set; }
    
    public string? CurrentSubscription { get; set; }
    
    public string? CurrentLocation { get; set; } = "eastus";
    
    public UserPreferences Preferences { get; set; } = new();
    
    // Additional properties for the AI agent
    public string? CurrentSubscriptionId { get; set; }
    
    public string? CurrentRegion { get; set; }
    
    public List<string> PendingOperations { get; set; } = new();
    
    // Failure tracking to avoid repeating mistakes
    public List<DeploymentFailure> FailureHistory { get; set; } = new();
    
    // Learning from failures
    public Dictionary<string, List<string>> WorkingConfigurations { get; set; } = new();
    
    public Dictionary<string, List<string>> FailedConfigurations { get; set; } = new();
    
    public AzureConfiguration Azure { get; set; } = new();
}

/// <summary>
/// Represents an Azure resource
/// </summary>
public class AzureResource
{
    public string Id { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public string Type { get; set; } = string.Empty;
    
    public string Location { get; set; } = string.Empty;
    
    public string ResourceGroup { get; set; } = string.Empty;
    
    public ResourceStatus Status { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object> Properties { get; set; } = new();
    
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Represents user intent from natural language
/// </summary>
public class UserIntent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string OriginalInput { get; set; } = string.Empty;
    
    public IntentType Type { get; set; }
    
    public string Action { get; set; } = string.Empty;
    
    public List<string> Resources { get; set; } = new();
    
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public double Confidence { get; set; }
}

/// <summary>
/// Represents an executed action
/// </summary>
public class ExecutedAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string IntentId { get; set; } = string.Empty;
    
    public ExecutionStrategy Strategy { get; set; }
    
    public string ActionType { get; set; } = string.Empty;
    
    public string Details { get; set; } = string.Empty;
    
    public ExecutionStatus Status { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    public string? Result { get; set; }
    
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// User preferences for deployments
/// </summary>
public class UserPreferences
{
    public string DefaultLocation { get; set; } = "eastus";
    
    public string DefaultResourceGroup { get; set; } = "rg-ai-agent";
    
    public string PreferredPricingTier { get; set; } = "Basic";
    
    public bool EnableAutoApproval { get; set; } = false;
    
    public bool EnableCostOptimization { get; set; } = true;
    
    public bool EnableSecurityValidation { get; set; } = true;
    
    public List<string> PreferredRegions { get; set; } = new() { "eastus", "westus2", "westeurope" };
}

/// <summary>
/// Enums for various states and types
/// </summary>
public enum CommandStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public enum ResourceStatus
{
    Creating,
    Running,
    Stopped,
    Failed,
    Deleting,
    Deleted
}

public enum IntentType
{
    Create,
    Update,
    Delete,
    Query,
    Monitor,
    Optimize
}

public enum ExecutionStrategy
{
    Command,
    Template,
    Hybrid,
    SDK
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Azure-specific configuration and preferences
/// </summary>
public class AzureConfiguration
{
    public string? DefaultSubscriptionId { get; set; }
    public string? DefaultResourceGroup { get; set; }
    public string? DefaultRegion { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public ResourceNamingConvention Naming { get; set; } = new();
}

/// <summary>
/// Resource naming convention preferences
/// </summary>
public class ResourceNamingConvention
{
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public string? Environment { get; set; }
    public bool IncludeTimestamp { get; set; } = false;
}

/// <summary>
/// Rate limiting configuration for API calls
/// </summary>
public class RateLimitConfiguration
{
    public int MaxRetries { get; set; } = 2;  // Reduced from 3 to avoid long waits
    public int BaseDelaySeconds { get; set; } = 5;  // Reduced from 60 to 5 seconds
    public bool UseExponentialBackoff { get; set; } = true;
}

/// <summary>
/// Deployment timeout and async operation configuration
/// </summary>
public class AsyncOperationConfiguration
{
    public int TimeoutMinutes { get; set; } = 60;
    public int PollIntervalSeconds { get; set; } = 30;
    public bool ContinueOnError { get; set; } = false;
}

/// <summary>
/// Tracks deployment failures to avoid repeating mistakes
/// </summary>
public class DeploymentFailure
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ResourceType { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string VmSize { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public Dictionary<string, string> FailedConfiguration { get; set; } = new();
}
public class DeploymentConfiguration
{
    public int TimeoutMinutes { get; set; } = 30;
    public int PollingIntervalSeconds { get; set; } = 30;
    public bool EnableAsyncMode { get; set; } = true;
    public bool ShowProgressUpdates { get; set; } = true;
    public string ApiBaseUrl { get; set; } = string.Empty; // Will be dynamically detected
}

/// <summary>
/// Configuration for AKS cluster context management
/// </summary>
public class AksContextConfiguration
{
    public bool AutoDiscoverClusters { get; set; } = true;
    public string DefaultClusterName { get; set; } = string.Empty;
    public string DefaultResourceGroupName { get; set; } = string.Empty;
    public string PreferredClusterName { get; set; } = string.Empty;
    public bool UseKubectlContext { get; set; } = true;
}

/// <summary>
/// Represents information about an AKS cluster
/// </summary>
public class AksClusterInfo
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string KubernetesVersion { get; set; } = string.Empty;
    public ClusterStatus Status { get; set; } = ClusterStatus.Unknown;
    public string FqdnUrl { get; set; } = string.Empty;
    public bool IsCurrentContext { get; set; } = false;
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Status of an AKS cluster
/// </summary>
public enum ClusterStatus
{
    Unknown,
    Creating,
    Running,
    Stopped,
    Failed,
    Updating,
    Deleting
}
