# Enterprise Kubernetes MCP Server

A secure, enterprise-grade Model Context Protocol (MCP) server for Kubernetes cluster management. This server provides controlled access to Kubernetes resources with comprehensive audit logging and security features.

## 🔐 Security Features

- **Enterprise-grade Security**: Designed for organizational security requirements
- **Audit Logging**: Comprehensive logging of all operations
- **Azure Identity Integration**: Uses Azure CLI credentials for secure authentication
- **No External Dependencies**: Self-contained server under your organization's control
- **Controlled Access**: Configurable cluster access restrictions

## 🚀 Features

### Supported Operations

- **Cluster Connection**: Connect to AKS clusters using Azure CLI credentials
- **Pod Management**: List, describe, and monitor pods across namespaces
- **Deployment Monitoring**: View deployment status and configurations
- **Service Discovery**: List and inspect Kubernetes services
- **Namespace Operations**: Browse cluster namespaces
- **Multi-cluster Support**: Manage multiple connected clusters

### MCP Protocol Compliance

- Fully compliant with Model Context Protocol specification
- JSON-RPC 2.0 transport over stdio
- Standard tool/resource schema definitions
- Error handling and validation

## 🛠️ Installation & Setup

### Prerequisites

- .NET 8.0 or later
- Azure CLI installed and configured (`az login`)
- Access to Azure Kubernetes Service clusters

### Build the Server

```bash
cd KubernetesMcpServer
dotnet build
dotnet publish -c Release
```

### Run the Server

```bash
dotnet run
```

The server communicates via stdin/stdout using the MCP protocol.

## 📋 Available Tools

### 1. connect_aks_cluster
Connect to an Azure Kubernetes Service cluster.

**Parameters:**
- `clusterName` (required): Name of the AKS cluster
- `resourceGroup` (required): Azure resource group name
- `subscriptionId` (optional): Azure subscription ID

### 2. get_pods
List all pods in a connected cluster.

**Parameters:**
- `clusterName` (required): Name of the connected cluster
- `namespace` (optional): Kubernetes namespace filter

### 3. get_pod_details
Get detailed information about a specific pod.

**Parameters:**
- `clusterName` (required): Name of the connected cluster
- `podName` (required): Name of the pod
- `namespace` (optional): Kubernetes namespace hint

### 4. get_deployments
List all deployments in a connected cluster.

**Parameters:**
- `clusterName` (required): Name of the connected cluster
- `namespace` (optional): Kubernetes namespace filter

### 5. get_services
List all services in a connected cluster.

**Parameters:**
- `clusterName` (required): Name of the connected cluster
- `namespace` (optional): Kubernetes namespace filter

### 6. get_namespaces
List all namespaces in a connected cluster.

**Parameters:**
- `clusterName` (required): Name of the connected cluster

### 7. list_connected_clusters
List all currently connected clusters.

**Parameters:** None

## 🔧 Integration with AI Agents

This MCP server can be integrated with AI agents and the Azure AI Agent system. Example integration:

```csharp
// In your AI agent configuration
services.AddMcpClient("kubernetes", new McpClientOptions
{
    ServerPath = "dotnet",
    ServerArgs = ["run", "--project", "KubernetesMcpServer"],
    Transport = McpTransport.Stdio
});
```

## 📊 Example Usage

### Connect to a Cluster
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "connect_aks_cluster",
    "arguments": {
      "clusterName": "aks-dev-aksworkload-si-002",
      "resourceGroup": "rg-dev-aksworkload-si-002"
    }
  }
}
```

### List Pods
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "get_pods",
    "arguments": {
      "clusterName": "aks-dev-aksworkload-si-002",
      "namespace": "kube-system"
    }
  }
}
```

## 🔒 Security Considerations

1. **Authentication**: Uses Azure CLI credentials - ensure proper Azure RBAC setup
2. **Network Security**: Server runs locally and doesn't expose external ports
3. **Audit Trail**: All operations are logged for security compliance
4. **Least Privilege**: Only provides read-access to cluster resources
5. **No Secrets**: Does not expose or store sensitive Kubernetes secrets

## 📝 Configuration

Edit `appsettings.json` to configure:

- **Logging levels**: Adjust verbosity for audit requirements
- **Security settings**: Configure allowed clusters and authentication
- **Performance settings**: Set connection limits and timeouts

## 🚨 Enterprise Deployment

For enterprise deployment:

1. **Code Review**: Review all source code for security compliance
2. **Security Scanning**: Run static analysis and vulnerability scans
3. **Network Isolation**: Deploy in controlled network environments
4. **Monitoring**: Integrate with your organization's monitoring systems
5. **Backup**: Ensure configuration and logs are properly backed up

## 📈 Monitoring & Observability

The server provides comprehensive logging for:

- Connection attempts and status
- All tool executions with parameters
- Error conditions and exceptions
- Performance metrics and timing

## 🤝 Contributing

This is an enterprise-controlled codebase. All changes should:

1. Follow security review processes
2. Include comprehensive tests
3. Update documentation
4. Maintain backward compatibility

## 📄 License

Enterprise License - Internal Use Only

---

**Security Notice**: This server provides access to Kubernetes clusters. Ensure proper Azure RBAC configuration and monitor all usage through audit logs.
