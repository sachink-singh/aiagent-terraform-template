# Azure AI Agent Production Deployment Guide

## Quick Reference: Authentication by Deployment Target

| Service | Primary Auth | Setup Command | Configuration |
|---------|--------------|---------------|---------------|
| **App Service** | System-Assigned MI | `az webapp identity assign` | Environment variables |
| **Container Apps** | System-Assigned MI | `az containerapp identity assign` | Managed Identity |
| **Azure Functions** | System-Assigned MI | `az functionapp identity assign` | App Settings |
| **AKS** | Workload Identity | Setup federated identity | Service Account |
| **VM** | System-Assigned MI | `az vm identity assign` | Azure SDK |

## 🚀 Deployment Scenarios

### 1. Azure App Service (Recommended)

#### Step 1: Enable Managed Identity
```bash
# Create App Service with System-Assigned Managed Identity
az webapp create \
  --resource-group myResourceGroup \
  --plan myAppServicePlan \
  --name my-azure-ai-agent \
  --runtime "DOTNETCORE:8.0" \
  --assign-identity

# Get the principal ID for role assignment
principalId=$(az webapp show \
  --resource-group myResourceGroup \
  --name my-azure-ai-agent \
  --query identity.principalId --output tsv)
```

#### Step 2: Assign Required Permissions
```bash
# Assign Contributor role for resource management
az role assignment create \
  --assignee $principalId \
  --role "Contributor" \
  --scope "/subscriptions/{subscription-id}"

# Assign specific roles for Azure OpenAI (if using)
az role assignment create \
  --assignee $principalId \
  --role "Cognitive Services OpenAI User" \
  --scope "/subscriptions/{subscription-id}/resourceGroups/{openai-rg}/providers/Microsoft.CognitiveServices/accounts/{openai-account}"
```

#### Step 3: Configure App Settings
```bash
# Set production configuration
az webapp config appsettings set \
  --resource-group myResourceGroup \
  --name my-azure-ai-agent \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    Azure__UseSDKExecutor=true \
    Azure__SubscriptionId="{subscription-id}" \
    AzureOpenAI__Endpoint="https://your-openai.openai.azure.com/" \
    AzureOpenAI__DeploymentName="gpt-4"
```

#### Step 4: Deploy Application
```bash
# Build and deploy
dotnet publish -c Release
az webapp deploy \
  --resource-group myResourceGroup \
  --name my-azure-ai-agent \
  --src-path ./bin/Release/net8.0/publish \
  --type zip
```

### 2. Azure Container Apps

#### Step 1: Create Container App with Managed Identity
```bash
# Create Container Apps environment
az containerapp env create \
  --resource-group myResourceGroup \
  --name my-containerapp-env \
  --location eastus

# Build and push container image
az acr build \
  --registry myregistry \
  --image azure-ai-agent:latest \
  --file Dockerfile .

# Create Container App with System-Assigned Identity
az containerapp create \
  --resource-group myResourceGroup \
  --name azure-ai-agent \
  --environment my-containerapp-env \
  --image myregistry.azurecr.io/azure-ai-agent:latest \
  --assign-system-identity
```

#### Step 2: Configure Environment Variables
```bash
# Set environment variables
az containerapp update \
  --resource-group myResourceGroup \
  --name azure-ai-agent \
  --set-env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    Azure__UseSDKExecutor=true \
    Azure__SubscriptionId="{subscription-id}" \
    AzureOpenAI__Endpoint="https://your-openai.openai.azure.com/" \
    AzureOpenAI__DeploymentName="gpt-4"
```

### 3. Azure Functions

#### Step 1: Create Function App with Managed Identity
```bash
# Create Function App
az functionapp create \
  --resource-group myResourceGroup \
  --consumption-plan-location eastus \
  --runtime dotnet \
  --runtime-version 8 \
  --functions-version 4 \
  --name my-azure-ai-function \
  --storage-account mystorageaccount \
  --assign-identity
```

#### Step 2: Configure Function App Settings
```bash
# Configure app settings
az functionapp config appsettings set \
  --resource-group myResourceGroup \
  --name my-azure-ai-function \
  --settings \
    FUNCTIONS_WORKER_RUNTIME=dotnet \
    Azure__UseSDKExecutor=true \
    Azure__SubscriptionId="{subscription-id}" \
    AzureOpenAI__Endpoint="https://your-openai.openai.azure.com/"
```

### 4. Azure Kubernetes Service (AKS)

#### Step 1: Enable Workload Identity
```bash
# Create AKS cluster with Workload Identity
az aks create \
  --resource-group myResourceGroup \
  --name myAKSCluster \
  --enable-oidc-issuer \
  --enable-workload-identity \
  --generate-ssh-keys
```

