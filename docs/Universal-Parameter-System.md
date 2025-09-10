# Universal Azure Resource Parameter System

## Overview

The Azure AI Agent now features a **completely universal parameter input system** that works for **ANY Azure resource type**. This system eliminates hardcoded forms and provides dynamic, intelligent parameter generation for all Azure services.

## 🌟 Key Features

### ✅ **Universal Design**
- **ANY Azure Resource**: Works with VMs, Storage, AKS, Web Apps, Key Vault, Cosmos DB, SQL, Redis, Service Bus, etc.
- **ANY Parameter Format**: Supports strings, numbers, booleans, choices, secrets, complex JSON objects
- **ANY Operation**: Create, update, delete, configure operations
- **Dynamic Generation**: No hardcoded forms - everything is generated dynamically

### ✅ **Intelligent Parameter Handling**
- **Type Support**: String, Number, Boolean, Choice (dropdown), Password/Secret
- **Validation**: Pattern matching, length limits, required/optional fields
- **Grouping**: Logical parameter groups (Basic, Security, Performance, etc.)
- **Ordering**: Smart parameter ordering for better UX
- **Defaults**: Intelligent default values
- **Advanced Options**: Advanced parameters marked separately

### ✅ **Enhanced User Experience**
- **Grouped Parameters**: Parameters organized by category
- **Progressive Disclosure**: Basic parameters first, advanced options marked
- **Smart Validation**: Real-time validation with helpful messages
- **Context-Aware**: Different parameters based on operation type

## 🎯 Supported Resource Types

| Resource Type | Code | Full Name |
|---------------|------|-----------|
| `aks` | AKS | Azure Kubernetes Service |
| `vm` | VM | Virtual Machine |
| `storage` | Storage | Storage Account |
| `webapp` | WebApp | Web App / App Service |
| `keyvault` | KeyVault | Key Vault |
| `cosmosdb` | CosmosDB | Cosmos DB |
| `sql` | SQL | SQL Database |
| `redis` | Redis | Azure Cache for Redis |
| `servicebus` | ServiceBus | Service Bus |
| `appgateway` | AppGateway | Application Gateway |
| `loadbalancer` | LoadBalancer | Load Balancer |
| `vnet` | VNet | Virtual Network |
| `resourcegroup` | ResourceGroup | Resource Group |
| `functionapp` | FunctionApp | Function App |
| `containerapp` | ContainerApp | Container App |
| `acr` | ACR | Container Registry |

## 🚀 Usage Examples

### Basic Usage
```
# Generate a form for any resource
create resource form aks
generate vm form
create storage form
```

### Advanced Usage
```
# Specify operation type
create resource form aks create
create resource form vm update
create resource form storage delete
```

### List Available Resources
```
list supported resource types
show available forms
```

## 📝 Parameter Definition Schema

Each parameter supports the following properties:

```csharp
public class ParameterDefinition
{
    public string Name { get; set; }                    // Internal parameter name
    public string DisplayName { get; set; }             // User-friendly display name
    public string? Placeholder { get; set; }            // Input placeholder text
    public bool Required { get; set; }                  // Is parameter required?
    public string? DefaultValue { get; set; }           // Default value
    public bool IsSecret { get; set; }                  // Is this a password/secret?
    public string Description { get; set; }             // Help text description
    public string? Type { get; set; }                   // Parameter type (string, number, choice, boolean)
    public string[]? AllowedValues { get; set; }        // Valid choices for choice type
    public string? ValidationPattern { get; set; }      // Regex validation pattern
    public string? ValidationMessage { get; set; }      // Custom validation message
    public int? MinLength { get; set; }                 // Minimum length
    public int? MaxLength { get; set; }                 // Maximum length
    public string? Group { get; set; }                  // Parameter group name
    public int Order { get; set; }                      // Display order
    public bool IsAdvanced { get; set; }                // Is advanced parameter?
}
```

## 🔧 Example: AKS Cluster Form

When you request `create resource form aks`, the system generates:

### Basic Configuration
- **Cluster Name** *(Required)* - Name for your AKS cluster (3-63 characters, alphanumeric and hyphens only)
- **Resource Group** *(Required)* - Name of the resource group to create the cluster in
- **Azure Region** *(Required)* - Azure region where the cluster will be deployed (Default: eastus)

### Node Configuration  
- **Initial Node Count** *(Required)* - Number of nodes in the default node pool (1-100) (Default: 3)
- **Node VM Size** *(Required)* - Size of the virtual machines for cluster nodes (Default: Standard_DS2_v2)

### Security Configuration
- **Enable RBAC** *(Optional)* - Enable Role-Based Access Control (Default: true)

### Monitoring & Add-ons
- **Enable Container Insights** *(Optional)* - Enable Azure Monitor Container Insights for monitoring (Default: true)

## 🔄 Extensibility

### Adding New Resource Types

To add support for a new Azure resource:

1. **Add to GetResourceParameterTemplate method**:
```csharp
"newresource" => GetNewResourceParameters(operation),
```

2. **Create parameter method**:
```csharp
private List<ParameterDefinition> GetNewResourceParameters(string operation)
{
    return new List<ParameterDefinition>
    {
        new() { Name = "resourceName", DisplayName = "Resource Name", Required = true, ... },
        // Add more parameters as needed
    };
}
```

3. **Add to supported types list** in `ListSupportedResourceTypes()`

### Custom Parameter Types

The system supports these parameter types:
- **string**: Text input
- **number**: Numeric input with validation
- **boolean**: True/false choice
- **choice**: Dropdown with predefined options
- **password**: Hidden text input for secrets

## 🎨 Form Generation Flow

1. **User Request**: `create resource form aks`
2. **Resource Detection**: System identifies "aks" as Azure Kubernetes Service
3. **Parameter Lookup**: Calls `GetAKSParameters("create")`
4. **Form Generation**: Creates organized form with validation
5. **User Interaction**: User fills out the form
6. **Validation**: Real-time validation with helpful messages
7. **Submission**: Form data processed for resource creation

## 🔒 Security Features

- **Secret Parameters**: Marked with `IsSecret = true` for password fields
- **Validation**: Pattern validation for security-sensitive fields
- **No Storage**: Sensitive data not stored in memory longer than necessary

## 📊 Benefits

### For Users
- **Consistent Experience**: Same UX across all Azure resources
- **Guided Input**: Smart defaults and validation prevent errors
- **Context Awareness**: Relevant parameters for each resource type
- **Progressive Disclosure**: Basic options first, advanced when needed

### For Developers
- **No Hardcoding**: Add new resources without code changes to forms
- **Maintainable**: Single system handles all resource types
- **Extensible**: Easy to add new parameter types and validation
- **Type Safe**: Strong typing prevents runtime errors

## 🚀 Future Enhancements

- **Dynamic Parameter Discovery**: Auto-discover parameters from Azure APIs
- **Conditional Parameters**: Show/hide parameters based on other selections
- **Parameter Dependencies**: Link related parameters
- **Custom Validation**: User-defined validation rules
- **Template Integration**: Integration with ARM/Bicep templates
- **Bulk Operations**: Handle multiple resources with single form

---

**The universal parameter system ensures that the Azure AI Agent can handle ANY Azure resource with a consistent, intelligent, and user-friendly interface!**
