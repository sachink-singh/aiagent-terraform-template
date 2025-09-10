using System.ComponentModel;
using System.Text;
using k8s;
using k8s.Models;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace AzureAIAgent.Plugins;

/// <summary>
/// AKS MCP Plugin for querying internal state of deployed AKS clusters
/// Provides deep visibility into Kubernetes resources and workloads
/// </summary>
public class AksMcpPlugin
{
    private readonly Dictionary<string, IKubernetes> _kubernetesClients = new();

    [KernelFunction("GetPods")]
    [Description("Get all pods in an AKS cluster or use current kubectl context")]
    public async Task<string> GetPods(
        [Description("Name of the AKS deployment (or 'current' to use current kubectl context)")] string deploymentName = "current",
        [Description("Namespace to filter pods (optional)")] string? namespaceFilter = null,
        [Description("Show only failed/problematic pods")] bool onlyProblematic = false)
    {
        try
        {
            IKubernetes client;
            
            // If deploymentName is 'current' or no explicit connection exists, use current kubectl context
            if (deploymentName == "current" || !_kubernetesClients.TryGetValue(deploymentName, out client!))
            {
                try
                {
                    // Use current kubectl context
                    var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    client = new Kubernetes(config);
                    
                    // Test the connection
                    var testNamespaces = await client.CoreV1.ListNamespaceAsync();
                    Console.WriteLine($"✅ Using current kubectl context with {testNamespaces.Items.Count} namespaces");
                }
                catch (Exception ex)
                {
                    return $"❌ Failed to connect using current kubectl context: {ex.Message}\n" +
                           $"💡 Ensure you're logged in: 'az aks get-credentials --resource-group <rg> --name {deploymentName}'";
                }
            }

            var pods = string.IsNullOrEmpty(namespaceFilter)
                ? await client.CoreV1.ListPodForAllNamespacesAsync()
                : await client.CoreV1.ListNamespacedPodAsync(namespaceFilter);

            var result = new StringBuilder();
            result.AppendLine($"Pods in cluster: {pods.Items.Count} total");
            result.AppendLine();

            if (pods.Items.Count == 0)
            {
                result.AppendLine("No pods found.");
                return result.ToString();
            }

            // Group by namespace
            var groupedPods = pods.Items.GroupBy(p => p.Metadata.NamespaceProperty).OrderBy(g => g.Key);

            foreach (var namespaceGroup in groupedPods)
            {
                var namespacePods = namespaceGroup.ToList();
                
                if (onlyProblematic)
                {
                    namespacePods = namespacePods.Where(p => 
                        p.Status.Phase != "Running" || 
                        p.Status.ContainerStatuses?.Any(c => c.RestartCount > 0) == true).ToList();
                }

                if (namespacePods.Count == 0) continue;

                result.AppendLine($"Namespace: {namespaceGroup.Key}");

                foreach (var pod in namespacePods.OrderBy(p => p.Metadata.Name))
                {
                    var name = pod.Metadata.Name ?? "Unknown";
                    var status = pod.Status.Phase ?? "Unknown";
                    var restarts = pod.Status.ContainerStatuses?.Sum(c => c.RestartCount) ?? 0;
                    var age = pod.Metadata.CreationTimestamp.HasValue 
                        ? (DateTime.UtcNow - pod.Metadata.CreationTimestamp.Value).Days + "d"
                        : "Unknown";
                    var node = pod.Spec.NodeName ?? "Unscheduled";

                    // Hierarchical format: name first, then details below with tree arrow
                    result.AppendLine($"{name}");
                    result.AppendLine($"└→ Status: {status} | Restarts: {restarts} | Age: {age} | Node: {node}");
                    result.AppendLine();
                }
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error retrieving pods: {ex.Message}";
        }
    }

    [KernelFunction("GetServices")]
    [Description("Get all services in an AKS cluster or use current kubectl context")]
    public async Task<string> GetServices(
        [Description("Name of the AKS deployment (or 'current' to use current kubectl context)")] string deploymentName = "current",
        [Description("Namespace to filter services (optional)")] string? namespaceFilter = null)
    {
        try
        {
            IKubernetes client;
            
            // Use current kubectl context if deploymentName is 'current' or no explicit connection exists
            if (deploymentName == "current" || !_kubernetesClients.TryGetValue(deploymentName, out client!))
            {
                try
                {
                    var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    client = new Kubernetes(config);
                }
                catch (Exception ex)
                {
                    return $"❌ Failed to connect using current kubectl context: {ex.Message}\n" +
                           $"💡 Ensure you're logged in: 'az aks get-credentials --resource-group <rg> --name {deploymentName}'";
                }
            }

            var services = string.IsNullOrEmpty(namespaceFilter)
                ? await client.CoreV1.ListServiceForAllNamespacesAsync()
                : await client.CoreV1.ListNamespacedServiceAsync(namespaceFilter);

            var result = new StringBuilder();
            result.AppendLine($"📋 **Services in AKS Cluster** ({services.Items.Count} total)");
            result.AppendLine();

            if (services.Items.Count == 0)
            {
                result.AppendLine("No services found.");
                return result.ToString();
            }

            // Group by namespace
            var groupedServices = services.Items.GroupBy(s => s.Metadata.NamespaceProperty).OrderBy(g => g.Key);

            foreach (var namespaceGroup in groupedServices)
            {
                result.AppendLine($"## 🌐 **Namespace: {namespaceGroup.Key}**");
                result.AppendLine();

                foreach (var service in namespaceGroup.OrderBy(s => s.Metadata.Name))
                {
                    var name = service.Metadata.Name ?? "Unknown";
                    var type = service.Spec.Type ?? "ClusterIP";
                    var clusterIp = service.Spec.ClusterIP ?? "None";
                    var externalIp = service.Status?.LoadBalancer?.Ingress?.FirstOrDefault()?.Ip ?? 
                                   service.Spec.ExternalIPs?.FirstOrDefault() ?? 
                                   (service.Spec.Type == "LoadBalancer" ? "<pending>" : "<none>");
                    var ports = string.Join(", ", service.Spec.Ports?.Select(p => $"{p.Port}/{p.Protocol}") ?? new[] { "None" });

                    // Service type emoji
                    var typeEmoji = type switch
                    {
                        "LoadBalancer" => "🌍",
                        "NodePort" => "🔗",
                        "ClusterIP" => "🔒",
                        "ExternalName" => "🔄",
                        _ => "📡"
                    };

                    result.AppendLine($"**{typeEmoji} {name}**");
                    result.AppendLine($"└─ Type: `{type}` | Cluster IP: `{clusterIp}` | External IP: `{externalIp}`");
                    result.AppendLine($"└─ Ports: `{ports}`");
                    result.AppendLine();
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error retrieving services: {ex.Message}";
        }
    }

    [KernelFunction("GetNamespaces")]
    [Description("Get all namespaces in an AKS cluster or use current kubectl context")]
    public async Task<string> GetNamespaces(
        [Description("Name of the AKS deployment (or 'current' to use current kubectl context)")] string deploymentName = "current")
    {
        try
        {
            IKubernetes client;
            
            // Use current kubectl context if deploymentName is 'current' or no explicit connection exists
            if (deploymentName == "current" || !_kubernetesClients.TryGetValue(deploymentName, out client!))
            {
                try
                {
                    var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    client = new Kubernetes(config);
                }
                catch (Exception ex)
                {
                    return $"❌ Failed to connect using current kubectl context: {ex.Message}\n" +
                           $"💡 Ensure you're logged in: 'az aks get-credentials --resource-group <rg> --name {deploymentName}'";
                }
            }

            var namespaces = await client.CoreV1.ListNamespaceAsync();

            var result = new StringBuilder();
            result.AppendLine($"📋 **Namespaces in AKS Cluster** ({namespaces.Items.Count} total)");
            result.AppendLine();

            foreach (var ns in namespaces.Items.OrderBy(n => n.Metadata.Name))
            {
                var name = ns.Metadata.Name ?? "Unknown";
                var status = ns.Status.Phase ?? "Unknown";
                var age = ns.Metadata.CreationTimestamp.HasValue 
                    ? (DateTime.UtcNow - ns.Metadata.CreationTimestamp.Value).Days + "d"
                    : "Unknown";

                // Status emoji for namespaces
                var statusEmoji = status switch
                {
                    "Active" => "✅",
                    "Terminating" => "⏳",
                    _ => "❔"
                };

                result.AppendLine($"**{statusEmoji} {name}**");
                result.AppendLine($"└─ Status: `{status}` | Age: `{age}`");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error retrieving namespaces: {ex.Message}";
        }
    }

    [KernelFunction("GetNodes")]
    [Description("Get all nodes in an AKS cluster or use current kubectl context")]
    public async Task<string> GetNodes(
        [Description("Name of the AKS deployment (or 'current' to use current kubectl context)")] string deploymentName = "current")
    {
        try
        {
            IKubernetes client;
            
            // Use current kubectl context if deploymentName is 'current' or no explicit connection exists
            if (deploymentName == "current" || !_kubernetesClients.TryGetValue(deploymentName, out client!))
            {
                try
                {
                    var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    client = new Kubernetes(config);
                }
                catch (Exception ex)
                {
                    return $"❌ Failed to connect using current kubectl context: {ex.Message}\n" +
                           $"💡 Ensure you're logged in: 'az aks get-credentials --resource-group <rg> --name {deploymentName}'";
                }
            }

            var nodes = await client.CoreV1.ListNodeAsync();

            var result = new StringBuilder();
            result.AppendLine($"�️ **Nodes in AKS Cluster** ({nodes.Items.Count} total)");
            result.AppendLine();

            foreach (var node in nodes.Items.OrderBy(n => n.Metadata.Name))
            {
                var name = node.Metadata.Name ?? "Unknown";
                var status = node.Status.Conditions?.FirstOrDefault(c => c.Type == "Ready")?.Status == "True" ? "Ready" : "NotReady";
                var roles = string.Join(", ", node.Metadata.Labels?.Where(l => l.Key.Contains("node-role")).Select(l => l.Key.Split('/').Last()) ?? new[] { "worker" });
                var age = node.Metadata.CreationTimestamp.HasValue 
                    ? (DateTime.UtcNow - node.Metadata.CreationTimestamp.Value).Days + "d"
                    : "Unknown";
                var version = node.Status.NodeInfo?.KubeletVersion ?? "Unknown";

                // Status emoji for nodes
                var statusEmoji = status == "Ready" ? "✅" : "❌";

                // Role emoji
                var roleEmoji = roles.Contains("control-plane") || roles.Contains("master") ? "🎛️" : "⚙️";

                result.AppendLine($"**{statusEmoji} {roleEmoji} {name}**");
                result.AppendLine($"└─ Status: `{status}` | Roles: `{roles}` | Age: `{age}` | Version: `{version}`");
                result.AppendLine();
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error retrieving nodes: {ex.Message}";
        }
    }

    [KernelFunction("ConnectToAksCluster")]
    [Description("Connect to an AKS cluster using deployment information")]
    public async Task<string> ConnectToAksCluster(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Terraform directory containing cluster configuration")] string? terraformDirectory = null)
    {
        try
        {
            // If already connected, return success
            if (_kubernetesClients.ContainsKey(deploymentName))
            {
                return $"✅ Already connected to AKS cluster: {deploymentName}";
            }

            // Try current kubectl context first
            try
            {
                var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                var client = new Kubernetes(config);
                
                // Test the connection
                var testNamespaces = await client.CoreV1.ListNamespaceAsync();
                _kubernetesClients[deploymentName] = client;
                
                return $"✅ Connected to AKS cluster '{deploymentName}' using current kubectl context\n" +
                       $"📊 Found {testNamespaces.Items.Count} namespaces in the cluster";
            }
            catch (Exception ex)
            {
                return $"❌ Failed to connect to AKS cluster '{deploymentName}': {ex.Message}\n" +
                       $"💡 Ensure you're logged in: 'az aks get-credentials --resource-group <rg> --name {deploymentName}'";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Error connecting to AKS cluster: {ex.Message}";
        }
    }

    [KernelFunction("GetAksClusterOverview")]
    [Description("Get overview information about an AKS cluster")]
    public async Task<string> GetAksClusterOverview(
        [Description("Name of the AKS deployment")] string deploymentName)
    {
        try
        {
            IKubernetes client;
            
            // Use current kubectl context if no explicit connection exists
            if (!_kubernetesClients.TryGetValue(deploymentName, out client!))
            {
                try
                {
                    var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    client = new Kubernetes(config);
                }
                catch (Exception ex)
                {
                    return $"❌ Failed to connect using current kubectl context: {ex.Message}";
                }
            }

            var nodes = await client.CoreV1.ListNodeAsync();
            var namespaces = await client.CoreV1.ListNamespaceAsync();
            var pods = await client.CoreV1.ListPodForAllNamespacesAsync();
            var services = await client.CoreV1.ListServiceForAllNamespacesAsync();

            var result = new StringBuilder();
            result.AppendLine($"📊 **AKS Cluster Overview: {deploymentName}**");
            result.AppendLine();
            result.AppendLine($"🖥️  **Nodes**: {nodes.Items.Count}");
            result.AppendLine($"📦 **Namespaces**: {namespaces.Items.Count}");
            result.AppendLine($"🚀 **Pods**: {pods.Items.Count} total");
            result.AppendLine($"🌐 **Services**: {services.Items.Count} total");
            result.AppendLine();

            // Node health summary
            var readyNodes = nodes.Items.Count(n => n.Status.Conditions?.FirstOrDefault(c => c.Type == "Ready")?.Status == "True");
            result.AppendLine($"**Node Health**: {readyNodes}/{nodes.Items.Count} Ready");

            // Pod status summary
            var runningPods = pods.Items.Count(p => p.Status.Phase == "Running");
            var pendingPods = pods.Items.Count(p => p.Status.Phase == "Pending");
            var failedPods = pods.Items.Count(p => p.Status.Phase == "Failed");
            
            result.AppendLine($"**Pod Status**: {runningPods} Running, {pendingPods} Pending, {failedPods} Failed");
            result.AppendLine();

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error getting cluster overview: {ex.Message}";
        }
    }

    [KernelFunction("GetAksPods")]
    [Description("Get pods information (alias for GetPods)")]
    public async Task<string> GetAksPods(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Namespace to filter pods (optional)")] string? namespaceFilter = null,
        [Description("Show only failed/problematic pods")] bool onlyProblematic = false)
    {
        return await GetPods(deploymentName, namespaceFilter, onlyProblematic);
    }

    [KernelFunction("GetAksServices")]
    [Description("Get services information (alias for GetServices)")]
    public async Task<string> GetAksServices(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Namespace to filter services (optional)")] string? namespaceFilter = null)
    {
        return await GetServices(deploymentName, namespaceFilter);
    }

    [KernelFunction("GetAksLogs")]
    [Description("Get logs from a specific pod container")]
    public async Task<string> GetAksLogs(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Namespace name")] string namespaceName,
        [Description("Pod name")] string podName,
        [Description("Container name (optional)")] string? containerName = null,
        [Description("Number of lines to retrieve")] int lines = 100)
    {
        try
        {
            IKubernetes client;
            
            // Use current kubectl context if no explicit connection exists
            if (!_kubernetesClients.TryGetValue(deploymentName, out client!))
            {
                try
                {
                    var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
                    client = new Kubernetes(config);
                }
                catch (Exception ex)
                {
                    return $"❌ Failed to connect using current kubectl context: {ex.Message}";
                }
            }

            var logsStream = await client.CoreV1.ReadNamespacedPodLogAsync(
                name: podName,
                namespaceParameter: namespaceName,
                container: containerName,
                tailLines: lines);

            string logs;
            using (var reader = new StreamReader(logsStream))
            {
                logs = await reader.ReadToEndAsync();
            }

            var result = new StringBuilder();
            result.AppendLine($"📋 **Logs for Pod: {podName}**");
            if (!string.IsNullOrEmpty(containerName))
                result.AppendLine($"**Container**: {containerName}");
            result.AppendLine($"**Namespace**: {namespaceName}");
            result.AppendLine($"**Lines**: {lines}");
            result.AppendLine();
            result.AppendLine("```");
            result.AppendLine(logs);
            result.AppendLine("```");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error retrieving logs: {ex.Message}";
        }
    }

    [KernelFunction("ExecuteKubectlCommand")]
    [Description("Execute a kubectl command (limited to safe read-only operations)")]
    public async Task<string> ExecuteKubectlCommand(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Kubectl command to execute")] string command)
    {
        // For safety, only allow read-only kubectl commands
        var allowedCommands = new[] { "get", "describe", "logs", "top", "version", "cluster-info" };
        var commandParts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (commandParts.Length == 0 || !allowedCommands.Contains(commandParts[0].ToLower()))
        {
            return $"❌ Command not allowed. Only read-only commands are supported: {string.Join(", ", allowedCommands)}";
        }

        try
        {
            // For now, provide helpful suggestions for common kubectl commands
            var result = new StringBuilder();
            result.AppendLine($"💡 **Kubectl Command Suggestion for: {command}**");
            result.AppendLine();
            
            switch (commandParts[0].ToLower())
            {
                case "get":
                    if (commandParts.Length > 1)
                    {
                        switch (commandParts[1].ToLower())
                        {
                            case "pods":
                                return await GetPods(deploymentName);
                            case "services":
                            case "svc":
                                return await GetServices(deploymentName);
                            case "namespaces":
                            case "ns":
                                return await GetNamespaces(deploymentName);
                            case "nodes":
                                return await GetNodes(deploymentName);
                            default:
                                result.AppendLine($"Use the specific MCP functions for {commandParts[1]}:");
                                result.AppendLine("- GetPods for pods information");
                                result.AppendLine("- GetServices for services information");
                                result.AppendLine("- GetNamespaces for namespaces information");
                                result.AppendLine("- GetNodes for nodes information");
                                break;
                        }
                    }
                    break;
                case "describe":
                    result.AppendLine("Use GetPods with specific filters or GetAksClusterOverview for detailed information");
                    break;
                case "logs":
                    result.AppendLine("Use GetAksLogs function with pod name, namespace, and container details");
                    break;
                default:
                    result.AppendLine("This command is supported but not yet implemented in the MCP plugin");
                    break;
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error executing kubectl command: {ex.Message}";
        }
    }
}
