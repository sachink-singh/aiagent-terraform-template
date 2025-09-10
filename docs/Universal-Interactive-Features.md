# Universal Interactive Azure Resource Management Guide

This guide explains how to use the enhanced Azure AI Agent with universal interactive capabilities for **all Azure resource types** including adaptive cards, parameter forms, and clickable actions.

## 🎯 **New Interactive Features**

### ✅ **Universal Resource Listing with Interactive Actions**
- 📁 **Resource Groups** - with actions for managing, tagging, deleting
- 🖥️ **Virtual Machines** - with start/stop/restart/connect actions  
- 💾 **Storage Accounts** - with container/key/metrics management
- 🌐 **Web Apps** - with restart/logs/scaling/deployment actions
- 🐳 **Kubernetes Pods** - with logs/metrics/exec/restart actions

### ✅ **Smart Parameter Input Forms**
- 📝 Dynamic parameter collection for any Azure operation
- ✅ Required/optional field validation
- 🔒 Secure input for passwords and secrets
- 💡 Smart defaults and placeholder text

### ✅ **Adaptive Card Integration**
- 🎮 Clickable resource cards with contextual actions
- 🔄 Interactive forms for parameter input
- 📊 Rich data display with status indicators
- 🚀 One-click action execution

## 🚀 **How to Use**

### 1. **List Resource Groups Interactively**

**User:** `"list resource groups"`

**Result:** Interactive card showing all resource groups with clickable actions:
```
📁 Azure Resource Groups

Found 5 resource groups. Click on any resource group for actions:

🔹 rg-production-web
   📍 Location: East US  
   🆔 ID: /subscriptions/.../rg-production-web
   
   💡 Actions: 📋 List Resources, 📊 View Metrics, 🏷️ Manage Tags, 🗑️ Delete RG

🔹 rg-development-api
   📍 Location: West US 2
   🆔 ID: /subscriptions/.../rg-development-api
   
   💡 Actions: 📋 List Resources, 📊 View Metrics, 🏷️ Manage Tags, 🗑️ Delete RG
```

### 2. **List Virtual Machines with Actions**

**User:** `"list virtual machines"`

**Result:** Interactive list with VM management actions:
```
🖥️ Virtual Machines (3 found)

🟢 vm-web-prod-01
   📍 Location: East US
   📦 Size: Standard_D4s_v3
   📁 Resource Group: rg-production-web
   ⚡ Status: VM running

   💡 Actions: ▶️ Start VM, ⏹️ Stop VM, 🔄 Restart VM, 📊 View Metrics, 🔗 Connect (RDP/SSH)

🔴 vm-dev-testing
   📍 Location: West US 2
   📦 Size: Standard_B2s
   📁 Resource Group: rg-development-api
   ⚡ Status: VM stopped

   💡 Actions: ▶️ Start VM, ⏹️ Stop VM, 🔄 Restart VM, 📊 View Metrics, 🔗 Connect (RDP/SSH)
```

### 3. **List Storage Accounts with Management Options**

**User:** `"list storage accounts"`

**Result:** Storage accounts with administrative actions:
```
💾 Storage Accounts (2 found)

💾 storageprodweb001
   📍 Location: East US
   📁 Resource Group: rg-production-web
   🏷️ Kind: StorageV2
   📊 Access Tier: Hot
   🔄 Replication: LRS

   💡 Actions: 📦 List Containers, 🔑 Access Keys, 📊 Storage Metrics, 🛡️ Security Settings
```

### 4. **List Web Apps with Deployment Actions**

**User:** `"list web apps"`

**Result:** Web applications with management capabilities:
```
🌐 Web Apps (2 found)

🟢 webapp-prod-api
   🔗 URL: https://webapp-prod-api.azurewebsites.net
   📍 Location: East US
   📁 Resource Group: rg-production-web
   🏷️ Kind: app
   ⚡ State: Running

   💡 Actions: 🔄 Restart App, 📋 View Logs, 📈 Scale App, 🚀 Deploy Code, 🌐 Open in Browser
```

### 5. **List Kubernetes Pods with Container Actions**

**User:** `"list pods from cluster aks-prod in resource group rg-production"`

**Result:** Kubernetes pods with container management:
```
🐳 Kubernetes Pods - aks-prod (8 found)

🟢 nginx-deployment-7d6b8c8c8d-4x2w9
   📂 Namespace: default
   ⚡ Phase: Running
   ✅ Ready: 1/1

   💡 Actions: 📋 View Logs, 📊 Pod Metrics, 🔄 Restart Pod, 🖥️ Exec into Pod, 📝 Describe Pod

🟡 api-service-845b7f8c9d-7k3m2
   📂 Namespace: production
   ⚡ Phase: Pending
   ✅ Ready: 0/1

   💡 Actions: 📋 View Logs, 📊 Pod Metrics, 🔄 Restart Pod, 🖥️ Exec into Pod, 📝 Describe Pod
```

### 6. **Generate Parameter Input Forms**

