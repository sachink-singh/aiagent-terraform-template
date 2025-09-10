# Organization GitHub Template Configuration Guide

This guide explains how to configure the Azure AI Agent to use your organization's private GitHub repository with enterprise Terraform templates that include organizational policies and compliance requirements.

## 🏢 Overview

The Azure AI Agent can be configured to use your organization's private GitHub repository containing:
- ✅ **Compliance-ready Terraform templates**
- ✅ **Organization security policies**
- ✅ **Cost allocation tags**
- ✅ **Network security configurations**
- ✅ **Enterprise naming conventions**

## 🔧 Configuration Steps

### 1. Repository Structure

Organize your organization's repository with this recommended structure:

```
terraform-templates/
├── compute/
│   ├── vm-standard/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   └── vm-secure/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
├── containers/
│   ├── aks-enterprise/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   └── aks-dev/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
├── web/
│   ├── webapp-secure/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   └── webapp-standard/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
├── storage/
│   └── storage-secure/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
├── database/
│   └── sql-enterprise/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
├── networking/
│   └── vnet-hub-spoke/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
└── security/
    └── keyvault-enterprise/
        ├── main.tf
        ├── variables.tf
        └── outputs.tf
```

### 2. Application Configuration

Update your `appsettings.json` files with your organization's details:

#### Console Application (`AzureAIAgent.Console/appsettings.json`):

```json
{
  "GitHubTemplate": {
    "RepositoryOwner": "YOUR-ORGANIZATION",
    "RepositoryName": "terraform-templates",
    "Branch": "main",
    "AccessToken": "",
    "BaseUrl": "https://api.github.com",
    "RawContentUrl": "https://raw.githubusercontent.com"
  }
}
```

#### API Application (`AzureAIAgent.Api/appsettings.json`):

```json
{
  "GitHubTemplate": {
    "RepositoryOwner": "YOUR-ORGANIZATION", 
    "RepositoryName": "terraform-templates",
    "Branch": "main",
    "AccessToken": "",
    "BaseUrl": "https://api.github.com",
    "RawContentUrl": "https://raw.githubusercontent.com"
  }
}
```

### 3. Authentication Setup

For **private repositories**, you need to set up authentication:

#### Option A: Environment Variable (Recommended)
```bash
export GITHUB_TOKEN="your-github-token-here"
```

#### Option B: Configuration File
```json
{
  "GitHubTemplate": {
    "AccessToken": "your-github-token-here"
  }
}
```

#### Creating a GitHub Token:
1. Go to GitHub → Settings → Developer settings → Personal access tokens
2. Generate new token (classic)
3. Select scopes: `repo` (for private repositories)
4. Copy the token and set it in your configuration

### 4. Template Metadata Configuration

Update `GitHubTemplateService.cs` with your organization's templates:

```csharp
// Template definitions for your organization
private readonly Dictionary<string, TemplateMetadata> _templates = new()
{
    ["org-vm-standard"] = new TemplateMetadata
    {
        Id = "org-vm-standard",
        Name = "Organization Standard VM",
        Description = "Enterprise VM with security, monitoring, and compliance policies",
        Category = "compute",
        GitHubUrl = "https://raw.githubusercontent.com/YOUR-ORG/terraform-templates/main/compute/vm-standard/main.tf",
        Parameters = new[]
        {
            new TemplateParameter { Name = "vm_name", Type = "string", Description = "Virtual machine name", Required = true },
            new TemplateParameter { Name = "business_unit", Type = "string", Description = "Business unit for cost allocation", Required = true },
            new TemplateParameter { Name = "environment", Type = "string", Description = "Environment (dev/test/prod)", Required = true },
            new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true }
        }
    },
    ["org-aks-enterprise"] = new TemplateMetadata
    {
        Id = "org-aks-enterprise", 
        Name = "Organization Enterprise AKS",
        Description = "Enterprise AKS cluster with RBAC, network policies, and monitoring",
        Category = "containers",
        GitHubUrl = "https://raw.githubusercontent.com/YOUR-ORG/terraform-templates/main/containers/aks-enterprise/main.tf",
        Parameters = new[]
        {
            new TemplateParameter { Name = "cluster_name", Type = "string", Description = "AKS cluster name", Required = true },
            new TemplateParameter { Name = "business_unit", Type = "string", Description = "Business unit for cost allocation", Required = true },
            new TemplateParameter { Name = "environment", Type = "string", Description = "Environment (dev/test/prod)", Required = true }
        }
    }
    // Add more of your organization's templates...
};
```

