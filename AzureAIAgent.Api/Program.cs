using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AzureAIAgent.Core;
using AzureAIAgent.Core.Interfaces;
using AzureAIAgent.Core.Services;
using AzureAIAgent.Core.Models;
using AzureAIAgent.Api.Models;
using System.IO;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add memory cache
builder.Services.AddMemoryCache();

// Add core services
builder.Services.AddSingleton<ISessionManager, InMemorySessionManager>();
builder.Services.AddSingleton<IAzureCommandExecutor, AzureCommandExecutor>();
builder.Services.AddSingleton<ITemplateDeployer, BicepTemplateDeployer>();

// Add AKS context service for dynamic cluster management
builder.Services.AddSingleton<AzureAIAgent.Core.Services.IAksContextService, AzureAIAgent.Core.Services.AksContextService>();

// Add GitHub template and Adaptive Card services
builder.Services.AddHttpClient<AzureAIAgent.Core.Services.GitHubTemplateService>();
builder.Services.AddSingleton<AzureAIAgent.Core.Services.GitHubTemplateService>(provider =>
    new AzureAIAgent.Core.Services.GitHubTemplateService(
        provider.GetRequiredService<HttpClient>(),
        provider.GetRequiredService<ILogger<AzureAIAgent.Core.Services.GitHubTemplateService>>(),
        provider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<AzureAIAgent.Core.Services.AdaptiveCardService>();
builder.Services.AddSingleton<AzureAIAgent.Core.Services.IUniversalInteractiveService, AzureAIAgent.Core.Services.UniversalInteractiveService>();

// Add port detection service for dynamic API URL detection
builder.Services.AddHttpClient<AzureAIAgent.Core.Services.IPortDetectionService, AzureAIAgent.Core.Services.PortDetectionService>();

// Configure Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();

// Get AI configuration
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var azureOpenAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var azureOpenAiApiKey = builder.Configuration["AzureOpenAI:ApiKey"];

if (!string.IsNullOrEmpty(openAiApiKey))
{
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: builder.Configuration["OpenAI:Model"] ?? "gpt-4o", 
        apiKey: openAiApiKey);
}
else if (!string.IsNullOrEmpty(azureOpenAiEndpoint) && !string.IsNullOrEmpty(azureOpenAiApiKey))
{
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4",
        endpoint: azureOpenAiEndpoint,
        apiKey: azureOpenAiApiKey);
}
else
{
    throw new InvalidOperationException("No AI service configured. Please set OpenAI or Azure OpenAI configuration.");
}

// Register AzureResourcePlugin as a service with dependencies
builder.Services.AddSingleton<AzureAIAgent.Plugins.AzureResourcePlugin>(provider => 
{
    return new AzureAIAgent.Plugins.AzureResourcePlugin(
        provider.GetRequiredService<AzureAIAgent.Core.Services.GitHubTemplateService>());
});

// Register MCP Kubernetes Plugin
builder.Services.AddSingleton<AzureAIAgent.Plugins.McpKubernetesPlugin>();

// Register AKS MCP Plugin  
builder.Services.AddSingleton<AzureAIAgent.Plugins.AksMcpPlugin>();

// Register GitHub Terraform Plugin for actual deployment execution
builder.Services.AddSingleton<AzureAIAgent.Plugins.GitHubTerraformPlugin>(provider => 
{
    return new AzureAIAgent.Plugins.GitHubTerraformPlugin(
        provider.GetRequiredService<AzureAIAgent.Core.Services.GitHubTemplateService>(),
        provider.GetRequiredService<AzureAIAgent.Core.Services.AdaptiveCardService>(),
        provider.GetRequiredService<ILogger<AzureAIAgent.Plugins.GitHubTerraformPlugin>>(),
        provider.GetRequiredService<AzureAIAgent.Core.Interfaces.ISessionManager>());
});

// Add plugins with explicit naming and dependency injection from service provider
// Note: Plugin will be added after kernel is built and service provider is available

var kernel = kernelBuilder.Build();

// Add the plugins after services are available
builder.Services.AddSingleton(provider => 
{
    var azureResourcePlugin = provider.GetRequiredService<AzureAIAgent.Plugins.AzureResourcePlugin>();
    kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(azureResourcePlugin, "AzureResourcePlugin"));
    
    var mcpKubernetesPlugin = provider.GetRequiredService<AzureAIAgent.Plugins.McpKubernetesPlugin>();
    kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(mcpKubernetesPlugin, "McpKubernetesPlugin"));
    
    var aksMcpPlugin = provider.GetRequiredService<AzureAIAgent.Plugins.AksMcpPlugin>();
    kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(aksMcpPlugin, "AksMcpPlugin"));
    
    return kernel;
});

