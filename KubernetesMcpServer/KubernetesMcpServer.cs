using System.Text.Json;
using Microsoft.Extensions.Logging;
using KubernetesMcpServer.Models;
using KubernetesMcpServer.Services;

namespace KubernetesMcpServer;

/// <summary>
/// Enterprise Kubernetes MCP Server
/// Provides secure, auditable access to Kubernetes clusters via Model Context Protocol
/// </summary>
public class KubernetesMcpServer
{
    private readonly KubernetesService _kubernetesService;
    private readonly ILogger<KubernetesMcpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public KubernetesMcpServer(KubernetesService kubernetesService, ILogger<KubernetesMcpServer> logger)
    {
        _kubernetesService = kubernetesService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Handle MCP initialization request
    /// </summary>
    public async Task<McpMessage> HandleInitializeAsync(McpMessage request)
    {
        _logger.LogInformation("Initializing Kubernetes MCP Server");

        var serverInfo = new McpServerInfo();
        var capabilities = new McpCapabilities
        {
            Tools = new ToolsCapability { ListChanged = false },
            Resources = new ResourcesCapability { Subscribe = false, ListChanged = false }
        };

        var result = new
        {
            protocolVersion = "2024-11-05",
            serverInfo,
            capabilities
        };

        return new McpMessage
        {
            Id = request.Id,
            Result = JsonSerializer.SerializeToElement(result, _jsonOptions)
        };
    }

    /// <summary>
    /// Handle tools/list request - returns available Kubernetes operations
    /// </summary>
    public async Task<McpMessage> HandleToolsListAsync(McpMessage request)
    {
        _logger.LogInformation("Listing available tools");

        var tools = new List<McpTool>
        {
            new()
            {
                Name = "connect_current_context",
                Description = "Connect to Kubernetes cluster using current kubectl context",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name to assign to the cluster connection" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "connect_aks_cluster",
                Description = "Connect to an Azure Kubernetes Service (AKS) cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the AKS cluster" },
                        resourceGroup = new { type = "string", description = "Azure resource group name" },
                        subscriptionId = new { type = "string", description = "Azure subscription ID (optional)" }
                    },
                    required = new[] { "clusterName", "resourceGroup" }
                })
            },
            new()
            {
                Name = "get_pods",
                Description = "Get all pods from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_pod_details",
                Description = "Get detailed information about a specific pod",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        podName = new { type = "string", description = "Name of the pod" },
                        @namespace = new { type = "string", description = "Kubernetes namespace hint (optional)" }
                    },
                    required = new[] { "clusterName", "podName" }
                })
            },
            new()
            {
                Name = "get_deployments",
                Description = "Get all deployments from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_services",
                Description = "Get all services from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_namespaces",
                Description = "Get all namespaces from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_configmaps",
                Description = "Get all config maps from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_secrets",
                Description = "Get all secrets from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_ingress",
                Description = "Get all ingress resources from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_persistent_volumes",
                Description = "Get all persistent volumes from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_cronjobs",
                Description = "Get all CronJobs from a connected Kubernetes cluster", 
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_jobs",
                Description = "Get all Jobs from a connected Kubernetes cluster",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        @namespace = new { type = "string", description = "Kubernetes namespace filter (optional)" }
                    },
                    required = new[] { "clusterName" }
                })
            },
            new()
            {
                Name = "get_pod_logs",
                Description = "Get logs from a specific pod container",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        podName = new { type = "string", description = "Name of the pod" },
                        @namespace = new { type = "string", description = "Kubernetes namespace (optional)" },
                        containerName = new { type = "string", description = "Container name (optional)" },
                        tailLines = new { type = "integer", description = "Number of lines to retrieve (default: 100)", minimum = 1, maximum = 1000 }
                    },
                    required = new[] { "clusterName", "podName" }
                })
            },
            new()
            {
                Name = "describe_pod",
                Description = "Get detailed description of a specific pod (equivalent to kubectl describe pod)",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new
                    {
                        clusterName = new { type = "string", description = "Name of the connected cluster" },
                        podName = new { type = "string", description = "Name of the pod" },
                        @namespace = new { type = "string", description = "Kubernetes namespace (optional)" }
                    },
                    required = new[] { "clusterName", "podName" }
                })
            },
            new()
            {
                Name = "list_connected_clusters",
                Description = "List all currently connected Kubernetes clusters",
                InputSchema = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new { }
                })
            }
        };

        var result = new { tools };

        return new McpMessage
        {
            Id = request.Id,
            Result = JsonSerializer.SerializeToElement(result, _jsonOptions)
        };
    }

    /// <summary>
    /// Handle tools/call request - execute Kubernetes operations
    /// </summary>
    public async Task<McpMessage> HandleToolsCallAsync(McpMessage request)
    {
        try
        {
            if (!request.Params.HasValue)
            {
                return CreateErrorResponse(request.Id, -32602, "Missing parameters");
            }

            var @params = request.Params.Value;
            var toolName = @params.GetProperty("name").GetString();
            var arguments = @params.GetProperty("arguments");

            _logger.LogInformation("Executing tool: {ToolName}", toolName);

            object result = toolName switch
            {
                "connect_current_context" => await HandleConnectCurrentContext(arguments),
                "connect_aks_cluster" => await HandleConnectAksCluster(arguments),
                "get_pods" => await HandleGetPods(arguments),
                "get_pod_details" => await HandleGetPodDetails(arguments),
                "get_pod_logs" => await HandleGetPodLogs(arguments),
                "describe_pod" => await HandleDescribePod(arguments),
                "get_deployments" => await HandleGetDeployments(arguments),
                "get_services" => await HandleGetServices(arguments),
                "get_configmaps" => await HandleGetConfigMaps(arguments),
                "get_secrets" => await HandleGetSecrets(arguments),
                "get_ingress" => await HandleGetIngress(arguments),
                "get_persistent_volumes" => await HandleGetPersistentVolumes(arguments),
                "get_cronjobs" => await HandleGetCronJobs(arguments),
                "get_jobs" => await HandleGetJobs(arguments),
                "get_namespaces" => await HandleGetNamespaces(arguments),
                "list_connected_clusters" => HandleListConnectedClusters(),
                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };

            return new McpMessage
            {
                Id = request.Id,
                Result = JsonSerializer.SerializeToElement(new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, _jsonOptions) } } }, _jsonOptions)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool");
            return CreateErrorResponse(request.Id, -32603, $"Tool execution failed: {ex.Message}");
        }
    }

    private async Task<object> HandleConnectCurrentContext(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;

        var success = await _kubernetesService.ConnectToCurrentContextAsync(clusterName);
        
        return new
        {
            success,
            message = success ? $"Successfully connected to cluster using kubectl context: {clusterName}" : $"Failed to connect to cluster using kubectl context: {clusterName}",
            clusterName,
            contextMethod = "kubectl"
        };
    }

    private async Task<object> HandleConnectAksCluster(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var resourceGroup = arguments.GetProperty("resourceGroup").GetString()!;
        var subscriptionId = arguments.TryGetProperty("subscriptionId", out var subProp) ? subProp.GetString() : null;

        var success = await _kubernetesService.ConnectToAksClusterAsync(clusterName, resourceGroup, subscriptionId);
        
        return new
        {
            success,
            message = success ? $"Successfully connected to AKS cluster: {clusterName}" : $"Failed to connect to AKS cluster: {clusterName}",
            clusterName,
            resourceGroup,
            subscriptionId
        };
    }

    private async Task<JsonElement> HandleGetPods(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;

        return await _kubernetesService.GetPodsAsync(clusterName, @namespace);
    }

    private async Task<JsonElement> HandleGetPodDetails(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var podName = arguments.GetProperty("podName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;

        return await _kubernetesService.GetPodDetailsAsync(clusterName, podName, @namespace);
    }

    private async Task<JsonElement> HandleGetPodLogs(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var podName = arguments.GetProperty("podName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;
        var containerName = arguments.TryGetProperty("containerName", out var containerProp) ? containerProp.GetString() : null;
        var tailLines = arguments.TryGetProperty("tailLines", out var linesProp) ? linesProp.GetInt32() : 100;

        return await _kubernetesService.GetPodLogsAsync(clusterName, podName, @namespace, containerName, tailLines);
    }

    private async Task<JsonElement> HandleGetDeployments(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;

        return await _kubernetesService.GetDeploymentsAsync(clusterName, @namespace);
    }

    private async Task<JsonElement> HandleGetServices(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;

        return await _kubernetesService.GetServicesAsync(clusterName, @namespace);
    }

    private async Task<JsonElement> HandleGetConfigMaps(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;

        return await _kubernetesService.GetConfigMapsAsync(clusterName, @namespace);
    }

    private async Task<JsonElement> HandleGetSecrets(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;

        return await _kubernetesService.GetSecretsAsync(clusterName, @namespace);
    }

    private async Task<JsonElement> HandleGetIngress(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var @namespace = arguments.TryGetProperty("namespace", out var nsProp) ? nsProp.GetString() : null;

        return await _kubernetesService.GetIngressAsync(clusterName, @namespace);
    }

    private async Task<JsonElement> HandleGetPersistentVolumes(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;

        return await _kubernetesService.GetPersistentVolumesAsync(clusterName);
    }

    private async Task<JsonElement> HandleGetCronJobs(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        
        string? namespaceFilter = null;
        if (arguments.TryGetProperty("namespace", out var nsElement))
        {
            namespaceFilter = nsElement.GetString();
        }

        return await _kubernetesService.GetCronJobsAsync(clusterName, namespaceFilter);
    }

    private async Task<JsonElement> HandleGetJobs(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        
        string? namespaceFilter = null;
        if (arguments.TryGetProperty("namespace", out var nsElement))
        {
            namespaceFilter = nsElement.GetString();
        }

        return await _kubernetesService.GetJobsAsync(clusterName, namespaceFilter);
    }

    private async Task<JsonElement> HandleGetNamespaces(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        return await _kubernetesService.GetNamespacesAsync(clusterName);
    }

    private object HandleListConnectedClusters()
    {
        var clusters = _kubernetesService.GetConnectedClusters().ToList();
        return new
        {
            connectedClusters = clusters,
            totalClusters = clusters.Count,
            message = clusters.Count > 0 ? $"Found {clusters.Count} connected cluster(s)" : "No clusters currently connected"
        };
    }

    private async Task<object> HandleDescribePod(JsonElement arguments)
    {
        var clusterName = arguments.GetProperty("clusterName").GetString()!;
        var podName = arguments.GetProperty("podName").GetString()!;
        var namespaceName = arguments.TryGetProperty("namespace", out var nsElement) ? nsElement.GetString() : null;

        var result = await _kubernetesService.DescribePodAsync(clusterName, podName, namespaceName);
        
        // Convert JsonElement to a structured response
        return new
        {
            podName,
            clusterName,
            @namespace = namespaceName,
            description = result.ToString()
        };
    }

    private static McpMessage CreateErrorResponse(object? id, int code, string message)
    {
        return new McpMessage
        {
            Id = id,
            Error = new McpError { Code = code, Message = message }
        };
    }
}
