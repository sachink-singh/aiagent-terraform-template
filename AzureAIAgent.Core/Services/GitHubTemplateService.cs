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
                new TemplateParameter { Name = "kubernetes_version", Type = "string", Description = "Kubernetes version", Required = false, Default = "1.31.10" }
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
        },
        ["keyvault-standard"] = new TemplateMetadata
        {
            Id = "keyvault-standard",
            Name = "Azure Key Vault",
            Description = "Azure Key Vault with RBAC, access policies, and monitoring",
            Category = "security",
            GitHubUrl = "https://raw.githubusercontent.com/sachink-singh/aiagent-terraform-template/main/terraform-templates/keyvault/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "keyvault_name", Type = "string", Description = "Key Vault name (must be globally unique)", Required = true },
                new TemplateParameter { Name = "resource_group_name", Type = "string", Description = "Resource group name", Required = true },
                new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true, Default = "East US" },
                new TemplateParameter { Name = "sku_name", Type = "string", Description = "Key Vault SKU (standard/premium)", Required = false, Default = "standard" },
                new TemplateParameter { Name = "enable_rbac_authorization", Type = "bool", Description = "Enable RBAC authorization", Required = false, Default = "true" },
                new TemplateParameter { Name = "enabled_for_disk_encryption", Type = "bool", Description = "Enable for disk encryption", Required = false, Default = "true" },
                new TemplateParameter { Name = "enabled_for_deployment", Type = "bool", Description = "Enable for VM deployment", Required = false, Default = "true" },
                new TemplateParameter { Name = "enabled_for_template_deployment", Type = "bool", Description = "Enable for template deployment", Required = false, Default = "true" },
                new TemplateParameter { Name = "soft_delete_retention_days", Type = "number", Description = "Soft delete retention days", Required = false, Default = "7" },
                new TemplateParameter { Name = "purge_protection_enabled", Type = "bool", Description = "Enable purge protection", Required = false, Default = "false" },
                new TemplateParameter { Name = "network_acls_default_action", Type = "string", Description = "Network ACLs default action (Allow/Deny)", Required = false, Default = "Allow" },
                new TemplateParameter { Name = "environment", Type = "string", Description = "Environment tag (dev/test/prod)", Required = true },
                new TemplateParameter { Name = "project_name", Type = "string", Description = "Project name for tagging", Required = true },
                new TemplateParameter { Name = "owner", Type = "string", Description = "Owner tag", Required = true }
            }
        },
        ["keyvault-enterprise"] = new TemplateMetadata
        {
            Id = "keyvault-enterprise",
            Name = "Enterprise Key Vault",
            Description = "Enterprise Key Vault with private endpoints, advanced security, and compliance",
            Category = "security", 
            GitHubUrl = "https://raw.githubusercontent.com/sachink-singh/aiagent-terraform-template/main/terraform-templates/keyvault-enterprise/main.tf",
            Parameters = new[]
            {
                new TemplateParameter { Name = "keyvault_name", Type = "string", Description = "Key Vault name (must be globally unique)", Required = true },
                new TemplateParameter { Name = "resource_group_name", Type = "string", Description = "Resource group name", Required = true },
                new TemplateParameter { Name = "location", Type = "string", Description = "Azure region", Required = true, Default = "East US" },
                new TemplateParameter { Name = "sku_name", Type = "string", Description = "Key Vault SKU (standard/premium)", Required = false, Default = "premium" },
                new TemplateParameter { Name = "enable_private_endpoint", Type = "bool", Description = "Enable private endpoint", Required = false, Default = "true" },
                new TemplateParameter { Name = "subnet_id", Type = "string", Description = "Subnet ID for private endpoint", Required = false },
                new TemplateParameter { Name = "enable_diagnostic_settings", Type = "bool", Description = "Enable diagnostic settings", Required = false, Default = "true" },
                new TemplateParameter { Name = "log_analytics_workspace_id", Type = "string", Description = "Log Analytics workspace ID", Required = false },
                new TemplateParameter { Name = "environment", Type = "string", Description = "Environment tag (dev/test/prod)", Required = true },
                new TemplateParameter { Name = "business_unit", Type = "string", Description = "Business unit for cost allocation", Required = true },
                new TemplateParameter { Name = "compliance_framework", Type = "string", Description = "Compliance framework (SOC2/HIPAA/PCI)", Required = false }
            }
        }
    };

    public List<TemplateMetadata> GetAvailableTemplates() 
    {
        // For backwards compatibility, return hardcoded templates
        // In production, you'd call DiscoverAllTemplatesAsync() periodically
        return _templates.Values.ToList();
    }

    /// <summary>
    /// Gets all available templates using auto-discovery
    /// Use this method for dynamic template loading
    /// </summary>
    public async Task<List<TemplateMetadata>> GetAvailableTemplatesAsync()
    {
        try
        {
            // Try auto-discovery first
            var discoveredTemplates = await DiscoverAllTemplatesAsync();
            
            if (discoveredTemplates.Any())
            {
                _logger.LogInformation("Auto-discovered {Count} templates from repository", discoveredTemplates.Count);
                return discoveredTemplates;
            }
            
            // Fallback to hardcoded templates
            _logger.LogInformation("Using hardcoded template definitions as fallback");
            return _templates.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during template discovery, using hardcoded templates");
            return _templates.Values.ToList();
        }
    }

    /// <summary>
    /// Builds a GitHub raw content URL for your organization's repository
    /// </summary>
    private string BuildOrganizationTemplateUrl(string templatePath)
    {
        return $"{_rawContentUrl}/{_repositoryOwner}/{_repositoryName}/{_branch}/{templatePath}";
    }

    /// <summary>
    /// Enhanced auto-discovery that scans the entire repository structure
    /// This eliminates the need to manually add templates to code
    /// </summary>
    public async Task<List<TemplateMetadata>> DiscoverAllTemplatesAsync()
    {
        var discoveredTemplates = new List<TemplateMetadata>();
        
        try
        {
            // Get repository contents via GitHub API
            var repoContentsUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/contents/terraform-templates";
            var response = await _httpClient.GetAsync(repoContentsUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var contentJson = await response.Content.ReadAsStringAsync();
                var contents = JsonSerializer.Deserialize<GitHubContent[]>(contentJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (contents != null)
                {
                    foreach (var category in contents.Where(c => c.Type == "dir"))
                    {
                        await DiscoverTemplatesInCategory(category.Name, discoveredTemplates);
                    }
                }
            }
            else
            {
                // Fallback: Use known paths if API fails
                await DiscoverKnownTemplates(discoveredTemplates);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-discovery, falling back to known templates");
            await DiscoverKnownTemplates(discoveredTemplates);
        }
        
        return discoveredTemplates;
    }

    private async Task DiscoverTemplatesInCategory(string category, List<TemplateMetadata> discoveredTemplates)
    {
        try
        {
            var categoryUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}/contents/terraform-templates/{category}";
            var response = await _httpClient.GetAsync(categoryUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var contentJson = await response.Content.ReadAsStringAsync();
                var templates = JsonSerializer.Deserialize<GitHubContent[]>(contentJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (templates != null)
                {
                    foreach (var template in templates.Where(t => t.Type == "dir"))
                    {
                        await ProcessTemplateDirectory(category, template.Name, discoveredTemplates);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering templates in category: {Category}", category);
        }
    }

    private async Task ProcessTemplateDirectory(string category, string templateName, List<TemplateMetadata> discoveredTemplates)
    {
        try
        {
            var templatePath = $"terraform-templates/{category}/{templateName}";
            var mainTfUrl = $"{_rawContentUrl}/{_repositoryOwner}/{_repositoryName}/{_branch}/{templatePath}/main.tf";
            var variablesTfUrl = $"{_rawContentUrl}/{_repositoryOwner}/{_repositoryName}/{_branch}/{templatePath}/variables.tf";
            
            // Check if main.tf exists
            var mainTfResponse = await _httpClient.GetAsync(mainTfUrl);
            if (mainTfResponse.IsSuccessStatusCode)
            {
                var mainTfContent = await mainTfResponse.Content.ReadAsStringAsync();
                
                // Try to get variables.tf for parameter extraction
                var parameters = new List<TemplateParameter>();
                var variablesResponse = await _httpClient.GetAsync(variablesTfUrl);
                if (variablesResponse.IsSuccessStatusCode)
                {
                    var variablesContent = await variablesResponse.Content.ReadAsStringAsync();
                    parameters = ExtractParametersFromVariablesFile(variablesContent);
                }
                else
                {
                    // Fallback to extracting from main.tf
                    parameters = ExtractParametersFromTemplate(mainTfContent).ToList();
                }

                var templateId = $"{category}-{templateName}";
                
                discoveredTemplates.Add(new TemplateMetadata
                {
                    Id = templateId,
                    Name = FormatTemplateName(templateName),
                    Description = ExtractDescriptionFromTemplate(mainTfContent, templateName),
                    Category = category,
                    GitHubUrl = mainTfUrl,
                    Parameters = parameters.ToArray()
                });
                
                _logger.LogInformation("Discovered template: {TemplateId} at {Path}", templateId, templatePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing template directory: {Category}/{Template}", category, templateName);
        }
    }

    private async Task DiscoverKnownTemplates(List<TemplateMetadata> discoveredTemplates)
    {
        // Fallback to scanning known directory structure
        var knownPaths = new[]
        {
            "terraform-templates/compute/vm-standard",
            "terraform-templates/compute/vm-secure",
            "terraform-templates/storage/storage-account",
            "terraform-templates/storage/storage-secure",
            "terraform-templates/database/sql-database",
            "terraform-templates/database/cosmosdb",
            "terraform-templates/networking/vnet-standard",
            "terraform-templates/networking/load-balancer",
            "terraform-templates/security/keyvault",
            "terraform-templates/security/keyvault-enterprise",
            "terraform-templates/containers/aks-cluster",
            "terraform-templates/containers/container-registry",
            "terraform-templates/web/webapp-basic",
            "terraform-templates/web/webapp-secure"
        };

        foreach (var path in knownPaths)
        {
            var mainTfUrl = $"{_rawContentUrl}/{_repositoryOwner}/{_repositoryName}/{_branch}/{path}/main.tf";
            var response = await _httpClient.GetAsync(mainTfUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var pathParts = path.Split('/');
                var category = pathParts[1];
                var templateName = pathParts[2];
                var templateId = $"{category}-{templateName}";
                
                var content = await response.Content.ReadAsStringAsync();
                
                discoveredTemplates.Add(new TemplateMetadata
                {
                    Id = templateId,
                    Name = FormatTemplateName(templateName),
                    Description = ExtractDescriptionFromTemplate(content, templateName),
                    Category = category,
                    GitHubUrl = mainTfUrl,
                    Parameters = ExtractParametersFromTemplate(content)
                });
                
                _logger.LogInformation("Discovered known template: {TemplateId}", templateId);
            }
        }
    }

    private List<TemplateParameter> ExtractParametersFromVariablesFile(string variablesContent)
    {
        var parameters = new List<TemplateParameter>();
        
        // Simple regex-based extraction from variables.tf
        // You could enhance this with proper HCL parsing
        var variablePattern = @"variable\s+""([^""]+)""\s*\{([^}]+)\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(variablesContent, variablePattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            var variableBlock = match.Groups[2].Value;
            
            // Extract description
            var descriptionMatch = System.Text.RegularExpressions.Regex.Match(variableBlock, @"description\s*=\s*""([^""]+)""");
            var description = descriptionMatch.Success ? descriptionMatch.Groups[1].Value : $"Parameter for {variableName}";
            
            // Extract type
            var typeMatch = System.Text.RegularExpressions.Regex.Match(variableBlock, @"type\s*=\s*(\w+)");
            var type = typeMatch.Success ? typeMatch.Groups[1].Value : "string";
            
            // Extract default
            var defaultMatch = System.Text.RegularExpressions.Regex.Match(variableBlock, @"default\s*=\s*""?([^""\n]+)""?");
            var defaultValue = defaultMatch.Success ? defaultMatch.Groups[1].Value.Trim('"') : null;
            
            // Check if required (no default value typically means required)
            var required = !defaultMatch.Success;
            
            parameters.Add(new TemplateParameter
            {
                Name = variableName,
                Type = type,
                Description = description,
                Required = required,
                Default = defaultValue
            });
        }
        
        return parameters;
    }

    private string FormatTemplateName(string templateName)
    {
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            templateName.Replace("-", " ").Replace("_", " ")
        );
    }

    private string ExtractDescriptionFromTemplate(string templateContent, string templateName)
    {
        // Try to extract description from comments
        var lines = templateContent.Split('\n');
        var descriptionLines = lines.Where(l => l.TrimStart().StartsWith("# ") && 
                                              !l.Contains("terraform") && 
                                              !l.Contains("resource") &&
                                              l.Length > 10)
                                    .Take(2)
                                    .Select(l => l.TrimStart().Substring(2).Trim());
        
        if (descriptionLines.Any())
        {
            return string.Join(" ", descriptionLines);
        }
        
        return $"Auto-discovered {FormatTemplateName(templateName)} template";
    }

    // GitHub API response model
    private class GitHubContent
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Url { get; set; } = "";
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
    random = {
      source  = "hashicorp/random"
      version = "~> 3.1"
    }
  }
}

provider "azurerm" {
  features {}
}

# Variables with defaults
variable "workload_name" {
  description = "Name of the workload"
  type        = string
}

variable "project_name" {
  description = "Name of the project"
  type        = string
}

variable "cluster_name" {
  description = "Optional cluster name override"
  type        = string
  default     = ""
}

variable "owner" {
  description = "Owner of the resources"
  type        = string
}

variable "environment" {
  description = "Environment (dev, test, staging, prod)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "node_count" {
  description = "Number of nodes in the default node pool"
  type        = number
  default     = 3
}

variable "vm_size" {
  description = "Size of the Virtual Machine"
  type        = string
  default     = "Standard_DS2_v2"
}

# Random suffix for unique naming
resource "random_id" "suffix" {
  byte_length = 4
}

# Intelligent naming logic with conditional truncation only when needed
locals {
  # Clean input names (remove special characters, keep alphanumeric)
  clean_workload = replace(lower(var.workload_name), "/[^a-z0-9]/", "")
  clean_project = replace(lower(var.project_name), "/[^a-z0-9]/", "")
  clean_owner = replace(lower(var.owner), "/[^a-z0-9]/", "")
  clean_env = lower(var.environment)
  clean_location = lower(substr(var.location, 0, 12))  # Keep more location context
  
  # 8-character suffix for uniqueness
  suffix = random_id.suffix.hex
  
  # Calculate potential resource group name first
  potential_rg_name = "rg-${local.clean_workload}-${local.clean_env}-${local.clean_location}-${local.suffix}"
  
  # Calculate potential cluster name
  potential_cluster_name = var.cluster_name != "" ? var.cluster_name : "aks-${local.clean_workload}-${local.clean_env}-${local.suffix}"
  
  # Calculate what the node resource group would be
  potential_node_rg = "MC_${local.potential_rg_name}_${local.potential_cluster_name}_${var.location}"
  potential_node_rg_length = length(local.potential_node_rg)
  
  # Only truncate if node RG would exceed 80 characters
  needs_truncation = local.potential_node_rg_length > 80
  
  # Conditional truncation - only when needed
  workload_part = local.needs_truncation ? substr(local.clean_workload, 0, 8) : local.clean_workload
  env_part = local.needs_truncation ? substr(local.clean_env, 0, 3) : local.clean_env
  location_part = local.needs_truncation ? substr(local.clean_location, 0, 6) : local.clean_location
  
  # Final resource names - preserve meaning when possible
  rg_name = local.needs_truncation ? "rg-${local.workload_part}-${local.location_part}-${local.suffix}" : local.potential_rg_name
    
  cluster_name = local.needs_truncation ? (var.cluster_name != "" ? substr(var.cluster_name, 0, 18) : "aks-${local.workload_part}-${local.suffix}") : local.potential_cluster_name
  
  # Other resource names - only shorten if main names were shortened
  vnet_name = local.needs_truncation ? "vnet-${local.workload_part}-${local.suffix}" : "vnet-${local.clean_workload}-${local.clean_env}-${local.suffix}"
    
  subnet_name = local.needs_truncation ? "snet-aks-${local.env_part}-${local.suffix}" : "snet-aks-${local.clean_env}-${local.suffix}"
    
  log_name = local.needs_truncation ? "log-${local.workload_part}-${local.suffix}" : "log-${local.clean_workload}-${local.clean_env}-${local.suffix}"
  
  # Final validation: Calculate actual node RG length
  expected_node_rg = "MC_${local.rg_name}_${local.cluster_name}_${var.location}"
  node_rg_length = length(local.expected_node_rg)
  
  # Common tags
  common_tags = {
    Environment = var.environment
    Project     = var.project_name
    Owner       = var.owner
    WorkloadName = var.workload_name
    CreatedBy   = "AzureAIAgent"
    NodeRGLength = local.node_rg_length  # For debugging
  }
}

# Data sources
data "azurerm_client_config" "current" {}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = local.rg_name
  location = var.location
  tags     = local.common_tags
}

# Virtual Network
resource "azurerm_virtual_network" "main" {
  name                = local.vnet_name
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.common_tags
}

# Subnet for AKS
resource "azurerm_subnet" "aks" {
  name                 = local.subnet_name
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.1.0/24"]
}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "main" {
  name                = local.log_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.common_tags
}

# AKS Cluster
resource "azurerm_kubernetes_cluster" "main" {
  name                = local.cluster_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = local.cluster_name
  kubernetes_version  = "1.31.10"

  default_node_pool {
    name                = "default"
    node_count          = var.node_count
    vm_size             = var.vm_size
    os_disk_size_gb     = 30
    vnet_subnet_id      = azurerm_subnet.aks.id
    
    upgrade_settings {
      max_surge = "10%"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  role_based_access_control_enabled = true

  network_profile {
    network_plugin     = "azure"
    network_policy     = "azure"
    dns_service_ip     = "10.2.0.10"
    service_cidr       = "10.2.0.0/24"
    load_balancer_sku  = "standard"
  }

  oms_agent {
    log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  }

  tags = local.common_tags
}

# Role assignment for AKS to manage network
resource "azurerm_role_assignment" "aks_network_contributor" {
  scope                = azurerm_virtual_network.main.id
  role_definition_name = "Network Contributor"
  principal_id         = azurerm_kubernetes_cluster.main.identity[0].principal_id
}

# Outputs
output "resource_group_name" {
  description = "Name of the created resource group"
  value       = azurerm_resource_group.main.name
}

output "aks_cluster_name" {
  description = "Name of the AKS cluster"
  value       = azurerm_kubernetes_cluster.main.name
}

output "aks_cluster_id" {
  description = "ID of the AKS cluster"
  value       = azurerm_kubernetes_cluster.main.id
}

output "aks_fqdn" {
  description = "FQDN of the AKS cluster"
  value       = azurerm_kubernetes_cluster.main.fqdn
}

output "aks_node_resource_group" {
  description = "Name of the AKS node resource group"
  value       = azurerm_kubernetes_cluster.main.node_resource_group
}

output "kube_config" {
  description = "Raw Kubernetes config to be used by kubectl and other compatible tools"
  value       = azurerm_kubernetes_cluster.main.kube_config_raw
  sensitive   = true
}

output "client_certificate" {
  description = "Base64 encoded public certificate used by clients to authenticate to the Kubernetes cluster"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.client_certificate
  sensitive   = true
}

output "client_key" {
  description = "Base64 encoded private key used by clients to authenticate to the Kubernetes cluster"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.client_key
  sensitive   = true
}

output "cluster_ca_certificate" {
  description = "Base64 encoded public CA certificate used as the root of trust for the Kubernetes cluster"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.cluster_ca_certificate
  sensitive   = true
}

output "host" {
  description = "The Kubernetes cluster server host"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.host
  sensitive   = true
}

output "log_analytics_workspace_id" {
  description = "ID of the Log Analytics workspace"
  value       = azurerm_log_analytics_workspace.main.id
}

output "vnet_id" {
  description = "ID of the virtual network"
  value       = azurerm_virtual_network.main.id
}

output "subnet_id" {
  description = "ID of the AKS subnet"
  value       = azurerm_subnet.aks.id
}

# Validation outputs for debugging
output "expected_node_rg_name" {
  description = "Expected node resource group name"
  value       = local.expected_node_rg
}

output "node_rg_name_length" {
  description = "Length of the expected node resource group name"
  value       = local.node_rg_length
}

output "naming_truncation_applied" {
  description = "Whether name truncation was applied due to Azure limits"
  value       = local.needs_truncation
}

output "original_potential_length" {
  description = "What the node RG length would have been without truncation"
  value       = local.potential_node_rg_length
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
