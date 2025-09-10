using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using KubernetesMcpServer.Models;

namespace KubernetesMcpServer.Services;

/// <summary>
/// Enterprise Kubernetes service for secure cluster operations
/// Provides controlled access to Kubernetes resources with audit logging
/// </summary>
public class KubernetesService
{
    private readonly ILogger<KubernetesService> _logger;
    private readonly Dictionary<string, IKubernetes> _clients = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public KubernetesService(ILogger<KubernetesService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Connect to cluster using current kubectl context
    /// </summary>
    public async Task<bool> ConnectToCurrentContextAsync(string clusterName)
    {
        try
        {
            _logger.LogInformation("Connecting to cluster {ClusterName} using current kubectl context", clusterName);

            // Use the default kubectl configuration
            var config = KubernetesClientConfiguration.BuildDefaultConfig();
            var client = new Kubernetes(config);
            
            // Test connection
            var namespaces = await client.CoreV1.ListNamespaceAsync();
            _clients[clusterName] = client;
            
            _logger.LogInformation("Successfully connected to cluster {ClusterName} using kubectl context. Found {NamespaceCount} namespaces", 
                clusterName, namespaces.Items.Count);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to cluster {ClusterName} using kubectl context", clusterName);
            return false;
        }
    }

    /// <summary>
    /// Connect to AKS cluster using Azure CLI credentials
    /// </summary>
    public async Task<bool> ConnectToAksClusterAsync(string clusterName, string resourceGroup, string? subscriptionId = null)
    {
        try
        {
            _logger.LogInformation("Connecting to AKS cluster {ClusterName} in resource group {ResourceGroup}", 
                clusterName, resourceGroup);

            // Build Azure CLI command for getting credentials
            var azCommand = $"aks get-credentials --resource-group {resourceGroup} --name {clusterName} --file -";
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                azCommand = $"aks get-credentials --subscription {subscriptionId} --resource-group {resourceGroup} --name {clusterName} --file -";
            }

            // Execute Azure CLI to get kubeconfig
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "az",
                    Arguments = azCommand,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var kubeconfigContent = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("Failed to get AKS credentials: {Error}", error);
                return false;
            }

            // Create Kubernetes client from kubeconfig
            var config = KubernetesClientConfiguration.BuildConfigFromConfigObject(
                KubernetesYaml.Deserialize<k8s.KubeConfigModels.K8SConfiguration>(kubeconfigContent));
            
            var client = new Kubernetes(config);
            
            // Test connection
            var namespaces = await client.CoreV1.ListNamespaceAsync();
            _clients[clusterName] = client;
            
            _logger.LogInformation("Successfully connected to AKS cluster {ClusterName}. Found {NamespaceCount} namespaces", 
                clusterName, namespaces.Items.Count);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to AKS cluster {ClusterName}", clusterName);
            return false;
        }
    }

    /// <summary>
    /// Get all pods across all namespaces
    /// </summary>
    public async Task<JsonElement> GetPodsAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting pods from cluster {ClusterName}, namespace filter: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var pods = string.IsNullOrEmpty(namespaceFilter)
            ? await client.CoreV1.ListPodForAllNamespacesAsync()
            : await client.CoreV1.ListNamespacedPodAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalPods = pods.Items.Count,
            pods = pods.Items.Select(pod => new
            {
                name = pod.Metadata.Name,
                @namespace = pod.Metadata.NamespaceProperty,
                status = pod.Status?.Phase ?? "Unknown",
                restarts = pod.Status?.ContainerStatuses?.Sum(c => c.RestartCount) ?? 0,
                node = pod.Spec?.NodeName,
                created = pod.Metadata.CreationTimestamp,
                labels = pod.Metadata.Labels,
                containers = pod.Spec?.Containers?.Select(c => new
                {
                    name = c.Name,
                    image = c.Image,
                    ready = pod.Status?.ContainerStatuses?.FirstOrDefault(cs => cs.Name == c.Name)?.Ready ?? false
                })
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get detailed information about a specific pod
    /// </summary>
    public async Task<JsonElement> GetPodDetailsAsync(string clusterName, string podName, string? namespaceHint = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting details for pod {PodName} in cluster {ClusterName}", podName, clusterName);

        V1Pod? pod = null;
        string? foundNamespace = null;

        // If namespace is provided, try that first
        if (!string.IsNullOrEmpty(namespaceHint))
        {
            try
            {
                pod = await client.CoreV1.ReadNamespacedPodAsync(podName, namespaceHint);
                foundNamespace = namespaceHint;
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Pod not found in specified namespace, will search all namespaces
            }
        }

        // If not found or no namespace specified, search all namespaces
        if (pod == null)
        {
            var allPods = await client.CoreV1.ListPodForAllNamespacesAsync();
            pod = allPods.Items.FirstOrDefault(p => p.Metadata.Name == podName);
            foundNamespace = pod?.Metadata.NamespaceProperty;
        }

        if (pod == null)
        {
            throw new InvalidOperationException($"Pod {podName} not found in cluster {clusterName}");
        }

        var result = new
        {
            cluster = clusterName,
            name = pod.Metadata.Name,
            @namespace = foundNamespace,
            status = new
            {
                phase = pod.Status?.Phase,
                conditions = pod.Status?.Conditions?.Select(c => new
                {
                    type = c.Type,
                    status = c.Status,
                    reason = c.Reason,
                    message = c.Message,
                    lastTransition = c.LastTransitionTime
                }),
                containerStatuses = pod.Status?.ContainerStatuses?.Select(cs => new
                {
                    name = cs.Name,
                    ready = cs.Ready,
                    restartCount = cs.RestartCount,
                    image = cs.Image,
                    state = cs.State
                })
            },
            spec = new
            {
                nodeName = pod.Spec?.NodeName,
                containers = pod.Spec?.Containers?.Select(c => new
                {
                    name = c.Name,
                    image = c.Image,
                    resources = c.Resources,
                    env = c.Env?.Select(e => new { name = e.Name, value = e.Value })
                }),
                volumes = pod.Spec?.Volumes?.Select(v => new
                {
                    name = v.Name,
                    type = GetVolumeType(v)
                })
            },
            metadata = new
            {
                labels = pod.Metadata.Labels,
                annotations = pod.Metadata.Annotations,
                creationTimestamp = pod.Metadata.CreationTimestamp,
                ownerReferences = pod.Metadata.OwnerReferences?.Select(o => new
                {
                    kind = o.Kind,
                    name = o.Name,
                    apiVersion = o.ApiVersion
                })
            }
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get logs from a specific pod container
    /// </summary>
    public async Task<JsonElement> GetPodLogsAsync(string clusterName, string podName, string? namespaceHint = null, string? containerName = null, int tailLines = 100)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting logs for pod {PodName} in cluster {ClusterName}", podName, clusterName);

        V1Pod? pod = null;
        string? foundNamespace = null;

        // If namespace is provided, try that first
        if (!string.IsNullOrEmpty(namespaceHint))
        {
            try
            {
                pod = await client.CoreV1.ReadNamespacedPodAsync(podName, namespaceHint);
                foundNamespace = namespaceHint;
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Pod not found in specified namespace, will search all namespaces
            }
        }

        // If not found or no namespace specified, search all namespaces
        if (pod == null)
        {
            var allPods = await client.CoreV1.ListPodForAllNamespacesAsync();
            pod = allPods.Items.FirstOrDefault(p => p.Metadata.Name == podName);
            foundNamespace = pod?.Metadata.NamespaceProperty;
        }

        if (pod == null)
        {
            throw new InvalidOperationException($"Pod {podName} not found in cluster {clusterName}");
        }

        // Smart container name resolution
        var resolvedContainerName = ResolveContainerName(pod, containerName);
        
        try
        {
            // Get the logs
            var logsStream = await client.CoreV1.ReadNamespacedPodLogAsync(
                name: podName,
                namespaceParameter: foundNamespace!,
                container: resolvedContainerName,
                tailLines: tailLines);

            string logs;
            using (var reader = new StreamReader(logsStream))
            {
                logs = await reader.ReadToEndAsync();
            }

            var result = new
            {
                cluster = clusterName,
                podName = podName,
                @namespace = foundNamespace,
                containerName = resolvedContainerName,
                tailLines = tailLines,
                logs = logs,
                timestamp = DateTime.UtcNow
            };

            return JsonSerializer.SerializeToElement(result, _jsonOptions);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.BadRequest && 
                                                               ex.Response.Content.Contains("container name must be specified"))
        {
            // If we get a container specification error, return available containers
            var containers = pod.Spec.Containers.Select(c => c.Name).ToList();
            
            var errorResult = new
            {
                cluster = clusterName,
                podName = podName,
                @namespace = foundNamespace,
                error = "Multiple containers found. Please specify container name.",
                availableContainers = containers,
                suggestion = $"Try: 'Get logs from pod {podName} container {containers.First()}'",
                timestamp = DateTime.UtcNow
            };

            return JsonSerializer.SerializeToElement(errorResult, _jsonOptions);
        }
    }

    /// <summary>
    /// Resolve container name for pod logs
    /// </summary>
    private string? ResolveContainerName(V1Pod pod, string? requestedContainerName)
    {
        var containers = pod.Spec.Containers;
        
        // If container name is explicitly provided, validate it exists
        if (!string.IsNullOrEmpty(requestedContainerName))
        {
            if (containers.Any(c => c.Name.Equals(requestedContainerName, StringComparison.OrdinalIgnoreCase)))
            {
                return requestedContainerName;
            }
            else
            {
                _logger.LogWarning("Requested container {ContainerName} not found in pod {PodName}. Available: {Containers}", 
                    requestedContainerName, pod.Metadata.Name, string.Join(", ", containers.Select(c => c.Name)));
                // Fall through to auto-selection
            }
        }

        // Auto-select container based on smart rules
        
        // Rule 1: If only one container, use it
        if (containers.Count == 1)
        {
            return containers.First().Name;
        }

        // Rule 2: Look for main application containers (avoid init/sidecar containers)
        var mainContainers = containers.Where(c => 
            !c.Name.Contains("init") && 
            !c.Name.Contains("sidecar") && 
            !c.Name.Contains("proxy") &&
            !c.Name.Contains("istio") &&
            !c.Name.Contains("envoy") &&
            !c.Name.StartsWith("linkerd") &&
            !c.Name.Contains("fluentd") &&
            !c.Name.Contains("logspout") &&
            !c.Name.Contains("filebeat") &&
            !c.Name.Contains("prometheus") &&
            !c.Name.Contains("jaeger")).ToList();

        if (mainContainers.Count == 1)
        {
            return mainContainers.First().Name;
        }

        // Rule 3: For Microsoft Defender, prefer the main collector
        if (pod.Metadata.Name.Contains("microsoft-defender"))
        {
            var defenderMain = containers.FirstOrDefault(c => c.Name.Contains("pod-collector") && !c.Name.Contains("low-level"));
            if (defenderMain != null)
            {
                return defenderMain.Name;
            }
        }

        // Rule 4: Look for common main container patterns
        var commonMainNames = new[] { "app", "main", "server", "api", "web", "service" };
        foreach (var pattern in commonMainNames)
        {
            var match = containers.FirstOrDefault(c => c.Name.Contains(pattern));
            if (match != null)
            {
                return match.Name;
            }
        }

        // Rule 5: If multiple containers remain, return null to trigger the "specify container" error
        // This will show available containers to the user
        if (containers.Count > 1)
        {
            _logger.LogInformation("Multiple containers found in pod {PodName}, user needs to specify: {Containers}", 
                pod.Metadata.Name, string.Join(", ", containers.Select(c => c.Name)));
            return null; // This will trigger the BadRequest which we handle gracefully
        }

        // Fallback: first container
        return containers.FirstOrDefault()?.Name;
    }

    /// <summary>
    /// Get detailed description of a specific pod (equivalent to kubectl describe pod)
    /// </summary>
    public async Task<JsonElement> DescribePodAsync(string clusterName, string podName, string? namespaceHint = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Describing pod {PodName} in cluster {ClusterName}", podName, clusterName);

        V1Pod? pod = null;
        string? foundNamespace = null;

        // If namespace is provided, try that first
        if (!string.IsNullOrEmpty(namespaceHint))
        {
            try
            {
                pod = await client.CoreV1.ReadNamespacedPodAsync(podName, namespaceHint);
                foundNamespace = namespaceHint;
            }
            catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Pod not found in specified namespace, will search all namespaces
            }
        }

        // If not found or no namespace specified, search all namespaces
        if (pod == null)
        {
            var allPods = await client.CoreV1.ListPodForAllNamespacesAsync();
            pod = allPods.Items.FirstOrDefault(p => p.Metadata.Name == podName);
            foundNamespace = pod?.Metadata.NamespaceProperty;
        }

        if (pod == null)
        {
            var errorResult = new
            {
                cluster = clusterName,
                podName = podName,
                @namespace = namespaceHint,
                error = $"Pod '{podName}' not found in cluster '{clusterName}'",
                suggestion = namespaceHint != null ? 
                    $"Pod '{podName}' not found in namespace '{namespaceHint}'. Try searching in other namespaces or use 'kubectl get pods --all-namespaces | grep {podName}'" :
                    $"Pod '{podName}' not found in any namespace. Use 'kubectl get pods --all-namespaces' to list all pods",
                timestamp = DateTime.UtcNow
            };
            return JsonSerializer.SerializeToElement(errorResult, _jsonOptions);
        }

        // Build comprehensive pod description
        var description = new
        {
            cluster = clusterName,
            @namespace = foundNamespace,
            name = pod.Metadata.Name,
            uid = pod.Metadata.Uid,
            resourceVersion = pod.Metadata.ResourceVersion,
            labels = pod.Metadata.Labels,
            annotations = pod.Metadata.Annotations,
            creationTimestamp = pod.Metadata.CreationTimestamp,
            status = new
            {
                phase = pod.Status.Phase,
                podIP = pod.Status.PodIP,
                hostIP = pod.Status.HostIP,
                nodeName = pod.Spec.NodeName,
                startTime = pod.Status.StartTime,
                conditions = pod.Status.Conditions?.Select(c => new
                {
                    type = c.Type,
                    status = c.Status,
                    lastTransitionTime = c.LastTransitionTime,
                    reason = c.Reason,
                    message = c.Message
                }).ToList(),
                containerStatuses = pod.Status.ContainerStatuses?.Select(cs => new
                {
                    name = cs.Name,
                    ready = cs.Ready,
                    restartCount = cs.RestartCount,
                    image = cs.Image,
                    imageID = cs.ImageID,
                    containerID = cs.ContainerID,
                    state = cs.State,
                    lastState = cs.LastState
                }).ToList()
            },
            spec = new
            {
                restartPolicy = pod.Spec.RestartPolicy,
                serviceAccount = pod.Spec.ServiceAccount,
                serviceAccountName = pod.Spec.ServiceAccountName,
                nodeName = pod.Spec.NodeName,
                containers = pod.Spec.Containers?.Select(c => new
                {
                    name = c.Name,
                    image = c.Image,
                    ports = c.Ports?.Select(p => new
                    {
                        containerPort = p.ContainerPort,
                        protocol = p.Protocol,
                        name = p.Name
                    }).ToList(),
                    env = c.Env?.Select(e => new
                    {
                        name = e.Name,
                        value = e.Value,
                        valueFrom = e.ValueFrom
                    }).ToList(),
                    resources = c.Resources,
                    volumeMounts = c.VolumeMounts?.Select(vm => new
                    {
                        name = vm.Name,
                        mountPath = vm.MountPath,
                        readOnly = vm.ReadOnlyProperty
                    }).ToList()
                }).ToList(),
                volumes = pod.Spec.Volumes?.Select(v => new
                {
                    name = v.Name,
                    configMap = v.ConfigMap,
                    secret = v.Secret,
                    emptyDir = v.EmptyDir,
                    persistentVolumeClaim = v.PersistentVolumeClaim
                }).ToList()
            },
            timestamp = DateTime.UtcNow
        };

        return JsonSerializer.SerializeToElement(description, _jsonOptions);
    }

    /// <summary>
    /// Get all deployments in cluster
    /// </summary>
    public async Task<JsonElement> GetDeploymentsAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting deployments from cluster {ClusterName}, namespace: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var deployments = string.IsNullOrEmpty(namespaceFilter)
            ? await client.AppsV1.ListDeploymentForAllNamespacesAsync()
            : await client.AppsV1.ListNamespacedDeploymentAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalDeployments = deployments.Items.Count,
            deployments = deployments.Items.Select(dep => new
            {
                name = dep.Metadata.Name,
                @namespace = dep.Metadata.NamespaceProperty,
                replicas = dep.Spec?.Replicas ?? 0,
                readyReplicas = dep.Status?.ReadyReplicas ?? 0,
                availableReplicas = dep.Status?.AvailableReplicas ?? 0,
                updatedReplicas = dep.Status?.UpdatedReplicas ?? 0,
                created = dep.Metadata.CreationTimestamp,
                labels = dep.Metadata.Labels,
                strategy = dep.Spec?.Strategy?.Type
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get all services in cluster
    /// </summary>
    public async Task<JsonElement> GetServicesAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting services from cluster {ClusterName}, namespace: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var services = string.IsNullOrEmpty(namespaceFilter)
            ? await client.CoreV1.ListServiceForAllNamespacesAsync()
            : await client.CoreV1.ListNamespacedServiceAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalServices = services.Items.Count,
            services = services.Items.Select(svc => new
            {
                name = svc.Metadata.Name,
                @namespace = svc.Metadata.NamespaceProperty,
                type = svc.Spec?.Type,
                clusterIP = svc.Spec?.ClusterIP,
                externalIPs = svc.Spec?.ExternalIPs,
                ports = svc.Spec?.Ports?.Select(p => new
                {
                    name = p.Name,
                    port = p.Port,
                    targetPort = p.TargetPort?.Value,
                    protocol = p.Protocol
                }),
                selector = svc.Spec?.Selector,
                created = svc.Metadata.CreationTimestamp
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get all config maps in cluster
    /// </summary>
    public async Task<JsonElement> GetConfigMapsAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting config maps from cluster {ClusterName}, namespace: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var configMaps = string.IsNullOrEmpty(namespaceFilter)
            ? await client.CoreV1.ListConfigMapForAllNamespacesAsync()
            : await client.CoreV1.ListNamespacedConfigMapAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalConfigMaps = configMaps.Items.Count,
            configMaps = configMaps.Items.Select(cm => new
            {
                name = cm.Metadata.Name,
                @namespace = cm.Metadata.NamespaceProperty,
                dataKeys = cm.Data?.Keys.ToList() ?? new List<string>(),
                created = cm.Metadata.CreationTimestamp,
                labels = cm.Metadata.Labels
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get all secrets in cluster (metadata only for security)
    /// </summary>
    public async Task<JsonElement> GetSecretsAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting secrets from cluster {ClusterName}, namespace: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var secrets = string.IsNullOrEmpty(namespaceFilter)
            ? await client.CoreV1.ListSecretForAllNamespacesAsync()
            : await client.CoreV1.ListNamespacedSecretAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalSecrets = secrets.Items.Count,
            secrets = secrets.Items.Select(secret => new
            {
                name = secret.Metadata.Name,
                @namespace = secret.Metadata.NamespaceProperty,
                type = secret.Type,
                dataKeys = secret.Data?.Keys.ToList() ?? new List<string>(),
                created = secret.Metadata.CreationTimestamp,
                labels = secret.Metadata.Labels
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get all ingress resources in cluster
    /// </summary>
    public async Task<JsonElement> GetIngressAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting ingress resources from cluster {ClusterName}, namespace: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var ingresses = string.IsNullOrEmpty(namespaceFilter)
            ? await client.NetworkingV1.ListIngressForAllNamespacesAsync()
            : await client.NetworkingV1.ListNamespacedIngressAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalIngresses = ingresses.Items.Count,
            ingresses = ingresses.Items.Select(ing => new
            {
                name = ing.Metadata.Name,
                @namespace = ing.Metadata.NamespaceProperty,
                hosts = ing.Spec?.Rules?.Select(rule => rule.Host).ToList() ?? new List<string>(),
                loadBalancer = ing.Status?.LoadBalancer?.Ingress?.Select(lb => new 
                {
                    ip = lb.Ip,
                    hostname = lb.Hostname
                }).ToList(),
                created = ing.Metadata.CreationTimestamp,
                labels = ing.Metadata.Labels
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get all persistent volumes in cluster
    /// </summary>
    public async Task<JsonElement> GetPersistentVolumesAsync(string clusterName)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting persistent volumes from cluster {ClusterName}", clusterName);

        var pvs = await client.CoreV1.ListPersistentVolumeAsync();

        var result = new
        {
            cluster = clusterName,
            totalPersistentVolumes = pvs.Items.Count,
            persistentVolumes = pvs.Items.Select(pv => new
            {
                name = pv.Metadata.Name,
                capacity = pv.Spec?.Capacity?.FirstOrDefault().Value,
                accessModes = pv.Spec?.AccessModes,
                reclaimPolicy = pv.Spec?.PersistentVolumeReclaimPolicy,
                status = pv.Status?.Phase,
                storageClass = pv.Spec?.StorageClassName,
                created = pv.Metadata.CreationTimestamp,
                labels = pv.Metadata.Labels
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get all CronJobs in cluster
    /// </summary>
    public async Task<JsonElement> GetCronJobsAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting CronJobs from cluster {ClusterName}, namespace: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var cronJobs = string.IsNullOrEmpty(namespaceFilter)
            ? await client.BatchV1.ListCronJobForAllNamespacesAsync()
            : await client.BatchV1.ListNamespacedCronJobAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalCronJobs = cronJobs.Items.Count,
            cronJobs = cronJobs.Items.Select(cj => new
            {
                name = cj.Metadata.Name,
                @namespace = cj.Metadata.NamespaceProperty,
                schedule = cj.Spec?.Schedule,
                suspend = cj.Spec?.Suspend ?? false,
                lastScheduleTime = cj.Status?.LastScheduleTime,
                lastSuccessfulTime = cj.Status?.LastSuccessfulTime,
                activeJobs = cj.Status?.Active?.Count ?? 0,
                created = cj.Metadata.CreationTimestamp,
                labels = cj.Metadata.Labels
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get all Jobs in cluster
    /// </summary>
    public async Task<JsonElement> GetJobsAsync(string clusterName, string? namespaceFilter = null)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        _logger.LogInformation("Getting Jobs from cluster {ClusterName}, namespace: {Namespace}", 
            clusterName, namespaceFilter ?? "all");

        var jobs = string.IsNullOrEmpty(namespaceFilter)
            ? await client.BatchV1.ListJobForAllNamespacesAsync()
            : await client.BatchV1.ListNamespacedJobAsync(namespaceFilter);

        var result = new
        {
            cluster = clusterName,
            @namespace = namespaceFilter ?? "all",
            totalJobs = jobs.Items.Count,
            jobs = jobs.Items.Select(job => new
            {
                name = job.Metadata.Name,
                @namespace = job.Metadata.NamespaceProperty,
                parallelism = job.Spec?.Parallelism,
                completions = job.Spec?.Completions,
                activeDeadlineSeconds = job.Spec?.ActiveDeadlineSeconds,
                succeeded = job.Status?.Succeeded ?? 0,
                failed = job.Status?.Failed ?? 0,
                active = job.Status?.Active ?? 0,
                startTime = job.Status?.StartTime,
                completionTime = job.Status?.CompletionTime,
                created = job.Metadata.CreationTimestamp,
                labels = job.Metadata.Labels
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get cluster namespaces
    /// </summary>
    public async Task<JsonElement> GetNamespacesAsync(string clusterName)
    {
        if (!_clients.TryGetValue(clusterName, out var client))
        {
            throw new InvalidOperationException($"Not connected to cluster {clusterName}");
        }

        var namespaces = await client.CoreV1.ListNamespaceAsync();
        
        var result = new
        {
            cluster = clusterName,
            totalNamespaces = namespaces.Items.Count,
            namespaces = namespaces.Items.Select(ns => new
            {
                name = ns.Metadata.Name,
                status = ns.Status?.Phase,
                created = ns.Metadata.CreationTimestamp,
                labels = ns.Metadata.Labels
            }).ToList()
        };

        return JsonSerializer.SerializeToElement(result, _jsonOptions);
    }

    /// <summary>
    /// Get connected clusters
    /// </summary>
    public IEnumerable<string> GetConnectedClusters()
    {
        return _clients.Keys;
    }

    private static string GetVolumeType(V1Volume volume)
    {
        if (volume.ConfigMap != null) return "ConfigMap";
        if (volume.Secret != null) return "Secret";
        if (volume.PersistentVolumeClaim != null) return "PersistentVolumeClaim";
        if (volume.EmptyDir != null) return "EmptyDir";
        if (volume.HostPath != null) return "HostPath";
        if (volume.AzureDisk != null) return "AzureDisk";
        if (volume.AzureFile != null) return "AzureFile";
        return "Unknown";
    }
}
