# 🔧 AKS MCP Connection Fix

## Issue
User tried to query pods in AKS cluster `aks-dev-aksworkload-si-002` but got:
```
❌ No AKS cluster context found. Please ensure you're connected to an AKS cluster.
```

## Root Cause
The AKS MCP plugin requires establishing a connection to the Kubernetes cluster before querying resources. The connection wasn't established first.

## Solution: Connect First, Then Query

### Method 1: Connect to Existing AKS Cluster (Recommended)
```
Connect to existing AKS cluster aks-dev-aksworkload-si-002 in resource group rg-dev-aksworkload-si
```

This will:
1. Use Azure CLI credentials to get cluster credentials
2. Establish Kubernetes client connection
3. Test the connection by listing namespaces

### Method 2: Connect via Terraform (if deployed via Terraform)
```
Connect to AKS cluster aks-dev-aksworkload-si-002 from terraform directory /path/to/terraform
```

This will:
1. Extract kubeconfig from Terraform outputs
2. Create Kubernetes client from kubeconfig
3. Test connection

### Then Query Pods
After successful connection:
```
List all pods in AKS cluster aks-dev-aksworkload-si-002
```

## Expected Workflow
1. **Connect**: `Connect to existing AKS cluster aks-dev-aksworkload-si-002 in resource group [RG_NAME]`
2. **Verify**: Should see `✅ Successfully connected to AKS cluster`
3. **Query**: `List all pods in AKS cluster aks-dev-aksworkload-si-002`
4. **Result**: Should see pod listings with status, namespace, etc.

## Alternative: Auto-Connect Enhancement
The system could be enhanced to automatically attempt connection when a query is made to an unconnected cluster, but currently requires explicit connection.
