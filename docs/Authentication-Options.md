# Azure Authentication Options for Production Deployment

## Overview
When deploying the Azure AI Agent to Azure, you need production-ready authentication instead of `az login`. This document outlines all available options.

## 1. 🏆 Managed Identity (Recommended)

### System-Assigned Managed Identity
```csharp
// Add to Program.cs
using Azure.Identity;

// Configure DefaultAzureCredential for production
builder.Services.AddSingleton<TokenCredential>(provider =>
{
    var options = new DefaultAzureCredentialOptions
    {
        // For App Service, Container Apps, Azure Functions
        ManagedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"] // Optional for user-assigned
    };
    return new DefaultAzureCredential(options);
});

// Update AzureCommandExecutor to use Azure SDK instead of CLI
builder.Services.AddSingleton<IAzureCommandExecutor, AzureSDKCommandExecutor>();
```

### User-Assigned Managed Identity
```json
// appsettings.Production.json
{
  "Azure": {
    "ManagedIdentity": {
      "ClientId": "your-user-assigned-mi-client-id"
    }
  }
}
```

### Deployment Services Supporting Managed Identity:
- ✅ **Azure App Service**
- ✅ **Azure Container Apps** 
- ✅ **Azure Functions**
- ✅ **Azure Kubernetes Service (AKS)**
- ✅ **Azure Virtual Machines**

## 2. 🔑 Service Principal with Certificate

### Setup
```csharp
// Program.cs
var credential = new ClientCertificateCredential(
    tenantId: builder.Configuration["Azure:TenantId"],
    clientId: builder.Configuration["Azure:ClientId"], 
    certificatePath: builder.Configuration["Azure:CertificatePath"]);

builder.Services.AddSingleton<TokenCredential>(credential);
```

### Configuration
```json
// appsettings.Production.json
{
  "Azure": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-service-principal-client-id",
    "CertificatePath": "/app/certs/azure-sp.pfx"
  }
}
```

## 3. 🔐 Service Principal with Secret

### Setup
```csharp
// Program.cs  
var credential = new ClientSecretCredential(
    tenantId: builder.Configuration["Azure:TenantId"],
    clientId: builder.Configuration["Azure:ClientId"],
    clientSecret: builder.Configuration["Azure:ClientSecret"]);

builder.Services.AddSingleton<TokenCredential>(credential);
```

### Configuration (Environment Variables)
```bash
# Set as environment variables or Key Vault references
AZURE_TENANT_ID=your-tenant-id
AZURE_CLIENT_ID=your-service-principal-client-id  
AZURE_CLIENT_SECRET=your-service-principal-secret
```

## 4. 🏢 Workload Identity (AKS/Kubernetes)

### For AKS deployments
```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: azure-ai-agent
spec:
  template:
    metadata:
      labels:
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: azure-ai-agent-sa
      containers:
      - name: azure-ai-agent
        image: your-registry/azure-ai-agent:latest
        env:
        - name: AZURE_CLIENT_ID
          value: "your-workload-identity-client-id"
```

## 5. 🔗 Federated Identity (GitHub Actions, External)

### For CI/CD pipelines
```csharp
// Use DefaultAzureCredential which automatically detects federated tokens
var credential = new DefaultAzureCredential();
```

## Implementation Strategy

### Step 1: Create Azure SDK Command Executor
Replace Azure CLI dependency with Azure SDK for better authentication support.

### Step 2: Update Dependency Injection
Configure TokenCredential based on deployment environment.

### Step 3: Environment-Specific Configuration
Use different authentication methods per environment (dev/staging/prod).

### Step 4: Permission Configuration
Ensure the identity has required Azure permissions:
- Contributor (for resource creation)
- Reader (for resource queries)
- Custom roles (for specific operations)

## Deployment Targets & Recommended Auth

| Deployment Target | Recommended Auth | Fallback |
|-------------------|------------------|----------|
| Azure App Service | System-Assigned MI | Service Principal |
| Azure Container Apps | System-Assigned MI | User-Assigned MI |
| Azure Functions | System-Assigned MI | Service Principal |
| AKS | Workload Identity | Service Principal |
| Azure VMs | System-Assigned MI | Service Principal |
| External (AWS/GCP) | Service Principal | Federated Identity |

## Security Best Practices

1. **Never store secrets in code**
2. **Use Key Vault for sensitive configuration**
3. **Rotate credentials regularly**
4. **Apply principle of least privilege**
5. **Monitor authentication logs**
6. **Use certificate-based auth over secrets**

## Next Steps

1. Choose authentication method based on deployment target
2. Implement Azure SDK command executor
3. Configure environment-specific settings
4. Test authentication in staging environment
5. Deploy with proper RBAC permissions