**User:** `"create a parameter form for VM deployment"`

**Result:** Interactive form for collecting parameters:
```
📝 VM Deployment Parameters

Please provide the following parameters:

🔹 Virtual Machine Name (Required)
   The name for your new virtual machine

🔹 VM Size (Required)
   The Azure VM size (Default: Standard_D2s_v3)

🔹 Resource Group (Required)
   Target resource group for deployment

🔹 Location (Required)
   Azure region for the VM (Default: East US)

🔹 Admin Username (Required)
   Administrator username for the VM

🔹 Admin Password (Required)
   Administrator password (secure input)

💡 Submit this form to execute the action.
```

## 🛠 **Available Kernel Functions**

### Resource Listing Functions
- `ListResourceGroupsInteractive()` - Interactive resource group listing
- `ListVirtualMachines(resourceGroupName?)` - VM listing with actions
- `ListStorageAccounts(resourceGroupName?)` - Storage account management  
- `ListWebApps(resourceGroupName?)` - Web app management
- `ListKubernetesPods(clusterName, resourceGroupName, namespace?)` - Pod management

### Interactive Form Generation
- `GenerateParameterForm(title, actionName, parametersJson)` - Dynamic parameter forms

### Adaptive Card Generation (AdaptiveCardService)
- `GenerateResourceGroupListCard(resourceGroups)` - Resource group cards
- `GenerateVirtualMachineListCard(vms)` - VM management cards
- `GenerateStorageAccountListCard(storageAccounts)` - Storage cards
- `GenerateWebAppListCard(webApps)` - Web app cards
- `GenerateKubernetesResourceCard(pods, clusterName)` - Kubernetes cards
- `GenerateParameterInputCard(title, actionName, parameters)` - Input forms

## 💡 **Usage Examples**

### Basic Resource Discovery
```
User: "show me all my resource groups"
User: "list virtual machines in rg-production"
User: "what web apps do I have?"
User: "show storage accounts"
```

### Interactive Management  
```
User: "list VMs with actions"
User: "show pods from my AKS cluster with management options"
User: "display storage accounts with administrative actions"
```

### Action Execution
```
User: "restart the VM named vm-web-01"
User: "show logs for pod nginx-deployment-xyz"
User: "scale up the web app api-service"
User: "get access keys for storage account mystorage"
```

### Parameter Collection
```
User: "create a form for VM deployment parameters"
User: "generate input form for AKS cluster creation"
User: "show parameter form for storage account setup"
```

## 🔧 **Technical Implementation**

### Models and Data Structures
The system uses shared models defined in `AzureAIAgent.Core.Models.TemplateModels.cs`:

```csharp
public class ParameterDefinition
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string? Placeholder { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsSecret { get; set; }
    public string Description { get; set; }
}
```

### Azure CLI Integration
All resource listings use the existing `ExecuteAzureCommand()` method for secure Azure CLI execution:

```csharp
var command = "group list --query '[].{name:name, location:location}' --output json";
var output = await ExecuteAzureCommand(command);
```

### JSON Response Parsing
Smart JSON parsing with null-safe property access:

```csharp
var powerState = vm.TryGetProperty("powerState", out JsonElement powerProp) 
    ? powerProp.GetString() : "Unknown";
```

## 🎨 **Adaptive Card Features**

### Interactive Elements
- **Clickable containers** with `selectAction` for resource selection
- **Action sets** with contextual operations per resource type
- **Form inputs** with validation and smart defaults
- **Status indicators** with color-coded icons (🟢🟡🔴)

### Visual Design
- **Consistent layout** with column sets and proper spacing
- **Icon usage** for easy recognition and visual appeal
- **Color coding** for status and importance levels
- **Responsive design** that works across different clients

### Action Types
- `Action.Submit` - Execute operations with data payload
- `Action.ShowCard` - Expand inline cards for detailed actions  
- `Action.OpenUrl` - Direct links to Azure portal or web apps

## 🔐 **Security Considerations**

### Secure Parameter Handling
- Password fields use `style: "Password"` for secure input
- Sensitive data marked with `IsSecret: true`
- No parameter values logged or exposed in responses

### Azure Authentication
- Relies on existing Azure CLI authentication
- No credential storage or caching
- Respects Azure RBAC and subscription access

### Action Validation
- All actions validate resource existence before execution
- Resource group and subscription scope validation
- Error handling for insufficient permissions

## 🚀 **Future Enhancements**

### Planned Features
- **Real-time status updates** for long-running operations
- **Bulk operations** for multiple resource management
- **Custom action definitions** via configuration
- **Enhanced filtering and search** capabilities
- **Resource dependency visualization**

### Integration Opportunities
- **Microsoft Teams** adaptive card support
- **Power Platform** integration for workflow automation
- **Azure Monitor** integration for real-time metrics
- **GitHub Actions** for automated deployment workflows

This universal interactive system transforms Azure resource management from command-line operations into an intuitive, clickable experience! 🎉
