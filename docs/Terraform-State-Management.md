# Terraform State Management for Azure AI Agent

## Overview
Terraform already handles infrastructure state management. We only need to add a thin orchestration layer for user session tracking and deployment management.

## What Terraform Handles (Built-in)
- ✅ **Infrastructure State**: `terraform.tfstate` tracks all resource states
- ✅ **State Locking**: Prevents concurrent modifications
- ✅ **Remote State**: Can store in Azure Storage, AWS S3, Terraform Cloud
- ✅ **Change Detection**: Knows what needs to be created/updated/destroyed
- ✅ **Dependency Management**: Handles resource dependencies automatically
- ✅ **Rollback Capability**: Can destroy or modify resources based on state

## What We Add (Minimal Orchestration Layer)

### 1. Session-to-Deployment Mapping
```json
{
  "sessionId": "session-123",
  "deployments": [
    {
      "deploymentId": "deploy-456",
      "terraformDir": "/tmp/terraform-20250819-143022",
      "templateName": "resource-group-with-storage",
      "status": "applied",
      "createdAt": "2025-08-19T14:30:22Z",
      "resources": ["azurerm_resource_group.main", "azurerm_storage_account.main"]
    }
  ]
}
```

### 2. Terraform State File Integration
Instead of creating our own state management, we read Terraform's state:

```bash
# Get current state from Terraform
terraform show -json > current-state.json

# List all resources in current state
terraform state list

# Get specific resource details
terraform state show azurerm_resource_group.main
```

## Implementation Strategy

### Leverage Terraform's Built-in State Management

```csharp
// Instead of creating complex state management, we use Terraform commands
public class TerraformStateManager
{
    public async Task<List<string>> GetDeployedResources(string deploymentDir)
    {
        // Let Terraform tell us what's deployed
        var result = await ExecuteTerraformCommand("state list", deploymentDir);
        return result.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
    
    public async Task<string> GetResourceDetails(string deploymentDir, string resourceName)
    {
        // Get detailed resource info from Terraform state
        return await ExecuteTerraformCommand($"state show {resourceName}", deploymentDir);
    }
    
    public async Task<bool> IsResourceDeployed(string deploymentDir, string resourceName)
    {
        var resources = await GetDeployedResources(deploymentDir);
        return resources.Contains(resourceName);
    }
}
```

### Use Remote State Backend (Production)

```hcl
# Configure remote state in main.tf
terraform {
  backend "azurerm" {
    resource_group_name  = "terraform-state-rg"
    storage_account_name = "terraformstatestorage"
    container_name       = "tfstate"
    key                  = "azure-ai-agent/terraform.tfstate"
  }
}
```

## Recommended Approach: Minimal State Layer

### 1. Session Context Only
```csharp
public class SessionDeploymentContext
{
    public string SessionId { get; set; }
    public List<DeploymentInfo> Deployments { get; set; } = new();
}

public class DeploymentInfo
{
    public string DeploymentId { get; set; }
    public string TerraformDirectory { get; set; }
    public string TemplateName { get; set; }
    public DeploymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Let Terraform handle the actual resource state
    public async Task<List<string>> GetCurrentResources()
    {
        // Delegate to Terraform
        return await ExecuteTerraformCommand("state list", TerraformDirectory);
    }
}
```

### 2. Leverage Terraform Workspaces
```bash
# Create isolated environments per session/user
terraform workspace new session-123
terraform workspace select session-123

# Each workspace has its own state
terraform apply
```

### 3. Use Terraform Output for Resource Info
```hcl
# In main.tf, define outputs
output "resource_summary" {
  value = {
    resource_group_name = azurerm_resource_group.main.name
    storage_account_name = azurerm_storage_account.main.name
    resources_created = [
      azurerm_resource_group.main.id,
      azurerm_storage_account.main.id
    ]
  }
}
```

```csharp
// Read Terraform outputs instead of maintaining separate state
public async Task<string> GetDeploymentSummary(string deploymentDir)
{
    return await ExecuteTerraformCommand("output -json", deploymentDir);
}
```

## Production Considerations

### 1. Remote State with Locking
```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "terraform-state-rg"
    storage_account_name = "terraformstate"
    container_name       = "tfstate"
    key                  = "deployments/${var.session_id}/terraform.tfstate"
    
    # Enable state locking
    use_azuread_auth = true
  }
}
```

### 2. State File Security
- Store state in Azure Storage with RBAC
- Enable encryption at rest
- Use Managed Identity for access
- Implement state file backup

### 3. Multi-User Isolation
```bash
# Use session-specific state files
terraform init -backend-config="key=sessions/${SESSION_ID}/terraform.tfstate"
```

## Benefits of This Approach

1. **✅ No Duplication**: Don't recreate what Terraform already does
2. **✅ Single Source of Truth**: Terraform state is authoritative
3. **✅ Built-in Features**: Get locking, remote state, change detection for free
4. **✅ Ecosystem Compatible**: Works with Terraform Cloud, Atlantis, etc.
5. **✅ Disaster Recovery**: Standard Terraform backup/restore procedures
6. **✅ Minimal Code**: Less code to maintain and debug

## What NOT to Build

❌ **Custom Resource State Database**: Terraform already has this  
❌ **Resource Dependency Tracking**: Terraform handles this  
❌ **Change Detection Logic**: Terraform plan/apply does this  
❌ **State Locking Mechanism**: Terraform backends provide this  
❌ **Resource Relationship Mapping**: Terraform graph handles this  

## Conclusion

**Stick with Terraform's state management and add only a thin session tracking layer.**

The AI agent should:
1. Generate Terraform templates
2. Execute Terraform commands
3. Track which deployments belong to which sessions
4. Let Terraform handle all the complex state management

This approach is simpler, more reliable, and leverages proven infrastructure tooling.
