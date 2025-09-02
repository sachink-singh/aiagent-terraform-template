using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using AzureAIAgent.Core.Models;

namespace AzureAIAgent.Core.Services;

public class GitHubTemplateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubTemplateService> _logger;
    private readonly IConfiguration? _configuration;
    private readonly string _repositoryOwner;
    private readonly string _repositoryName;
    private readonly string _branch;
    private readonly string? _accessToken;
    private readonly string _rawContentUrl;

    public GitHubTemplateService(HttpClient httpClient, ILogger<GitHubTemplateService> logger, IConfiguration? configuration = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        
        // Load configuration
        _repositoryOwner = _configuration?["GitHubTemplate:RepositoryOwner"] ?? "YOUR-ORG";
        _repositoryName = _configuration?["GitHubTemplate:RepositoryName"] ?? "terraform-templates";
        _branch = _configuration?["GitHubTemplate:Branch"] ?? "main";
        _accessToken = _configuration?["GitHubTemplate:AccessToken"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        _rawContentUrl = _configuration?["GitHubTemplate:RawContentUrl"] ?? "https://raw.githubusercontent.com";
        
        // Configure authentication for private repositories
        if (!string.IsNullOrEmpty(_accessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            _logger.LogInformation("GitHub authentication configured for private repository access");
        }
        
        _logger.LogInformation("GitHubTemplateService configured for repository: {Owner}/{Repo}", _repositoryOwner, _repositoryName);
    }

    // Template definitions with metadata - Updated for Organization Repository
    private readonly Dictionary<string, TemplateMetadata> _templates = new()
    {
        // Example: Your organization's VM template with compliance policies
        ["org-vm-standard"] = new TemplateMetadata
        {
            Id = "org-vm-standard",
            Name = "Organization Standard VM",
            Description = "Enterprise VM with security, monitoring, and compliance policies",
            Category = "compute",
            GitHubUrl = "https://raw.githubusercontent.com/YOUR-ORG/terraform-templates/main/compute/standard-vm/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "vm_name", Type = "string", Description = "Virtual machine name", Required = true },
                new TemplateParameter { Name = "vm_size", Type = "string", Description = "VM size", Required = true, Default = "Standard_D2s_v3" },
                new TemplateParameter { Name = "business_unit", Type = "string", Description = "Business unit for cost allocation", Required = true },
                new TemplateParameter { Name = "environment", Type = "string", Description = "Environment (dev/test/prod)", Required = true },
                new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true, Default = "East US" }
            }
        },
        // Example: Your organization's AKS template with enterprise policies
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
                new TemplateParameter { Name = "node_pool_size", Type = "string", Description = "Node pool VM size", Required = true, Default = "Standard_D4s_v3" },
                new TemplateParameter { Name = "business_unit", Type = "string", Description = "Business unit for cost allocation", Required = true },
                new TemplateParameter { Name = "environment", Type = "string", Description = "Environment (dev/test/prod)", Required = true },
                new TemplateParameter { Name = "rbac_enabled", Type = "bool", Description = "Enable RBAC", Required = false, Default = "true" }
            }
        },
        // Example: Your organization's web app template with security policies
        ["org-webapp-secure"] = new TemplateMetadata
        {
            Id = "org-webapp-secure",
            Name = "Organization Secure Web App",
            Description = "Enterprise web app with WAF, SSL, and monitoring",
            Category = "web",
            GitHubUrl = "https://raw.githubusercontent.com/YOUR-ORG/terraform-templates/main/web/secure-webapp/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "app_name", Type = "string", Description = "Web app name", Required = true },
                new TemplateParameter { Name = "business_unit", Type = "string", Description = "Business unit for cost allocation", Required = true },
                new TemplateParameter { Name = "environment", Type = "string", Description = "Environment (dev/test/prod)", Required = true },
                new TemplateParameter { Name = "ssl_cert_name", Type = "string", Description = "SSL certificate name", Required = false }
            }
        },
        // You can keep existing Azure templates as fallback or remove them
        ["aks-cluster"] = new TemplateMetadata
        {
            Id = "aks-cluster",
            Name = "AKS Kubernetes Cluster",
            Description = "Managed Kubernetes cluster with auto-scaling",
            Category = "containers",
            GitHubUrl = "https://raw.githubusercontent.com/sachink-singh/aiagent-terraform-template/main/terraform-templates/aks-cluster/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "workload_name", Type = "string", Description = "Name of the workload", Required = true },
                new TemplateParameter { Name = "project_name", Type = "string", Description = "Name of the project", Required = true },
                new TemplateParameter { Name = "owner", Type = "string", Description = "Owner of the resources", Required = true },
                new TemplateParameter { Name = "environment", Type = "string", Description = "Environment (dev, test, staging, prod)", Required = false, Default = "dev" },
                new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = false, Default = "East US" },
                new TemplateParameter { Name = "node_count", Type = "number", Description = "Number of nodes in the default node pool", Required = false, Default = "3" },
                new TemplateParameter { Name = "vm_size", Type = "string", Description = "Size of the Virtual Machine", Required = false, Default = "Standard_DS2_v2" },
                new TemplateParameter { Name = "enable_autoscaling", Type = "bool", Description = "Enable autoscaling", Required = false, Default = "true" },
                new TemplateParameter { Name = "enable_rbac", Type = "bool", Description = "Enable RBAC", Required = false, Default = "true" },
                new TemplateParameter { Name = "network_policy", Type = "string", Description = "Network policy to use (azure, calico, or none)", Required = false, Default = "azure" },
                new TemplateParameter { Name = "kubernetes_version", Type = "string", Description = "Kubernetes version", Required = false, Default = "1.28" }
            }
        },
        ["webapp-basic"] = new TemplateMetadata
        {
            Id = "webapp-basic",
            Name = "Web App Service",
            Description = "App Service with custom domain support",
            Category = "web",
            GitHubUrl = "https://raw.githubusercontent.com/Azure/terraform-azurerm-appservice/main/examples/app-service/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "app_name", Type = "string", Description = "Web app name", Required = true },
                new TemplateParameter { Name = "app_service_plan_tier", Type = "string", Description = "App Service plan tier", Required = true, Default = "Standard" },
                new TemplateParameter { Name = "app_service_plan_size", Type = "string", Description = "App Service plan size", Required = true, Default = "S1" },
                new TemplateParameter { Name = "runtime_stack", Type = "string", Description = "Runtime stack (dotnet/python/node)", Required = true, Default = "dotnet" },
                new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true, Default = "East US" }
            }
        },
        ["storage-account"] = new TemplateMetadata
        {
            Id = "storage-account",
            Name = "Storage Account",
            Description = "General-purpose storage with blob containers",
            Category = "storage",
            GitHubUrl = "https://raw.githubusercontent.com/Azure/terraform-azurerm-storage/main/examples/storage-account/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "storage_account_name", Type = "string", Description = "Storage account name", Required = true },
                new TemplateParameter { Name = "account_tier", Type = "string", Description = "Performance tier (Standard/Premium)", Required = true, Default = "Standard" },
                new TemplateParameter { Name = "replication_type", Type = "string", Description = "Replication type (LRS/GRS/ZRS)", Required = true, Default = "LRS" },
                new TemplateParameter { Name = "access_tier", Type = "string", Description = "Access tier (Hot/Cool)", Required = false, Default = "Hot" },
                new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true, Default = "East US" }
            }
        },
        ["sql-database"] = new TemplateMetadata
        {
            Id = "sql-database",
            Name = "SQL Database",
            Description = "Azure SQL Database with server and firewall rules",
            Category = "database",
            GitHubUrl = "https://raw.githubusercontent.com/Azure/terraform-azurerm-database/main/examples/sql-database/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "server_name", Type = "string", Description = "SQL Server name", Required = true },
                new TemplateParameter { Name = "database_name", Type = "string", Description = "Database name", Required = true },
                new TemplateParameter { Name = "admin_login", Type = "string", Description = "Administrator login", Required = true },
                new TemplateParameter { Name = "admin_password", Type = "string", Description = "Administrator password", Required = true, Sensitive = true },
                new TemplateParameter { Name = "sku_name", Type = "string", Description = "Database SKU", Required = true, Default = "S0" },
                new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true, Default = "East US" }
            }
        }
    };

    public List<TemplateMetadata> GetAvailableTemplates() => _templates.Values.ToList();

    /// <summary>
    /// Builds a GitHub raw content URL for your organization's repository
    /// </summary>
    private string BuildOrganizationTemplateUrl(string templatePath)
    {
        return $"{_rawContentUrl}/{_repositoryOwner}/{_repositoryName}/{_branch}/{templatePath}";
    }

    /// <summary>
    /// Discovers and loads templates from your organization's repository structure
    /// Call this method to auto-discover templates from your repo
    /// </summary>
    public async Task<List<TemplateMetadata>> DiscoverOrganizationTemplatesAsync()
    {
        var discoveredTemplates = new List<TemplateMetadata>();
        
        try
        {
            // Common paths where templates might be stored in enterprise repos
            var templatePaths = new[]
            {
                "compute/vm-standard/main.tf",
                "compute/vm-secure/main.tf", 
                "containers/aks-enterprise/main.tf",
                "containers/aks-dev/main.tf",
                "web/webapp-secure/main.tf",
                "web/webapp-standard/main.tf",
                "storage/storage-secure/main.tf",
                "database/sql-enterprise/main.tf",
                "networking/vnet-hub-spoke/main.tf",
                "security/keyvault-enterprise/main.tf"
            };
            
            foreach (var path in templatePaths)
            {
                var url = BuildOrganizationTemplateUrl(path);
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var templateId = Path.GetDirectoryName(path)?.Replace("/", "-") ?? "unknown";
                    var category = path.Split('/')[0];
                    
                    discoveredTemplates.Add(new TemplateMetadata
                    {
                        Id = templateId,
                        Name = $"Organization {System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(templateId.Replace("-", " "))}",
                        Description = $"Enterprise template from {path}",
                        Category = category,
                        GitHubUrl = url,
                        Parameters = ExtractParametersFromTemplate(await response.Content.ReadAsStringAsync())
                    });
                    
                    _logger.LogInformation("Discovered template: {TemplateId} at {Path}", templateId, path);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering organization templates");
        }
        
        return discoveredTemplates;
    }

    private TemplateParameter[] ExtractParametersFromTemplate(string templateContent)
    {
        // Basic parameter extraction from Terraform files
        // You can enhance this to parse actual variable blocks
        var parameters = new List<TemplateParameter>();
        
        // Add common organization parameters
        parameters.AddRange(new[]
        {
            new TemplateParameter { Name = "business_unit", Type = "string", Description = "Business unit for cost allocation", Required = true },
            new TemplateParameter { Name = "environment", Type = "string", Description = "Environment (dev/test/prod)", Required = true },
            new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true, Default = "East US" },
            new TemplateParameter { Name = "project_name", Type = "string", Description = "Project name for resource naming", Required = true }
        });
        
        return parameters.ToArray();
    }

    public IEnumerable<TemplateMetadata> GetTemplatesByCategory(string category) =>
        _templates.Values.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

    public TemplateMetadata? GetTemplate(string templateId) =>
        _templates.TryGetValue(templateId, out var template) ? template : null;

    public async Task<string?> DownloadTemplateAsync(string templateId)
    {
        try
        {
            if (!_templates.TryGetValue(templateId, out var template))
            {
                _logger.LogWarning("Template {TemplateId} not found", templateId);
                return null;
            }

            _logger.LogInformation("Downloading template {TemplateId} from {Url}", templateId, template.GitHubUrl);
            
            var response = await _httpClient.GetAsync(template.GitHubUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully downloaded template {TemplateId}", templateId);
                return content;
            }
            else
            {
                _logger.LogError("Failed to download template {TemplateId}: {StatusCode}", templateId, response.StatusCode);
                return GetFallbackTemplate(templateId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading template {TemplateId}", templateId);
            return GetFallbackTemplate(templateId);
        }
    }

    private string GetFallbackTemplate(string templateId)
    {
        _logger.LogInformation("Using fallback template for {TemplateId}", templateId);
        
        return templateId switch
        {
            "vm-basic" => GenerateVMTemplate(),
            "aks-cluster" => GenerateAKSTemplate(),
            "webapp-basic" => GenerateWebAppTemplate(),
            "storage-account" => GenerateStorageTemplate(),
            "sql-database" => GenerateSQLTemplate(),
            _ => GenerateBasicTemplate()
        };
    }

    private string GenerateVMTemplate() => """
terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.environment}-${var.vm_name}"
  location = var.location
}

resource "azurerm_virtual_network" "main" {
  name                = "vnet-${var.environment}-${var.vm_name}"
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_subnet" "internal" {
  name                 = "internal"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.2.0/24"]
}

resource "azurerm_linux_virtual_machine" "main" {
  count               = var.os_type == "linux" ? 1 : 0
  name                = "vm-${var.environment}-${var.vm_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  size                = var.vm_size
  admin_username      = var.admin_username

  network_interface_ids = [
    azurerm_network_interface.main.id,
  ]

  admin_ssh_key {
    username   = var.admin_username
    public_key = file("~/.ssh/id_rsa.pub")
  }

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = "Premium_LRS"
  }

  source_image_reference {
    publisher = "Canonical"
    offer     = "0001-com-ubuntu-server-focal"
    sku       = "20_04-lts-gen2"
    version   = "latest"
  }
}

variable "vm_name" {
  description = "Virtual machine name"
  type        = string
}

variable "vm_size" {
  description = "VM size"
  type        = string
  default     = "Standard_D2s_v3"
}

variable "admin_username" {
  description = "Admin username"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "environment" {
  description = "Environment"
  type        = string
  default     = "dev"
}

variable "os_type" {
  description = "Operating system type"
  type        = string
  default     = "linux"
}
""";

    private string GenerateAKSTemplate() => """
terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.environment}-${var.cluster_name}"
  location = var.location
}

resource "azurerm_kubernetes_cluster" "main" {
  name                = "aks-${var.environment}-${var.cluster_name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = "aks-${var.environment}-${var.cluster_name}"

  default_node_pool {
    name       = "default"
    node_count = var.node_count
    vm_size    = var.node_size
  }

  identity {
    type = "SystemAssigned"
  }
}

variable "cluster_name" {
  description = "AKS cluster name"
  type        = string
}

variable "node_count" {
  description = "Initial node count"
  type        = number
  default     = 3
}

variable "node_size" {
  description = "Node VM size"
  type        = string
  default     = "Standard_D2s_v3"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "environment" {
  description = "Environment"
  type        = string
  default     = "dev"
}

output "kube_config" {
  value = azurerm_kubernetes_cluster.main.kube_config_raw
  sensitive = true
}
""";

    private string GenerateWebAppTemplate() => """
terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.environment}-${var.app_name}"
  location = var.location
}

resource "azurerm_service_plan" "main" {
  name                = "asp-${var.environment}-${var.app_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku_name            = "${var.app_service_plan_tier}${var.app_service_plan_size}"
  os_type             = "Linux"
}

resource "azurerm_linux_web_app" "main" {
  name                = "app-${var.environment}-${var.app_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_service_plan.main.location
  service_plan_id     = azurerm_service_plan.main.id

  site_config {
    application_stack {
      dotnet_version = var.runtime_stack == "dotnet" ? "6.0" : null
      python_version = var.runtime_stack == "python" ? "3.9" : null
      node_version   = var.runtime_stack == "node" ? "16-lts" : null
    }
  }
}

variable "app_name" {
  description = "Web app name"
  type        = string
}

variable "app_service_plan_tier" {
  description = "App Service plan tier"
  type        = string
  default     = "P"
}

variable "app_service_plan_size" {
  description = "App Service plan size"
  type        = string
  default     = "1v2"
}

variable "runtime_stack" {
  description = "Runtime stack"
  type        = string
  default     = "dotnet"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "environment" {
  description = "Environment"
  type        = string
  default     = "dev"
}
""";

    private string GenerateStorageTemplate() => """
terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.environment}-storage"
  location = var.location
}

resource "azurerm_storage_account" "main" {
  name                     = replace(lower("st${var.environment}${var.storage_account_name}"), "-", "")
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = var.account_tier
  account_replication_type = var.replication_type
  access_tier              = var.access_tier
}

variable "storage_account_name" {
  description = "Storage account name"
  type        = string
}

variable "account_tier" {
  description = "Performance tier"
  type        = string
  default     = "Standard"
}

variable "replication_type" {
  description = "Replication type"
  type        = string
  default     = "LRS"
}

variable "access_tier" {
  description = "Access tier"
  type        = string
  default     = "Hot"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "environment" {
  description = "Environment"
  type        = string
  default     = "dev"
}
""";

    private string GenerateSQLTemplate() => """
terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${var.environment}-sql"
  location = var.location
}

resource "azurerm_mssql_server" "main" {
  name                         = "sql-${var.environment}-${var.server_name}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.admin_login
  administrator_login_password = var.admin_password
}

resource "azurerm_mssql_database" "main" {
  name           = var.database_name
  server_id      = azurerm_mssql_server.main.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = "LicenseIncluded"
  sku_name       = var.sku_name
}

variable "server_name" {
  description = "SQL Server name"
  type        = string
}

variable "database_name" {
  description = "Database name"
  type        = string
}

variable "admin_login" {
  description = "Administrator login"
  type        = string
}

variable "admin_password" {
  description = "Administrator password"
  type        = string
  sensitive   = true
}

variable "sku_name" {
  description = "Database SKU"
  type        = string
  default     = "S0"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "environment" {
  description = "Environment"
  type        = string
  default     = "dev"
}
""";

    private string GenerateBasicTemplate() => """
# Basic Terraform template
terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "main" {
  name     = "rg-example"
  location = "East US"
}
""";
}
