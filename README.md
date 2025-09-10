# Azure AI Agent - C# Implementation

An intelligent AI agent built with .NET 8 and Semantic Kernel that helps create and manage Azure resources through natural language commands.

## Architecture

This solution uses a **hybrid approach** for maximum flexibility:

1. **Template-based Deployment**: Uses Bicep templates for complex infrastructure deployments
2. **Command-based Operations**: Executes Azure CLI commands for simple operations  
3. **SDK-based Management**: Uses Azure SDK for programmatic resource management

## Projects

- **AzureAIAgent.Core**: Core logic, interfaces, and models
- **AzureAIAgent.Console**: Console application for interactive testing
- **AzureAIAgent.Api**: Web API for REST-based interactions
- **AzureAIAgent.Plugins**: Semantic Kernel plugins for Azure operations
- **AzureAIAgent.Tests**: Unit tests

## Prerequisites

1. **.NET 8 SDK** - Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
2. **Azure CLI** - Install from [https://docs.microsoft.com/cli/azure/install-azure-cli](https://docs.microsoft.com/cli/azure/install-azure-cli)
3. **AI Service** - Either:
   - OpenAI API key from [https://platform.openai.com](https://platform.openai.com)
   - Azure OpenAI Service endpoint and key

## Setup

### 1. Clone and Build

```bash
git clone <repository-url>
cd infraAgent.NET
dotnet restore
dotnet build
```

### 2. Configure AI Service

Choose one of the following options:

#### Option A: OpenAI
Set environment variable:
```bash
export OPENAI_API_KEY="your-openai-api-key"
```

#### Option B: Azure OpenAI
Update `AzureAIAgent.Console/appsettings.json`:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-azure-openai-key",
    "DeploymentName": "gpt-4"
  },
  "RateLimit": {
    "MaxRetries": 3,
    "BaseDelaySeconds": 60,
    "UseExponentialBackoff": true
  }
}
```

### 3. Rate Limiting Configuration (Optional)

The application includes intelligent rate limiting for Azure OpenAI API calls. Configure retry behavior in `appsettings.json`:

- **MaxRetries**: Number of retry attempts (default: 3)
- **BaseDelaySeconds**: Initial delay between retries (default: 60)
- **UseExponentialBackoff**: Whether to use exponential backoff (default: true)

💡 **For S0 (Free) Tier Users**: The default settings are optimized for free tier rate limits. The system will automatically retry with appropriate delays when hitting rate limits.

### 4. Deployment Timeout Configuration (Optional)

Configure timeout handling for long-running deployments:

```json
{
  "Deployment": {
    "TimeoutMinutes": 30,
    "PollingIntervalSeconds": 30,
    "EnableAsyncMode": true,
    "ShowProgressUpdates": true
  }
}
```

- **TimeoutMinutes**: Maximum time to wait for deployments (default: 30)
- **EnableAsyncMode**: Use background deployments to prevent timeouts (default: true)
- **ShowProgressUpdates**: Show real-time progress during deployments (default: true)

### 5. Azure CLI Login

```bash
az login
az account set --subscription "your-subscription-id"
```

## Running the Console Application

```bash
cd AzureAIAgent.Console
dotnet run
```

## Example Commands

Once the console app is running, try these natural language commands:

- "Create a new resource group called 'my-app-rg' in East US"
- "Deploy a basic web app with App Service plan"
- "Create a storage account for my application data"
- "Show me my current Azure subscription"
- "Generate a Bicep template for a complete web application stack"

## Available Console Commands

- **Your natural language requests** - The AI will interpret and execute them
- `exit` or `quit` - Exit the application
- `clear` - Clear conversation history
- `history` - Show conversation history

## Features

### ✅ Core Capabilities
- Semantic Kernel integration for natural language processing
- In-memory session management for conversation context
- Azure CLI command execution
- Bicep template deployment framework
- Plugin architecture for extensibility

### 🚀 Enhanced Auto-Resolution Features
- **Automatic Error Resolution**: Detects and fixes common deployment issues transparently
- **Resource Import**: Automatically imports existing Azure resources to prevent recreation during retries
- **Version Validation**: Real-time validation of AKS versions and region compatibility
- **Region Auto-Selection**: Intelligent region switching based on resource availability
- **Partial Failure Recovery**: Handles partial deployment failures gracefully by importing successful resources
- **VM Size Compatibility**: Automatic detection and correction of VM size availability in regions
- **Silent Directory Creation**: Automatically creates required directories without user intervention
- **Rate Limiting & Retry Logic**: Automatic handling of Azure OpenAI rate limits with configurable backoff
- **Timeout Management**: Configurable timeouts for long-running deployments with progress monitoring
- **Async Deployments**: Background deployment support to prevent browser/API timeouts
- Console application for interactive testing
- **Intelligent Resource Management**: Smart VM deployment with interactive parameter collection
- **State Synchronization**: Terraform state and Azure resource drift detection
- **Resource Detection**: Checks both Azure and Terraform state before creating resources

### 🎯 Smart Infrastructure Management

The agent now includes advanced capabilities for intelligent resource management:

#### **Interactive VM Deployment**
When you say "deploy virtual machine", the agent will:
- Ask clarifying questions about VM requirements (size, OS, location, etc.)
- Check for existing resources that can be reused
- Generate optimized Terraform templates
- Avoid duplicating existing infrastructure

#### **State Synchronization** 
- **Check Terraform State Sync**: Use `check terraform state sync` to verify synchronization between Terraform state and Azure resources
- **Detect State Drift**: Identifies when Terraform thinks it manages resources that don't exist in Azure
- **Import Existing Resources**: Use `import existing resource` to bring Azure resources under Terraform management
- **Hybrid Resource Detection**: Checks both Azure CLI and Terraform state files for complete resource inventory

#### **🔄 Incremental Deployment Recovery**
- **Failed Deployment Analysis**: Use `analyze failed deployment` to understand what went wrong
- **Selective Recovery**: Use `recover failed deployment` to retry only failed components
- **State-Aware Recovery**: Imports successful resources to state, deploys only missing ones
- **Prevents Resource Duplication**: Won't recreate VNets, subnets, or other resources that already exist

#### **Resource Reuse Intelligence**
- Before creating new resources, checks if compatible resources already exist
- Uses Terraform data sources to reference existing resources instead of recreating them
- Prevents resource conflicts and reduces costs
- Maintains infrastructure consistency

#### **⏱️ Timeout & Async Deployment Management**
- **Background Deployments**: Use `apply terraform template async` for long-running deployments
- **Progress Monitoring**: Use `monitor deployment progress` to track deployment status
- **Deployment Status**: Use `check async deployment status` to check completion
- **Configurable Timeouts**: Deployments automatically timeout appropriately (30 min for apply/destroy, 5 min for other operations)
- **Real-time Progress**: See live deployment progress with timestamps
- **No Browser Timeouts**: Async mode prevents frontend timeout issues

### Example Commands

```bash
# Standard deployment (may timeout for large resources)
"apply the terraform template"