// Add chat completion service
builder.Services.AddSingleton<IChatCompletionService>(provider => 
    provider.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

// Add HttpClient for internal API calls
builder.Services.AddHttpClient();

// Add main AI Agent
builder.Services.AddSingleton<AzureAIAgent.Core.IAzureAIAgent, AzureAIAgent.Core.AzureAIAgent>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable static files for chat interface
app.UseDefaultFiles();

// Configure static files with no-cache for JavaScript files in development
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name.EndsWith(".js"))
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers.Pragma = "no-cache";
                ctx.Context.Response.Headers.Expires = "0";
            }
        }
    });
}
else
{
    app.UseStaticFiles();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

// Test endpoint for clickable output
app.MapGet("/api/test/clickable-output", () =>
{
    var testResponse = @"Here are your Kubernetes pods:

NAME                     READY   STATUS    RESTARTS   AGE
nginx-deployment-12345   1/1     Running   0          2d5h
redis-cache-67890       1/1     Running   1          1d  
web-frontend-abc123     0/1     Pending   0          5m
api-backend-def456      1/1     Failed    2          3h

Azure resources in your subscription:

Name                Location    ResourceGroup    Status
myWebApp           East US     rg-production    Running
myDatabase         West US     rg-data         Succeeded
myStorage          Central US  rg-storage      Creating

Docker containers:

CONTAINER ID   IMAGE                COMMAND                  CREATED       STATUS
nginx-web      nginx:latest         ""/docker-entrypoint...""   2 hours ago   Up 2 hours
redis-cache    redis:6.2-alpine     ""docker-entrypoint.s...""   1 day ago     Up 1 day";

    return Results.Ok(new { message = testResponse });
});

// Test Interactive Cards API - Direct test for interactive cards
app.MapGet("/api/test/interactive-cards", async (
    AzureAIAgent.Core.Services.AdaptiveCardService cardService,
    AzureAIAgent.Core.Services.IAksContextService aksContextService) =>
{
    try
    {
        var samplePods = new List<dynamic>
        {
            new { name = "azure-cns-s98tb", @namespace = "kube-system", status = "Running", ready = "1/1" },
            new { name = "azure-ip-masq-agent-6dh57", @namespace = "kube-system", status = "Running", ready = "1/1" },
            new { name = "azure-npm-fvpxk", @namespace = "kube-system", status = "Running", ready = "1/1" },
            new { name = "cloud-node-manager-hnf2h", @namespace = "kube-system", status = "Running", ready = "1/1" },
            new { name = "coredns-6f776c8fb5-qlrtj", @namespace = "kube-system", status = "Running", ready = "1/1" },
            new { name = "coredns-6f776c8fb5-r8ck9", @namespace = "kube-system", status = "Running", ready = "1/1" },
            new { name = "metrics-server-6bb78bfcc5-s56w6", @namespace = "kube-system", status = "Running", ready = "2/2" },
            new { name = "kube-proxy-bp799", @namespace = "kube-system", status = "Running", ready = "1/1" }
        }.Select(pod => new
        {
            name = pod.name,
            @namespace = pod.@namespace,
            status = pod.status,
            ready = pod.ready
        }).Cast<dynamic>().ToList();

        var currentCluster = await aksContextService.GetCurrentClusterAsync();
        var clusterName = currentCluster?.Name ?? "default-cluster";
        
        var interactiveCard = cardService.GenerateAksPodCards(samplePods, clusterName);
        
        return Results.Ok(new AzureAIAgent.Api.Models.ChatResponse
        {
            Message = $"🃏 Interactive Pods List for {clusterName}",
            AdaptiveCard = System.Text.Json.JsonSerializer.Deserialize<object>(interactiveCard),
            ContentType = "adaptive-card",
            Success = true
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new AzureAIAgent.Api.Models.ChatResponse
        {
            Success = false,
            Error = ex.Message
        });
    }
})
.WithName("TestInteractiveCards")
.WithTags("Test")
.WithOpenApi();

// Azure AI Agent endpoints
app.MapPost("/api/agent/chat", async (ChatRequest request, AzureAIAgent.Core.IAzureAIAgent agent) =>
{
    try
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        var result = await agent.ProcessRequestAsync(sessionId, request.Message);
        
        // Check if the result contains an adaptive card
        var response = new AzureAIAgent.Api.Models.ChatResponse
        {
            SessionId = sessionId,
            Success = true,
            Error = null
        };

        // Enhanced adaptive card detection - look for 🃏 marker and JSON content
        if (result.Contains("🃏"))
        {
            Console.WriteLine($"[DEBUG] Card emoji detected in result. Length: {result.Length}");
            Console.WriteLine($"[DEBUG] First 300 chars: {result.Substring(0, Math.Min(300, result.Length))}");
            
            // Try to extract the JSON from the response - look for first { to last }
            var jsonStart = result.IndexOf("{");
            var jsonEnd = result.LastIndexOf("}");
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                try
                {
                    var jsonContent = result.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    Console.WriteLine($"[DEBUG] Attempting to parse JSON, length: {jsonContent.Length}");
                    Console.WriteLine($"[DEBUG] JSON preview: {jsonContent.Substring(0, Math.Min(200, jsonContent.Length))}...");
                    
                    // Validate that it's actually an AdaptiveCard JSON
                    if (jsonContent.Contains("\"type\"") && jsonContent.Contains("\"AdaptiveCard\""))
                    {
                        // Parse as JsonDocument to preserve the exact structure
                        using var jsonDoc = JsonDocument.Parse(jsonContent);
                        var cardObject = jsonDoc.RootElement.Clone();
                        
                        response.AdaptiveCard = cardObject;
                        response.ContentType = "adaptive-card";
                        
                        // Extract the message text (everything before the JSON)
                        var messageText = result.Substring(0, jsonStart).Trim();
                        
                        // Clean up the message text - remove the 🃏 marker line and extra formatting
                        var lines = messageText.Split('\n');
                        var cleanLines = lines.Where(line => 
                            !line.Trim().StartsWith("🃏") && 
                            !string.IsNullOrWhiteSpace(line) &&
                            !line.Trim().StartsWith("💡")).ToList();
                        
                        response.Message = string.Join("\n", cleanLines).Trim();
                        if (string.IsNullOrEmpty(response.Message))
                        {
                            response.Message = "Interactive form generated successfully!";
                        }
                        
                        Console.WriteLine($"[DEBUG] Card parsed successfully! Content type: {response.ContentType}");
                        Console.WriteLine($"[DEBUG] Message: {response.Message}");
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] JSON doesn't appear to be an AdaptiveCard");
                        response.Message = result;
                        response.ContentType = "text";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG] JSON parsing failed: {ex.Message}");
                    // If JSON parsing fails, treat as regular text
                    response.Message = result;
                    response.ContentType = "text";
                }
            }
            else
            {
                Console.WriteLine("[DEBUG] No valid JSON found in result with 🃏 marker");
                response.Message = result;
                response.ContentType = "text";
            }
        }
        else
        {
            Console.WriteLine($"[DEBUG] No card marker found in result: {result.Substring(0, Math.Min(100, result.Length))}...");
            response.Message = result;
            response.ContentType = "text";
        }
        
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new AzureAIAgent.Api.Models.ChatResponse
        {
            SessionId = request.SessionId ?? string.Empty,
            Message = "",
            Success = false,
            Error = ex.Message,
            ContentType = "text"
        });
    }
})
.WithName("ChatWithAgent")
.WithTags("Azure AI Agent")
.WithOpenApi();

