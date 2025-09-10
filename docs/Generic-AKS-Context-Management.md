# AKS Context Management - Generic Cluster Support

## Overview

Previously, the AzureAIAgent.Core had hardcoded AKS cluster names (`aks-dev-aksworkload-si-002` and `rg-dev-aksworkload-si-002`) which made it work only with a specific cluster. This has been refactored to support any AKS cluster dynamically.

## Changes Made

### 1. New Configuration Models
- Added `AksContextConfiguration` for managing cluster discovery settings
- Added `AksClusterInfo` to represent cluster information
- Added `ClusterStatus` enum for cluster states

### 2. AKS Context Service
Created `IAksContextService` and `AksContextService` that provides:
- **Automatic cluster discovery** from Azure CLI
- **kubectl context integration** to use current cluster
- **Dynamic cluster switching** capability
- **Preferred cluster selection** based on configuration

### 3. Configuration Updates
Added AKS context configuration to `appsettings.json`:
```json
{
  "AksContext": {
    "AutoDiscoverClusters": true,
    "DefaultClusterName": "",
    "DefaultResourceGroupName": "",
    "PreferredClusterName": "",
    "UseKubectlContext": true
  }
}
```

### 4. Updated Core Logic
- `AzureAIAgent.cs` now uses the AKS context service instead of hardcoded values
- All cluster connections are now dynamic
- Error handling for missing cluster context

## How It Works

### Auto-Discovery Flow
1. **kubectl context check**: First tries to get current kubectl context
2. **Azure CLI discovery**: Lists all AKS clusters in current subscription
3. **Preferred cluster selection**: Uses configured preferred cluster if available
4. **Fallback selection**: Uses first running cluster or first available cluster

### Configuration Options

#### 1. Auto-Discovery (Default)
```json
{
  "AksContext": {
    "AutoDiscoverClusters": true,
    "UseKubectlContext": true
  }
}
```
- Automatically finds and uses current cluster
- Works with any cluster without configuration

#### 2. Preferred Cluster
```json
{
  "AksContext": {
    "AutoDiscoverClusters": true,
    "PreferredClusterName": "my-preferred-cluster",
    "UseKubectlContext": true
  }
}
```
- Auto-discovers but prefers a specific cluster name

#### 3. Fixed Cluster
```json
{
  "AksContext": {
    "AutoDiscoverClusters": false,
    "DefaultClusterName": "my-cluster",
    "DefaultResourceGroupName": "my-rg"
  }
}
```
- Uses a specific cluster configuration

## Benefits

### ✅ Generic Support
- Works with **any AKS cluster** in your subscription
- No more hardcoded cluster names
- Automatically adapts to your environment

### ✅ Developer Friendly
- Uses your current `kubectl` context automatically
- No configuration needed for most scenarios
- Easy cluster switching with `kubectl config use-context`

### ✅ Production Ready
- Supports explicit cluster configuration
- Error handling for missing clusters
- Logging for troubleshooting

### ✅ Backward Compatible
- Existing functionality preserved
- Configuration-driven behavior
- Graceful fallbacks

## Usage Examples

### 1. Using with kubectl context
```bash
# Set your desired cluster as current context
kubectl config use-context my-cluster

# The agent will automatically use this cluster
# No additional configuration needed
```

### 2. Using with preferred cluster
```bash
# Configure preferred cluster in appsettings.json
# Agent will find and use this cluster automatically
```

### 3. Switching clusters programmatically
```csharp
// Use AKS context service to switch clusters
var success = await aksContextService.SetCurrentClusterAsync("new-cluster", "new-rg");
```

## Migration from Hardcoded Values

### Before (Hardcoded)
```csharp
await _kernel.InvokeAsync(connectFunction, new KernelArguments
{
    ["clusterName"] = "aks-dev-aksworkload-si-002",
    ["resourceGroupName"] = "rg-dev-aksworkload-si-002",
    ["deploymentName"] = "aks-dev-aksworkload-si-002"
});
```

### After (Dynamic)
```csharp
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
```

## Error Handling

The service provides clear error messages when:
- No AKS clusters are found
- kubectl is not configured
- Azure CLI is not available
- Cluster connection fails

Example error response:
```
❌ No AKS cluster context found. Please ensure you're connected to an AKS cluster.
```

## Future Enhancements

Potential future improvements:
- **Multi-cluster support**: Work with multiple clusters simultaneously
- **Cluster health monitoring**: Check cluster status before operations
- **Cluster recommendations**: Suggest optimal cluster based on workload
- **Configuration UI**: Visual cluster selection interface

## Conclusion

The AKS context management system now provides **generic, flexible support for any AKS cluster** while maintaining ease of use and backward compatibility. This eliminates the hardcoded cluster dependency and makes the agent truly portable across different AKS environments.