## 🎯 Organization Template Requirements

### Standard Parameters

All organization templates should include these standard parameters:

```terraform
variable "business_unit" {
  description = "Business unit for cost allocation and access control"
  type        = string
}

variable "environment" {
  description = "Environment (dev/test/prod)"
  type        = string
  validation {
    condition     = contains(["dev", "test", "prod"], var.environment)
    error_message = "Environment must be dev, test, or prod."
  }
}

variable "project_name" {
  description = "Project name for resource naming convention"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}
```

### Standard Tags

All resources should include organization-standard tags:

```terraform
locals {
  common_tags = {
    BusinessUnit = var.business_unit
    Environment  = var.environment
    Project      = var.project_name
    CreatedBy    = "AzureAIAgent"
    CreatedDate  = timestamp()
  }
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.project_name}-${var.environment}"
  location = var.location
  tags     = local.common_tags
}
```

### Security Policies

Example security configurations that should be included:

```terraform
# Network Security Group with organization policies
resource "azurerm_network_security_group" "main" {
  name                = "nsg-${var.project_name}-${var.environment}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  # Organization-required security rules
  security_rule {
    name                       = "DenyDirectInternetInbound"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Deny"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = "Internet"
    destination_address_prefix = "*"
  }

  tags = local.common_tags
}
```

## 🚀 Usage Examples

Once configured, users can request infrastructure using natural language:

```
User: "Create a secure web application for the marketing team in production"

Agent: I found your organization's secure web app template! This includes:
✅ WAF protection
✅ SSL/TLS encryption  
✅ Application Insights monitoring
✅ Organization security policies
✅ Proper cost allocation tags

Required parameters:
- Business Unit: marketing
- Environment: prod
- Project Name: [user provides]

Deploying from: YOUR-ORG/terraform-templates/web/webapp-secure/main.tf
```

## 🔍 Auto-Discovery Feature

The system can auto-discover templates from your repository:

```csharp
// Call this to discover all templates in your repository
var discoveredTemplates = await gitHubTemplateService.DiscoverOrganizationTemplatesAsync();
```

This will scan common paths and automatically add templates to the available catalog.

## ⚙️ Advanced Configuration

### Custom Repository Structure

If your repository uses a different structure, update the discovery paths:

```csharp
var templatePaths = new[]
{
    "azure/compute/standard-vm/main.tf",
    "azure/networking/hub-spoke/main.tf", 
    "azure/security/key-vault/main.tf"
    // Add your organization's specific paths
};
```

### Branch Strategy

Use different branches for different environments:

```json
{
  "GitHubTemplate": {
    "Branch": "production"  // or "development", "staging"
  }
}
```

### Multiple Repositories

For organizations with multiple template repositories, you can create multiple `GitHubTemplateService` instances or extend the service to support multiple repositories.

## 🛡️ Security Best Practices

1. **Use GitHub Tokens**: Never commit tokens to your repository
2. **Least Privilege**: Token should only have `repo` scope for template repositories
3. **Token Rotation**: Regularly rotate GitHub tokens
4. **Branch Protection**: Protect your main branch with required reviews
5. **Audit Logging**: Monitor template usage and modifications

## 📋 Checklist

- [ ] Repository structure organized by category
- [ ] Standard parameters defined in all templates
- [ ] Common tags implemented
- [ ] Security policies incorporated
- [ ] GitHub token configured
- [ ] appsettings.json updated with organization details
- [ ] Template metadata updated in GitHubTemplateService.cs
- [ ] Test deployment with organization templates

## 🤝 Support

For questions about configuring your organization's templates:

1. Check that your repository structure matches the expected paths
2. Verify GitHub token has appropriate permissions
3. Test template URLs are accessible via raw.githubusercontent.com
4. Review logs for authentication or download errors

Your organization's templates will now be the primary choice when users request infrastructure! 🎉