# Async deployment (recommended for AKS, VMs, etc.)
"apply terraform template async"
"start async deployment"

# Monitor progress
"monitor deployment progress"
"check deployment status my-deployment-id"

# Connection troubleshooting
"diagnose connection issues"
"test deployment connectivity"
```

# State synchronization check
"check terraform state sync" 
→ Shows which resources are in sync, which have drifted

# Import existing Azure resources
"import my existing storage account into terraform"
→ Generates import configuration and guides through the process

# Smart resource detection
"create a VM in my existing network"
→ Detects existing VNets and reuses them instead of creating new ones

# Deployment recovery scenarios
"analyze failed deployment"
→ Shows which resources succeeded/failed and provides recovery options

"recover failed deployment terraform-20250822-143052 skip-existing"
→ Imports successful VNet/subnet, deploys only the failed VM

# Selective deployment strategies
"recover failed deployment latest retry-failed"
→ Only retries the components that failed, skips successful ones
```

### 🚧 In Progress
- Functional Azure CLI command executor
- Bicep template generation from natural language
- Cost estimation integration
- Security validation

### 📋 Planned
- Web API for REST-based interactions
- Persistent state management with database
- Azure Cost Management integration
- Advanced security validation
- Template library and recommendations

## Development

### Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐
│  Console App    │    │    Web API      │
└────────┬────────┘    └────────┬────────┘
         │                      │
         └──────────┬───────────┘
                    │
         ┌─────────────────┐
         │   Core Logic    │
         │                 │
         │ ┌─────────────┐ │
         │ │ AI Agent    │ │
         │ └─────────────┘ │
         │ ┌─────────────┐ │
         │ │Session Mgmt │ │
         │ └─────────────┘ │
         │ ┌─────────────┐ │
         │ │Azure Ops    │ │
         │ └─────────────┘ │
         └─────────────────┘
                 │
    ┌─────────────────────────┐
    │   Semantic Kernel       │
    │   ┌─────────────────┐   │
    │   │ Azure Plugins   │   │
    │   └─────────────────┘   │
    └─────────────────────────┘
                 │
    ┌─────────────────────────┐
    │      Azure              │
    │ ┌─────┐ ┌─────┐ ┌─────┐ │
    │ │ CLI │ │ SDK │ │Bicep│ │
    │ └─────┘ └─────┘ └─────┘ │
    └─────────────────────────┘
