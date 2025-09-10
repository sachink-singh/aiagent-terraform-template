using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace AzureAIAgent.Plugins;

/// <summary>
/// MCP Client Plugin for connecting to the Enterprise Kubernetes MCP Server
/// Provides secure access to Kubernetes operations through Model Context Protocol
/// </summary>
public class McpKubernetesPlugin : IDisposable
{
    private readonly ILogger<McpKubernetesPlugin> _logger;
    private Process? _mcpServerProcess;
    private StreamWriter? _mcpServerInput;
    private StreamReader? _mcpServerOutput;
    private readonly object _lock = new();
    private bool _isInitialized = false;
    private int _requestId = 0;

    public McpKubernetesPlugin(ILogger<McpKubernetesPlugin> logger)
    {
        _logger = logger;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        Process? newProcess = null;
        
        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                _logger.LogInformation("Starting Enterprise Kubernetes MCP Server");

                // Start the MCP server process
                var currentDirectory = Directory.GetCurrentDirectory();
                var solutionRoot = Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory;
                if (Path.GetFileName(currentDirectory) != "infraAgent.NET")
                {
                    // If we're in a project subdirectory, go up to find the solution root
                    var temp = currentDirectory;
                    while (temp != null && !Directory.Exists(Path.Combine(temp, "KubernetesMcpServer")))
                    {
                        temp = Directory.GetParent(temp)?.FullName;
                    }
                    solutionRoot = temp ?? currentDirectory;
                }
                var mcpServerPath = Path.Combine(solutionRoot, "KubernetesMcpServer", "bin", "Release", "net8.0", "KubernetesMcpServer.dll");
                
                _logger.LogInformation("Looking for MCP server at: {McpServerPath}", mcpServerPath);
                _logger.LogInformation("MCP server file exists: {Exists}", File.Exists(mcpServerPath));
                _logger.LogInformation("Working directory will be: {WorkingDir}", Path.GetDirectoryName(mcpServerPath));

                _mcpServerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"exec \"{mcpServerPath}\"",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(mcpServerPath),
                        Environment = 
                        {
                            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
                        }
                    }
                };

                _mcpServerProcess.Start();
                _mcpServerInput = _mcpServerProcess.StandardInput;
                _mcpServerOutput = _mcpServerProcess.StandardOutput;

                _logger.LogInformation("MCP Server process started with PID: {ProcessId}", _mcpServerProcess.Id);
                _logger.LogInformation("Process has exited: {HasExited}", _mcpServerProcess.HasExited);
                newProcess = _mcpServerProcess;
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start MCP Server process");
                throw;
            }
        }

        // Give the MCP server a moment to fully initialize (outside the lock)
        if (newProcess != null)
        {
            await Task.Delay(2000);
        }

        // Initialize the MCP server
        await SendMcpRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { tools = new { } }
        });
    }

    [KernelFunction("ConnectToCurrentContext")]
    [Description("Connect to Kubernetes cluster using current kubectl context")]
    public async Task<string> ConnectToCurrentContextAsync(
        [Description("Name to assign to the cluster connection")] string clusterName)
    {
        try
        {
            await EnsureInitializedAsync();

            var request = new
            {
                name = "connect_current_context",
                arguments = new { clusterName }
            };

            var response = await SendMcpRequestAsync("tools/call", request);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to cluster using current context: {ClusterName}", clusterName);
            return $"❌ Failed to connect to cluster {clusterName} using kubectl context: {ex.Message}";
        }
    }

    [KernelFunction("ConnectToAksCluster")]
    [Description("Connect to an Azure Kubernetes Service (AKS) cluster using the MCP server")]
    public async Task<string> ConnectToAksClusterAsync(
        [Description("Name of the AKS cluster")] string clusterName,
        [Description("Azure resource group name")] string resourceGroup,
        [Description("Azure subscription ID (optional)")] string? subscriptionId = null)
    {
        try
        {
            await EnsureInitializedAsync();

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "connect_aks_cluster",
                arguments = new
                {
                    clusterName,
                    resourceGroup,
                    subscriptionId
                }
            });

            _logger.LogInformation("Connected to AKS cluster {ClusterName} in resource group {ResourceGroup}", 
                clusterName, resourceGroup);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to AKS cluster {ClusterName}", clusterName);
            return $"❌ Failed to connect to AKS cluster {clusterName}: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesPods")]
    [Description("Get all pods from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesPodsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Kubernetes namespace filter (optional)")] string? namespaceName = null)
    {
        try
        {
            await EnsureInitializedAsync();

            _logger.LogInformation("Requesting pods from cluster {ClusterName}, namespace: {Namespace}", 
                clusterName, namespaceName ?? "all");

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_pods",
                arguments = new
                {
                    clusterName,
                    @namespace = namespaceName
                }
            });

            _logger.LogInformation("Successfully retrieved pods from cluster {ClusterName}", clusterName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pods from cluster {ClusterName}: {ErrorMessage}", clusterName, ex.Message);
            return $"❌ Failed to get pods: {ex.Message}";
        }
    }

    [KernelFunction("DescribePod")]
    [Description("Get detailed description of a specific pod using the MCP server")]
    public async Task<string> DescribePodAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Name of the pod")] string podName,
        [Description("Kubernetes namespace hint (optional)")] string? namespaceName = null)
    {
        try
        {
            await EnsureInitializedAsync();

            _logger.LogInformation("Requesting description for pod {PodName} from cluster {ClusterName}, namespace: {Namespace}", 
                podName, clusterName, namespaceName ?? "all");

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "describe_pod",
                arguments = new
                {
                    clusterName,
                    podName,
                    @namespace = namespaceName
                }
            });

            _logger.LogInformation("Successfully retrieved description for pod {PodName}", podName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to describe pod {PodName}: {ErrorMessage}", podName, ex.Message);
            return $"❌ Failed to describe pod: {ex.Message}";
        }
    }

    [KernelFunction("GetPodLogs")]
    [Description("Get logs from a specific pod using the MCP server")]
    public async Task<string> GetPodLogsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Name of the pod")] string podName,
        [Description("Kubernetes namespace hint (optional)")] string? namespaceName = null,
        [Description("Container name (optional)")] string? containerName = null,
        [Description("Number of tail lines to retrieve")] int tailLines = 100)
    {
        try
        {
            await EnsureInitializedAsync();

            _logger.LogInformation("Requesting logs for pod {PodName} from cluster {ClusterName}", podName, clusterName);

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_pod_logs",
                arguments = new
                {
                    clusterName,
                    podName,
                    @namespace = namespaceName,
                    containerName,
                    tailLines
                }
            });

            _logger.LogInformation("Successfully retrieved logs for pod {PodName}", podName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get logs for pod {PodName}: {ErrorMessage}", podName, ex.Message);
            return $"❌ Failed to get pod logs: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesDeployments")]
    [Description("Get all deployments from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesDeploymentsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Kubernetes namespace filter (optional)")] string? namespaceName = null)
    {
        try
        {
            await EnsureInitializedAsync();

            _logger.LogInformation("Requesting deployments from cluster {ClusterName}, namespace: {Namespace}", 
                clusterName, namespaceName ?? "all");

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_deployments",
                arguments = new
                {
                    clusterName,
                    @namespace = namespaceName
                }
            });

            _logger.LogInformation("Successfully retrieved deployments from cluster {ClusterName}", clusterName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deployments from cluster {ClusterName}: {ErrorMessage}", clusterName, ex.Message);
            return $"❌ Failed to get deployments: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesServices")]
    [Description("Get all services from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesServicesAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Kubernetes namespace filter (optional)")] string? namespaceName = null)
    {
        try
        {
            await EnsureInitializedAsync();

            _logger.LogInformation("Requesting services from cluster {ClusterName}, namespace: {Namespace}", 
                clusterName, namespaceName ?? "all");

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_services",
                arguments = new
                {
                    clusterName,
                    @namespace = namespaceName
                }
            });

            _logger.LogInformation("Successfully retrieved services from cluster {ClusterName}", clusterName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get services from cluster {ClusterName}: {ErrorMessage}", clusterName, ex.Message);
            return $"❌ Failed to get services: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesNamespaces")]
    [Description("Get all namespaces from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesNamespacesAsync(
        [Description("Name of the connected cluster")] string clusterName)
    {
        try
        {
            await EnsureInitializedAsync();

            _logger.LogInformation("Requesting namespaces from cluster {ClusterName}", clusterName);

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_namespaces",
                arguments = new
                {
                    clusterName
                }
            });

            _logger.LogInformation("Successfully retrieved namespaces from cluster {ClusterName}", clusterName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get namespaces from cluster {ClusterName}: {ErrorMessage}", clusterName, ex.Message);
            return $"❌ Failed to get namespaces: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesPodDetails")]
    [Description("Get detailed information about a specific pod using the MCP server")]
    public async Task<string> GetKubernetesPodDetailsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Name of the pod")] string podName,
        [Description("Kubernetes namespace hint (optional)")] string? namespaceName = null)
    {
        try
        {
            await EnsureInitializedAsync();

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_pod_details",
                arguments = new
                {
                    clusterName,
                    podName,
                    @namespace = namespaceName
                }
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pod details for {PodName}", podName);
            return $"❌ Failed to get pod details: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesConfigMaps")]
    [Description("Get all ConfigMaps from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesConfigMapsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Optional namespace filter")] string? namespaceFilter = null)
    {
        try
        {
            await EnsureInitializedAsync();

            object arguments;
            if (namespaceFilter != null)
            {
                arguments = new { clusterName, namespaceFilter };
            }
            else
            {
                arguments = new { clusterName };
            }

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_configmaps",
                arguments
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get ConfigMaps from cluster {ClusterName}", clusterName);
            return $"❌ Failed to get ConfigMaps: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesSecrets")]
    [Description("Get all Secrets from a connected Kubernetes cluster using the MCP server (metadata only)")]
    public async Task<string> GetKubernetesSecretsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Optional namespace filter")] string? namespaceFilter = null)
    {
        try
        {
            await EnsureInitializedAsync();

            object arguments;
            if (namespaceFilter != null)
            {
                arguments = new { clusterName, namespaceFilter };
            }
            else
            {
                arguments = new { clusterName };
            }

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_secrets",
                arguments
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Secrets from cluster {ClusterName}", clusterName);
            return $"❌ Failed to get Secrets: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesIngress")]
    [Description("Get all Ingress resources from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesIngressAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Optional namespace filter")] string? namespaceFilter = null)
    {
        try
        {
            await EnsureInitializedAsync();

            object arguments;
            if (namespaceFilter != null)
            {
                arguments = new { clusterName, namespaceFilter };
            }
            else
            {
                arguments = new { clusterName };
            }

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_ingress",
                arguments
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Ingress resources from cluster {ClusterName}", clusterName);
            return $"❌ Failed to get Ingress resources: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesPersistentVolumes")]
    [Description("Get all Persistent Volumes from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesPersistentVolumesAsync(
        [Description("Name of the connected cluster")] string clusterName)
    {
        try
        {
            await EnsureInitializedAsync();

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_persistent_volumes",
                arguments = new { clusterName }
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Persistent Volumes from cluster {ClusterName}", clusterName);
            return $"❌ Failed to get Persistent Volumes: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesCronJobs")]
    [Description("Get all CronJobs from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesCronJobsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Optional namespace filter")] string? namespaceFilter = null)
    {
        try
        {
            await EnsureInitializedAsync();

            object arguments;
            if (namespaceFilter != null)
            {
                arguments = new { clusterName, namespaceFilter };
            }
            else
            {
                arguments = new { clusterName };
            }

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_cronjobs",
                arguments
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get CronJobs from cluster {ClusterName}", clusterName);
            return $"❌ Failed to get CronJobs: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesJobs")]
    [Description("Get all Jobs from a connected Kubernetes cluster using the MCP server")]
    public async Task<string> GetKubernetesJobsAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Optional namespace filter")] string? namespaceFilter = null)
    {
        try
        {
            await EnsureInitializedAsync();

            object arguments;
            if (namespaceFilter != null)
            {
                arguments = new { clusterName, namespaceFilter };
            }
            else
            {
                arguments = new { clusterName };
            }

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "get_jobs",
                arguments
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Jobs from cluster {ClusterName}", clusterName);
            return $"❌ Failed to get Jobs: {ex.Message}";
        }
    }

    [KernelFunction("GetKubernetesResources")]
    [Description("Get any type of Kubernetes resources from a connected cluster using the MCP server")]
    public async Task<string> GetKubernetesResourcesAsync(
        [Description("Name of the connected cluster")] string clusterName,
        [Description("Type of resource: pods, deployments, services, namespaces, configmaps, secrets, ingress, persistent_volumes, cronjobs, jobs")] string resourceType,
        [Description("Optional namespace filter")] string? namespaceFilter = null)
    {
        try
        {
            await EnsureInitializedAsync();

            // First ensure we're connected to the cluster
            try
            {
                var connectResult = await ConnectToCurrentContextAsync(clusterName);
                _logger.LogInformation("Cluster connection result for {ResourceType}: {Result}", 
                    resourceType, connectResult);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to cluster for {ResourceType}: {Error}", 
                    resourceType, ex.Message);
                // Continue anyway to try getting resources
            }

            // Map resource type to MCP tool name
            var toolName = resourceType.ToLowerInvariant() switch
            {
                "pods" => "get_pods",
                "deployments" => "get_deployments", 
                "services" => "get_services",
                "namespaces" => "get_namespaces",
                "configmaps" => "get_configmaps",
                "secrets" => "get_secrets",
                "ingress" => "get_ingress",
                "persistent_volumes" or "pv" => "get_persistent_volumes",
                "cronjobs" => "get_cronjobs",
                "jobs" => "get_jobs",
                _ => throw new ArgumentException($"Unsupported resource type: {resourceType}")
            };

            object arguments;
            if (namespaceFilter != null && toolName != "get_namespaces" && toolName != "get_persistent_volumes")
            {
                arguments = new { clusterName, namespaceFilter };
            }
            else
            {
                arguments = new { clusterName };
            }

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = toolName,
                arguments
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get {ResourceType} resources from cluster {ClusterName}", resourceType, clusterName);
            return $"❌ Failed to get {resourceType}: {ex.Message}";
        }
    }

    [KernelFunction("ListConnectedClusters")]
    [Description("List all currently connected Kubernetes clusters")]
    public async Task<string> ListConnectedClustersAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            var result = await SendMcpRequestAsync("tools/call", new
            {
                name = "list_connected_clusters",
                arguments = new { }
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list connected clusters");
            return $"❌ Failed to list connected clusters: {ex.Message}";
        }
    }

    private async Task<string> SendMcpRequestAsync(string method, object parameters)
    {
        if (_mcpServerInput == null || _mcpServerOutput == null)
        {
            throw new InvalidOperationException("MCP server not initialized");
        }

        var requestId = Interlocked.Increment(ref _requestId);
        var request = new
        {
            jsonrpc = "2.0",
            id = requestId,
            method,
            @params = parameters
        };

        var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        _logger.LogDebug("Sending MCP request: {Method} (ID: {RequestId})", method, requestId);

        await _mcpServerInput.WriteLineAsync(requestJson);
        await _mcpServerInput.FlushAsync();

        // Read responses until we get a valid JSON response
        string? responseJson = null;
        int maxAttempts = 10;
        int attempts = 0;
        
        while (attempts < maxAttempts)
        {
            var line = await _mcpServerOutput.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
            {
                throw new InvalidOperationException("No response from MCP server");
            }

            // Skip non-JSON lines (build output, info messages, etc.)
            if (line.StartsWith("{") && line.Contains("jsonrpc"))
            {
                responseJson = line;
                break;
            }
            
            // Log and skip non-JSON content
            _logger.LogDebug("Skipping non-JSON output from MCP server: {Line}", line.Length > 100 ? line[..100] + "..." : line);
            attempts++;
        }

        if (string.IsNullOrEmpty(responseJson))
        {
            throw new InvalidOperationException($"Failed to get valid JSON response from MCP server after {maxAttempts} attempts");
        }

        _logger.LogDebug("Received MCP response for ID: {RequestId}", requestId);
        
        // Log the raw response BEFORE trying to parse it
        _logger.LogError("Raw MCP server response: {Response}", responseJson);

        // Check for null or empty response
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw new InvalidOperationException("MCP server returned null or empty response");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(responseJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError("Failed to parse JSON response: {Response}", responseJson);
            throw new InvalidOperationException($"Invalid JSON response from MCP server: {ex.Message}");
        }

        using (doc)
        {
            JsonElement root;
            try
            {
                root = doc.RootElement;
                
                // Log the raw JSON response for debugging
                _logger.LogDebug("MCP server raw response: {Response}", responseJson);
                _logger.LogDebug("JSON root element kind: {Kind}", root.ValueKind);

                if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
                {
                    _logger.LogDebug("Found error element in response");
                    var errorMessage = "Unknown error";
                    if (errorElement.TryGetProperty("message", out var messageElement))
                    {
                        errorMessage = messageElement.GetString() ?? "Unknown error";
                    }
                    throw new InvalidOperationException($"MCP server error: {errorMessage}");
                }
            }
            catch (Exception jsonEx)
            {
                _logger.LogError(jsonEx, "Error processing JSON response: {Response}", responseJson);
                throw new InvalidOperationException($"Failed to process MCP server response: {jsonEx.Message}");
            }

            if (root.TryGetProperty("result", out var resultElement))
            {
                // Handle different response types
                // For "initialize" method, just return success message
                if (method == "initialize")
                {
                    _logger.LogDebug("MCP server initialized successfully");
                    return "MCP server initialized successfully";
                }
                
                // For tool calls, extract the content array
                if (resultElement.TryGetProperty("content", out var contentElement) && 
                    contentElement.ValueKind == JsonValueKind.Array && 
                    contentElement.GetArrayLength() > 0)
                {
                    var firstContent = contentElement[0];
                    if (firstContent.TryGetProperty("text", out var textElement))
                    {
                        var content = textElement.GetString() ?? "No content";
                        
                        _logger.LogDebug("MCP server returned content: {ContentLength} chars", content.Length);
                        
                        // Don't truncate pod listing responses - we need the full JSON for proper formatting
                        return content;
                    }
                }
                else
                {
                    // If no content array, try to return the raw result as string
                    _logger.LogDebug("No content array found, returning raw result");
                    return resultElement.ToString() ?? "No result content";
                }
            }
            
            // If neither error nor result is found, the response might be malformed
            _logger.LogWarning("MCP response contains neither error nor result. Raw response: {Response}", responseJson);
            throw new InvalidOperationException("MCP server returned malformed response with no error or result");
        }
    }

    public void Dispose()
    {
        try
        {
            _mcpServerInput?.Close();
            _mcpServerOutput?.Close();
            
            if (_mcpServerProcess != null && !_mcpServerProcess.HasExited)
            {
                _mcpServerProcess.Kill();
            }
            
            _mcpServerProcess?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing MCP client");
        }
    }
}
