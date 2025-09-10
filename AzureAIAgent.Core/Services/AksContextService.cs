using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AzureAIAgent.Core.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzureAIAgent.Core.Services;

public interface IAksContextService
{
    Task<AksClusterInfo?> GetCurrentClusterAsync();
    Task<List<AksClusterInfo>> GetAvailableClustersAsync();
    Task<bool> SetCurrentClusterAsync(string clusterName, string resourceGroupName);
    Task<AksClusterInfo?> AutoDiscoverCurrentClusterAsync();
    Task<string> GetCurrentDeploymentNameAsync();
}

public class AksContextService : IAksContextService
{
    private readonly ILogger<AksContextService> _logger;
    private readonly AksContextConfiguration _config;
    private AksClusterInfo? _currentCluster;
    private readonly object _lockObject = new();

    public AksContextService(ILogger<AksContextService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _config = configuration.GetSection("AksContext").Get<AksContextConfiguration>() ?? new AksContextConfiguration();
    }

    public async Task<AksClusterInfo?> GetCurrentClusterAsync()
    {
        lock (_lockObject)
        {
            if (_currentCluster != null)
            {
                return _currentCluster;
            }
        }

        if (_config.AutoDiscoverClusters)
        {
            return await AutoDiscoverCurrentClusterAsync();
        }

        // Use configured default cluster
        if (!string.IsNullOrEmpty(_config.DefaultClusterName) && !string.IsNullOrEmpty(_config.DefaultResourceGroupName))
        {
            var cluster = new AksClusterInfo
            {
                Name = _config.DefaultClusterName,
                ResourceGroupName = _config.DefaultResourceGroupName,
                IsCurrentContext = true
            };

            lock (_lockObject)
            {
                _currentCluster = cluster;
            }

            return cluster;
        }

        return null;
    }