```

### Key Components

1. **AzureAIAgent**: Main orchestrator that processes natural language and coordinates operations
2. **SessionManager**: Manages conversation state and context
3. **AzureCommandExecutor**: Executes Azure CLI commands safely
4. **BicepTemplateDeployer**: Handles Bicep template generation and deployment
5. **AzureResourcePlugin**: Semantic Kernel plugin exposing Azure operations

## Security Considerations

- All Azure CLI commands are validated before execution
- Dangerous operations require explicit confirmation
- Session isolation prevents cross-session data leakage
- Azure credentials are managed through Azure CLI/SDK authentication

## Troubleshooting

### "Azure CLI not found"
Ensure Azure CLI is installed and available in PATH.

### "Not logged into Azure"
Run `az login` to authenticate with Azure.

### "Rate limit exceeded" (HTTP 429)
The application includes automatic retry logic for rate limits. If you continue experiencing issues:

1. **Wait and Retry**: The system automatically waits 60 seconds before retrying
2. **Upgrade Your Tier**: Consider upgrading from S0 (free) to pay-as-you-go
3. **Adjust Rate Limiting**: Modify `RateLimit` settings in `appsettings.json`:
   ```json
   {
     "RateLimit": {
       "MaxRetries": 5,
       "BaseDelaySeconds": 120,
       "UseExponentialBackoff": true
     }
   }
   ```
4. **Check Quota**: Visit [Azure OpenAI Quota Management](https://aka.ms/oai/quotaincrease)

### "Failed to fetch" or Connection Errors

Use the built-in diagnostics to identify the issue:

```bash
# Run connection diagnostics
"diagnose connection issues"

# Test deployment connectivity
"test deployment connectivity in eastus"
```

**Common causes and solutions:**
1. **Azure CLI not logged in**: Run `az login`
2. **Network/Firewall issues**: Check corporate proxy settings
3. **Insufficient permissions**: Verify Azure subscription permissions
4. **Resource name conflicts**: Check for existing resources with same names
5. **Region unavailability**: Try a different Azure region
6. **Terraform state issues**: Clear temp directories and retry

**Quick fixes:**
```bash
# Check Azure authentication
az account show

# Clear Terraform cache
rm -rf %TEMP%/terraform-*

# Test basic connectivity
curl -I https://management.azure.com
```

### "OpenAI/Azure OpenAI configuration missing"
Set the OPENAI_API_KEY environment variable or configure Azure OpenAI in appsettings.json.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Implement your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License.