app.MapGet("/api/agent/history/{sessionId}", async (string sessionId, AzureAIAgent.Core.IAzureAIAgent agent) =>
{
    try
    {
        var result = await agent.GetChatHistoryAsync(sessionId);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
})
.WithName("GetConversationHistory")
.WithTags("Azure AI Agent")
.WithOpenApi();

app.MapDelete("/api/agent/history/{sessionId}", async (string sessionId, AzureAIAgent.Core.IAzureAIAgent agent) =>
{
    try
    {
        await agent.ClearChatHistoryAsync(sessionId);
        return Results.Ok(new { message = "Conversation cleared successfully" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("ClearConversationHistory")
.WithTags("Azure AI Agent")
.WithOpenApi();

app.MapGet("/api/agent/status", () =>
{
    return Results.Ok(new
    {
        Status = "Azure AI Agent is running",
        Version = "1.0.0",
        Timestamp = DateTime.UtcNow,
        AiServiceConfigured = !string.IsNullOrEmpty(openAiApiKey) || 
                            (!string.IsNullOrEmpty(azureOpenAiEndpoint) && !string.IsNullOrEmpty(azureOpenAiApiKey))
    });
})
.WithName("GetAgentStatus")
.WithTags("Azure AI Agent")
.WithOpenApi();

// Template Gallery API - Get available templates as Adaptive Card
app.MapGet("/api/templates/gallery", async (
    string? category,
    AzureAIAgent.Core.Services.GitHubTemplateService templateService,
    AzureAIAgent.Core.Services.AdaptiveCardService cardService) =>
{
    try
    {
        var templates = string.IsNullOrEmpty(category) 
            ? templateService.GetAvailableTemplates() 
            : templateService.GetTemplatesByCategory(category);
        var adaptiveCard = cardService.GenerateTemplateGalleryCard(templates);
        
        return Results.Ok(new AzureAIAgent.Api.Models.TemplateGalleryResponse
        {
            AdaptiveCard = adaptiveCard,
            Templates = templates.Select(t => new AzureAIAgent.Api.Models.TemplateInfo
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Category = t.Category,
                Parameters = t.Parameters.Select(p => new AzureAIAgent.Api.Models.TemplateParameterInfo
                {
                    Name = p.Name,
                    Type = p.Type,
                    Description = p.Description,
                    Default = p.Default,
                    Required = p.Required,
                    Sensitive = p.Sensitive
                }).ToList()
            }).ToList(),
            Success = true
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new AzureAIAgent.Api.Models.TemplateGalleryResponse
        {
            Success = false,
            Error = ex.Message
        });
    }
})
.WithName("GetTemplateGallery")
.WithTags("Template Management")
.WithOpenApi();

// Template Parameter Form API - Get parameter form for specific template
app.MapGet("/api/templates/{templateId}/form", async (
    string templateId,
    AzureAIAgent.Core.Services.GitHubTemplateService templateService,
    AzureAIAgent.Core.Services.AdaptiveCardService cardService) =>
{
    try
    {
        var template = templateService.GetTemplate(templateId);
        if (template == null)
        {
            return Results.NotFound(new AzureAIAgent.Api.Models.TemplateParameterFormResponse
            {
                Success = false,
                Error = $"Template '{templateId}' not found"
            });
        }

        var adaptiveCard = cardService.GenerateParameterFormCard(template);
        
        return Results.Ok(new AzureAIAgent.Api.Models.TemplateParameterFormResponse
        {
            AdaptiveCard = adaptiveCard,
            Template = new AzureAIAgent.Api.Models.TemplateInfo
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                Category = template.Category,
                Parameters = template.Parameters.Select(p => new AzureAIAgent.Api.Models.TemplateParameterInfo
                {
                    Name = p.Name,
                    Type = p.Type,
                    Description = p.Description,
                    Default = p.Default,
                    Required = p.Required,
                    Sensitive = p.Sensitive
                }).ToList()
            },
            Success = true
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new AzureAIAgent.Api.Models.TemplateParameterFormResponse
        {
            Success = false,
            Error = ex.Message
        });
    }
})
.WithName("GetTemplateParameterForm")
.WithTags("Template Management")
.WithOpenApi();

// Template Deployment API - Deploy template with parameters
app.MapPost("/api/templates/deploy", async (
    AzureAIAgent.Api.Models.TemplateDeploymentRequest request,
    AzureAIAgent.Core.Services.GitHubTemplateService templateService,
    AzureAIAgent.Core.Services.AdaptiveCardService cardService,
    IMemoryCache cache) =>
{
    try
    {
        var template = templateService.GetTemplate(request.TemplateId);
        if (template == null)
        {
            return Results.NotFound(new AzureAIAgent.Api.Models.TemplateDeploymentResponse
            {
                Success = false,
                Error = $"Template '{request.TemplateId}' not found"
            });
        }

        // Generate deployment ID
        var deploymentId = Guid.NewGuid().ToString("N")[..8];
        
        // Create initial status card
        var statusCard = cardService.GenerateDeploymentStatusCard(
            deploymentId, 
            "Starting", 
            $"Initializing deployment of {template.Name}...");

        // Store deployment status in cache (simulate deployment process)
        var deploymentStatus = new
        {
            DeploymentId = deploymentId,
            TemplateId = request.TemplateId,
            Parameters = request.Parameters,
            Status = "Starting",
            StartTime = DateTime.UtcNow
        };
        cache.Set($"template_deployment_{deploymentId}", deploymentStatus, TimeSpan.FromHours(1));

        // Start background deployment simulation
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            
            var updatedStatus = new
            {
                DeploymentId = deploymentId,
                TemplateId = request.TemplateId,
                Parameters = request.Parameters,
                Status = "Deploying",
                StartTime = DateTime.UtcNow
            };
            cache.Set($"template_deployment_{deploymentId}", updatedStatus, TimeSpan.FromHours(1));
            
            await Task.Delay(5000);
            
            var completedStatus = new
            {
                DeploymentId = deploymentId,
                TemplateId = request.TemplateId,
                Parameters = request.Parameters,
                Status = "Completed",
                StartTime = DateTime.UtcNow
            };
            cache.Set($"template_deployment_{deploymentId}", completedStatus, TimeSpan.FromHours(24));
        });

        return Results.Ok(new AzureAIAgent.Api.Models.TemplateDeploymentResponse
        {
            DeploymentId = deploymentId,
            AdaptiveCard = statusCard,
            Success = true
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new AzureAIAgent.Api.Models.TemplateDeploymentResponse
        {
            Success = false,
            Error = ex.Message
        });
    }
})
.WithName("DeployTemplate")
.WithTags("Template Management")
.WithOpenApi();

// Template Deployment Status API - Get deployment status with updated card
app.MapGet("/api/templates/deployments/{deploymentId}/status", async (
    string deploymentId,
    AzureAIAgent.Core.Services.AdaptiveCardService cardService,
    IMemoryCache cache) =>
{
    try
    {
        if (!cache.TryGetValue($"template_deployment_{deploymentId}", out var deploymentStatus))
        {
            return Results.NotFound(new { error = "Deployment not found" });
        }

        dynamic status = deploymentStatus!;
        var statusCard = cardService.GenerateDeploymentStatusCard(
            deploymentId,
            status.Status,
            $"Template '{status.TemplateId}' deployment {status.Status.ToString().ToLower()}");

        return Results.Ok(new
        {
            DeploymentId = deploymentId,
            Status = status.Status,
            AdaptiveCard = statusCard,
            Success = true
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetTemplateDeploymentStatus")
.WithTags("Template Management")
.WithOpenApi();

// Get Terraform deployment status endpoint for real-time updates
app.MapGet("/api/azure/terraform-status/{deploymentId}", async (
    string deploymentId,
    IServiceProvider serviceProvider) =>
{
    try
    {
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("📊 Status request for deployment {DeploymentId}", deploymentId);
        
        if (!cache.TryGetValue($"deployment_status_{deploymentId}", out var deploymentStatusObj))
        {
            return Results.NotFound(new { 
                success = false,
                error = "Deployment not found",
                deploymentId = deploymentId
            });
        }

        var status = deploymentStatusObj as DeploymentProgressStatus;
        if (status == null)
        {
            return Results.BadRequest(new { 
                success = false,
                error = "Invalid deployment status format",
                deploymentId = deploymentId
            });
        }

        return Results.Ok(new
        {
            success = true,
            deploymentId = status.DeploymentId,
            status = status.Status,
            progress = status.Progress,
            message = status.Message,
            terraformPhase = status.TerraformPhase,
            detailedMessage = status.DetailedMessage,
            isCompleted = status.IsCompleted,
            hasError = status.HasError,
            errorMessage = status.ErrorMessage,
            startTime = status.StartTime,
            completedTime = status.CompletedTime,
            resourcesCreated = status.ResourcesCreated,
            phaseLog = status.PhaseLog
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { 
            success = false,
            error = ex.Message,
            deploymentId = deploymentId
        });
    }
})
.WithName("GetTerraformDeploymentStatus")
.WithTags("Terraform")
.WithOpenApi();

// Terraform deployment endpoint
app.MapPost("/api/azure/deploy-terraform", async (
    TerraformDeployRequest request,
    IServiceProvider serviceProvider) =>
{
    try
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("🚀 API /api/azure/deploy-terraform called with sessionId={SessionId}, parameters count={ParameterCount}", 
            request.SessionId, request.Parameters?.Count ?? 0);
        
        var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
        var terraformPlugin = serviceProvider.GetRequiredService<AzureAIAgent.Plugins.GitHubTerraformPlugin>();
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        
        // Create deployment ID
        var deploymentId = Guid.NewGuid().ToString();
        logger.LogInformation("Generated deployment ID: {DeploymentId}", deploymentId);
        
        // Store Terraform configuration in session
        var session = await sessionManager.GetOrCreateSessionAsync(request.SessionId ?? Guid.NewGuid().ToString());
        
        // Store parameters in session if provided
        if (request.Parameters != null && request.Parameters.Count > 0)
        {
            var parametersJson = JsonSerializer.Serialize(request.Parameters);
            if (session?.State?.Context != null)
            {
                session.State.Context["terraform_parameters"] = parametersJson;
                await sessionManager.UpdateSessionAsync(session);
                logger.LogInformation("✅ Stored {Count} parameters in session {SessionId} for terraform deployment", 
                    request.Parameters.Count, request.SessionId);
            }
            else
            {
                logger.LogWarning("❌ Cannot store parameters - session, state, or context is null for session {SessionId}", request.SessionId);
            }
        }
        else
        {
            logger.LogInformation("ℹ️ No parameters provided in API request for session {SessionId}", request.SessionId);
        }
        
        logger.LogInformation("About to start background task for deployment {DeploymentId}", deploymentId);
        Console.WriteLine($"🚀 Creating background task for deployment: {deploymentId}");
        Console.WriteLine($"🔧 ServiceProvider available: {serviceProvider != null}");
        Console.WriteLine($"📥 Request details - SessionId: {request.SessionId}, ParameterCount: {request.Parameters?.Count ?? 0}");
        
        // Get logger separately since we need it in background task
        var taskLogger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        Console.WriteLine("✅ Successfully retrieved all required services for background task");
        
        // Start actual deployment in background using GitHubTerraformPlugin
        var backgroundTask = Task.Run(async () =>
        {
            Console.WriteLine("🔥🔥🔥 BACKGROUND TASK STARTED - DEPLOYMENT ID: " + deploymentId);
            Console.WriteLine("🔧 Background task is now running...");
            
            try
            {
                taskLogger.LogInformation("🔥 Background deployment task ACTUALLY STARTED for deployment {DeploymentId}", deploymentId);
                
                Console.WriteLine("🔧 Using pre-retrieved GitHubTerraformPlugin...");
                
                // Plugin is already retrieved before background task
                if (terraformPlugin == null)
                {
                    Console.WriteLine("❌❌❌ ERROR: GitHubTerraformPlugin is NULL!");
                    taskLogger.LogError("❌ GitHubTerraformPlugin is null! Cannot proceed with deployment");
                    throw new InvalidOperationException("GitHubTerraformPlugin not available");
                }
                
                Console.WriteLine("✅ GitHubTerraformPlugin obtained successfully");
                taskLogger.LogInformation("✅ Successfully obtained GitHubTerraformPlugin instance");
                
                // Initial status
                Console.WriteLine("📊 Setting up initial deployment status...");
                var status = new DeploymentProgressStatus
                {
                    DeploymentId = deploymentId,
                    Status = "Initializing",
                    Progress = 10,
                    Message = "Starting Terraform deployment...",
                    StartTime = DateTime.UtcNow,
                    ResourcesCreated = null,
                    TerraformPhase = "initializing",
                    DetailedMessage = "Preparing Terraform environment and downloading templates...",
                    PhaseLog = new List<string> { $"{DateTime.UtcNow:HH:mm:ss} - Deployment started" }
                };
                cache.Set($"deployment_status_{deploymentId}", status, TimeSpan.FromHours(1));

                // Define status update action for the plugin to use
                Action<string, int, string, string> updateStatus = (phase, progress, message, detailedMessage) =>
                {
                    Console.WriteLine($"📊 STATUS UPDATE: {phase} - {progress}% - {message}");
                    status.TerraformPhase = phase;
                    status.Progress = progress;
                    status.Message = message;
                    status.DetailedMessage = detailedMessage;
                    status.PhaseLog.Add($"{DateTime.UtcNow:HH:mm:ss} - {phase}: {detailedMessage}");
                    status.LastOutputTime = DateTime.UtcNow;
                    cache.Set($"deployment_status_{deploymentId}", status, TimeSpan.FromHours(1));
                    taskLogger.LogInformation("📊 Status update: {Phase} - {Progress}% - {Message}", phase, progress, message);
                };

                // Define terraform output callback for real-time streaming
                Action<string> outputCallback = (output) =>
                {
                    Console.WriteLine($"📟 TERRAFORM OUTPUT: {output}");
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        status.TerraformOutput.Add($"{DateTime.UtcNow:HH:mm:ss} {output}");
                        status.LastOutputTime = DateTime.UtcNow;
                        
                        // Keep only last 500 lines to prevent memory issues
                        if (status.TerraformOutput.Count > 500)
                        {
                            status.TerraformOutput.RemoveAt(0);
                        }
                        
                        // Update current command based on output
                        if (output.StartsWith("$ terraform"))
                        {
                            status.CurrentTerraformCommand = output;
                        }
                        
                        cache.Set($"deployment_status_{deploymentId}", status, TimeSpan.FromHours(1));
                        taskLogger.LogInformation("📟 Terraform output: {Output}", output);
                    }
                };

                // Update status before calling plugin
                Console.WriteLine("📥 Updating status to 'downloading'...");
                updateStatus("downloading", 20, "Downloading templates", "Fetching Terraform templates from repository...");

                // Get parameters from session context (where they were stored during form submission)
                string parametersJson = null;
                try 
                {
                    var session = await sessionManager.GetOrCreateSessionAsync(request.SessionId ?? Guid.NewGuid().ToString());
                    if (session?.State?.Context?.ContainsKey("terraform_parameters") == true)
                    {
                        parametersJson = session.State.Context["terraform_parameters"]?.ToString();
                        Console.WriteLine($"✅ Retrieved parameters from session context: {parametersJson?.Substring(0, Math.Min(200, parametersJson?.Length ?? 0)) ?? "NULL"}");
                    }
                    else
                    {
                        Console.WriteLine("❌ No terraform_parameters found in session context");
                        Console.WriteLine($"🔍 Session context keys: {string.Join(", ", session?.State?.Context?.Keys?.ToArray() ?? new string[0])}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error retrieving parameters from session: {ex.Message}");
                    taskLogger.LogError(ex, "Error retrieving parameters from session");
                }

                Console.WriteLine($"🔍 Parameters JSON: {parametersJson ?? "NULL"}");
                Console.WriteLine($"🔍 Session ID: {request.SessionId}");
                Console.WriteLine("🚀 About to call terraformPlugin.DeployTemplateWithCallback...");
                
                taskLogger.LogInformation("About to call terraformPlugin.DeployTemplateWithCallback with templateId=aks-cluster, parametersJson={ParametersJson}, sessionId={SessionId}", 
                    parametersJson, request.SessionId);

                // Update status to initializing
                updateStatus("initializing", 30, "Initializing Terraform", "Preparing Terraform environment...");

                // Call the Terraform deployment plugin with callback for real-time output
                Console.WriteLine("🔄 Calling terraformPlugin.DeployTemplateWithCallback NOW...");
                var deploymentResult = terraformPlugin.DeployTemplateWithCallback(
                    "aks-cluster", 
                    parametersJson,
                    request.SessionId,
                    outputCallback).Result; // Use .Result to make it synchronous

                Console.WriteLine($"✅ terraformPlugin.DeployTemplate COMPLETED!");
                Console.WriteLine($"📝 Result length: {deploymentResult?.Length ?? 0}");
                Console.WriteLine($"📝 Result preview: {deploymentResult?.Substring(0, Math.Min(500, deploymentResult?.Length ?? 0)) ?? "NULL"}");
                
                taskLogger.LogInformation("✅ terraformPlugin.DeployTemplate completed. Result length: {Length}, First 500 chars: {Result}", 
                    deploymentResult?.Length ?? 0, 
                    deploymentResult?.Substring(0, Math.Min(500, deploymentResult?.Length ?? 0)) ?? "null");

                // Parse deployment result to determine success/failure
                bool isSuccess = !string.IsNullOrEmpty(deploymentResult) && 
                                deploymentResult.Contains("Apply complete") && 
                                !deploymentResult.Contains("❌ Failed") &&
                                !deploymentResult.Contains("Error");

                Console.WriteLine($"🔍 DEPLOYMENT ANALYSIS:");
                Console.WriteLine($"  - Result is null/empty: {string.IsNullOrEmpty(deploymentResult)}");
                Console.WriteLine($"  - Contains 'Apply complete': {deploymentResult?.Contains("Apply complete") ?? false}");
                Console.WriteLine($"  - Contains '❌ Failed': {deploymentResult?.Contains("❌ Failed") ?? false}");
                Console.WriteLine($"  - Contains 'Error': {deploymentResult?.Contains("Error") ?? false}");
                Console.WriteLine($"  - Final Success Status: {isSuccess}");

                taskLogger.LogInformation("Deployment result analysis: isSuccess={IsSuccess}, contains 'Apply complete'={ContainsApplyComplete}, contains error markers={ContainsErrors}", 
                    isSuccess, 
                    deploymentResult?.Contains("Apply complete") ?? false,
                    (deploymentResult?.Contains("❌ Failed") ?? false) || (deploymentResult?.Contains("Error") ?? false));

                if (isSuccess)
                {
                    Console.WriteLine("🎉 DEPLOYMENT SUCCESS! Updating status to completed...");
                    // Success status
                    updateStatus("completed", 100, "Deployment completed successfully!", "All Azure resources have been created successfully.");
                    status.Status = "Completed";
                    status.IsCompleted = true;
                    status.CompletedTime = DateTime.UtcNow;
                    status.ResourcesCreated = new[] { "Resource Group", "AKS Cluster", "Virtual Network", "Network Security Group" };
                    cache.Set($"deployment_status_{deploymentId}", status, TimeSpan.FromHours(24));
                }
                else
                {
                    Console.WriteLine("❌ DEPLOYMENT FAILED! Updating status to error...");
                    Console.WriteLine($"❌ Error details: {deploymentResult}");
                    // Failure status
                    updateStatus("error", 0, "Deployment failed", $"Deployment failed. Error: {deploymentResult}");
                    status.Status = "Failed";
                    status.IsCompleted = true;
                    status.HasError = true;
                    status.CompletedTime = DateTime.UtcNow;
                    status.ErrorMessage = deploymentResult;
                    cache.Set($"deployment_status_{deploymentId}", status, TimeSpan.FromHours(1));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌❌❌ CRITICAL ERROR IN BACKGROUND TASK!");
                Console.WriteLine($"❌ Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"❌ Exception Message: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                Console.WriteLine($"❌ Inner Exception: {ex.InnerException?.Message ?? "None"}");
                
                taskLogger.LogError(ex, "❌ CRITICAL ERROR in background deployment task for deployment {DeploymentId}", deploymentId);
                taskLogger.LogError("❌ Exception Type: {ExceptionType}", ex.GetType().Name);
                taskLogger.LogError("❌ Exception Message: {ExceptionMessage}", ex.Message);
                taskLogger.LogError("❌ Stack Trace: {StackTrace}", ex.StackTrace);
                
                var errorStatus = new DeploymentProgressStatus
                {
                    DeploymentId = deploymentId,
                    Status = "Failed",
                    Progress = 0,
                    Message = $"Deployment failed: {ex.Message}",
                    DetailedMessage = $"Error: {ex.GetType().Name} - {ex.Message}",
                    StartTime = DateTime.UtcNow,
                    CompletedTime = DateTime.UtcNow,
                    HasError = true,
                    ErrorMessage = ex.ToString(),
                    ResourcesCreated = null,
                    PhaseLog = new List<string> 
                    { 
                        $"{DateTime.UtcNow:HH:mm:ss} - Deployment started",
                        $"{DateTime.UtcNow:HH:mm:ss} - ERROR: {ex.Message}"
                    }
                };
                cache.Set($"deployment_status_{deploymentId}", errorStatus, TimeSpan.FromHours(1));
                
                Console.WriteLine("💾 Error status saved to cache");
            }
            finally
            {
                Console.WriteLine($"🏁 Background task completed for deployment: {deploymentId}");
            }
        });
        
        // Don't await the task, but make sure it actually starts
        logger.LogInformation("Background task created successfully, task status: {TaskStatus}", backgroundTask.Status);
        
        // Store the deployment state in context
        if (session?.State?.Context != null)
        {
            session.State.Context["LastTerraformDeployment"] = new
            {
                Code = request.TerraformCode,
                Parameters = request.Parameters,
                DeployedAt = DateTime.UtcNow,
                Status = "InProgress",
                DeploymentId = deploymentId
            };
        }
        
        await sessionManager.UpdateSessionAsync(session);
        
        return Results.Ok(new TerraformDeployResponse
        {
            Success = true,
            ResourcesCreated = "Deployment started",
            DeploymentTime = "Starting...",
            SessionId = session.Id,
            DeploymentId = deploymentId
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new TerraformDeployResponse
        {
            Success = false,
            Error = ex.Message,
            Suggestion = "Please check your Terraform configuration and Azure credentials."
        });
    }
})
.WithName("DeployTerraform")
.WithTags("Azure AI Agent")
.WithOpenApi();

// Add deployment status endpoint
app.MapGet("/api/azure/deployment-status/{deploymentId}", async (
    string deploymentId,
    IServiceProvider serviceProvider) =>
{
    try
    {
        var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
        
        // Check if deployment status exists in cache
        if (cache.TryGetValue($"deployment_status_{deploymentId}", out var status))
        {
            return Results.Ok(status);
        }
        
        // Return not found if deployment doesn't exist
        return Results.NotFound(new { message = "Deployment not found" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetDeploymentStatus")
.WithTags("Azure AI Agent")
.WithOpenApi();

// Terraform state sync and recovery endpoint (now on-demand only)
app.MapPost("/api/azure/sync-terraform-state", async (
    TerraformSyncRequest request,
    IServiceProvider serviceProvider) =>
{
    try
    {
        var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
        
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        
        // Perform on-demand sync only when explicitly requested
        await sessionManager.SyncTerraformStateAsync(sessionId);
        
        // Get session after sync
        var session = await sessionManager.GetSessionAsync(sessionId);
        if (session == null)
        {
            return Results.BadRequest(new { success = false, message = "Session not found after sync" });
        }
        
        var deploymentSummary = new List<object>();
        var recoverableDeployments = new List<object>();
        
        foreach (var deployment in session.Deployments)
        {
            await deployment.SyncStatusWithTerraformAsync();
            var resources = await deployment.GetDeployedResourcesAsync();
            var (canRecover, reason) = await deployment.CanRecoverFromFailureAsync();
            
            deploymentSummary.Add(new
            {
                DeploymentId = deployment.DeploymentId,
                Status = deployment.Status.ToString(),
                ResourceCount = resources.Count,
                Resources = resources,
                CanRecover = canRecover,
                RecoveryReason = reason,
                CreatedAt = deployment.CreatedAt
            });
            
            if (canRecover)
            {
                recoverableDeployments.Add(new
                {
                    DeploymentId = deployment.DeploymentId,
                    Reason = reason,
                    Resources = resources
                });
            }
        }
        
        await sessionManager.UpdateSessionAsync(session);
        
        return Results.Ok(new TerraformSyncResponse
        {
            Success = true,
            SessionId = session.Id,
            TotalDeployments = session.Deployments.Count,
            ActiveDeployments = session.Deployments.Count(d => d.Status == AzureAIAgent.Core.Models.DeploymentStatus.Applied),
            FailedDeployments = session.Deployments.Count(d => d.Status == AzureAIAgent.Core.Models.DeploymentStatus.Failed),
            RecoverableDeployments = recoverableDeployments.Count,
            DeploymentSummary = deploymentSummary,
            RecoverableDeploymentsList = recoverableDeployments,
            Message = $"Synchronized {session.Deployments.Count} deployments. {recoverableDeployments.Count} can be recovered."
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new TerraformSyncResponse
        {
            Success = false,
            Error = ex.Message,
            Message = "Failed to sync Terraform state. Please check your environment."
        });
    }
})
.WithName("SyncTerraformState")
.WithTags("Azure AI Agent")
.WithOpenApi();

// Terraform deployment recovery endpoint
app.MapPost("/api/azure/recover-terraform-deployment", async (
    TerraformRecoveryRequest request,
    IServiceProvider serviceProvider) =>
{
    try
    {
        var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
        
        // Get session
        var session = await sessionManager.GetOrCreateSessionAsync(request.SessionId ?? Guid.NewGuid().ToString());
        
        // Find the deployment to recover
        var deployment = session.Deployments.FirstOrDefault(d => d.DeploymentId == request.DeploymentId);
        if (deployment == null)
        {
            return Results.Ok(new TerraformRecoveryResponse
            {
                Success = false,
                Error = $"Deployment {request.DeploymentId} not found in session."
            });
        }

        // Check if recovery is possible
        var (canRecover, reason) = await deployment.CanRecoverFromFailureAsync();
        if (!canRecover)
        {
            return Results.Ok(new TerraformRecoveryResponse
            {
                Success = false,
                Error = $"Cannot recover deployment: {reason}"
            });
        }

        // Attempt recovery by continuing from current state
        await deployment.SyncStatusWithTerraformAsync();
        var resources = await deployment.GetDeployedResourcesAsync();
        
        // Update deployment status
        deployment.Status = AzureAIAgent.Core.Models.DeploymentStatus.Applied;
        await sessionManager.UpdateSessionAsync(session);
        
        return Results.Ok(new TerraformRecoveryResponse
        {
            Success = true,
            DeploymentId = deployment.DeploymentId,
            SessionId = session.Id,
            ResourcesRecovered = resources,
            Message = $"Successfully recovered deployment {deployment.DeploymentId} with {resources.Count} resources."
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new TerraformRecoveryResponse
        {
            Success = false,
            Error = ex.Message,
            Message = "Failed to recover deployment. Please check the deployment state."
        });
    }
})
.WithName("RecoverTerraformDeployment")
.WithTags("Azure AI Agent")
.WithOpenApi();

// Keep the original WeatherForecast for testing
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithTags("Sample")
.WithOpenApi();

// Deployment status page for real-time monitoring
app.MapGet("/deployment-status/{deploymentId}", async (string deploymentId, IServiceProvider serviceProvider) =>
{
    var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Terraform Deployment Status - {deploymentId}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            margin: 0;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
        }}
        .container {{
            max-width: 800px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .deployment-id {{
            font-family: 'Courier New', monospace;
            background: #f5f5f5;
            padding: 8px 12px;
            border-radius: 6px;
            display: inline-block;
            margin-top: 10px;
        }}
        .status-card {{
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
            transition: all 0.3s ease;
        }}
        .status-initializing {{ border-color: #ffa726; background: #fff8e1; }}
        .status-downloading {{ border-color: #42a5f5; background: #e3f2fd; }}
        .status-init {{ border-color: #26c6da; background: #e0f7fa; }}
        .status-plan {{ border-color: #ffa726; background: #fff8e1; }}
        .status-apply {{ border-color: #ff7043; background: #fbe9e7; }}
        .status-completed {{ border-color: #66bb6a; background: #e8f5e8; }}
        .status-error {{ border-color: #ef5350; background: #ffebee; }}
        .progress-bar {{
            background: #e0e0e0;
            border-radius: 10px;
            height: 20px;
            overflow: hidden;
            margin: 15px 0;
        }}
        .progress-fill {{
            height: 100%;
            background: linear-gradient(90deg, #4caf50, #8bc34a);
            transition: width 0.5s ease;
            border-radius: 10px;
        }}
        .phase-log {{
            background: #f8f9fa;
            border-radius: 6px;
            padding: 15px;
            margin: 15px 0;
            max-height: 200px;
            overflow-y: auto;
            font-family: 'Courier New', monospace;
            font-size: 12px;
        }}
        .loading {{
            display: inline-block;
            width: 20px;
            height: 20px;
            border: 3px solid #f3f3f3;
            border-top: 3px solid #3498db;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }}
        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}
        .timestamp {{
            color: #666;
            font-size: 12px;
        }}
        .auto-refresh {{
            text-align: center;
            margin-top: 20px;
            padding: 10px;
            background: #f0f0f0;
            border-radius: 6px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🚀 Terraform Deployment Status</h1>
            <div class=""deployment-id"">Deployment ID: {deploymentId}</div>
        </div>
        
        <div id=""status-container"">
            <div class=""loading""></div> Loading status...
        </div>
        
        <div class=""auto-refresh"">
            🔄 Auto-refreshing every 3 seconds
        </div>
    </div>

    <script>
        const deploymentId = '{deploymentId}';
        let lastUpdate = '';
        
        async function fetchStatus() {{
            try {{
                const response = await fetch(`/api/azure/terraform-status/${{deploymentId}}`);
                const data = await response.json();
                
                if (data.success) {{
                    updateStatusDisplay(data);
                }} else {{
                    document.getElementById('status-container').innerHTML = `
                        <div class=""status-card status-error"">
                            <h3>❌ Error</h3>
                            <p>${{data.error || 'Deployment not found'}}</p>
                        </div>
                    `;
                }}
            }} catch (error) {{
                console.error('Error fetching status:', error);
                document.getElementById('status-container').innerHTML = `
                    <div class=""status-card status-error"">
                        <h3>❌ Connection Error</h3>
                        <p>Unable to fetch deployment status</p>
                    </div>
                `;
            }}
        }}
        
        function updateStatusDisplay(data) {{
            const statusClass = `status-${{data.terraformPhase || 'initializing'}}`;
            const progressPercent = data.progress || 0;
            
            const phaseLogHtml = data.phaseLog && data.phaseLog.length > 0 
                ? data.phaseLog.map(log => `<div>${{log}}</div>`).join('')
                : '<div>No logs available</div>';
                
            const resourcesHtml = data.resourcesCreated && data.resourcesCreated.length > 0
                ? `<div><strong>Resources Created:</strong><ul>${{data.resourcesCreated.map(r => `<li>${{r}}</li>`).join('')}}</ul></div>`
                : '';
            
            document.getElementById('status-container').innerHTML = `
                <div class=""status-card ${{statusClass}}"">
                    <h3>${{getPhaseIcon(data.terraformPhase)}} ${{data.message || 'Processing...'}}</h3>
                    <p><strong>Phase:</strong> ${{data.terraformPhase || 'initializing'}}</p>
                    <p><strong>Status:</strong> ${{data.status || 'Running'}}</p>
                    <p><strong>Details:</strong> ${{data.detailedMessage || 'Working...'}}</p>
                    
                    <div class=""progress-bar"">
                        <div class=""progress-fill"" style=""width: ${{progressPercent}}%""></div>
                    </div>
                    <div class=""timestamp"">Progress: ${{progressPercent}}% | Started: ${{new Date(data.startTime).toLocaleString()}}</div>
                    
                    ${{resourcesHtml}}
                    
                    ${{data.hasError ? `<div style=""color: red; margin-top: 10px;""><strong>Error:</strong> ${{data.errorMessage}}</div>` : ''}}
                    
                    <details style=""margin-top: 15px;"">
                        <summary>Phase Log</summary>
                        <div class=""phase-log"">${{phaseLogHtml}}</div>
                    </details>
                </div>
            `;
        }}
        
        function getPhaseIcon(phase) {{
            const icons = {{
                'initializing': '🔄',
                'downloading': '📥',
                'init': '⚙️',
                'plan': '📋',
                'apply': '🚀',
                'completed': '✅',
                'error': '❌'
            }};
            return icons[phase] || '🔄';
        }}
        
        // Initial fetch
        fetchStatus();
        
        // Auto-refresh every 3 seconds
        setInterval(fetchStatus, 3000);
    </script>
</body>
</html>";

    return Results.Content(html, "text/html");
})
.WithName("GetDeploymentStatusPage")
.WithTags("Deployment Status")
.WithOpenApi();

app.Run();

// DTOs for API
public record ChatRequest(string Message, string? SessionId = null);

public record ChatResponse
{
    public string SessionId { get; init; } = string.Empty;
    public string Response { get; init; } = string.Empty;
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Warnings { get; init; } = new();
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Terraform deployment DTOs
public record TerraformDeployRequest
{
    public string TerraformCode { get; init; } = string.Empty;
    public Dictionary<string, string> Parameters { get; init; } = new();
    public string? SessionId { get; init; }
}

public record TerraformDeployResponse
{
    public bool Success { get; init; }
    public string? ResourcesCreated { get; init; }
    public string? DeploymentTime { get; init; }
    public string? SessionId { get; init; }
    public string? DeploymentId { get; init; }
    public string? Error { get; init; }
    public string? Suggestion { get; init; }
}

// Terraform state sync DTOs
public record TerraformSyncRequest
{
    public string? SessionId { get; init; }
}

public record TerraformSyncResponse
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public int TotalDeployments { get; init; }
    public int ActiveDeployments { get; init; }
    public int FailedDeployments { get; init; }
    public int RecoverableDeployments { get; init; }
    public IEnumerable<object> DeploymentSummary { get; init; } = Enumerable.Empty<object>();
    public IEnumerable<object> RecoverableDeploymentsList { get; init; } = Enumerable.Empty<object>();
    public string? Message { get; init; }
    public string? Error { get; init; }
}

// Deployment status model
public class DeploymentProgressStatus
{
    public string DeploymentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string[]? ResourcesCreated { get; set; }
    
    // Enhanced Terraform-specific status tracking
    public string TerraformPhase { get; set; } = string.Empty; // "init", "plan", "apply", "complete", "error"
    public string DetailedMessage { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public bool HasError { get; set; } = false;
    public DateTime? CompletedTime { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> PhaseLog { get; set; } = new List<string>();
    
    // Real-time Terraform output streaming
    public List<string> TerraformOutput { get; set; } = new List<string>();
    public string CurrentTerraformCommand { get; set; } = string.Empty;
    public DateTime? LastOutputTime { get; set; }
}

// Terraform recovery DTOs
public record TerraformRecoveryRequest
{
    public string DeploymentId { get; init; } = string.Empty;
    public string? SessionId { get; init; }
}

public record TerraformRecoveryResponse
{
    public bool Success { get; init; }
    public string? DeploymentId { get; init; }
    public string? SessionId { get; init; }
    public IEnumerable<string> ResourcesRecovered { get; init; } = Enumerable.Empty<string>();
    public string? Message { get; init; }
    public string? Error { get; init; }
}