    public async Task<List<AksClusterInfo>> GetAvailableClustersAsync()
    {
        var clusters = new List<AksClusterInfo>();

        try
        {
            // Get clusters from Azure CLI
            using var process = new Process();
            process.StartInfo.FileName = "az";
            process.StartInfo.Arguments = "aks list --output json";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                var clusterData = JsonSerializer.Deserialize<JsonElement[]>(output);
                if (clusterData != null)
                {
                    foreach (var cluster in clusterData)
                    {
                        var clusterInfo = new AksClusterInfo
                        {
                            Name = cluster.GetProperty("name").GetString() ?? string.Empty,
                            ResourceGroupName = cluster.GetProperty("resourceGroup").GetString() ?? string.Empty,
                            Region = cluster.GetProperty("location").GetString() ?? string.Empty,
                            KubernetesVersion = cluster.GetProperty("kubernetesVersion").GetString() ?? string.Empty,
                            Status = ParseClusterStatus(cluster.GetProperty("provisioningState").GetString())
                        };

                        if (cluster.TryGetProperty("fqdn", out var fqdnElement))
                        {
                            clusterInfo.FqdnUrl = fqdnElement.GetString() ?? string.Empty;
                        }

                        clusters.Add(clusterInfo);
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to list AKS clusters: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            // Don't treat Azure CLI not being available as a critical error
            if (ex is Win32Exception win32Ex && win32Ex.NativeErrorCode == 2)
            {
                _logger.LogInformation("Azure CLI not found. AKS cluster discovery disabled. Using kubectl context only.");
            }
            else
            {
                _logger.LogError(ex, "Error listing AKS clusters");
            }
        }

        return clusters;
    }

    public async Task<bool> SetCurrentClusterAsync(string clusterName, string resourceGroupName)
    {
        try
        {
            // Get credentials for the cluster
            using var process = new Process();
            process.StartInfo.FileName = "az";
            process.StartInfo.Arguments = $"aks get-credentials --resource-group {resourceGroupName} --name {clusterName} --overwrite-existing";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var cluster = new AksClusterInfo
                {
                    Name = clusterName,
                    ResourceGroupName = resourceGroupName,
                    IsCurrentContext = true,
                    LastAccessed = DateTime.UtcNow
                };

                lock (_lockObject)
                {
                    _currentCluster = cluster;
                }

                _logger.LogInformation("Successfully set current AKS cluster to {ClusterName} in {ResourceGroup}", clusterName, resourceGroupName);
                return true;
            }
            else
            {
                _logger.LogError("Failed to set current AKS cluster: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting current AKS cluster");
            return false;
        }
    }

    public async Task<AksClusterInfo?> AutoDiscoverCurrentClusterAsync()
    {
        try
        {
            // First try to get current kubectl context
            var kubectlCluster = await GetCurrentKubectlContextAsync();
            if (kubectlCluster != null)
            {
                lock (_lockObject)
                {
                    _currentCluster = kubectlCluster;
                }
                return kubectlCluster;
            }

            // If no kubectl context, try to find clusters and use preferred one
            var clusters = await GetAvailableClustersAsync();
            if (clusters.Count == 0)
            {
                _logger.LogWarning("No AKS clusters found in current subscription");
                return null;
            }

            // Use preferred cluster if specified
            if (!string.IsNullOrEmpty(_config.PreferredClusterName))
            {
                var preferredCluster = clusters.FirstOrDefault(c => c.Name.Contains(_config.PreferredClusterName, StringComparison.OrdinalIgnoreCase));
                if (preferredCluster != null)
                {
                    await SetCurrentClusterAsync(preferredCluster.Name, preferredCluster.ResourceGroupName);
                    return preferredCluster;
                }
            }

            // Use the first running cluster
            var runningCluster = clusters.FirstOrDefault(c => c.Status == ClusterStatus.Running);
            if (runningCluster != null)
            {
                await SetCurrentClusterAsync(runningCluster.Name, runningCluster.ResourceGroupName);
                return runningCluster;
            }

            // Use the first cluster as fallback
            var firstCluster = clusters.First();
            await SetCurrentClusterAsync(firstCluster.Name, firstCluster.ResourceGroupName);
            return firstCluster;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-discovering current AKS cluster");
            return null;
        }
    }

    public async Task<string> GetCurrentDeploymentNameAsync()
    {
        var cluster = await GetCurrentClusterAsync();
        return cluster?.Name ?? "default-cluster";
    }

    private async Task<AksClusterInfo?> GetCurrentKubectlContextAsync()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "kubectl";
            process.StartInfo.Arguments = "config current-context";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                var contextName = output.Trim();
                _logger.LogInformation("Current kubectl context: {Context}", contextName);

                // Parse AKS context name to extract cluster and resource group
                // AKS context format is typically: clustername or clustername_resourcegroup_subscriptionId
                var match = Regex.Match(contextName, @"^([^_]+)(?:_([^_]+))?");
                if (match.Success)
                {
                    var clusterName = match.Groups[1].Value;
                    var resourceGroup = match.Groups.Count > 2 ? match.Groups[2].Value : string.Empty;

                    // If we don't have resource group from context, try to find it
                    if (string.IsNullOrEmpty(resourceGroup))
                    {
                        var clusters = await GetAvailableClustersAsync();
                        var foundCluster = clusters.FirstOrDefault(c => c.Name.Equals(clusterName, StringComparison.OrdinalIgnoreCase));
                        if (foundCluster != null)
                        {
                            resourceGroup = foundCluster.ResourceGroupName;
                        }
                    }

                    return new AksClusterInfo
                    {
                        Name = clusterName,
                        ResourceGroupName = resourceGroup,
                        IsCurrentContext = true,
                        LastAccessed = DateTime.UtcNow
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting current kubectl context - this is normal if kubectl is not configured");
        }

        return null;
    }

    private static ClusterStatus ParseClusterStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "succeeded" => ClusterStatus.Running,
            "creating" => ClusterStatus.Creating,
            "updating" => ClusterStatus.Updating,
            "deleting" => ClusterStatus.Deleting,
            "failed" => ClusterStatus.Failed,
            _ => ClusterStatus.Unknown
        };
    }
}