#### Step 2: Create Workload Identity
```bash
# Create managed identity for workload
az identity create \
  --resource-group myResourceGroup \
  --name azure-ai-agent-identity

# Get identity details
USER_ASSIGNED_CLIENT_ID=$(az identity show \
  --resource-group myResourceGroup \
  --name azure-ai-agent-identity \
  --query 'clientId' -o tsv)

# Create Kubernetes service account
kubectl create serviceaccount azure-ai-agent-sa

# Establish federated identity credential
az identity federated-credential create \
  --name azure-ai-agent-federated \
  --identity-name azure-ai-agent-identity \
  --resource-group myResourceGroup \
  --issuer $(az aks show --resource-group myResourceGroup --name myAKSCluster --query "oidcIssuerProfile.issuerUrl" -o tsv) \
  --subject system:serviceaccount:default:azure-ai-agent-sa
```

#### Step 3: Deploy with Workload Identity
```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: azure-ai-agent
spec:
  replicas: 1
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
        image: myregistry.azurecr.io/azure-ai-agent:latest
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Azure__UseSDKExecutor
          value: "true"
        - name: Azure__SubscriptionId
          value: "{subscription-id}"
        - name: AZURE_CLIENT_ID
          value: "{user-assigned-client-id}"
        ports:
        - containerPort: 5050
```

## 🔐 Security Best Practices

### 1. Principle of Least Privilege
```bash
# Create custom role for AI Agent operations
az role definition create --role-definition '{
  "Name": "Azure AI Agent Operator",
  "Description": "Custom role for Azure AI Agent with minimal required permissions",
  "Actions": [
    "Microsoft.Resources/subscriptions/resourceGroups/read",
    "Microsoft.Resources/subscriptions/resourceGroups/write",
    "Microsoft.Storage/storageAccounts/read",
    "Microsoft.Storage/storageAccounts/write",
    "Microsoft.Compute/virtualMachines/read",
    "Microsoft.Web/sites/read",
    "Microsoft.Web/sites/write"
  ],
  "NotActions": [],
  "AssignableScopes": ["/subscriptions/{subscription-id}"]
}'
```

### 2. Network Security
```bash
# Restrict access to specific IP ranges
az webapp config access-restriction add \
  --resource-group myResourceGroup \
  --name my-azure-ai-agent \
  --rule-name "AllowOfficeNetwork" \
  --action Allow \
  --ip-address 203.0.113.0/24 \
  --priority 300
```

### 3. Key Vault Integration
```bash
# Store sensitive configuration in Key Vault
az keyvault create \
  --resource-group myResourceGroup \
  --name my-ai-agent-keyvault

# Grant access to managed identity
az keyvault set-policy \
  --name my-ai-agent-keyvault \
  --object-id $principalId \
  --secret-permissions get list
```

## 🔍 Monitoring and Troubleshooting

### Application Insights Integration
```bash
# Create Application Insights
az monitor app-insights component create \
  --app my-azure-ai-agent-insights \
  --location eastus \
  --resource-group myResourceGroup

# Configure App Service to use Application Insights
az webapp config appsettings set \
  --resource-group myResourceGroup \
  --name my-azure-ai-agent \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="{connection-string}"
```

### Common Issues and Solutions

1. **Authentication Failed**
   ```bash
   # Check managed identity assignment
   az webapp identity show --resource-group myResourceGroup --name my-azure-ai-agent
   
   # Verify role assignments
   az role assignment list --assignee $principalId
   ```

2. **Azure OpenAI Access Denied**
   ```bash
   # Assign Cognitive Services OpenAI User role
   az role assignment create \
     --assignee $principalId \
     --role "Cognitive Services OpenAI User" \
     --scope "/subscriptions/{sub-id}/resourceGroups/{rg}/providers/Microsoft.CognitiveServices/accounts/{openai}"
   ```

3. **Insufficient Permissions**
   ```bash
   # Check effective permissions
   az role assignment list \
     --assignee $principalId \
     --include-inherited \
     --include-groups
   ```

## 📋 Deployment Checklist

- [ ] Choose deployment target (App Service, Container Apps, Functions, AKS)
- [ ] Enable appropriate managed identity type
- [ ] Assign required Azure RBAC roles
- [ ] Configure environment-specific settings
- [ ] Set up Azure OpenAI access (if using)
- [ ] Configure Application Insights monitoring
- [ ] Set up network access restrictions
- [ ] Test authentication in staging environment
- [ ] Deploy to production
- [ ] Verify functionality with sample requests
- [ ] Set up alerts and monitoring
- [ ] Document runbook for operations team

## 🎯 Next Steps

1. **Choose Your Deployment Target**: Start with Azure App Service for simplest setup
2. **Run Setup Scripts**: Use the commands above for your chosen service
3. **Test Authentication**: Verify managed identity is working
4. **Deploy Application**: Push your code to Azure
5. **Validate Functionality**: Test with sample Azure operations

The Azure AI Agent will automatically use the appropriate authentication method based on the environment and configuration!
