# Production Authentication Architecture

## Overview
This document outlines how to deploy the Azure AI Agent to AKS with proper user authentication and authorization.

## Authentication Flow

```
Frontend (React/Blazor) → Azure AD → API Gateway → Azure AI Agent Pod → Azure Resources
                                        ↓
                                   User Token Validation
                                        ↓
                                   Service Principal/Managed Identity
                                        ↓
                                   RBAC-controlled Access
```

## Implementation Components

### 1. Frontend Authentication
```javascript
// React/Angular frontend with MSAL
import { PublicClientApplication } from "@azure/msal-browser";

const msalConfig = {
    auth: {
        clientId: "your-app-registration-client-id",
        authority: "https://login.microsoftonline.com/your-tenant-id"
    }
};

const msalInstance = new PublicClientApplication(msalConfig);

// Get token for API calls
const getAccessToken = async () => {
    const request = {
        scopes: ["api://your-api-app-id/access_as_user"]
    };
    
    const response = await msalInstance.acquireTokenSilent(request);
    return response.accessToken;
};
```

### 2. API Authentication Middleware
```csharp
public class UserAuthenticationMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var token = ExtractBearerToken(context.Request);
        
        if (token != null)
        {
            var userClaims = await ValidateTokenAsync(token);
            var azureCredential = await GetAzureCredentialForUserAsync(userClaims);
            
            // Store in context for downstream services
            context.Items["UserCredential"] = azureCredential;
            context.Items["UserClaims"] = userClaims;
        }
        
        await next(context);
    }
}
```

### 3. Enhanced MCP Kubernetes Plugin
```csharp
public class AuthenticatedMcpKubernetesPlugin : IMcpKubernetesPlugin
{
    private readonly IUserAuthenticationService _authService;
    
    public async Task<string> GetPodsAsync(string clusterId, string userId)
    {
        // Get user-specific kubernetes client
        var kubeConfig = await _authService.GetUserKubernetesConfigAsync(userId);
        var client = new Kubernetes(kubeConfig);
        
        // Apply user's RBAC restrictions
        var allowedNamespaces = await GetUserAllowedNamespacesAsync(userId);
        
        var pods = new List<V1Pod>();
        foreach (var ns in allowedNamespaces)
        {
            var namespacePods = await client.ListNamespacedPodAsync(ns);
            pods.AddRange(namespacePods.Items);
        }
        
        return SerializePods(pods);
    }
}
```

## Deployment Configuration

### 1. Workload Identity Setup
```bash
# Create managed identity
az identity create --name azure-ai-agent-identity --resource-group $RG

# Create service account
kubectl create serviceaccount azure-ai-agent-sa

# Link service account to managed identity
az aks pod-identity add \
  --resource-group $RG \
  --cluster-name $CLUSTER_NAME \
  --namespace default \
  --name azure-ai-agent-identity \
  --identity-resource-id $IDENTITY_RESOURCE_ID
```

### 2. RBAC Configuration
```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: azure-ai-agent-base-role
rules:
- apiGroups: [""]
  resources: ["pods", "services", "namespaces"]
  verbs: ["get", "list"]
- apiGroups: ["apps"]
  resources: ["deployments"]
  verbs: ["get", "list"]

---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: azure-ai-agent-user-role
  namespace: user-namespace
rules:
- apiGroups: [""]
  resources: ["pods/log"]
  verbs: ["get"]
```

### 3. Azure RBAC for Resource Management
```bash
# Assign roles to managed identity
az role assignment create \
  --assignee $MANAGED_IDENTITY_PRINCIPAL_ID \
  --role "Azure Kubernetes Service Cluster User Role" \
  --scope $CLUSTER_RESOURCE_ID

az role assignment create \
  --assignee $MANAGED_IDENTITY_PRINCIPAL_ID \
  --role "Reader" \
  --scope $SUBSCRIPTION_ID
```

## Security Considerations

### 1. Token Validation
```csharp
public class TokenValidationService
{
    public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = await GetAzureADSigningKeysAsync(),
            ValidateIssuer = true,
            ValidIssuer = $"https://sts.windows.net/{_tenantId}/",
            ValidateAudience = true,
            ValidAudience = _clientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        
        var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
        return principal;
    }
}
```

### 2. Least Privilege Access
```csharp
public class UserPermissionService
{
    public async Task<List<string>> GetUserAllowedNamespacesAsync(string userId)
    {
        // Query your user management system
        var userGroups = await GetUserGroupsAsync(userId);
        
        // Map groups to Kubernetes namespaces
        var allowedNamespaces = new List<string>();
        
        foreach (var group in userGroups)
        {
            var namespaces = await GetGroupNamespacesAsync(group);
            allowedNamespaces.AddRange(namespaces);
        }
        
        return allowedNamespaces.Distinct().ToList();
    }
}
```

## Configuration Examples

### 1. appsettings.Production.json
```json
{
  "Authentication": {
    "AzureAd": {
      "Instance": "https://login.microsoftonline.com/",
      "TenantId": "your-tenant-id",
      "ClientId": "your-api-app-registration-id",
      "Audience": "api://your-api-app-id"
    },
    "ManagedIdentity": {
      "ClientId": "your-managed-identity-client-id"
    }
  },
  "Kubernetes": {
    "ClusterEndpoint": "https://your-cluster-api-server",
    "DefaultNamespace": "default"
  },
  "Azure": {
    "SubscriptionId": "your-subscription-id",
    "ResourceGroup": "your-resource-group"
  }
}
```

### 2. Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: azure-ai-agent
  namespace: azure-ai-agent
spec:
  replicas: 3
  selector:
    matchLabels:
      app: azure-ai-agent
  template:
    metadata:
      labels:
        app: azure-ai-agent
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: azure-ai-agent-sa
      containers:
      - name: azure-ai-agent
        image: your-acr.azurecr.io/azure-ai-agent:latest
        ports:
        - containerPort: 5050
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: AZURE_CLIENT_ID
          value: "your-managed-identity-client-id"
        - name: AZURE_TENANT_ID
          value: "your-tenant-id"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5050
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 5050
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: azure-ai-agent-service
spec:
  selector:
    app: azure-ai-agent
  ports:
  - protocol: TCP
    port: 80
    targetPort: 5050
  type: ClusterIP

---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: azure-ai-agent-ingress
  annotations:
    kubernetes.io/ingress.class: azure/application-gateway
    appgw.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
  - hosts:
    - your-domain.com
    secretName: tls-secret
  rules:
  - host: your-domain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: azure-ai-agent-service
            port:
              number: 80
```

## Migration Steps

1. **Update Authentication**: Implement user token validation and Azure credential management
2. **Configure RBAC**: Set up Kubernetes and Azure RBAC for user permissions
3. **Deploy Infrastructure**: Use managed identity and workload identity
4. **Test Security**: Verify users can only access their allowed resources
5. **Monitor Access**: Implement logging and monitoring for security auditing

## Benefits of This Approach

- ✅ **Secure**: No stored credentials, proper token validation
- ✅ **Scalable**: Managed identity handles token rotation
- ✅ **Auditable**: All actions are logged with user context
- ✅ **Granular**: Fine-grained RBAC controls
- ✅ **Cloud-native**: Uses Azure AD and Kubernetes RBAC
