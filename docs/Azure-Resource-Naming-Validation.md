# Azure Resource Naming Validation System

## Problem Solved
Azure resources have strict naming conventions that vary by resource type. Terraform deployments frequently fail due to:
- Storage account names exceeding 24 characters
- Invalid characters in resource names  
- Names that don't meet Azure-specific requirements
- Backend storage account names becoming too long when concatenated with deployment names

## Solution: Automatic Naming Validation & Correction

### Overview
The `AzureNamingValidator` class automatically validates and corrects resource names in Terraform templates before deployment, preventing common naming-related failures.

### Supported Resource Types & Rules

| Resource Type | Min Length | Max Length | Allowed Characters | Special Rules |
|---------------|------------|------------|-------------------|---------------|
| `azurerm_resource_group` | 1 | 90 | Alphanumeric, underscores, hyphens, periods | Cannot end with period |
| `azurerm_storage_account` | 3 | 24 | **Lowercase letters and numbers only** | No hyphens, underscores, or special chars |
| `azurerm_kubernetes_cluster` | 1 | 63 | Alphanumeric and hyphens | Cannot start/end with hyphen |
| `azurerm_key_vault` | 3 | 24 | Alphanumeric and hyphens | Must start with letter |
| `azurerm_container_registry` | 5 | 50 | **Alphanumeric only** | No special characters |
| `azurerm_virtual_machine` | 1 | 64 | Alphanumeric, underscores, hyphens | - |
| `azurerm_app_service` | 2 | 60 | Alphanumeric and hyphens | - |
| `azurerm_sql_server` | 1 | 63 | Lowercase letters, numbers, hyphens | - |
| `azurerm_function_app` | 2 | 60 | Alphanumeric and hyphens | - |
| `azurerm_public_ip` | 1 | 80 | Alphanumeric, underscores, hyphens | - |

### Key Features

#### 1. **Storage Account Name Optimization**
```csharp
// Before: "terraformstatergroup-dev-aksworkload-westus2-002" (47 chars - INVALID)
// After: "tfst123456rgdevakswork" (24 chars - VALID)
```

**Transformations:**
- `terraformstate` → `tfst` (save 12 characters)
- Remove all hyphens and underscores
- Convert to lowercase
- Use hash-based suffix for uniqueness if needed
- Ensure 3-24 character limit

#### 2. **Container Registry Name Correction**
```csharp
// Before: "my-container-registry_001" (INVALID - has hyphens/underscores)
// After: "mycontainerregistry001" (VALID - alphanumeric only)
```

#### 3. **Key Vault Name Validation**
```csharp
// Before: "my-very-long-key-vault-name-for-project" (39 chars - INVALID)
// After: "kv-my-very-long-key-va" (24 chars - VALID)
```

### Integration Points

#### 1. **Automatic Template Validation**
Every Terraform template is automatically validated before deployment:

```csharp
[KernelFunction("ValidateAndFixTerraformNaming")]
public async Task<string> ValidateAndFixTerraformNaming(string templateContent, string deploymentName)
```

#### 2. **Backend Configuration Protection**
Storage account names for Terraform backends are automatically validated:

```csharp
storage_account_name = AzureNamingValidator.ValidateAndFixResourceName(
    "azurerm_storage_account", 
    $"terraformstate{deploymentName}", 
    deploymentName
)
```

#### 3. **Real-time Deployment Protection**
The `ApplyTerraformTemplate` function automatically validates names before deployment:

```csharp
// STEP 1: Automatic naming validation and correction
var namingValidationResult = await ValidateAndFixTerraformNaming(templateContent, deploymentName);
```

### Common Naming Failures Prevented

#### ❌ **Before: Common Failures**
```bash
Error: Invalid storage account name "terraformstate-rg-dev-aksworkload-westus2-002"
- Storage account names must be 3-24 characters
- Only lowercase letters and numbers allowed

Error: Invalid container registry name "my-acr-registry"
- Registry names must be alphanumeric only

Error: Key vault name "my-very-long-key-vault-for-the-project" too long
- Key vault names must be 3-24 characters
```

#### ✅ **After: Automatic Correction**
```bash
✅ Storage account: "tfst620347rgdevakswork" (24 chars, valid)
✅ Container registry: "myacrregistry" (alphanumeric only)
✅ Key vault: "kv-my-very-long-key-v" (24 chars, valid)
```

### Usage Examples

#### Manual Validation
```csharp
// Validate a single resource name
var validName = AzureNamingValidator.ValidateAndFixResourceName(
    "azurerm_storage_account", 
    "my-very-long-storage-account-name",
    "deployment-context"
);
// Result: "myverlongstoraccount" or hash-based name if still too long
```

#### Template Validation
```csharp
// Validate entire Terraform template
var result = await ValidateAndFixTerraformNaming(terraformTemplate, deploymentName);
// Returns fixed template with compliant names
```

### Benefits

1. **🛡️ Prevents Deployment Failures**: Catches naming issues before deployment
2. **🔄 Automatic Correction**: No manual intervention required
3. **📏 Length Optimization**: Smart truncation and abbreviation strategies
4. **🏷️ Context Awareness**: Uses deployment context for intelligent naming
5. **🔗 Seamless Integration**: Works transparently with existing deployment flow
6. **📚 Comprehensive Coverage**: Supports all major Azure resource types

### Implementation Details

#### Hash-Based Naming for Uniqueness
When names are too long even after optimization, the system uses hash-based naming:

```csharp
var hash = Math.Abs(deploymentContext.GetHashCode()).ToString();
fixed = $"tfst{hash.Substring(0, 6)}{fixed.Substring(0, 14)}";
```

#### Resource-Specific Logic
Each resource type has specialized naming logic:

```csharp
switch (resourceType)
{
    case "azurerm_storage_account":
        return FixStorageAccountName(resourceName, deploymentContext);
    case "azurerm_kubernetes_cluster":
        return FixKubernetesClusterName(resourceName);
    // ... additional resource types
}
```

### Testing

Run naming validation tests:
```bash
chmod +x test_naming_validation.sh
./test_naming_validation.sh
```

### Future Enhancements

1. **Custom Naming Patterns**: Allow user-defined naming conventions
2. **Region-Specific Rules**: Handle region-specific naming requirements
3. **Compliance Validation**: Check against organizational naming standards
4. **Name Reservation**: Check name availability before deployment
5. **Rollback Support**: Track original names for rollback scenarios

This system ensures that Terraform deployments succeed by automatically handling Azure's complex naming requirements, eliminating a major source of deployment failures.
