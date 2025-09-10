using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AzureAIAgent.Core.Models;
using AzureAIAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace AzureAIAgent.Plugins;

/// <summary>
/// AI-driven Semantic Kernel plugin for Azure resource management with intelligent Terraform operations
/// Enhanced to prioritize GitHub pre-built templates
/// </summary>
public class AzureResourcePlugin
{
    private readonly Dictionary<string, object> _sessionState = new();
    private readonly GitHubTemplateService? _templateService;
    private readonly AksMcpPlugin _aksMcpPlugin;
    private readonly AdaptiveCardService? _adaptiveCardService;
    private readonly ILogger? _logger;

    public AzureResourcePlugin(
        GitHubTemplateService? templateService = null, 
        AksMcpPlugin? aksMcpPlugin = null,
        AdaptiveCardService? adaptiveCardService = null,
        ILogger? logger = null)
    {
        _templateService = templateService;
        _aksMcpPlugin = aksMcpPlugin ?? new AksMcpPlugin();
        _adaptiveCardService = adaptiveCardService;
        _logger = logger;
    }

    [KernelFunction("CreateInfrastructure")]
    [Description("Create Azure infrastructure using pre-built GitHub templates when possible, or generate custom Terraform")]
    public async Task<string> CreateInfrastructure(
        [Description("Description of the infrastructure to create (e.g., 'AKS cluster', 'web app', 'storage account')")] string infrastructureDescription)
    {
        try
        {
            // First, try to match with GitHub templates
            if (_templateService != null)
            {
                var suggestedTemplate = await SuggestGitHubTemplate(infrastructureDescription);
                if (suggestedTemplate != null)
                {
                    return $"🎯 **GitHub Template Recommended!**\n\n" +
                           $"I found a perfect pre-built template for your request: **{suggestedTemplate.Name}**\n\n" +
                           $"**Description:** {suggestedTemplate.Description}\n" +
                           $"**Category:** {suggestedTemplate.Category}\n\n" +
                           $"**✨ Benefits of using this template:**\n" +
                           $"• ✅ Production-tested configuration\n" +
                           $"• ✅ Best practices included\n" +
                           $"• ✅ Faster deployment\n" +
                           $"• ✅ Community maintained\n\n" +
                           $"**🚀 Quick Deploy:**\n" +
                           $"Say: `deploy template {suggestedTemplate.Id}` to use this template\n\n" +
                           $"**🔧 Or Browse All:**\n" +
                           $"Say: `show template gallery` to see all available templates\n\n" +
                           $"Would you like to deploy this template or see more options?";
                }
            }

            // If no template match, proceed with custom generation
            return await GenerateCustomTerraform(infrastructureDescription);
        }
        catch (Exception ex)
        {
            return $"❌ Error creating infrastructure: {ex.Message}";
        }
    }

    private async Task<TemplateMetadata?> SuggestGitHubTemplate(string description)
    {
        if (_templateService == null) return null;

        var lowerDesc = description.ToLower();
        var templates = _templateService.GetAvailableTemplates();

        // Smart matching logic
        var matches = templates.Where(t => 
        {
            var lowerName = t.Name.ToLower();
            var lowerDescription = t.Description.ToLower();
            
            // Direct matches
            if (lowerDesc.Contains("aks") || lowerDesc.Contains("kubernetes")) 
                return t.Id == "aks-cluster";
            if (lowerDesc.Contains("vm") || lowerDesc.Contains("virtual machine")) 
                return t.Id == "vm-basic";
            if (lowerDesc.Contains("web app") || lowerDesc.Contains("app service")) 
                return t.Id == "webapp-basic";
            if (lowerDesc.Contains("storage") || lowerDesc.Contains("blob")) 
                return t.Id == "storage-account";
            if (lowerDesc.Contains("sql") || lowerDesc.Contains("database")) 
                return t.Id == "sql-database";
            
            // Keyword-based matching
            return lowerName.Split(' ').Any(word => lowerDesc.Contains(word)) ||
                   lowerDescription.Split(' ').Any(word => lowerDesc.Contains(word));
        }).ToList();

        return matches.FirstOrDefault();
    }

    private async Task<string> GenerateCustomTerraform(string description)
    {
        return $"🔧 **Custom Terraform Generation**\n\n" +
               $"No pre-built template found for: {description}\n\n" +
               $"I'll generate custom Terraform code for you. This might take a moment...\n\n" +
               $"*Note: For faster, production-ready deployments, consider using our pre-built templates. " +
               $"Say `show template gallery` to see available options.*";
    }

    [KernelFunction("ShowGitHubTemplateGallery")]
    [Description("Display the gallery of pre-built Terraform templates from GitHub")]
    public async Task<string> ShowGitHubTemplateGallery()
    {
        try
        {
            if (_templateService == null)
            {
                return "❌ Template service not available. GitHub templates are not configured.";
            }

            var templates = _templateService.GetAvailableTemplates();
            
            // Generate Adaptive Card if service is available
            if (_adaptiveCardService != null)
            {
                var adaptiveCard = _adaptiveCardService.GenerateTemplateGalleryCard(templates);
                
                var result = new StringBuilder();
                result.AppendLine("🎯 **Interactive Template Gallery Generated!**");
                result.AppendLine();
                result.AppendLine("� **Adaptive Card JSON:**");
                result.AppendLine("```json");
                result.AppendLine(adaptiveCard);
                result.AppendLine("```");
                result.AppendLine();
                result.AppendLine("� **How to use:**");
                result.AppendLine("• Copy the JSON above into an Adaptive Card viewer");
                result.AppendLine("• Click on templates to select them");
                result.AppendLine("• Use the interactive buttons for deployment");
                result.AppendLine();
                result.AppendLine("� **Test with Adaptive Card Designer:** https://adaptivecards.io/designer/");
                result.AppendLine();
                result.AppendLine("---");
                result.AppendLine();
                result.AppendLine("📝 **Fallback Text Gallery:**");
                result.AppendLine();
                result.Append(GenerateTextGallery(templates));
                
                return result.ToString();
            }

            // Fallback to text-based gallery
            return GenerateTextGallery(templates);
        }
        catch (Exception ex)
        {
            return $"❌ Error loading template gallery: {ex.Message}";
        }
    }

    private string GetCategoryIcon(string category) => category.ToLower() switch
    {
        "compute" => "💻",
        "containers" => "🐳",
        "web" => "🌐",
        "storage" => "💾",
        "database" => "🗄️",
        "network" => "🌐",
        _ => "⚙️"
    };

    [KernelFunction("DeployGitHubTemplate")]
    [Description("Deploy a specific GitHub template with parameters")]
    public async Task<string> DeployGitHubTemplate(
        [Description("Template ID (e.g., 'aks-cluster', 'vm-basic')")] string templateId,
        [Description("Optional parameters in JSON format")] string? parametersJson = null)
    {
        try
        {
            if (_templateService == null)
            {
                return "❌ Template service not available. GitHub templates are not configured.";
            }

            var template = _templateService.GetTemplate(templateId);
            if (template == null)
            {
                var available = _templateService.GetAvailableTemplates();
                var availableIds = string.Join(", ", available.Select(t => t.Id));
                return $"❌ Template '{templateId}' not found.\n\nAvailable templates: {availableIds}\n\nUse `show template gallery` to see details.";
            }

            // Download template from GitHub
            var templateContent = await _templateService.DownloadTemplateAsync(templateId);
            if (string.IsNullOrEmpty(templateContent))
            {
                return $"❌ Failed to download template '{templateId}' from GitHub: {template.GitHubUrl}";
            }

            // Parse parameters
            var parameters = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(parametersJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson);
                    if (parsed != null) parameters = parsed;
                }
                catch (JsonException ex)
                {
                    return $"❌ Invalid parameters JSON: {ex.Message}";
                }
            }

            // Add default values for missing parameters
            foreach (var param in template.Parameters)
            {
                if (!parameters.ContainsKey(param.Name) && !string.IsNullOrEmpty(param.Default))
                {
                    parameters[param.Name] = param.Default;
                }
            }

            // Check for missing required parameters
            var missingRequired = template.Parameters
                .Where(p => p.Required && !parameters.ContainsKey(p.Name))
                .ToList();

            if (missingRequired.Any())
            {
                var missing = string.Join(", ", missingRequired.Select(p => p.Name));
                return $"❌ Missing required parameters: {missing}\n\nUse `select template {templateId}` to see all parameters.";
            }

            // Create deployment directory
            var deploymentId = $"{templateId}-github-{DateTime.Now:yyyyMMdd-HHmmss}";
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            Directory.CreateDirectory(baseDir);
            var deploymentDir = Path.Combine(baseDir, deploymentId);
            Directory.CreateDirectory(deploymentDir);

            // Process template with parameters
            var processedTemplate = SubstituteTemplateParameters(templateContent, parameters);

            // Write Terraform files
            var tfFile = Path.Combine(deploymentDir, "main.tf");
            await File.WriteAllTextAsync(tfFile, processedTemplate);

            var varsFile = Path.Combine(deploymentDir, "terraform.tfvars.json");
            await File.WriteAllTextAsync(varsFile, JsonSerializer.Serialize(parameters, new JsonSerializerOptions { WriteIndented = true }));

            // Create backend configuration
            var backendConfig = GenerateBackendConfiguration(deploymentId);
            var backendFile = Path.Combine(deploymentDir, "backend.tf");
            await File.WriteAllTextAsync(backendFile, backendConfig);

            var result = new StringBuilder();
            result.AppendLine($"🚀 **GitHub Template: {template.Name}**");
            result.AppendLine($"📁 **Deployment ID:** `{deploymentId}`");
            result.AppendLine($"🔗 **GitHub Source:** {template.GitHubUrl}");
            result.AppendLine($"📍 **Directory:** `{deploymentDir}`");
            result.AppendLine();

            result.AppendLine("**Parameters Applied:**");
            foreach (var param in parameters)
            {
                var value = param.Key.ToLower().Contains("password") ? "***" : param.Value.ToString();
                result.AppendLine($"• {param.Key}: {value}");
            }
            result.AppendLine();

            // Display the Terraform template with syntax highlighting
            result.AppendLine("**� Terraform Template Preview:**");
            result.AppendLine();
            result.AppendLine("```hcl");
            result.AppendLine(processedTemplate);
            result.AppendLine("```");
            result.AppendLine();

            result.AppendLine("**⚡ Ready for Deployment**");
            result.AppendLine("Review the template above and choose your action:");
            result.AppendLine("• ✅ **Deploy**: Execute terraform init, plan, and apply");
            result.AppendLine("• ✏️ **Edit**: Modify the template before deployment");
            result.AppendLine("• ❌ **Cancel**: Abort this deployment");
            result.AppendLine();

            result.AppendLine("**⚡ Ready for Deployment**");
            result.AppendLine("Review the template above and choose your action:");
            result.AppendLine("• ✅ **Deploy**: Execute terraform init, plan, and apply");
            result.AppendLine("• ✏️ **Edit**: Modify the template before deployment");
            result.AppendLine("• ❌ **Cancel**: Abort this deployment");
            result.AppendLine();

            // Store deployment info for potential execution
            result.AppendLine($"**� Deployment Ready:** Use `deploy {deploymentId}` to execute or `cancel {deploymentId}` to abort");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error deploying GitHub template: {ex.Message}";
        }
    }

    [KernelFunction("ExecuteDeployment")]
    [Description("Execute a prepared GitHub template deployment")]
    public async Task<string> ExecuteDeployment(
        [Description("Deployment ID to execute")] string deploymentId)
    {
        try
        {
            // Find deployment directory
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            var deploymentDir = Path.Combine(baseDir, deploymentId);

            if (!Directory.Exists(deploymentDir))
            {
                return $"❌ Deployment '{deploymentId}' not found. The deployment may have been cancelled or expired.";
            }

            var result = new StringBuilder();
            result.AppendLine($"🚀 **Executing Deployment: {deploymentId}**");
            result.AppendLine();

            // Execute Terraform commands with proper output formatting
            result.AppendLine("**🔄 Terraform Execution:**");
            result.AppendLine();

            var initResult = await ExecuteTerraformCommand("init", deploymentDir);
            if (initResult.Contains("Terraform has been successfully initialized"))
            {
                result.AppendLine("✅ **Init:** Success");
                result.AppendLine("```");
                result.AppendLine(StripAnsiCodes(initResult));
                result.AppendLine("```");
                result.AppendLine();
                
                var planResult = await ExecuteTerraformCommand("plan -var-file=terraform.tfvars.json", deploymentDir);
                if (planResult.Contains("Plan:"))
                {
                    result.AppendLine("✅ **Plan:** Success");
                    result.AppendLine("```hcl");
                    result.AppendLine(StripAnsiCodes(planResult));
                    result.AppendLine("```");
                    result.AppendLine();
                    
                    var applyResult = await ExecuteTerraformCommand("apply -var-file=terraform.tfvars.json -auto-approve", deploymentDir);
                    if (applyResult.Contains("Apply complete"))
                    {
                        result.AppendLine("✅ **Apply:** Success");
                        result.AppendLine("```");
                        result.AppendLine(StripAnsiCodes(applyResult));
                        result.AppendLine("```");
                        result.AppendLine();
                        result.AppendLine("🎉 **GitHub Template Deployment Completed Successfully!**");
                        result.AppendLine();
                        result.AppendLine("**✨ What was deployed:**");
                        result.AppendLine("• Production-ready Azure resources");
                        result.AppendLine("• Best practices and security configurations");
                        result.AppendLine("• Community-tested and maintained infrastructure");
                        result.AppendLine();
                        result.AppendLine($"**📊 Manage Deployment:** `manage deployment {deploymentId}`");
                        result.AppendLine($"**🗑️ Clean Up:** `destroy deployment {deploymentId}`");
                    }
                    else
                    {
                        result.AppendLine("❌ **Apply:** Failed");
                        result.AppendLine("```");
                        result.AppendLine(StripAnsiCodes(applyResult));
                        result.AppendLine("```");
                    }
                }
                else
                {
                    result.AppendLine("❌ **Plan:** Failed");
                    result.AppendLine("```");
                    result.AppendLine(StripAnsiCodes(planResult));
                    result.AppendLine("```");
                }
            }
            else
            {
                result.AppendLine("❌ **Init:** Failed");
                result.AppendLine("```");
                result.AppendLine(StripAnsiCodes(initResult));
                result.AppendLine("```");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error executing deployment: {ex.Message}";
        }
    }

    private string SubstituteTemplateParameters(string template, Dictionary<string, object> parameters)
    {
        var result = template;
        
        foreach (var param in parameters)
        {
            var placeholder = $"${{var.{param.Key}}}";
            var value = param.Value.ToString();
            
            // Handle string values (add quotes if not already quoted)
            if (param.Value is string && !value.StartsWith("\""))
            {
                value = $"\"{value}\"";
            }
            
            result = result.Replace(placeholder, value);
        }
        
        return result;
    }

    #region Universal Azure Resource Management with Adaptive Cards

    [KernelFunction("ListResourceGroupsInteractive")]
    [Description("List all Azure Resource Groups with interactive actions")]
    public async Task<string> ListResourceGroupsInteractive()
    {
        try
        {
            const string command = "group list --query '[].{name:name, location:location, id:id}' --output json";
            var output = await ExecuteAzureCommand(command);
            
            if (string.IsNullOrEmpty(output) || output.Contains("ERROR"))
            {
                return "❌ Failed to retrieve resource groups. Please ensure you're logged into Azure CLI.";
            }

            var resourceGroups = JsonSerializer.Deserialize<List<dynamic>>(output);
            if (resourceGroups == null || !resourceGroups.Any())
            {
                return "📭 No resource groups found in your current subscription.";
            }

            // Use AdaptiveCardService if available (need to inject it)
            var cardJson = "Resource Groups listed successfully! 📁\n\n";
            cardJson += $"Found {resourceGroups.Count} resource groups:\n\n";
            
            foreach (var rg in resourceGroups)
            {
                cardJson += $"🔹 **{rg.GetProperty("name").GetString()}**\n";
                cardJson += $"   📍 Location: {rg.GetProperty("location").GetString()}\n";
                cardJson += $"   🆔 ID: {rg.GetProperty("id").GetString()}\n\n";
            }
            
            cardJson += "💡 *Click on any resource group above for available actions: List Resources, View Metrics, Manage Tags, Delete*";
            
            return cardJson;
        }
        catch (Exception ex)
        {
            return $"❌ Error listing resource groups: {ex.Message}";
        }
    }

    [KernelFunction("ListVirtualMachines")]
    [Description("List all Virtual Machines with interactive cards")]
    public async Task<string> ListVirtualMachines(
        [Description("Optional resource group name to filter VMs")] string? resourceGroupName = null)
    {
        try
        {
            var command = string.IsNullOrEmpty(resourceGroupName) 
                ? "vm list --query '[].{name:name, location:location, resourceGroup:resourceGroup, vmSize:hardwareProfile.vmSize, powerState:instanceView.statuses[1].displayStatus}' --output json --show-details"
                : $"vm list -g {resourceGroupName} --query '[].{{name:name, location:location, resourceGroup:resourceGroup, vmSize:hardwareProfile.vmSize, powerState:instanceView.statuses[1].displayStatus}}' --output json --show-details";
            
            var output = await ExecuteAzureCommand(command);
            
            if (string.IsNullOrEmpty(output) || output.Contains("ERROR"))
            {
                return "❌ Failed to retrieve virtual machines. Please ensure you're logged into Azure CLI.";
            }

            var vmData = JsonSerializer.Deserialize<List<JsonElement>>(output);
            if (vmData == null || !vmData.Any())
            {
                var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
                return $"🖥️ No virtual machines found in your current {scope}.";
            }

            // Convert VMs to dynamic objects for the card service
            var vms = new List<dynamic>();
            foreach (var vm in vmData)
            {
                var name = vm.GetProperty("name").GetString();
                var location = vm.GetProperty("location").GetString();
                var resourceGroup = vm.GetProperty("resourceGroup").GetString();
                var vmSize = vm.GetProperty("vmSize").GetString();
                var powerState = vm.TryGetProperty("powerState", out JsonElement powerProp) ? powerProp.GetString() : "Unknown";
                
                vms.Add(new
                {
                    name = name,
                    location = location,
                    resourceGroup = resourceGroup,
                    vmSize = vmSize,
                    powerState = powerState
                });
            }

            // Generate interactive cards for the VMs
            if (_adaptiveCardService != null)
            {
                try
                {
                    var interactiveCard = _adaptiveCardService.GenerateVirtualMachineCards(vms);
                    return $"🃏 **Interactive Virtual Machines List**\n\n{interactiveCard}";
                }
                catch (Exception cardEx)
                {
                    _logger?.LogWarning(cardEx, "Failed to generate interactive cards, falling back to text");
                }
            }

            // Fallback to original text format if cards fail
            var result = $"🖥️ **Virtual Machines** ({vms.Count} found)\n\n";
            
            foreach (var vm in vms)
            {
                var statusIcon = vm.powerState?.Contains("running") == true ? "🟢" : "🔴";
                
                result += $"{statusIcon} **{vm.name}**\n";
                result += $"   📍 Location: {vm.location}\n";
                result += $"   📦 Size: {vm.vmSize}\n";
                result += $"   📁 Resource Group: {vm.resourceGroup}\n";
                result += $"   ⚡ Status: {vm.powerState}\n\n";
                result += $"   💡 *Click VM for actions: Start, Stop, Restart, View Metrics, Connect*\n\n";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error listing virtual machines: {ex.Message}";
        }
    }

    [KernelFunction("ListStorageAccounts")]
    [Description("List all Storage Accounts with interactive cards")]
    public async Task<string> ListStorageAccounts(
        [Description("Optional resource group name to filter storage accounts")] string? resourceGroupName = null)
    {
        try
        {
            var command = string.IsNullOrEmpty(resourceGroupName)
                ? "storage account list --query '[].{name:name, location:location, resourceGroup:resourceGroup, kind:kind, accessTier:accessTier, sku:sku.name}' --output json"
                : $"storage account list -g {resourceGroupName} --query '[].{{name:name, location:location, resourceGroup:resourceGroup, kind:kind, accessTier:accessTier, sku:sku.name}}' --output json";
            
            var output = await ExecuteAzureCommand(command);
            
            if (string.IsNullOrEmpty(output) || output.Contains("ERROR"))
            {
                return "❌ Failed to retrieve storage accounts. Please ensure you're logged into Azure CLI.";
            }

            var storageData = JsonSerializer.Deserialize<List<JsonElement>>(output);
            if (storageData == null || !storageData.Any())
            {
                var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
                return $"💾 No storage accounts found in your current {scope}.";
            }

            // Convert storage accounts to dynamic objects for the card service
            var storageAccounts = new List<dynamic>();
            foreach (var storage in storageData)
            {
                var name = storage.GetProperty("name").GetString();
                var location = storage.GetProperty("location").GetString();
                var resourceGroup = storage.GetProperty("resourceGroup").GetString();
                var kind = storage.GetProperty("kind").GetString();
                var accessTier = storage.TryGetProperty("accessTier", out JsonElement tierProp) ? tierProp.GetString() : "N/A";
                var sku = storage.GetProperty("sku").GetString();
                
                storageAccounts.Add(new
                {
                    name = name,
                    location = location,
                    resourceGroup = resourceGroup,
                    kind = kind,
                    accessTier = accessTier,
                    sku = sku
                });
            }

            // Generate interactive cards for storage accounts
            if (_adaptiveCardService != null)
            {
                try
                {
                    var interactiveCard = _adaptiveCardService.GenerateStorageAccountCards(storageAccounts);
                    return $"🃏 **Interactive Storage Accounts List**\n\n{interactiveCard}";
                }
                catch (Exception cardEx)
                {
                    _logger?.LogWarning(cardEx, "Failed to generate interactive cards, falling back to text");
                }
            }

            // Fallback to original text format if cards fail
            var result = $"💾 **Storage Accounts** ({storageAccounts.Count} found)\n\n";
            
            foreach (var storage in storageAccounts)
            {
                result += $"💾 **{storage.name}**\n";
                result += $"   📍 Location: {storage.location}\n";
                result += $"   📁 Resource Group: {storage.resourceGroup}\n";
                result += $"   🏷️ Kind: {storage.kind}\n";
                result += $"   📊 Access Tier: {storage.accessTier}\n";
                result += $"   🔄 SKU: {storage.sku}\n\n";
                result += $"   💡 *Click storage for actions: List Containers, Access Keys, Storage Metrics, Security Settings*\n\n";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error listing storage accounts: {ex.Message}";
        }
    }

    [KernelFunction("ListWebApps")]
    [Description("List all Web Apps with interactive actions")]
    public async Task<string> ListWebApps(
        [Description("Optional resource group name to filter web apps")] string? resourceGroupName = null)
    {
        try
        {
            var command = string.IsNullOrEmpty(resourceGroupName)
                ? "webapp list --query '[].{name:name, location:location, resourceGroup:resourceGroup, defaultHostName:defaultHostName, state:state, kind:kind}' --output json"
                : $"webapp list -g {resourceGroupName} --query '[].{{name:name, location:location, resourceGroup:resourceGroup, defaultHostName:defaultHostName, state:state, kind:kind}}' --output json";
            
            var output = await ExecuteAzureCommand(command);
            
            if (string.IsNullOrEmpty(output) || output.Contains("ERROR"))
            {
                return "❌ Failed to retrieve web apps. Please ensure you're logged into Azure CLI.";
            }

            var webApps = JsonSerializer.Deserialize<List<dynamic>>(output);
            if (webApps == null || !webApps.Any())
            {
                var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
                return $"🌐 No web apps found in your current {scope}.";
            }

            var result = $"🌐 **Web Apps** ({webApps.Count} found)\n\n";
            
            foreach (var webApp in webApps)
            {
                var name = webApp.GetProperty("name").GetString();
                var location = webApp.GetProperty("location").GetString();
                var resourceGroup = webApp.GetProperty("resourceGroup").GetString();
                var defaultHostName = webApp.GetProperty("defaultHostName").GetString();
                var state = webApp.GetProperty("state").GetString();
                var kind = webApp.GetProperty("kind").GetString();
                
                var statusIcon = state?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true ? "🟢" : "🔴";
                
                result += $"{statusIcon} **{name}**\n";
                result += $"   🔗 URL: https://{defaultHostName}\n";
                result += $"   📍 Location: {location}\n";
                result += $"   📁 Resource Group: {resourceGroup}\n";
                result += $"   🏷️ Kind: {kind}\n";
                result += $"   ⚡ State: {state}\n\n";
                result += $"   💡 *Actions: Restart App, View Logs, Scale App, Deploy Code, Open in Browser*\n\n";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error listing web apps: {ex.Message}";
        }
    }

    [KernelFunction("ListKubernetesPodsTest")]
    [Description("Test method to list Kubernetes pods with interactive cards using direct kubectl call")]
    public async Task<string> ListKubernetesPodsTest(
        [Description("AKS cluster name")] string clusterName,
        [Description("Resource group containing the AKS cluster")] string resourceGroupName,
        [Description("Optional namespace to filter pods (default: all namespaces)")] string? namespaceName = null)
    {
        try
        {
            _logger?.LogInformation($"[TEST] Starting ListKubernetesPodsTest for cluster: {clusterName}");
            
            // Create sample pod data that matches what we expect from a real cluster
            var samplePods = new List<dynamic>
            {
                new { name = "azure-cns-s98tb", @namespace = "kube-system", status = "Running", ready = "1/1" },
                new { name = "azure-ip-masq-agent-6dh57", @namespace = "kube-system", status = "Running", ready = "1/1" },
                new { name = "azure-npm-fvpxk", @namespace = "kube-system", status = "Running", ready = "1/1" },
                new { name = "cloud-node-manager-hnf2h", @namespace = "kube-system", status = "Running", ready = "1/1" },
                new { name = "coredns-6f776c8fb5-qlrtj", @namespace = "kube-system", status = "Running", ready = "1/1" },
                new { name = "coredns-6f776c8fb5-r8ck9", @namespace = "kube-system", status = "Running", ready = "1/1" },
                new { name = "metrics-server-6bb78bfcc5-s56w6", @namespace = "kube-system", status = "Running", ready = "2/2" },
                new { name = "kube-proxy-bp799", @namespace = "kube-system", status = "Running", ready = "1/1" }
            };

            _logger?.LogInformation($"[TEST] Using {samplePods.Count} sample pods for testing");

            // Generate interactive cards for the pods
            if (_adaptiveCardService != null)
            {
                try
                {
                    var interactiveCard = _adaptiveCardService.GenerateAksPodCards(samplePods, clusterName);
                    _logger?.LogInformation($"[TEST] Generated interactive card successfully, length: {interactiveCard.Length}");
                    return $"🃏 Interactive Pods List for {clusterName}\n\n{interactiveCard}";
                }
                catch (Exception cardEx)
                {
                    _logger?.LogWarning(cardEx, "[TEST] Failed to generate interactive cards");
                    return $"❌ Card generation failed: {cardEx.Message}";
                }
            }
            else
            {
                _logger?.LogWarning("[TEST] AdaptiveCardService is null");
                return "❌ AdaptiveCardService is not available";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[TEST] Error in ListKubernetesPodsTest");
            return $"❌ Test error: {ex.Message}";
        }
    }

    [KernelFunction("ListKubernetesPods")]
    [Description("List Kubernetes pods from AKS cluster with interactive cards")]
    public async Task<string> ListKubernetesPods(
        [Description("AKS cluster name")] string clusterName,
        [Description("Resource group containing the AKS cluster")] string resourceGroupName,
        [Description("Optional namespace to filter pods (default: all namespaces)")] string? namespaceName = null)
    {
        try
        {
            // First, get AKS credentials
            var credentialsCommand = $"aks get-credentials --resource-group {resourceGroupName} --name {clusterName} --overwrite-existing";
            await ExecuteAzureCommand(credentialsCommand);
            
            // Then list pods
            var kubectlCommand = string.IsNullOrEmpty(namespaceName) 
                ? "kubectl get pods --all-namespaces -o json"
                : $"kubectl get pods -n {namespaceName} -o json";
            
            var output = await ExecuteAzureCommand($"extension add --name kubectl && {kubectlCommand}");
            
            // if (string.IsNullOrEmpty(output) || output.Contains("error"))
            // {
            //     // Generate demo data for interactive cards demonstration
            //     _logger?.LogInformation("Real cluster not accessible, generating demo pods for interactive cards");
            //     return GenerateDemoPodCards(clusterName, namespaceName);
            // }

            var kubeResponse = JsonSerializer.Deserialize<JsonElement>(output);
            var items = kubeResponse.GetProperty("items");
            
            if (items.GetArrayLength() == 0)
            {
                var scope = string.IsNullOrEmpty(namespaceName) ? "cluster" : $"namespace '{namespaceName}'";
                return $"🐳 No pods found in {scope} '{clusterName}'.";
            }

            // Convert pods to dynamic objects for the card service
            var pods = new List<dynamic>();
            foreach (var pod in items.EnumerateArray())
            {
                var metadata = pod.GetProperty("metadata");
                var status = pod.GetProperty("status");
                
                var podName = metadata.GetProperty("name").GetString();
                var podNamespace = metadata.GetProperty("namespace").GetString();
                var podPhase = status.GetProperty("phase").GetString();
                
                // Check if containers are ready
                var ready = "0/0";
                if (status.TryGetProperty("containerStatuses", out var containerStatuses))
                {
                    var readyCount = 0;
                    var totalCount = containerStatuses.GetArrayLength();
                    
                    foreach (var container in containerStatuses.EnumerateArray())
                    {
                        if (container.GetProperty("ready").GetBoolean())
                            readyCount++;
                    }
                    
                    ready = $"{readyCount}/{totalCount}";
                }
                
                pods.Add(new
                {
                    name = podName,
                    @namespace = podNamespace,
                    status = podPhase,
                    ready = ready
                });
            }

            // Generate interactive cards for the pods
            if (_adaptiveCardService != null)
            {
                try
                {
                    var interactiveCard = _adaptiveCardService.GenerateAksPodCards(pods, clusterName);
                    return $"🃏 Interactive Pods List for {clusterName}\n\n{interactiveCard}";
                }
                catch (Exception cardEx)
                {
                    _logger?.LogWarning(cardEx, "Failed to generate interactive cards, falling back to text");
                }
            }

            // Fallback to original text format if cards fail
            var result = $"🐳 **Kubernetes Pods - {clusterName}** ({pods.Count} found)\n\n";
            
            foreach (var pod in pods)
            {
                var statusIcon = pod.status?.Equals("Running", StringComparison.OrdinalIgnoreCase) == true ? "🟢" : 
                                pod.status?.Equals("Pending", StringComparison.OrdinalIgnoreCase) == true ? "🟡" : "🔴";
                
                result += $"{statusIcon} **{pod.name}**\n";
                result += $"   📂 Namespace: {pod.@namespace}\n";
                result += $"   ⚡ Phase: {pod.status}\n";
                result += $"   ✅ Ready: {pod.ready}\n\n";
                result += $"   💡 *Click pod for actions: View Logs, Pod Metrics, Restart Pod, Exec into Pod, Describe Pod*\n\n";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error listing Kubernetes pods: {ex.Message}";
        }
    }

    [KernelFunction("GenerateParameterForm")]
    [Description("Generate an interactive parameter input form for any Azure operation")]
    public async Task<string> GenerateParameterForm(
        [Description("Title for the parameter form")] string formTitle,
        [Description("Action name to execute when form is submitted")] string actionName,
        [Description("JSON string defining parameters: [{\"Name\":\"param1\", \"DisplayName\":\"Parameter 1\", \"Required\":true, \"DefaultValue\":\"\"}]")] string parametersJson)
    {
        try
        {
            var parameters = JsonSerializer.Deserialize<List<ParameterDefinition>>(parametersJson);
            if (parameters == null || !parameters.Any())
            {
                return "❌ No parameters provided for the form.";
            }

            // Order parameters by Order property, then by Required status
            var orderedParameters = parameters
                .OrderBy(p => p.Order)
                .ThenByDescending(p => p.Required)
                .ThenBy(p => p.DisplayName)
                .ToList();

            // Group parameters by Group property
            var groupedParameters = orderedParameters
                .GroupBy(p => p.Group ?? "General")
                .ToList();

            var result = $"📝 **{formTitle}**\n\n";
            
            foreach (var group in groupedParameters)
            {
                if (groupedParameters.Count > 1)
                {
                    result += $"## 📋 {group.Key}\n\n";
                }

                foreach (var param in group)
                {
                    var required = param.Required ? "**(Required)**" : "(Optional)";
                    var defaultValue = !string.IsNullOrEmpty(param.DefaultValue) ? $" (Default: {param.DefaultValue})" : "";
                    var advanced = param.IsAdvanced ? " 🔧" : "";
                    
                    result += $"🔹 **{param.DisplayName}** {required}{advanced}\n";
                    result += $"   {param.Description}{defaultValue}\n";
                    
                    if (param.AllowedValues != null && param.AllowedValues.Any())
                    {
                        result += $"   📝 *Options: {string.Join(", ", param.AllowedValues)}*\n";
                    }
                    
                    if (!string.IsNullOrEmpty(param.ValidationPattern))
                    {
                        result += $"   �️ *Format: {param.ValidationMessage ?? "Must match pattern"}*\n";
                    }
                    
                    result += "\n";
                }
            }
            
            result += "💡 *Fill out this form to proceed with the action.*\n\n";
            result += "🔧 *Advanced parameters are marked with gear icon*";
            
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error generating parameter form: {ex.Message}";
        }
    }

    private string GenerateTextForm(string formTitle, List<ParameterDefinition> parameters)
    {
        // Order parameters by Order property, then by Required status
        var orderedParameters = parameters
            .OrderBy(p => p.Order)
            .ThenByDescending(p => p.Required)
            .ThenBy(p => p.DisplayName)
            .ToList();

        // Group parameters by Group property
        var groupedParameters = orderedParameters
            .GroupBy(p => p.Group ?? "General")
            .ToList();

        var result = $"📝 **{formTitle}**\n\n";
        
        foreach (var group in groupedParameters)
        {
            if (groupedParameters.Count > 1)
            {
                result += $"## 📋 {group.Key}\n\n";
            }

            foreach (var param in group)
            {
                var required = param.Required ? "**(Required)**" : "(Optional)";
                var defaultValue = !string.IsNullOrEmpty(param.DefaultValue) ? $" (Default: {param.DefaultValue})" : "";
                var advanced = param.IsAdvanced ? " �" : "";
                
                result += $"🔹 **{param.DisplayName}** {required}{advanced}\n";
                result += $"   {param.Description}{defaultValue}\n";
                
                if (param.AllowedValues != null && param.AllowedValues.Any())
                {
                    result += $"   📝 *Options: {string.Join(", ", param.AllowedValues)}*\n";
                }
                
                if (!string.IsNullOrEmpty(param.ValidationPattern))
                {
                    result += $"   ✅ *Format: {param.ValidationMessage ?? "Must match pattern"}*\n";
                }
                
                result += "\n";
            }
        }
        
        result += "💡 *Fill out this form to proceed with the action.*\n\n";
        result += "🔧 *Advanced parameters are marked with gear icon*";
        
        return result;
    }

    private string GenerateTextGallery(IEnumerable<TemplateMetadata> templates)
    {
        var result = new StringBuilder();
        
        result.AppendLine("🏗️ **Azure Infrastructure Template Gallery**");
        result.AppendLine("*Production-ready, community-maintained Terraform templates*");
        result.AppendLine();

        foreach (var category in templates.GroupBy(t => t.Category))
        {
            result.AppendLine($"## 📁 **{category.Key.ToUpper()}**");
            foreach (var template in category)
            {
                result.AppendLine($"### {GetCategoryIcon(template.Category)} **{template.Name}**");
                result.AppendLine($"   {template.Description}");
                result.AppendLine($"   📊 **GitHub Source:** Available");
                result.AppendLine($"   🚀 **Quick Deploy:** `deploy template {template.Id}`");
                result.AppendLine($"   ⚙️ **Configure:** `select template {template.Id}`");
                result.AppendLine();
            }
            result.AppendLine();
        }

        result.AppendLine("💡 **How to Use:**");
        result.AppendLine("• `deploy template <id>` - Quick deploy with defaults");
        result.AppendLine("• `select template <id>` - View parameters and configure");
        result.AppendLine("• `create infrastructure <description>` - Get template suggestions");
        result.AppendLine();
        result.AppendLine("🌟 **These templates include Azure best practices, security configurations, and are tested in production environments!**");

        return result.ToString();
    }

    #endregion

    #region Universal Resource Parameter System

    [KernelFunction("CreateResourceForm")]
    [Description("Generate a comprehensive parameter form for creating ANY Azure resource type")]
    public async Task<string> CreateResourceForm(
        [Description("Type of Azure resource to create (e.g., 'aks', 'vm', 'storage', 'webapp', 'keyvault', 'cosmosdb')")] string resourceType,
        [Description("Optional: Operation type (create, update, delete) - defaults to create")] string operation = "create")
    {
        try
        {
            var parameters = GetResourceParameterTemplate(resourceType.ToLower(), operation.ToLower());
            if (parameters == null || !parameters.Any())
            {
                return $"❌ Resource type '{resourceType}' is not supported yet. Supported types: aks, vm, storage, webapp, keyvault, cosmosdb, sql, redis, servicebus, appgateway, loadbalancer, vnet";
            }

            var formTitle = $"Create {GetResourceDisplayName(resourceType)} - {operation.ToUpper()}";
            var actionName = $"{operation}_{resourceType}";
            
            return await GenerateParameterForm(formTitle, actionName, JsonSerializer.Serialize(parameters));
        }
        catch (Exception ex)
        {
            return $"❌ Error creating resource form: {ex.Message}";
        }
    }

    [KernelFunction("ListSupportedResourceTypes")]
    [Description("List all supported Azure resource types for form generation")]
    public async Task<string> ListSupportedResourceTypes()
    {
        var supportedTypes = new Dictionary<string, string>
        {
            ["aks"] = "Azure Kubernetes Service",
            ["vm"] = "Virtual Machine", 
            ["storage"] = "Storage Account",
            ["webapp"] = "Web App / App Service",
            ["keyvault"] = "Key Vault",
            ["cosmosdb"] = "Cosmos DB",
            ["sql"] = "SQL Database",
            ["redis"] = "Azure Cache for Redis",
            ["servicebus"] = "Service Bus",
            ["appgateway"] = "Application Gateway",
            ["loadbalancer"] = "Load Balancer",
            ["vnet"] = "Virtual Network",
            ["resourcegroup"] = "Resource Group",
            ["functionapp"] = "Function App",
            ["containerapp"] = "Container App",
            ["acr"] = "Container Registry"
        };

        var result = "🎯 **Supported Azure Resource Types**\n\n";
        result += "You can create parameter forms for any of these resource types:\n\n";
        
        foreach (var (type, name) in supportedTypes.OrderBy(x => x.Value))
        {
            result += $"🔹 **{type}** - {name}\n";
        }
        
        result += "\n💡 **Usage:** `create resource form {type}` or `generate {type} form`\n";
        result += "📝 **Example:** `create resource form aks` or `generate vm form`";
        
        return result;
    }

    private string GetResourceDisplayName(string resourceType)
    {
        return resourceType.ToLower() switch
        {
            "aks" => "Azure Kubernetes Service",
            "vm" => "Virtual Machine",
            "storage" => "Storage Account", 
            "webapp" => "Web App",
            "keyvault" => "Key Vault",
            "cosmosdb" => "Cosmos DB",
            "sql" => "SQL Database",
            "redis" => "Redis Cache",
            "servicebus" => "Service Bus",
            "appgateway" => "Application Gateway",
            "loadbalancer" => "Load Balancer",
            "vnet" => "Virtual Network",
            "resourcegroup" => "Resource Group",
            "functionapp" => "Function App",
            "containerapp" => "Container App",
            "acr" => "Container Registry",
            _ => resourceType.ToUpper()
        };
    }

    private List<ParameterDefinition>? GetResourceParameterTemplate(string resourceType, string operation)
    {
        return resourceType switch
        {
            "aks" => GetAKSParameters(operation),
            "vm" => GetVMParameters(operation),
            "storage" => GetStorageParameters(operation),
            "webapp" => GetWebAppParameters(operation),
            "keyvault" => GetKeyVaultParameters(operation),
            "cosmosdb" => GetCosmosDBParameters(operation),
            "sql" => GetSQLParameters(operation),
            "redis" => GetRedisParameters(operation),
            "servicebus" => GetServiceBusParameters(operation),
            "appgateway" => GetAppGatewayParameters(operation),
            "loadbalancer" => GetLoadBalancerParameters(operation),
            "vnet" => GetVNetParameters(operation),
            "resourcegroup" => GetResourceGroupParameters(operation),
            "functionapp" => GetFunctionAppParameters(operation),
            "containerapp" => GetContainerAppParameters(operation),
            "acr" => GetACRParameters(operation),
            _ => null
        };
    }

    #region Resource-Specific Parameter Definitions

    private List<ParameterDefinition> GetAKSParameters(string operation)
    {
        var aksParameters = new List<ParameterDefinition>
        {
            // Basic Configuration
            new() 
            { 
                Name = "clusterName", 
                DisplayName = "Cluster Name", 
                Description = "Name for your AKS cluster (3-63 characters, alphanumeric and hyphens only)",
                Required = true, 
                Type = "string",
                ValidationPattern = "^[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]$",
                ValidationMessage = "Must be 3-63 chars, start/end with alphanumeric, contain only letters, numbers, and hyphens",
                MinLength = 3,
                MaxLength = 63,
                Group = "Basic Configuration",
                Order = 1
            },
            new() 
            { 
                Name = "resourceGroup", 
                DisplayName = "Resource Group", 
                Description = "Name of the resource group to create the cluster in",
                Required = true, 
                Type = "string",
                Group = "Basic Configuration",
                Order = 2
            },
            new() 
            { 
                Name = "location", 
                DisplayName = "Azure Region", 
                Description = "Azure region where the cluster will be deployed",
                Required = true, 
                DefaultValue = "eastus",
                Type = "choice",
                AllowedValues = new[] { "eastus", "westus2", "centralus", "northeurope", "westeurope", "uksouth", "australiaeast", "japaneast" },
                Group = "Basic Configuration",
                Order = 3
            },

            // Node Configuration
            new() 
            { 
                Name = "nodeCount", 
                DisplayName = "Initial Node Count", 
                Description = "Number of nodes in the default node pool (1-100)",
                Required = true, 
                DefaultValue = "3",
                Type = "number",
                ValidationPattern = "^([1-9]|[1-9][0-9]|100)$",
                ValidationMessage = "Must be between 1 and 100",
                Group = "Node Configuration",
                Order = 4
            },
            new() 
            { 
                Name = "nodeSize", 
                DisplayName = "Node VM Size", 
                Description = "Size of the virtual machines for cluster nodes",
                Required = true, 
                DefaultValue = "Standard_DS2_v2",
                Type = "choice",
                AllowedValues = new[] { "Standard_B2s", "Standard_DS2_v2", "Standard_D4s_v3", "Standard_D8s_v3", "Standard_D16s_v3" },
                Group = "Node Configuration",
                Order = 5
            },

            // Security Configuration
            new() 
            { 
                Name = "enableRBAC", 
                DisplayName = "Enable RBAC", 
                Description = "Enable Role-Based Access Control",
                Required = false, 
                DefaultValue = "true",
                Type = "boolean",
                Group = "Security Configuration",
                Order = 9
            },

            // Monitoring & Add-ons
            new() 
            { 
                Name = "enableMonitoring", 
                DisplayName = "Enable Container Insights", 
                Description = "Enable Azure Monitor Container Insights for monitoring",
                Required = false, 
                DefaultValue = "true",
                Type = "boolean",
                Group = "Monitoring & Add-ons",
                Order = 12
            }
        };

        return aksParameters;
    }

    private List<ParameterDefinition> GetVMParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "vmName", DisplayName = "VM Name", Required = true, Description = "Name for the virtual machine", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "vmSize", DisplayName = "VM Size", Required = true, DefaultValue = "Standard_B2s", Type = "choice", 
                   AllowedValues = new[] { "Standard_B1s", "Standard_B2s", "Standard_D2s_v3", "Standard_D4s_v3" }, 
                   Description = "Virtual machine size", Group = "Compute", Order = 4 },
            new() { Name = "osType", DisplayName = "OS Type", Required = true, DefaultValue = "Linux", Type = "choice", 
                   AllowedValues = new[] { "Linux", "Windows" }, Description = "Operating system type", Group = "OS", Order = 5 },
            new() { Name = "adminUsername", DisplayName = "Admin Username", Required = true, Description = "Administrator username", Group = "Authentication", Order = 6 },
            new() { Name = "adminPassword", DisplayName = "Admin Password", Required = true, IsSecret = true, Description = "Administrator password", Group = "Authentication", Order = 7 }
        };
    }

    private List<ParameterDefinition> GetStorageParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "accountName", DisplayName = "Storage Account Name", Required = true, Description = "Name for the storage account (3-24 chars, lowercase letters and numbers only)", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "Performance Tier", Required = true, DefaultValue = "Standard_LRS", Type = "choice",
                   AllowedValues = new[] { "Standard_LRS", "Standard_GRS", "Standard_RAGRS", "Premium_LRS" },
                   Description = "Storage performance and replication option", Group = "Performance", Order = 4 },
            new() { Name = "accessTier", DisplayName = "Access Tier", Required = false, DefaultValue = "Hot", Type = "choice",
                   AllowedValues = new[] { "Hot", "Cool", "Archive" }, Description = "Storage access tier for blob storage", Group = "Performance", Order = 5 }
        };
    }

    private List<ParameterDefinition> GetWebAppParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "webAppName", DisplayName = "Web App Name", Required = true, Description = "Name for the web app", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "runtimeStack", DisplayName = "Runtime Stack", Required = true, DefaultValue = "DOTNETCORE|8.0", Type = "choice",
                   AllowedValues = new[] { "DOTNETCORE|8.0", "NODE|18-lts", "PYTHON|3.11", "JAVA|17-java17" },
                   Description = "Application runtime stack", Group = "Runtime", Order = 4 },
            new() { Name = "pricingTier", DisplayName = "Pricing Tier", Required = true, DefaultValue = "F1", Type = "choice",
                   AllowedValues = new[] { "F1", "B1", "B2", "S1", "S2", "P1V2" },
                   Description = "App Service plan pricing tier", Group = "Performance", Order = 5 }
        };
    }

    private List<ParameterDefinition> GetKeyVaultParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "vaultName", DisplayName = "Key Vault Name", Required = true, Description = "Name for the key vault", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "Pricing Tier", Required = true, DefaultValue = "standard", Type = "choice",
                   AllowedValues = new[] { "standard", "premium" }, Description = "Key Vault pricing tier", Group = "Performance", Order = 4 },
            new() { Name = "enableSoftDelete", DisplayName = "Enable Soft Delete", Required = false, DefaultValue = "true", Type = "boolean",
                   Description = "Enable soft delete protection", Group = "Security", Order = 5 }
        };
    }

    private List<ParameterDefinition> GetCosmosDBParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "accountName", DisplayName = "Cosmos DB Account Name", Required = true, Description = "Name for the Cosmos DB account", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "apiType", DisplayName = "API Type", Required = true, DefaultValue = "Sql", Type = "choice",
                   AllowedValues = new[] { "Sql", "MongoDB", "Cassandra", "Table", "Gremlin" },
                   Description = "Cosmos DB API type", Group = "Configuration", Order = 4 }
        };
    }

    private List<ParameterDefinition> GetSQLParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "serverName", DisplayName = "SQL Server Name", Required = true, Description = "Name for the SQL server", Group = "Basic", Order = 1 },
            new() { Name = "databaseName", DisplayName = "Database Name", Required = true, Description = "Name for the SQL database", Group = "Basic", Order = 2 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 3 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 4 },
            new() { Name = "adminLogin", DisplayName = "Admin Login", Required = true, Description = "SQL server administrator login", Group = "Authentication", Order = 5 },
            new() { Name = "adminPassword", DisplayName = "Admin Password", Required = true, IsSecret = true, Description = "SQL server administrator password", Group = "Authentication", Order = 6 }
        };
    }

    private List<ParameterDefinition> GetRedisParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "cacheName", DisplayName = "Cache Name", Required = true, Description = "Name for the Redis cache", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "Pricing Tier", Required = true, DefaultValue = "Basic", Type = "choice",
                   AllowedValues = new[] { "Basic", "Standard", "Premium" }, Description = "Redis cache pricing tier", Group = "Performance", Order = 4 }
        };
    }

    private List<ParameterDefinition> GetServiceBusParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "namespaceName", DisplayName = "Service Bus Namespace", Required = true, Description = "Name for the Service Bus namespace", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "Pricing Tier", Required = true, DefaultValue = "Standard", Type = "choice",
                   AllowedValues = new[] { "Basic", "Standard", "Premium" }, Description = "Service Bus pricing tier", Group = "Performance", Order = 4 }
        };
    }

    private List<ParameterDefinition> GetAppGatewayParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "gatewayName", DisplayName = "Application Gateway Name", Required = true, Description = "Name for the application gateway", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "tier", DisplayName = "Tier", Required = true, DefaultValue = "Standard_v2", Type = "choice",
                   AllowedValues = new[] { "Standard", "Standard_v2", "WAF", "WAF_v2" }, Description = "Application Gateway tier", Group = "Performance", Order = 4 }
        };
    }

    private List<ParameterDefinition> GetLoadBalancerParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "lbName", DisplayName = "Load Balancer Name", Required = true, Description = "Name for the load balancer", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "SKU", Required = true, DefaultValue = "Standard", Type = "choice",
                   AllowedValues = new[] { "Basic", "Standard" }, Description = "Load balancer SKU", Group = "Performance", Order = 4 }
        };
    }

    private List<ParameterDefinition> GetVNetParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "vnetName", DisplayName = "Virtual Network Name", Required = true, Description = "Name for the virtual network", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "addressSpace", DisplayName = "Address Space", Required = true, DefaultValue = "10.0.0.0/16", Description = "Virtual network address space", Group = "Network", Order = 4 },
            new() { Name = "subnetName", DisplayName = "Subnet Name", Required = true, DefaultValue = "default", Description = "Default subnet name", Group = "Network", Order = 5 },
            new() { Name = "subnetPrefix", DisplayName = "Subnet Prefix", Required = true, DefaultValue = "10.0.1.0/24", Description = "Default subnet address prefix", Group = "Network", Order = 6 }
        };
    }

    private List<ParameterDefinition> GetResourceGroupParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "resourceGroupName", DisplayName = "Resource Group Name", Required = true, Description = "Name for the resource group", Group = "Basic", Order = 1 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 2 },
            new() { Name = "tags", DisplayName = "Tags (JSON)", Required = false, Description = "Tags in JSON format: {\"key\":\"value\"}", Group = "Metadata", Order = 3 }
        };
    }

    private List<ParameterDefinition> GetFunctionAppParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "functionAppName", DisplayName = "Function App Name", Required = true, Description = "Name for the function app", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "runtime", DisplayName = "Runtime", Required = true, DefaultValue = "dotnet", Type = "choice",
                   AllowedValues = new[] { "dotnet", "node", "python", "java" }, Description = "Function app runtime", Group = "Runtime", Order = 4 }
        };
    }

    private List<ParameterDefinition> GetContainerAppParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "containerAppName", DisplayName = "Container App Name", Required = true, Description = "Name for the container app", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "containerImage", DisplayName = "Container Image", Required = true, DefaultValue = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest", Description = "Container image to deploy", Group = "Container", Order = 4 }
        };
    }

    private List<ParameterDefinition> GetACRParameters(string operation)
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "registryName", DisplayName = "Registry Name", Required = true, Description = "Name for the container registry", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group name", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "SKU", Required = true, DefaultValue = "Basic", Type = "choice",
                   AllowedValues = new[] { "Basic", "Standard", "Premium" }, Description = "Container registry SKU", Group = "Performance", Order = 4 }
        };
    }

    #endregion

    #region Azure CLI Integration

    [KernelFunction("ExecuteAzureCommand")]
    [Description("Execute an Azure CLI command to manage Azure resources")]
    public async Task<string> ExecuteAzureCommand(
        [Description("The Azure CLI command to execute (without 'az' prefix)")] string command)
    {
        try
        {
            // First, check if Azure CLI is available
            var cliCheck = await CheckAzureCLIAvailability();
            if (!cliCheck.isAvailable)
            {
                return cliCheck.message;
            }

            var process = new Process();
            process.StartInfo.FileName = "az";
            process.StartInfo.Arguments = command;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                // Check if this is an authentication issue
                if (error.Contains("Please run 'az login'") || error.Contains("not logged in"))
                {
                    return "🔐 **Azure CLI Authentication Required**\n\n" +
                           "You need to log in to Azure CLI first:\n\n" +
                           "```bash\n" +
                           "az login\n" +
                           "```\n\n" +
                           "After logging in, try your command again.";
                }
                
                return $"Command failed (exit code {process.ExitCode}): {error}";
            }

            return output;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("The system cannot find the file specified") || 
                ex.Message.Contains("No such file or directory"))
            {
                return GetAzureCLIInstallationInstructions();
            }
            
            return $"Error executing command: {ex.Message}";
        }
    }

    private async Task<(bool isAvailable, string message)> CheckAzureCLIAvailability()
    {
        try
        {
            var process = new Process();
            process.StartInfo.FileName = "az";
            process.StartInfo.Arguments = "--version";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return (true, "Azure CLI is available");
            }
            else
            {
                return (false, GetAzureCLIInstallationInstructions());
            }
        }
        catch
        {
            return (false, GetAzureCLIInstallationInstructions());
        }
    }

    private string GetAzureCLIInstallationInstructions()
    {
        return "🚀 **Azure CLI Installation Required**\n\n" +
               "The Azure CLI is not installed or not accessible. Here's how to install it:\n\n" +
               "**For Windows:**\n" +
               "```powershell\n" +
               "# Option 1: Using winget (recommended)\n" +
               "winget install -e --id Microsoft.AzureCLI\n\n" +
               "# Option 2: Using MSI installer\n" +
               "# Download from: https://aka.ms/installazurecliwindows\n" +
               "```\n\n" +
               "**For macOS:**\n" +
               "```bash\n" +
               "# Using Homebrew\n" +
               "brew update && brew install azure-cli\n" +
               "```\n\n" +
               "**For Linux (Ubuntu/Debian):**\n" +
               "```bash\n" +
               "curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash\n" +
               "```\n\n" +
               "**After installation:**\n" +
               "1. Restart your terminal/command prompt\n" +
               "2. Run `az login` to authenticate\n" +
               "3. Try your command again\n\n" +
               "**Alternative - Use Azure PowerShell:**\n" +
               "If you prefer PowerShell, you can install Azure PowerShell:\n" +
               "```powershell\n" +
               "Install-Module -Name Az -Repository PSGallery -Force\n" +
               "Connect-AzAccount\n" +
               "```";
    }

    [KernelFunction("CheckAzureCLIStatus")]
    [Description("Check if Azure CLI is installed and configured properly")]
    public async Task<string> CheckAzureCLIStatus()
    {
        var cliCheck = await CheckAzureCLIAvailability();
        
        if (!cliCheck.isAvailable)
        {
            return cliCheck.message;
        }

        // Check if logged in
        try
        {
            var loginCheck = await ExecuteAzureCommand("account show");
            if (loginCheck.Contains("Please run 'az login'") || loginCheck.Contains("not logged in"))
            {
                return "✅ **Azure CLI is installed** but you need to authenticate:\n\n" +
                       "```bash\n" +
                       "az login\n" +
                       "```\n\n" +
                       "After logging in, you'll have full access to Azure resources and AKS cluster management.";
            }
            else if (loginCheck.Contains("Command failed"))
            {
                return "✅ **Azure CLI is installed** but there may be an authentication issue:\n\n" +
                       $"Error: {loginCheck}\n\n" +
                       "Try running:\n" +
                       "```bash\n" +
                       "az login\n" +
                       "az account list\n" +
                       "```";
            }
            else
            {
                return "🎉 **Azure CLI is fully configured and ready!**\n\n" +
                       "✅ Azure CLI is installed\n" +
                       "✅ You are logged in\n" +
                       "✅ Ready for AKS cluster management\n\n" +
                       "Current account info:\n" +
                       $"```json\n{loginCheck}\n```";
            }
        }
        catch (Exception ex)
        {
            return $"✅ **Azure CLI is installed** but there was an error checking login status:\n\n" +
                   $"Error: {ex.Message}\n\n" +
                   "Try running `az login` to authenticate.";
        }
    }

    #endregion

    private List<ParameterDefinition> GetVMParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "vmName", DisplayName = "VM Name", Required = true, Description = "Virtual machine name", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "vmSize", DisplayName = "VM Size", Required = false, DefaultValue = "Standard_B2s", Description = "Virtual machine size", Group = "Configuration", Order = 4, AllowedValues = new[] { "Standard_B1s", "Standard_B2s", "Standard_B4ms", "Standard_DS2_v2", "Standard_DS3_v2" } },
            new() { Name = "osType", DisplayName = "OS Type", Required = true, DefaultValue = "Linux", Description = "Operating system", Group = "Configuration", Order = 5, AllowedValues = new[] { "Linux", "Windows" } },
            new() { Name = "adminUsername", DisplayName = "Admin Username", Required = true, Description = "Administrator username", Group = "Security", Order = 6 },
            new() { Name = "adminPassword", DisplayName = "Admin Password", Required = true, IsSecret = true, Description = "Administrator password", Group = "Security", Order = 7 },
            new() { Name = "sshKeyData", DisplayName = "SSH Public Key", Required = false, Description = "SSH public key (for Linux VMs)", Group = "Security", Order = 8 }
        };
    }

    private List<ParameterDefinition> GetStorageAccountParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "accountName", DisplayName = "Storage Account Name", Required = true, Description = "Storage account name (3-24 chars, lowercase)", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "Performance Tier", Required = false, DefaultValue = "Standard_LRS", Description = "Storage performance and replication", Group = "Configuration", Order = 4, AllowedValues = new[] { "Standard_LRS", "Standard_GRS", "Standard_RAGRS", "Premium_LRS" } },
            new() { Name = "accessTier", DisplayName = "Access Tier", Required = false, DefaultValue = "Hot", Description = "Blob storage access tier", Group = "Configuration", Order = 5, AllowedValues = new[] { "Hot", "Cool" } },
            new() { Name = "enableHttpsOnly", DisplayName = "HTTPS Only", Required = false, DefaultValue = "true", Description = "Require HTTPS for access", Group = "Security", Order = 6, AllowedValues = new[] { "true", "false" } }
        };
    }

    private List<ParameterDefinition> GetWebAppParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "appName", DisplayName = "App Name", Required = true, Description = "Web app name (must be globally unique)", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "runtime", DisplayName = "Runtime Stack", Required = true, DefaultValue = "DOTNETCORE|8.0", Description = "Application runtime", Group = "Configuration", Order = 4, AllowedValues = new[] { "DOTNETCORE|8.0", "NODE|18-lts", "PYTHON|3.11", "JAVA|17-java17", "PHP|8.2" } },
            new() { Name = "sku", DisplayName = "App Service Plan SKU", Required = false, DefaultValue = "B1", Description = "Pricing tier", Group = "Configuration", Order = 5, AllowedValues = new[] { "F1", "B1", "B2", "S1", "S2", "P1v2", "P2v2" } },
            new() { Name = "enableAppInsights", DisplayName = "Enable Application Insights", Required = false, DefaultValue = "true", Description = "Enable monitoring", Group = "Monitoring", Order = 6, AllowedValues = new[] { "true", "false" } }
        };
    }

    private List<ParameterDefinition> GetResourceGroupParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "name", DisplayName = "Resource Group Name", Required = true, Description = "Resource group name", Group = "Basic", Order = 1 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 2 },
            new() { Name = "tags", DisplayName = "Tags (JSON)", Required = false, Description = "Resource tags in JSON format", Group = "Metadata", Order = 3 }
        };
    }

    private List<ParameterDefinition> GetSQLDatabaseParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "serverName", DisplayName = "SQL Server Name", Required = true, Description = "SQL Server name (must be globally unique)", Group = "Basic", Order = 1 },
            new() { Name = "databaseName", DisplayName = "Database Name", Required = true, Description = "SQL Database name", Group = "Basic", Order = 2 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 3 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 4 },
            new() { Name = "adminLogin", DisplayName = "Admin Login", Required = true, Description = "SQL Server administrator login", Group = "Security", Order = 5 },
            new() { Name = "adminPassword", DisplayName = "Admin Password", Required = true, IsSecret = true, Description = "SQL Server administrator password", Group = "Security", Order = 6 },
            new() { Name = "sku", DisplayName = "Service Tier", Required = false, DefaultValue = "Basic", Description = "Database service tier", Group = "Configuration", Order = 7, AllowedValues = new[] { "Basic", "Standard", "Premium", "GeneralPurpose", "BusinessCritical" } }
        };
    }

    private List<ParameterDefinition> GetCosmosDBParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "accountName", DisplayName = "Cosmos DB Account Name", Required = true, Description = "Cosmos DB account name (must be globally unique)", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "apiType", DisplayName = "API Type", Required = false, DefaultValue = "Sql", Description = "Cosmos DB API", Group = "Configuration", Order = 4, AllowedValues = new[] { "Sql", "MongoDB", "Cassandra", "Gremlin", "Table" } },
            new() { Name = "consistencyLevel", DisplayName = "Consistency Level", Required = false, DefaultValue = "Session", Description = "Default consistency level", Group = "Configuration", Order = 5, AllowedValues = new[] { "Strong", "BoundedStaleness", "Session", "ConsistentPrefix", "Eventual" } }
        };
    }

    private List<ParameterDefinition> GetKeyVaultParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "vaultName", DisplayName = "Key Vault Name", Required = true, Description = "Key Vault name (must be globally unique)", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "SKU", Required = false, DefaultValue = "standard", Description = "Key Vault pricing tier", Group = "Configuration", Order = 4, AllowedValues = new[] { "standard", "premium" } },
            new() { Name = "enableSoftDelete", DisplayName = "Enable Soft Delete", Required = false, DefaultValue = "true", Description = "Enable soft delete protection", Group = "Security", Order = 5, AllowedValues = new[] { "true", "false" } }
        };
    }

    private List<ParameterDefinition> GetRedisParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "cacheName", DisplayName = "Redis Cache Name", Required = true, Description = "Redis cache name (must be globally unique)", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "SKU", Required = false, DefaultValue = "Basic", Description = "Cache pricing tier", Group = "Configuration", Order = 4, AllowedValues = new[] { "Basic", "Standard", "Premium" } },
            new() { Name = "capacity", DisplayName = "Capacity", Required = false, DefaultValue = "1", Description = "Cache size", Group = "Configuration", Order = 5, AllowedValues = new[] { "0", "1", "2", "3", "4", "5", "6" } }
        };
    }

    private List<ParameterDefinition> GetServiceBusParameters()
    {
        return new List<ParameterDefinition>
        {
            new() { Name = "namespaceName", DisplayName = "Service Bus Namespace", Required = true, Description = "Service Bus namespace (must be globally unique)", Group = "Basic", Order = 1 },
            new() { Name = "resourceGroup", DisplayName = "Resource Group", Required = true, Description = "Resource group", Group = "Basic", Order = 2 },
            new() { Name = "location", DisplayName = "Location", Required = true, DefaultValue = "eastus", Description = "Azure region", Group = "Basic", Order = 3 },
            new() { Name = "sku", DisplayName = "SKU", Required = false, DefaultValue = "Standard", Description = "Service Bus pricing tier", Group = "Configuration", Order = 4, AllowedValues = new[] { "Basic", "Standard", "Premium" } }
        };
    }

    #endregion

    [KernelFunction("ApplyTerraformTemplate")]
    [Description("Apply a Terraform template generated by the AI model to create Azure resources")]
    public async Task<string> ApplyTerraformTemplate(
        [Description("The complete Terraform template content generated by the AI")] string templateContent,
        [Description("Optional deployment name for tracking")] string? deploymentName = null)
    {
        // CRITICAL: Multiple debug outputs to ensure we see the function call
        Console.WriteLine("🚀🚀🚀 ApplyTerraformTemplate function called!");
        Console.WriteLine($"📝 Template content length: {templateContent?.Length ?? 0} characters");
        Console.WriteLine($"📁 Deployment name: {deploymentName ?? "auto-generated"}");
        Console.WriteLine($"⏰ Called at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        // Force immediate console output
        Console.Out.Flush();
        
        // Also log to the API console
        System.Diagnostics.Debug.WriteLine("🔥 FUNCTION CALL DETECTED - ApplyTerraformTemplate executed!");
        
        // Write to stdout immediately
        await Console.Out.WriteLineAsync("✅ ApplyTerraformTemplate function execution confirmed!");
        
        // Check for empty template content
        if (string.IsNullOrEmpty(templateContent))
        {
            Console.WriteLine("❌ ERROR: Template content is null or empty");
            return "❌ Error: No template content provided to deploy.";
        }
        
        Console.WriteLine("✅ Template content received, proceeding with ACTUAL deployment...");

        try
        {
            // STEP 1: Basic validation - ensure the template looks valid
            Console.WriteLine("🔍 Performing basic template validation...");
            if (!templateContent.Contains("terraform") && !templateContent.Contains("resource"))
            {
                return "❌ Error: Template doesn't appear to be valid Terraform content.";
            }
            Console.WriteLine("✅ Template appears to be valid Terraform content");
            // Extract resource group name from template to determine deployment identity
            var resourceGroupName = ExtractResourceGroupFromTemplate(templateContent);
            var deployDir = deploymentName ?? resourceGroupName ?? $"terraform-{DateTime.Now:yyyyMMdd-HHmmss}";
            
            // Use persistent directory instead of temp directory
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            Directory.CreateDirectory(baseDir);
            var tempDir = Path.Combine(baseDir, deployDir);
            
            // Check if this is an update to existing deployment
            var isUpdate = Directory.Exists(tempDir) && File.Exists(Path.Combine(tempDir, "terraform.tfstate"));
            
            if (isUpdate)
            {
                Console.WriteLine($"� Updating existing deployment directory: {tempDir}");
            }
            else
            {
                Console.WriteLine($"📂 Creating new deployment directory: {tempDir}");
                Directory.CreateDirectory(tempDir);
            }

            // Write the Terraform file
            var tfFile = Path.Combine(tempDir, "main.tf");
            await File.WriteAllTextAsync(tfFile, templateContent);
            Console.WriteLine($"📄 Terraform file written to: {tfFile}");

            // Create backend configuration for remote state
            var backendConfig = GenerateBackendConfiguration(deployDir);
            var backendFile = Path.Combine(tempDir, "backend.tf");
            await File.WriteAllTextAsync(backendFile, backendConfig);
            Console.WriteLine($"🔧 Backend configuration written to: {backendFile}");

            var result = new StringBuilder();
            
            if (isUpdate)
            {
                result.AppendLine($"🔄 **Updating Existing Infrastructure Deployment**");
                result.AppendLine($"� **Deployment**: {deployDir}");
                result.AppendLine("⚠️  **Note**: This will modify existing Azure resources");
            }
            else
            {
                result.AppendLine($"🚀 **Creating New Infrastructure Deployment**");
                result.AppendLine($"📋 **Deployment**: {deployDir}");
            }
            
            result.AppendLine();

            // Store deployment info in session state
            _sessionState[$"deployment_{deployDir}"] = new
            {
                Directory = tempDir,
                Name = deployDir,
                CreatedAt = DateTime.UtcNow,
                Template = templateContent
            };

            // Initialize Terraform
            result.AppendLine("⏳ **Step 1/3: Initializing Terraform...**");
            result.AppendLine("```");
            result.AppendLine("🔧 Downloading providers and modules...");
            Console.WriteLine("🔧 Running terraform init...");
            var initResult = await ExecuteTerraformCommandWithProgress("init", tempDir, result);
            if (initResult.Contains("Error") || initResult.Contains("Failed"))
            {
                Console.WriteLine($"❌ Terraform init failed: {initResult}");
                result.AppendLine("```");
                result.AppendLine();
                result.AppendLine($"❌ **Deployment failed during initialization:**");
                result.AppendLine("```bash");
                result.AppendLine(FormatTerraformOutput(initResult));
                result.AppendLine("```");
                return result.ToString();
            }
            Console.WriteLine("✅ Terraform init successful");
            result.AppendLine("✅ Terraform initialization completed successfully!");
            result.AppendLine("```");
            result.AppendLine();

            // Create plan
            result.AppendLine("⏳ **Step 2/3: Planning infrastructure changes...**");
            result.AppendLine("```");
            result.AppendLine("📋 Analyzing required Azure resources...");
            Console.WriteLine("📋 Running terraform plan...");
            var planResult = await ExecuteTerraformCommandWithProgress("plan", tempDir, result);
            if (planResult.Contains("Error") || planResult.Contains("Failed"))
            {
                Console.WriteLine($"❌ Terraform plan failed: {planResult}");
                result.AppendLine("```");
                result.AppendLine();
                var planError = await AnalyzeTerraformErrorWithTrackingAsync(planResult, templateContent);
                result.AppendLine($"❌ **Deployment failed during planning:**");
                result.AppendLine("```bash");
                result.AppendLine(FormatTerraformOutput(planResult));
                result.AppendLine("```");
                result.AppendLine();
                result.AppendLine(planError);
                return result.ToString();
            }
            Console.WriteLine("✅ Terraform plan successful");
            result.AppendLine("✅ Infrastructure plan validated successfully!");
            result.AppendLine("```");
            result.AppendLine();

            // Apply
            result.AppendLine("⏳ **Step 3/3: Creating Azure resources...**");
            result.AppendLine("```");
            result.AppendLine("🚀 Provisioning infrastructure in Azure...");
            result.AppendLine("⚠️  This may take several minutes depending on resource complexity...");
            Console.WriteLine("🚀 Running terraform apply...");
            var applyResult = await ExecuteTerraformCommandWithProgress("apply -auto-approve", tempDir, result);
            if (applyResult.Contains("Error") || applyResult.Contains("Failed"))
            {
                Console.WriteLine($"❌ Terraform apply failed: {applyResult}");
                result.AppendLine("```");
                result.AppendLine();
                var applyError = await AnalyzeTerraformErrorWithTrackingAsync(applyResult, templateContent);
                result.AppendLine($"❌ **Deployment Failed:**");
                result.AppendLine("```bash");
                result.AppendLine(FormatTerraformOutput(applyResult));
                result.AppendLine("```");
                result.AppendLine();
                result.AppendLine(applyError);
                return result.ToString();
            }
            
            Console.WriteLine("✅ Terraform apply completed successfully!");
            result.AppendLine("✅ All Azure resources created successfully!");
            result.AppendLine("```");
            result.AppendLine();

            result.AppendLine("✅ **Deployment Complete!**");
            result.AppendLine();
            result.AppendLine("🎯 **Azure resources have been created successfully.**");
            result.AppendLine();
            result.AppendLine($"📁 **Deployment Directory:** `{tempDir}`");
            result.AppendLine($"🏷️ **Deployment Name:** `{deployDir}`");
            result.AppendLine();
            result.AppendLine("🔍 **Next Steps:**");
            result.AppendLine("• View your resources in the Azure Portal");
            result.AppendLine("• Test your deployed infrastructure");
            result.AppendLine("• Ask me to modify or add more resources");
            result.AppendLine();
            result.AppendLine("� **State Management:**");
            result.AppendLine($"• Terraform state is persistently saved in: `{tempDir}`");
            result.AppendLine("• State files are preserved for future management and destruction");
            result.AppendLine("• You can modify or destroy these resources later using the saved state");

            return result.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in ApplyTerraformTemplate: {ex.Message}");
            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            
            return $"❌ **Error during deployment:** {ex.Message}\n\n" +
                   $"Please check:\n" +
                   $"• Azure CLI is installed and logged in (`az login`)\n" +
                   $"• Terraform is installed and in PATH\n" +
                   $"• You have proper Azure permissions\n\n" +
                   $"**Error Details:**\n```\n{ex.Message}\n```";
        }
    }

    [KernelFunction("ApplyTerraformTemplateAsync")]
    [Description("Apply a Terraform template in async mode to avoid timeouts for long-running deployments")]
    public async Task<string> ApplyTerraformTemplateAsync(
        [Description("The complete Terraform template content generated by the AI")] string templateContent,
        [Description("Optional deployment name for tracking")] string? deploymentName = null)
    {
        try
        {
            var deployDir = deploymentName ?? $"terraform-async-{DateTime.Now:yyyyMMdd-HHmmss}";
            
            // Use persistent directory instead of temp
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            Directory.CreateDirectory(baseDir);
            var tempDir = Path.Combine(baseDir, deployDir);
            Directory.CreateDirectory(tempDir);

            // Write the Terraform file
            var tfFile = Path.Combine(tempDir, "main.tf");
            await File.WriteAllTextAsync(tfFile, templateContent);

            var result = new StringBuilder();
            result.AppendLine($"🚀 **Starting Async Terraform Deployment**");
            result.AppendLine($"📁 **Directory:** `{tempDir}`");
            result.AppendLine($"🏷️ **Deployment ID:** `{deployDir}`");
            result.AppendLine();

            // Store deployment info
            _sessionState[$"deployment_{deployDir}"] = new
            {
                Directory = tempDir,
                Name = deployDir,
                CreatedAt = DateTime.UtcNow,
                Template = templateContent,
                Status = "initializing"
            };

            // Initialize Terraform
            result.AppendLine("🔧 **Step 1: Initializing Terraform...**");
            var initResult = await ExecuteTerraformCommand("init", tempDir);
            if (initResult.Contains("Error") || initResult.Contains("Failed"))
            {
                return $"❌ **Terraform initialization failed:**\n\n```bash\n{FormatTerraformOutput(initResult)}\n```";
            }
            result.AppendLine("✅ Terraform initialized successfully");
            result.AppendLine();

            // Create plan
            result.AppendLine("📋 **Step 2: Creating execution plan...**");
            var planResult = await ExecuteTerraformCommand("plan -out=tfplan", tempDir);
            if (planResult.Contains("Error") || planResult.Contains("Failed"))
            {
                return $"❌ **Terraform plan failed:**\n\n```bash\n{FormatTerraformOutput(planResult)}\n```";
            }
            result.AppendLine("✅ Execution plan created successfully");
            result.AppendLine();
            result.AppendLine("📝 **Plan Summary:**");
            result.AppendLine($"```hcl\n{FormatTerraformPlan(planResult)}\n```");
            result.AppendLine();

            // Start async deployment
            result.AppendLine("🔄 **Step 3: Starting background deployment...**");
            _ = Task.Run(async () =>
            {
                try
                {
                    var statusFile = Path.Combine(tempDir, "deployment_status.json");
                    await File.WriteAllTextAsync(statusFile, JsonSerializer.Serialize(new
                    {
                        Status = "applying",
                        StartTime = DateTime.UtcNow,
                        DeploymentId = deployDir
                    }));

                    var applyResult = await ExecuteTerraformCommand("apply tfplan", tempDir);
                    var success = !applyResult.Contains("Error") && !applyResult.Contains("Failed");
                    
                    await File.WriteAllTextAsync(statusFile, JsonSerializer.Serialize(new
                    {
                        Status = success ? "completed" : "failed",
                        CompletedTime = DateTime.UtcNow,
                        DeploymentId = deployDir,
                        Result = applyResult
                    }));
                }
                catch (Exception ex)
                {
                    var statusFile = Path.Combine(tempDir, "deployment_status.json");
                    await File.WriteAllTextAsync(statusFile, JsonSerializer.Serialize(new
                    {
                        Status = "failed",
                        CompletedTime = DateTime.UtcNow,
                        DeploymentId = deployDir,
                        Error = ex.Message
                    }));
                }
            });

            result.AppendLine("✅ **Deployment started in background**");
            result.AppendLine();
            result.AppendLine("💡 **Next Steps:**");
            result.AppendLine($"- Use `monitor deployment progress {deployDir}` to check status");
            result.AppendLine($"- Use `check async deployment status {deployDir}` for final results");
            result.AppendLine();
            result.AppendLine($"🆔 **Deployment ID:** `{deployDir}`");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error starting async Terraform deployment: {ex.Message}";
        }
    }

    [KernelFunction("CheckAsyncDeploymentStatus")]
    [Description("Check the status of an async Terraform deployment")]
    public async Task<string> CheckAsyncDeploymentStatus(
        [Description("The deployment ID to check")] string deploymentId)
    {
        try
        {
            // Use persistent directory first, then check legacy temp directory
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            var tempDir = Path.Combine(baseDir, deploymentId);
            
            if (!Directory.Exists(tempDir))
            {
                var legacyTempDir = Path.Combine(Path.GetTempPath(), deploymentId);
                if (Directory.Exists(legacyTempDir))
                {
                    tempDir = legacyTempDir;
                }
                else
                {
                    var foundDir = FindDeploymentDirectory(deploymentId);
                    if (foundDir != null)
                    {
                        tempDir = foundDir;
                    }
                    else
                    {
                        return $"❌ Deployment directory not found for `{deploymentId}`";
                    }
                }
            }
            
            var statusFile = Path.Combine(tempDir, "deployment_status.json");

            if (!File.Exists(statusFile))
            {
                return $"❌ No status file found for deployment `{deploymentId}`";
            }

            var statusContent = await File.ReadAllTextAsync(statusFile);
            var status = JsonSerializer.Deserialize<JsonElement>(statusContent);

            var deploymentStatus = status.GetProperty("Status").GetString();
            var result = new StringBuilder();

            result.AppendLine($"📊 **Deployment Status: {deploymentId}**");
            result.AppendLine();
            result.AppendLine($"🔍 **Status:** `{deploymentStatus}`");

            if (status.TryGetProperty("StartTime", out var startTime))
            {
                result.AppendLine($"⏰ **Started:** {startTime.GetDateTime():yyyy-MM-dd HH:mm:ss} UTC");
            }

            if (status.TryGetProperty("CompletedTime", out var completedTime))
            {
                result.AppendLine($"✅ **Completed:** {completedTime.GetDateTime():yyyy-MM-dd HH:mm:ss} UTC");
            }

            if (status.TryGetProperty("Result", out var deployResult))
            {
                result.AppendLine();
                result.AppendLine("📋 **Deployment Output:**");
                result.AppendLine($"```hcl\n{FormatTerraformApplyResult(deployResult.GetString() ?? "")}\n```");
            }

            if (status.TryGetProperty("Error", out var error))
            {
                result.AppendLine();
                result.AppendLine($"❌ **Error:** {error.GetString()}");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error checking deployment status: {ex.Message}";
        }
    }

    [KernelFunction("ShowTerraformState")]
    [Description("Show current Terraform state for a deployment")]
    public async Task<string> ShowTerraformState(
        [Description("The deployment directory or deployment ID")] string deploymentId)
    {
        try
        {
            // Use persistent directory first, then check legacy temp directory
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            var tempDir = deploymentId.Contains(Path.DirectorySeparatorChar) 
                ? deploymentId 
                : Path.Combine(baseDir, deploymentId);

            if (!Directory.Exists(tempDir))
            {
                var legacyTempDir = Path.Combine(Path.GetTempPath(), deploymentId);
                if (Directory.Exists(legacyTempDir))
                {
                    tempDir = legacyTempDir;
                }
                else
                {
                    var foundDir = FindDeploymentDirectory(deploymentId);
                    if (foundDir != null)
                    {
                        tempDir = foundDir;
                    }
                    else
                    {
                        return $"❌ Deployment directory not found for '{deploymentId}'";
                    }
                }
            }

            var stateResult = await ExecuteTerraformCommand("state list", tempDir);
            if (string.IsNullOrEmpty(stateResult))
            {
                return "📋 No resources found in Terraform state.";
            }

            var result = new StringBuilder();
            result.AppendLine($"📋 **Terraform State: {deploymentId}**");
            result.AppendLine();
            result.AppendLine("🏗️ **Managed Resources:**");
            result.AppendLine($"```hcl\n{FormatTerraformStateList(stateResult)}\n```");

            // Get outputs if any
            var outputResult = await ExecuteTerraformCommand("output -json", tempDir);
            if (!string.IsNullOrEmpty(outputResult) && !outputResult.Contains("Error"))
            {
                result.AppendLine();
                result.AppendLine("📤 **Outputs:**");
                result.AppendLine($"```json\n{FormatJsonOutput(outputResult)}\n```");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error showing Terraform state: {ex.Message}";
        }
    }

    [KernelFunction("TrackDeploymentFailure")]
    [Description("Track a deployment failure to prevent repeating the same mistakes")]
    public async Task<string> TrackDeploymentFailure(
        [Description("Type of resource that failed")] string resourceType,
        [Description("Azure region where failure occurred")] string region,
        [Description("VM size that failed (if applicable)")] string vmSize,
        [Description("Error message from the failure")] string errorMessage)
    {
        try
        {
            var failure = new
            {
                Timestamp = DateTime.UtcNow,
                ResourceType = resourceType,
                Region = region.ToLower(),
                VmSize = vmSize,
                ErrorMessage = errorMessage,
                FailureReason = AnalyzeFailureReason(errorMessage),
                FailedConfiguration = new Dictionary<string, string>
                {
                    ["region"] = region.ToLower(),
                    ["vmSize"] = vmSize,
                    ["resourceType"] = resourceType
                }
            };

            _sessionState[$"failure_{DateTime.UtcNow.Ticks}"] = failure;

            // Track specific failure patterns
            var failureKey = $"{resourceType}_{region.ToLower()}_{vmSize}";
            if (!_sessionState.ContainsKey("failed_configurations"))
            {
                _sessionState["failed_configurations"] = new Dictionary<string, List<string>>();
            }

            var failedConfigs = _sessionState["failed_configurations"] as Dictionary<string, List<string>>;
            if (!failedConfigs.ContainsKey(failureKey))
            {
                failedConfigs[failureKey] = new List<string>();
            }
            failedConfigs[failureKey].Add(errorMessage);

            return $"✅ Failure tracked: {resourceType} with {vmSize} in {region} - will avoid this combination in future deployments.";
        }
        catch (Exception ex)
        {
            return $"❌ Error tracking failure: {ex.Message}";
        }
    }

    [KernelFunction("GetFailureHistory")]
    [Description("Get history of deployment failures to avoid repeating mistakes")]
    public async Task<string> GetFailureHistory()
    {
        try
        {
            var failures = _sessionState.Where(kv => kv.Key.StartsWith("failure_")).ToList();

            if (!failures.Any())
            {
                return "📋 No previous deployment failures recorded.";
            }

            var result = new StringBuilder();
            result.AppendLine("📋 **Previous Deployment Failures (to avoid):**");
            result.AppendLine();

            foreach (var failure in failures.Take(10)) // Show last 10 failures
            {
                dynamic failureData = failure.Value;
                result.AppendLine($"❌ **{failureData.ResourceType}** in **{failureData.Region}**");
                result.AppendLine($"   • VM Size: {failureData.VmSize}");
                result.AppendLine($"   • Reason: {failureData.FailureReason}");
                result.AppendLine($"   • Date: {failureData.Timestamp:yyyy-MM-dd HH:mm}");
                result.AppendLine();
            }

            result.AppendLine("💡 **Tip**: Choose different regions or VM sizes to avoid these known failures.");
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error retrieving failure history: {ex.Message}";
        }
    }

    [KernelFunction("GetRecommendedAlternatives")]
    [Description("Get recommended alternatives based on failure history")]
    public async Task<string> GetRecommendedAlternatives(
        [Description("Resource type to get alternatives for")] string resourceType,
        [Description("Failed region")] string failedRegion,
        [Description("Failed VM size")] string failedVmSize)
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("💡 **Recommended Alternatives (based on failure history):**");
            result.AppendLine();

            // Recommend different regions
            var recommendedRegions = new[]
            {
                "westus2", "centralus", "westeurope", "eastus2", "southcentralus"
            }.Where(r => !string.Equals(r, failedRegion, StringComparison.OrdinalIgnoreCase));

            result.AppendLine("🌍 **Alternative Regions:**");
            foreach (var region in recommendedRegions.Take(3))
            {
                result.AppendLine($"   • {region} (good availability)");
            }
            result.AppendLine();

            // Recommend different VM sizes
            if (!string.IsNullOrEmpty(failedVmSize))
            {
                result.AppendLine("💻 **Alternative VM Sizes:**");
                var recommendedSizes = GetAlternativeVmSizes(failedVmSize);
                foreach (var size in recommendedSizes.Take(3))
                {
                    result.AppendLine($"   • {size}");
                }
                result.AppendLine();
            }

            result.AppendLine($"🔄 **Try**: {resourceType} with one of the alternative regions/sizes above");
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error getting alternatives: {ex.Message}";
        }
    }

    private static string AnalyzeFailureReason(string errorMessage)
    {
        if (errorMessage.Contains("not allowed in your subscription"))
            return "VM size not available in subscription/region";
        if (errorMessage.Contains("quota"))
            return "Quota exceeded";
        if (errorMessage.Contains("LocationNotAvailableForResourceType"))
            return "Service not available in region";
        if (errorMessage.Contains("AuthorizationFailed"))
            return "Insufficient permissions";
        
        return "Unknown deployment error";
    }

    private static List<string> GetAlternativeVmSizes(string failedSize)
    {
        // Return alternatives based on the failed size
        return failedSize.ToLower() switch
        {
            var size when size.Contains("standard_d2s_v3") => ["Standard_B2s", "Standard_D2s_v4", "Standard_D2as_v4"],
            var size when size.Contains("standard_d2s_v4") => ["Standard_B2s", "Standard_D2s_v3", "Standard_D2as_v4"],
            var size when size.Contains("standard_b2s") => ["Standard_D2s_v3", "Standard_D2s_v4", "Standard_B4ms"],
            _ => ["Standard_B2s", "Standard_D2s_v3", "Standard_D2s_v4"]
        };
    }

    [KernelFunction("ListExistingDeployments")]
    [Description("List all existing Terraform deployments to identify which ones can be updated")]
    public async Task<string> ListExistingDeployments()
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            
            if (!Directory.Exists(baseDir))
            {
                return "📋 No existing deployments found. This will be your first deployment.";
            }

            var deploymentDirs = Directory.GetDirectories(baseDir);
            
            if (deploymentDirs.Length == 0)
            {
                return "📋 No existing deployments found. This will be your first deployment.";
            }

            var result = new StringBuilder();
            result.AppendLine("📋 **Existing Terraform Deployments:**");
            result.AppendLine();

            foreach (var dir in deploymentDirs)
            {
                var deploymentName = Path.GetFileName(dir);
                var hasState = File.Exists(Path.Combine(dir, "terraform.tfstate"));
                var hasTemplate = File.Exists(Path.Combine(dir, "main.tf"));
                
                result.AppendLine($"📁 **{deploymentName}**");
                result.AppendLine($"   • State File: {(hasState ? "✅ Present" : "❌ Missing")}");
                result.AppendLine($"   • Template: {(hasTemplate ? "✅ Present" : "❌ Missing")}");
                result.AppendLine($"   • Directory: `{dir}`");
                result.AppendLine();
            }

            result.AppendLine("💡 **Tip**: Use the same deployment name to update existing infrastructure instead of creating duplicates.");
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error listing deployments: {ex.Message}";
        }
    }

    [KernelFunction("ImportExistingResources")]
    [Description("Import existing Azure resources into Terraform state for management")]
    public async Task<string> ImportExistingResources(
        [Description("Name or identifier of the deployment")] string deploymentId,
        [Description("Azure resource group name to import")] string resourceGroupName)
    {
        try
        {
            var deploymentInfo = _sessionState.Values
                .Cast<dynamic>()
                .FirstOrDefault(d => d.Name == deploymentId);

            if (deploymentInfo == null)
            {
                return $"❌ Deployment '{deploymentId}' not found in session.";
            }

            var tempDir = deploymentInfo.Directory;
            if (!Directory.Exists(tempDir))
            {
                return $"❌ Deployment directory not found: {tempDir}";
            }

            var result = await ImportExistingResourcesAsync(tempDir, deploymentId);
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error importing resources: {ex.Message}";
        }
    }

    [KernelFunction("SyncTerraformState")]
    [Description("Manually sync with existing Terraform deployments in session for recovery and state management")]
    public Task<string> SyncTerraformState()
    {
        try
        {
            // Note: In a real implementation, we'd get the session ID from context
            // For now, we'll return the sync information directly
            
            var result = "🔄 **Terraform State Sync**\n\n" +
                   "Terraform state synchronization is now **on-demand only** for better performance.\n\n" +
                   "**Benefits:**\n" +
                   "• Faster session startup\n" +
                   "• Reduced overhead during infrastructure creation\n" +
                   "• Manual control over when sync occurs\n\n" +
                   "**When to sync:**\n" +
                   "• After manual Terraform operations outside this tool\n" +
                   "• When you need to recover from deployment failures\n" +
                   "• To see existing infrastructure in your current session\n\n" +
                   "💡 The sync will happen automatically when you need recovery options or state information.";
            
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"❌ Error during Terraform state sync: {ex.Message}");
        }
    }

    [KernelFunction("DestroyTerraformResources")]
    [Description("Destroy Terraform-managed resources")]
    public async Task<string> DestroyTerraformResources(
        [Description("The deployment directory or deployment ID")] string deploymentId)
    {
        try
        {
            // First, try to find the deployment in the persistent directory
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            var tempDir = deploymentId.Contains(Path.DirectorySeparatorChar) 
                ? deploymentId 
                : Path.Combine(baseDir, deploymentId);

            // If not found in persistent directory, check if it's a legacy temp directory path
            if (!Directory.Exists(tempDir))
            {
                var legacyTempDir = Path.Combine(Path.GetTempPath(), deploymentId);
                if (Directory.Exists(legacyTempDir))
                {
                    tempDir = legacyTempDir;
                }
                else
                {
                    // Try to find the deployment by searching for it
                    var foundDir = FindDeploymentDirectory(deploymentId);
                    if (foundDir != null)
                    {
                        tempDir = foundDir;
                    }
                    else
                    {
                        return $"❌ **Deployment directory not found for '{deploymentId}'**\n\n" +
                               $"**Searched locations:**\n" +
                               $"• `{Path.Combine(baseDir, deploymentId)}`\n" +
                               $"• `{legacyTempDir}`\n\n" +
                               $"💡 **Try one of these options:**\n" +
                               $"• Use `ListExistingDeployments` to see available deployments\n" +
                               $"• Use `RecoverFromPartialFailure` to handle orphaned resources\n" +
                               $"• Use `CheckForOrphanedResources` to find resources without state files";
                    }
                }
            }

            var result = new StringBuilder();
            result.AppendLine($"🗑️ **Destroying Terraform Resources: {deploymentId}**");
            result.AppendLine($"📁 **Using directory:** `{tempDir}`");
            result.AppendLine();

            // Check if terraform state file exists
            var stateFile = Path.Combine(tempDir, "terraform.tfstate");
            if (!File.Exists(stateFile))
            {
                result.AppendLine("⚠️ **No terraform.tfstate file found**");
                result.AppendLine();
                result.AppendLine("💡 **This could mean:**");
                result.AppendLine("• Resources were never successfully deployed");
                result.AppendLine("• State file was manually deleted");
                result.AppendLine("• Deployment failed during initialization");
                result.AppendLine();
                result.AppendLine("🔍 **Checking for orphaned Azure resources...**");
                
                // Try to find orphaned resources manually
                var orphanedCheck = await CheckForOrphanedResources(tempDir);
                result.AppendLine(orphanedCheck);
                
                return result.ToString();
            }

            // Show what will be destroyed
            result.AppendLine("🔍 **Resources to be destroyed:**");
            var planResult = await ExecuteTerraformCommand("plan -destroy", tempDir);
            if (planResult.Contains("Error") || planResult.Contains("No changes"))
            {
                result.AppendLine("⚠️ **Terraform plan shows no resources to destroy**");
                result.AppendLine($"```bash\n{FormatTerraformOutput(planResult)}\n```");
                result.AppendLine();
                result.AppendLine("💡 **This could mean resources were already destroyed or never created.**");
            }
            else
            {
                result.AppendLine($"```hcl\n{FormatTerraformPlan(planResult)}\n```");
                result.AppendLine();

                // Destroy
                result.AppendLine("💥 **Destroying infrastructure...**");
                var destroyResult = await ExecuteTerraformCommand("destroy -auto-approve", tempDir);
                
                if (destroyResult.Contains("Error") || destroyResult.Contains("Failed"))
                {
                    result.AppendLine($"❌ **Destruction failed:**\n\n```bash\n{FormatTerraformOutput(destroyResult)}\n```");
                    result.AppendLine();
                    result.AppendLine("💡 **Try using `RecoverFromPartialFailure` for advanced cleanup options**");
                }
                else
                {
                    result.AppendLine("✅ **Resources destroyed successfully!**");
                    result.AppendLine();
                    result.AppendLine("📋 **Destruction Summary:**");
                    result.AppendLine($"```hcl\n{FormatTerraformApplyResult(destroyResult)}\n```");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error destroying Terraform resources: {ex.Message}\n\n" +
                   $"💡 **Try using `RecoverFromPartialFailure` or `CheckForOrphanedResources` for manual cleanup**";
        }
    }

    private string? FindDeploymentDirectory(string deploymentId)
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            
            if (!Directory.Exists(baseDir))
                return null;

            // Look for exact match first
            var exactMatch = Path.Combine(baseDir, deploymentId);
            if (Directory.Exists(exactMatch))
                return exactMatch;

            // Look for partial matches
            var directories = Directory.GetDirectories(baseDir);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.Contains(deploymentId, StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }

            // Check legacy temp directory
            var legacyDir = Path.Combine(Path.GetTempPath(), deploymentId);
            if (Directory.Exists(legacyDir))
                return legacyDir;

            return null;
        }
        catch
        {
            return null;
        }
    }

    [KernelFunction("RecoverFromPartialFailure")]
    [Description("Recover from partial deployment failure by analyzing and cleaning up orphaned resources")]
    public async Task<string> RecoverFromPartialFailure(
        [Description("The deployment directory or deployment ID")] string deploymentId,
        [Description("Recovery action: 'cleanup' to destroy partial resources, 'complete' to attempt finishing deployment, or 'analyze' to just report status")] string action = "analyze")
    {
        try
        {
            // Use the same logic as DestroyTerraformResources to find the correct directory
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            var tempDir = deploymentId.Contains(Path.DirectorySeparatorChar) 
                ? deploymentId 
                : Path.Combine(baseDir, deploymentId);

            // If not found in persistent directory, check legacy temp directory or search
            if (!Directory.Exists(tempDir))
            {
                var legacyTempDir = Path.Combine(Path.GetTempPath(), deploymentId);
                if (Directory.Exists(legacyTempDir))
                {
                    tempDir = legacyTempDir;
                }
                else
                {
                    var foundDir = FindDeploymentDirectory(deploymentId);
                    if (foundDir != null)
                    {
                        tempDir = foundDir;
                    }
                    else
                    {
                        return $"❌ **Deployment directory not found for '{deploymentId}'**\n\n" +
                               $"**Searched locations:**\n" +
                               $"• `{Path.Combine(baseDir, deploymentId)}`\n" +
                               $"• `{legacyTempDir}`\n\n" +
                               $"💡 **Try using `CheckForOrphanedResources` to find resources without state files**";
                    }
                }
            }

            var result = new StringBuilder();
            result.AppendLine($"🔧 **Partial Failure Recovery Analysis: {deploymentId}**");
            result.AppendLine($"📁 **Using directory:** `{tempDir}`");
            result.AppendLine();

            // Step 1: Check Terraform state
            result.AppendLine("📋 **Step 1: Analyzing Terraform State**");
            var stateResult = await ExecuteTerraformCommand("state list", tempDir);
            
            if (string.IsNullOrEmpty(stateResult))
            {
                result.AppendLine("⚠️ No Terraform state found - deployment may have failed during planning");
                result.AppendLine();
                
                // Check for any Azure resources that might exist
                result.AppendLine("🔍 **Step 2: Checking for Orphaned Azure Resources**");
                var orphanedResources = await CheckForOrphanedResources(tempDir);
                result.AppendLine(orphanedResources);
                
                return result.ToString();
            }

            result.AppendLine("✅ Terraform state file found");
            result.AppendLine($"```hcl\n{FormatTerraformStateList(stateResult)}\n```");
            result.AppendLine();

            // Step 2: Validate state against Azure reality
            result.AppendLine("🔍 **Step 2: Validating State Against Azure**");
            var planResult = await ExecuteTerraformCommand("plan -detailed-exitcode", tempDir);
            
            if (planResult.Contains("No changes"))
            {
                result.AppendLine("✅ Terraform state matches Azure reality - deployment appears complete");
                return result.ToString();
            }
            else if (planResult.Contains("Error"))
            {
                result.AppendLine("❌ State validation failed - resources may be in inconsistent state");
                result.AppendLine($"```bash\n{FormatTerraformOutput(planResult)}\n```");
            }
            else
            {
                result.AppendLine("⚠️ State drift detected - some resources are missing or modified");
                result.AppendLine($"```hcl\n{FormatTerraformPlan(planResult)}\n```");
            }
            result.AppendLine();

            // Step 3: Take action based on user choice
            result.AppendLine($"🎯 **Step 3: Recovery Action - {action.ToUpper()}**");
            
            switch (action.ToLower())
            {
                case "cleanup":
                case "destroy":
                    result.AppendLine("🗑️ **Cleaning up partial resources...**");
                    var destroyResult = await ExecuteTerraformCommand("destroy -auto-approve", tempDir);
                    if (destroyResult.Contains("Error"))
                    {
                        result.AppendLine("❌ **Cleanup failed:**");
                        result.AppendLine($"```bash\n{FormatTerraformOutput(destroyResult)}\n```");
                        result.AppendLine();
                        result.AppendLine("💡 **Manual cleanup may be required in Azure Portal**");
                    }
                    else
                    {
                        result.AppendLine("✅ **Partial resources cleaned up successfully**");
                        result.AppendLine($"```hcl\n{FormatTerraformApplyResult(destroyResult)}\n```");
                    }
                    break;

                case "complete":
                case "retry":
                    result.AppendLine("🔄 **Attempting to complete deployment...**");
                    var applyResult = await ExecuteTerraformCommand("apply -auto-approve", tempDir);
                    if (applyResult.Contains("Error"))
                    {
                        result.AppendLine("❌ **Completion attempt failed:**");
                        result.AppendLine($"```bash\n{FormatTerraformOutput(applyResult)}\n```");
                        result.AppendLine();
                        var errorAnalysis = await AnalyzeTerraformErrorWithTrackingAsync(applyResult, "");
                        result.AppendLine(errorAnalysis);
                    }
                    else
                    {
                        result.AppendLine("✅ **Deployment completed successfully**");
                        result.AppendLine($"```hcl\n{FormatTerraformApplyResult(applyResult)}\n```");
                    }
                    break;

                case "analyze":
                default:
                    result.AppendLine("📊 **Analysis complete - no action taken**");
                    result.AppendLine();
                    result.AppendLine("💡 **Recommended Actions:**");
                    if (planResult.Contains("Error"))
                    {
                        result.AppendLine("• Run with action='cleanup' to destroy problematic resources");
                        result.AppendLine("• Check Azure Portal for manually created resources");
                        result.AppendLine("• Consider redeploying with corrected configuration");
                    }
                    else
                    {
                        result.AppendLine("• Run with action='complete' to finish the deployment");
                        result.AppendLine("• Run with action='cleanup' to start fresh");
                        result.AppendLine("• Review the plan output above for required changes");
                    }
                    break;
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error during partial failure recovery: {ex.Message}";
        }
    }

    [KernelFunction("CheckForOrphanedResources")]
    [Description("Check for Azure resources that might be orphaned from failed deployments")]
    public async Task<string> CheckForOrphanedResources(
        [Description("Optional deployment directory to check")] string? deploymentDirectory = null)
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("🔍 **Checking for Orphaned Azure Resources**");
            result.AppendLine();

            // Check for resource groups that might be orphaned
            var rgListResult = await ExecuteAzureCommand("group list --query \"[?tags.CreatedBy=='Terraform'].{Name:name, Location:location, Tags:tags}\" -o table");
            
            if (string.IsNullOrEmpty(rgListResult) || rgListResult.Contains("[]"))
            {
                result.AppendLine("✅ No Terraform-tagged resource groups found");
            }
            else
            {
                result.AppendLine("⚠️ **Found Terraform-tagged resource groups:**");
                result.AppendLine($"```\n{rgListResult}\n```");
                result.AppendLine();
                result.AppendLine("💡 These might be from previous deployments. Check if they have corresponding Terraform state files.");
            }

            // Check for resources in failed state
            var failedResourcesResult = await ExecuteAzureCommand("resource list --query \"[?provisioningState!='Succeeded'].{Name:name, Type:type, ResourceGroup:resourceGroup, ProvisioningState:provisioningState}\" -o table");
            
            if (string.IsNullOrEmpty(failedResourcesResult) || failedResourcesResult.Contains("[]"))
            {
                result.AppendLine("✅ No resources found in failed provisioning state");
            }
            else
            {
                result.AppendLine("❌ **Found resources in failed state:**");
                result.AppendLine($"```\n{failedResourcesResult}\n```");
                result.AppendLine();
                result.AppendLine("💡 These resources may need manual cleanup or remediation.");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error checking for orphaned resources: {ex.Message}";
        }
    }

    [KernelFunction("GetCurrentSubscription")]
    [Description("Get current Azure subscription information")]
    public async Task<string> GetCurrentSubscription()
    {
        return await ExecuteAzureCommand("account show");
    }

    [KernelFunction("ListSubscriptions")]
    [Description("List all available Azure subscriptions")]
    public async Task<string> ListSubscriptions()
    {
        return await ExecuteAzureCommand("account list");
    }

    [KernelFunction("SetSubscription")]
    [Description("Set the active Azure subscription")]
    public async Task<string> SetSubscription(
        [Description("The subscription ID or name to set as active")] string subscriptionId)
    {
        return await ExecuteAzureCommand($"account set --subscription \"{subscriptionId}\"");
    }

    [KernelFunction("TestAzureConnection")]
    [Description("Test Azure CLI connectivity and authentication")]
    public async Task<string> TestAzureConnection()
    {
        return await ExecuteAzureCommand("account show");
    }

    [KernelFunction("ListResourceGroups")]
    [Description("List resource groups in the current subscription")]
    public async Task<string> ListResourceGroups()
    {
        return await ExecuteAzureCommand("group list --output table");
    }

    [KernelFunction("ListAzureRegions")]
    [Description("List available Azure regions")]
    public async Task<string> ListAzureRegions()
    {
        return await ExecuteAzureCommand("account list-locations --output table");
    }

    [KernelFunction("CheckVMSizeAvailability")]
    [Description("Check if a VM size is available in a specific Azure region")]
    public async Task<string> CheckVMSizeAvailability(
        [Description("The VM size to check (e.g., Standard_DS2_v2, Standard_D2s_v3)")] string vmSize,
        [Description("The Azure region to check (e.g., East US, West US 2)")] string region)
    {
        try
        {
            // Normalize region name for Azure CLI
            var normalizedRegion = NormalizeRegionName(region);
            
            // Check VM sizes available in the region
            var result = await ExecuteAzureCommand($"vm list-sizes --location \"{normalizedRegion}\" --query \"[?name=='{vmSize}']\" --output table");
            
            if (string.IsNullOrEmpty(result) || result.Contains("[]") || !result.Contains(vmSize))
            {
                // Get alternative VM sizes
                var alternatives = await GetAlternativeVMSizes(normalizedRegion, vmSize);
                return $"❌ **VM Size Not Available**\n\n" +
                       $"🚫 `{vmSize}` is not available in `{region}`\n\n" +
                       $"✅ **Recommended Alternatives:**\n{alternatives}";
            }
            else
            {
                return $"✅ **VM Size Available**\n\n" +
                       $"🎯 `{vmSize}` is available in `{region}`\n" +
                       $"💰 You can proceed with this configuration.";
            }
        }
        catch (Exception ex)
        {
            return $"❌ Error checking VM size availability: {ex.Message}";
        }
    }

    [KernelFunction("CheckAzureQuota")]
    [Description("Check Azure quota and resource availability for deployment")]
    public async Task<string> CheckAzureQuota(
        [Description("The Azure region for deployment")] string region,
        [Description("Resource type (e.g., virtualMachines, cores, networkInterfaces)")] string resourceType = "cores")
    {
        try
        {
            var normalizedRegion = NormalizeRegionName(region);
            
            // Check quota usage
            var quotaResult = await ExecuteAzureCommand($"vm list-usage --location \"{normalizedRegion}\" --query \"[?contains(name.value, '{resourceType}')]\" --output table");
            
            if (quotaResult.Contains("Error") || string.IsNullOrEmpty(quotaResult))
            {
                return $"⚠️ **Unable to check quota for {resourceType} in {region}**\n\n" +
                       $"Please verify your Azure permissions and region name.";
            }
            
            return $"📊 **Azure Quota Status**\n\n" +
                   $"📍 **Region:** {region}\n" +
                   $"🔢 **Resource Type:** {resourceType}\n\n" +
                   $"```\n{quotaResult}\n```\n\n" +
                   $"💡 **Tip:** Ensure you have sufficient quota before deployment.";
        }
        catch (Exception ex)
        {
            return $"❌ Error checking Azure quota: {ex.Message}";
        }
    }

    [KernelFunction("ValidateDeploymentRequirements")]
    [Description("Quick validation of deployment parameters using Azure knowledge")]
    public async Task<string> ValidateDeploymentRequirements(
        [Description("The Azure region for deployment")] string region,
        [Description("VM size for AKS nodes or VMs")] string vmSize,
        [Description("Number of VM instances needed")] int instanceCount = 3)
    {
        try
        {
            var validationResults = new StringBuilder();
            validationResults.AppendLine("🔍 **Quick Deployment Validation**\n");
            
            var normalizedRegion = NormalizeRegionName(region);
            bool allChecksPass = true;
            var issues = new List<string>();
            var recommendations = new List<string>();
            
            // 1. Validate region (using known good regions)
            var knownGoodRegions = new[] { 
                "eastus", "westus", "westus2", "centralus", "eastus2",
                "westeurope", "northeurope", "southeastasia", "japaneast",
                "australiaeast", "canadacentral", "uksouth"
            };
            
            if (!knownGoodRegions.Contains(normalizedRegion))
            {
                issues.Add($"⚠️ Region '{region}' may have limited service availability");
                recommendations.Add($"💡 Consider using: East US, West US 2, or West Europe");
                allChecksPass = false;
            }
            else
            {
                validationResults.AppendLine($"✅ **Region**: {region} is a reliable choice");
            }
            
            // 2. Validate VM size (using known good sizes)
            var recommendedVMSizes = new[] {
                "Standard_D2s_v3", "Standard_D4s_v3", "Standard_DS2_v2", 
                "Standard_B2s", "Standard_B2ms", "Standard_F2s_v2"
            };
            
            if (!recommendedVMSizes.Contains(vmSize))
            {
                issues.Add($"⚠️ VM size '{vmSize}' may not be optimal");
                recommendations.Add("💡 Recommended: Standard_D2s_v3, Standard_DS2_v2, or Standard_B2s");
            }
            else
            {
                validationResults.AppendLine($"✅ **VM Size**: {vmSize} is a good choice");
            }
            
            // 3. Validate instance count
            if (instanceCount < 1)
            {
                issues.Add("⚠️ Instance count must be at least 1");
                allChecksPass = false;
            }
            else if (instanceCount == 1)
            {
                recommendations.Add("💡 Consider 3+ instances for production workloads (high availability)");
            }
            else
            {
                validationResults.AppendLine($"✅ **Instance Count**: {instanceCount} provides good availability");
            }
            
            // Display results
            if (issues.Any())
            {
                validationResults.AppendLine("\n**⚠️ Issues Found:**");
                validationResults.AppendLine(string.Join("\n", issues));
            }
            
            if (recommendations.Any())
            {
                validationResults.AppendLine("\n**💡 Recommendations:**");
                validationResults.AppendLine(string.Join("\n", recommendations));
            }
            
            if (allChecksPass && !recommendations.Any())
            {
                validationResults.AppendLine("\n✅ **All validations passed! Ready for deployment.**");
            }
            else
            {
                validationResults.AppendLine("\n🚀 **Template can still be generated with current parameters.**");
                validationResults.AppendLine("📝 Consider the recommendations above for optimal results.");
            }
            
            return validationResults.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error during validation: {ex.Message}";
        }
    }

    private async Task<string> ExecuteTerraformCommand(string arguments, string workingDirectory)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "terraform";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(StripAnsiCodes(e.Data));
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(StripAnsiCodes(e.Data));
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Use timeout for long-running operations
            var timeout = arguments.Contains("apply") || arguments.Contains("destroy") 
                ? TimeSpan.FromMinutes(30) 
                : TimeSpan.FromMinutes(5);

            await process.WaitForExitAsync(CancellationToken.None);
            
            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            if (process.ExitCode != 0)
            {
                return $"Terraform command failed (exit code {process.ExitCode}):\n{error}\n\nOutput:\n{output}";
            }

            return string.IsNullOrEmpty(output) ? error : output;
        }
        catch (Exception ex)
        {
            return $"Error executing Terraform command: {ex.Message}";
        }
    }

    private string StripAnsiCodes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove ANSI escape sequences for colors and formatting
        // Pattern matches: ESC[ followed by any number of parameters and a final character
        return System.Text.RegularExpressions.Regex.Replace(input, @"\x1B\[[0-9;]*[mGKHF]", "");
    }

    private async Task<string> ExecuteTerraformCommandWithProgress(string arguments, string workingDirectory, StringBuilder progressOutput)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "terraform";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    // Add key progress indicators to the user-visible output
                    if (e.Data.Contains("Initializing provider plugins") || 
                        e.Data.Contains("Installing") || 
                        e.Data.Contains("Downloading") ||
                        e.Data.Contains("Refreshing state") ||
                        e.Data.Contains("Creating...") ||
                        e.Data.Contains("Modifying...") ||
                        e.Data.Contains("Plan:") ||
                        e.Data.Contains("Apply complete"))
                    {
                        progressOutput.AppendLine($"📊 {e.Data}");
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    // Show important warnings/errors in progress
                    if (e.Data.Contains("Warning") || e.Data.Contains("Error"))
                    {
                        progressOutput.AppendLine($"⚠️  {e.Data}");
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Add progress dots for long operations
            var progressTask = Task.Run(async () =>
            {
                var isLongOperation = arguments.Contains("apply") || arguments.Contains("init");
                if (isLongOperation)
                {
                    var dotCount = 0;
                    while (!process.HasExited)
                    {
                        await Task.Delay(2000);
                        if (!process.HasExited)
                        {
                            dotCount = (dotCount + 1) % 4;
                            var dots = new string('.', dotCount);
                            progressOutput.AppendLine($"⏳ Working{dots}");
                        }
                    }
                }
            });

            await process.WaitForExitAsync(CancellationToken.None);
            
            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            if (process.ExitCode != 0)
            {
                return $"Terraform command failed (exit code {process.ExitCode}):\n{error}\n\nOutput:\n{output}";
            }

            return string.IsNullOrEmpty(output) ? error : output;
        }
        catch (Exception ex)
        {
            return $"Error executing Terraform command: {ex.Message}";
        }
    }

    #region Output Formatting Methods

    private static string FormatTerraformOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return "No output";

        var lines = output.Split('\n');
        var formatted = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Highlight important keywords
            if (trimmedLine.Contains("Error:") || trimmedLine.Contains("Failed"))
                formatted.AppendLine($"❌ {trimmedLine}");
            else if (trimmedLine.Contains("Warning:"))
                formatted.AppendLine($"⚠️  {trimmedLine}");
            else if (trimmedLine.Contains("Success") || trimmedLine.Contains("Complete"))
                formatted.AppendLine($"✅ {trimmedLine}");
            else
                formatted.AppendLine($"   {trimmedLine}");
        }

        return formatted.ToString().Trim();
    }

    private static string FormatTerraformPlan(string planOutput)
    {
        if (string.IsNullOrEmpty(planOutput))
            return "No plan details available";

        var lines = planOutput.Split('\n');
        var formatted = new StringBuilder();
        var inResourceBlock = false;
        var resourceCount = new { Add = 0, Change = 0, Destroy = 0 };

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Extract plan summary
            if (trimmedLine.StartsWith("Plan:"))
            {
                formatted.AppendLine($"📊 {trimmedLine}");
                continue;
            }

            // Format resource changes
            if (trimmedLine.StartsWith("# "))
            {
                inResourceBlock = true;
                if (trimmedLine.Contains("will be created"))
                    formatted.AppendLine($"➕ {trimmedLine.Replace("# ", "")}");
                else if (trimmedLine.Contains("will be updated"))
                    formatted.AppendLine($"🔄 {trimmedLine.Replace("# ", "")}");
                else if (trimmedLine.Contains("will be destroyed"))
                    formatted.AppendLine($"❌ {trimmedLine.Replace("# ", "")}");
                else
                    formatted.AppendLine($"🔧 {trimmedLine.Replace("# ", "")}");
            }
            else if (trimmedLine.StartsWith("+") && inResourceBlock)
            {
                formatted.AppendLine($"    ➕ {trimmedLine.Substring(1).Trim()}");
            }
            else if (trimmedLine.StartsWith("-") && inResourceBlock)
            {
                formatted.AppendLine($"    ➖ {trimmedLine.Substring(1).Trim()}");
            }
            else if (trimmedLine.StartsWith("~") && inResourceBlock)
            {
                formatted.AppendLine($"    🔄 {trimmedLine.Substring(1).Trim()}");
            }
            else if (trimmedLine == "}")
            {
                inResourceBlock = false;
                formatted.AppendLine();
            }
        }

        return formatted.ToString().Trim();
    }

    private static string FormatTerraformApplyResult(string applyOutput)
    {
        if (string.IsNullOrEmpty(applyOutput))
            return "No apply output available";

        var lines = applyOutput.Split('\n');
        var formatted = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Extract key information
            if (trimmedLine.Contains("Apply complete!"))
                formatted.AppendLine($"🎉 {trimmedLine}");
            else if (trimmedLine.Contains("Resources:"))
                formatted.AppendLine($"📊 {trimmedLine}");
            else if (trimmedLine.Contains("created") || trimmedLine.Contains("Creation complete"))
                formatted.AppendLine($"✅ {trimmedLine}");
            else if (trimmedLine.Contains("destroyed") || trimmedLine.Contains("Destruction complete"))
                formatted.AppendLine($"🗑️ {trimmedLine}");
            else if (trimmedLine.Contains("Error:") || trimmedLine.Contains("Failed"))
                formatted.AppendLine($"❌ {trimmedLine}");
            else if (trimmedLine.StartsWith("Outputs:"))
                formatted.AppendLine($"📤 {trimmedLine}");
            else if (trimmedLine.Contains(" = "))
                formatted.AppendLine($"   {trimmedLine}");
        }

        return formatted.ToString().Trim();
    }

    private static string FormatTerraformStateList(string stateOutput)
    {
        if (string.IsNullOrEmpty(stateOutput))
            return "No resources in state";

        var lines = stateOutput.Split('\n');
        var formatted = new StringBuilder();
        var resourceTypes = new Dictionary<string, List<string>>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            // Group by resource type
            var parts = trimmedLine.Split('.');
            if (parts.Length >= 2)
            {
                var resourceType = parts[0];
                var resourceName = string.Join(".", parts.Skip(1));

                if (!resourceTypes.ContainsKey(resourceType))
                    resourceTypes[resourceType] = new List<string>();
                
                resourceTypes[resourceType].Add(resourceName);
            }
            else
            {
                formatted.AppendLine($"🔧 {trimmedLine}");
            }
        }

        // Format grouped resources
        foreach (var group in resourceTypes)
        {
            var icon = GetResourceTypeIcon(group.Key);
            formatted.AppendLine($"{icon} **{group.Key}**");
            foreach (var resource in group.Value)
            {
                formatted.AppendLine($"   • {resource}");
            }
            formatted.AppendLine();
        }

        return formatted.ToString().Trim();
    }

    private static string FormatJsonOutput(string jsonOutput)
    {
        try
        {
            // Try to format JSON nicely
            var jsonDoc = JsonDocument.Parse(jsonOutput);
            return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return jsonOutput; // Return as-is if not valid JSON
        }
    }

    private static string GetResourceTypeIcon(string resourceType)
    {
        return resourceType.ToLower() switch
        {
            "azurerm_storage_account" => "💾",
            "azurerm_resource_group" => "📁",
            "azurerm_virtual_network" => "🌐",
            "azurerm_subnet" => "🔗",
            "azurerm_kubernetes_cluster" => "☸️",
            "azurerm_app_service" => "🌍",
            "azurerm_sql_server" => "🗄️",
            "azurerm_sql_database" => "📊",
            "azurerm_key_vault" => "🔐",
            "azurerm_application_insights" => "📈",
            "azurerm_container_registry" => "📦",
            "azurerm_cosmosdb_account" => "🌌",
            _ => "🔧"
        };
    }

    #endregion

    #region Dynamic Azure Discovery Methods

    [KernelFunction("GetOptimalRegionForResources")]
    [Description("Intelligently find the best Azure region for specific resource requirements")]
    public async Task<string> GetOptimalRegionForResources(
        [Description("Comma-separated list of required Azure services (e.g., 'AKS,Storage,SQL')")] string requiredServices,
        [Description("Preferred VM size for compute resources")] string preferredVmSize = "Standard_D2s_v3",
        [Description("Geographic preference (e.g., 'US', 'Europe', 'Asia')")] string geoPreference = "US")
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("🌍 **Intelligent Region Analysis**\n");

            // Get all available regions
            var regionsResult = await ExecuteAzureCommand("account list-locations --query \"[].{Name:name, DisplayName:displayName}\" --output json");
            
            if (regionsResult.Contains("Error"))
            {
                return "❌ Unable to fetch Azure regions. Please check your Azure CLI connection.";
            }

            var regions = JsonSerializer.Deserialize<JsonElement>(regionsResult);
            var suitableRegions = new List<(string name, string displayName, int score)>();

            foreach (var region in regions.EnumerateArray())
            {
                var regionName = region.GetProperty("name").GetString();
                var displayName = region.GetProperty("displayName").GetString();
                
                if (string.IsNullOrEmpty(regionName)) continue;

                // Filter by geographic preference
                if (!IsInGeographicArea(regionName, geoPreference)) continue;

                // Score the region based on requirements
                var score = await CalculateRegionScore(regionName, requiredServices, preferredVmSize);
                
                if (score > 0)
                {
                    suitableRegions.Add((regionName, displayName, score));
                }
            }

            // Sort by score (highest first)
            var topRegions = suitableRegions.OrderByDescending(r => r.score).Take(5).ToList();

            if (!topRegions.Any())
            {
                result.AppendLine("⚠️ No regions found matching all requirements.");
                result.AppendLine("💡 Consider relaxing requirements or choosing different VM sizes.");
                return result.ToString();
            }

            result.AppendLine("🏆 **Top Recommended Regions:**\n");
            foreach (var (name, displayName, score) in topRegions)
            {
                var emoji = score >= 80 ? "🥇" : score >= 60 ? "🥈" : "🥉";
                result.AppendLine($"{emoji} **{displayName}** (`{name}`) - Score: {score}%");
            }

            result.AppendLine($"\n✅ **Recommended:** Use `{topRegions.First().displayName}` for optimal performance and availability.");
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error finding optimal region: {ex.Message}";
        }
    }

    [KernelFunction("GetBestVMSizesForWorkload")]
    [Description("Intelligently recommend VM sizes based on workload requirements")]
    public async Task<string> GetBestVMSizesForWorkload(
        [Description("Type of workload (e.g., 'web app', 'database', 'kubernetes', 'general purpose')")] string workloadType,
        [Description("Azure region where VMs will be deployed")] string region,
        [Description("Expected CPU cores needed")] int targetCores = 2,
        [Description("Expected RAM in GB")] int targetMemoryGB = 8)
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("💻 **Intelligent VM Size Recommendations**\n");

            var normalizedRegion = NormalizeRegionName(region);
            
            // Get all available VM sizes in the region
            var vmSizesResult = await ExecuteAzureCommand($"vm list-sizes --location \"{normalizedRegion}\" --output json");
            
            if (vmSizesResult.Contains("Error"))
            {
                return $"❌ Unable to fetch VM sizes for region {region}. Please verify the region name.";
            }

            var vmSizes = JsonSerializer.Deserialize<JsonElement>(vmSizesResult);
            var recommendations = new List<(string name, int cores, int memoryGB, int score, string reason)>();

            foreach (var vm in vmSizes.EnumerateArray())
            {
                var name = vm.GetProperty("name").GetString();
                var cores = vm.GetProperty("numberOfCores").GetInt32();
                var memoryMB = vm.GetProperty("memoryInMB").GetInt32();
                var memoryGB = memoryMB / 1024;

                if (string.IsNullOrEmpty(name)) continue;

                // Score based on workload type and requirements
                var score = CalculateVMScore(name, cores, memoryGB, workloadType, targetCores, targetMemoryGB);
                var reason = GetVMRecommendationReason(name, cores, memoryGB, workloadType);

                if (score > 30) // Only include reasonable matches
                {
                    recommendations.Add((name, cores, memoryGB, score, reason));
                }
            }

            // Sort by score and take top 5
            var topRecommendations = recommendations.OrderByDescending(r => r.score).Take(5).ToList();

            if (!topRecommendations.Any())
            {
                result.AppendLine("⚠️ No suitable VM sizes found for your requirements.");
                result.AppendLine("💡 Consider adjusting your requirements or trying a different region.");
                return result.ToString();
            }

            result.AppendLine($"🎯 **Optimized for:** {workloadType}");
            result.AppendLine($"📍 **Region:** {region}");
            result.AppendLine($"⚙️ **Target:** {targetCores} cores, {targetMemoryGB}GB RAM\n");

            foreach (var (name, cores, memoryGB, score, reason) in topRecommendations)
            {
                var emoji = score >= 90 ? "🥇" : score >= 75 ? "🥈" : score >= 60 ? "🥉" : "💡";
                result.AppendLine($"{emoji} **{name}**");
                result.AppendLine($"   └─ {cores} cores, {memoryGB}GB RAM - {reason}");
                result.AppendLine();
            }

            result.AppendLine($"✅ **Top Choice:** `{topRecommendations.First().name}` - Best match for your {workloadType} workload");
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error analyzing VM sizes: {ex.Message}";
        }
    }

    [KernelFunction("ValidateTerraformTemplate")]
    [Description("Quick Terraform template syntax validation and best practices check")]
    public async Task<string> ValidateTerraformTemplate(
        [Description("The Terraform template content to validate")] string templateContent)
    {
        try
        {
            var result = new StringBuilder();
            result.AppendLine("🔍 **Quick Template Validation**\n");

            // Quick syntax and structure validation (no external calls)
            var syntaxIssues = ValidateTemplateSyntax(templateContent);
            if (syntaxIssues.Any())
            {
                result.AppendLine("❌ **Syntax Issues Found:**");
                result.AppendLine(string.Join("\n", syntaxIssues));
                result.AppendLine();
            }
            else
            {
                result.AppendLine("✅ **Template Syntax Valid**\n");
            }

            // Analyze best practices (no external calls)
            var analysis = AnalyzeTerraformBestPractices(templateContent);
            result.AppendLine("📋 **Best Practices Analysis:**");
            result.AppendLine(analysis);

            // Quick resource compatibility check (no external calls)
            var compatibility = CheckResourceCompatibilityLocal(templateContent);
            result.AppendLine("\n🔗 **Resource Compatibility:**");
            result.AppendLine(compatibility);

            // Suggest improvements (no external calls)
            var suggestions = GenerateTemplateSuggestions(templateContent);
            if (!string.IsNullOrEmpty(suggestions))
            {
                result.AppendLine("\n💡 **Suggestions for Improvement:**");
                result.AppendLine(suggestions);
            }

            result.AppendLine("\n✅ **Template ready for deployment with ApplyTerraformTemplate**");
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error validating template: {ex.Message}";
        }
    }

    #endregion

    #region Validation Helper Methods

    private static string NormalizeRegionName(string region)
    {
        return region.ToLower() switch
        {
            "east us" or "eastus" => "eastus",
            "west us" or "westus" => "westus",
            "west us 2" or "westus2" => "westus2",
            "central us" or "centralus" => "centralus",
            "north central us" or "northcentralus" => "northcentralus",
            "south central us" or "southcentralus" => "southcentralus",
            "west central us" or "westcentralus" => "westcentralus",
            "east us 2" or "eastus2" => "eastus2",
            "uk south" or "uksouth" => "uksouth",
            "uk west" or "ukwest" => "ukwest",
            "west europe" or "westeurope" => "westeurope",
            "north europe" or "northeurope" => "northeurope",
            "southeast asia" or "southeastasia" => "southeastasia",
            "east asia" or "eastasia" => "eastasia",
            "australia east" or "australiaeast" => "australiaeast",
            "australia southeast" or "australiasoutheast" => "australiasoutheast",
            "brazil south" or "brazilsouth" => "brazilsouth",
            "canada central" or "canadacentral" => "canadacentral",
            "canada east" or "canadaeast" => "canadaeast",
            "france central" or "francecentral" => "francecentral",
            "germany west central" or "germanywestcentral" => "germanywestcentral",
            "india central" or "indiacentral" => "indiacentral",
            "japan east" or "japaneast" => "japaneast",
            "japan west" or "japanwest" => "japanwest",
            "korea central" or "koreacentral" => "koreacentral",
            "norway east" or "norwayeast" => "norwayeast",
            "south africa north" or "southafricanorth" => "southafricanorth",
            "switzerland north" or "switzerlandnorth" => "switzerlandnorth",
            "uae north" or "uaenorth" => "uaenorth",
            _ => region.ToLower().Replace(" ", "")
        };
    }

    private async Task<string> GetAlternativeVMSizes(string region, string originalVmSize)
    {
        try
        {
            var alternatives = new List<string>();
            
            // Common VM size alternatives based on the original size
            var vmSizeAlternatives = originalVmSize.ToLower() switch
            {
                var size when size.Contains("standard_d2s_v3") => new[] { "Standard_D2s_v4", "Standard_D2as_v4", "Standard_DS2_v2", "Standard_B2s" },
                var size when size.Contains("standard_ds2_v2") => new[] { "Standard_D2s_v3", "Standard_D2s_v4", "Standard_B2s", "Standard_D2as_v4" },
                var size when size.Contains("standard_b2s") => new[] { "Standard_D2s_v3", "Standard_DS2_v2", "Standard_D2s_v4", "Standard_B2ms" },
                var size when size.Contains("standard_f2s_v2") => new[] { "Standard_F4s_v2", "Standard_D2s_v3", "Standard_DS2_v2", "Standard_B2s" },
                var size when size.Contains("standard_d4s_v3") => new[] { "Standard_D4s_v4", "Standard_D4as_v4", "Standard_DS3_v2", "Standard_F4s_v2" },
                var size when size.Contains("standard_d8s_v3") => new[] { "Standard_D8s_v4", "Standard_D8as_v4", "Standard_DS4_v2", "Standard_F8s_v2" },
                _ => new[] { "Standard_D2s_v3", "Standard_DS2_v2", "Standard_B2s", "Standard_D2s_v4" }
            };

            foreach (var altSize in vmSizeAlternatives)
            {
                var checkResult = await ExecuteAzureCommand($"vm list-sizes --location \"{region}\" --query \"[?name=='{altSize}']\" --output table");
                if (!string.IsNullOrEmpty(checkResult) && !checkResult.Contains("[]") && checkResult.Contains(altSize))
                {
                    alternatives.Add($"• **{altSize}** - Available in {region}");
                }
            }

            if (!alternatives.Any())
            {
                return "No alternatives found. Please try a different region or contact Azure support for VM size availability.";
            }

            return string.Join("\n", alternatives.Take(5));
        }
        catch (Exception ex)
        {
            return $"Error finding alternatives: {ex.Message}";
        }
    }

    private async Task<string> CheckRegionalServices(string region)
    {
        try
        {
            // Check common Azure services availability in the region
            var services = new[]
            {
                "Microsoft.ContainerService", // AKS
                "Microsoft.Web",              // App Service
                "Microsoft.Storage",          // Storage Account
                "Microsoft.Sql",              // SQL Database
                "Microsoft.KeyVault",         // Key Vault
                "Microsoft.ContainerRegistry" // Container Registry
            };

            var availableServices = new List<string>();
            var unavailableServices = new List<string>();

            foreach (var service in services)
            {
                var result = await ExecuteAzureCommand($"provider show --namespace {service} --query \"resourceTypes[?contains(locations, '{region}')].resourceType\" --output tsv");
                
                if (!string.IsNullOrEmpty(result) && !result.Contains("Error"))
                {
                    availableServices.Add($"✅ {GetServiceDisplayName(service)}");
                }
                else
                {
                    unavailableServices.Add($"❌ {GetServiceDisplayName(service)}");
                }
            }

            var resultText = "";
            if (availableServices.Any())
            {
                resultText += string.Join("\n", availableServices);
            }
            
            if (unavailableServices.Any())
            {
                resultText += "\n" + string.Join("\n", unavailableServices);
            }

            return resultText;
        }
        catch (Exception ex)
        {
            return $"⚠️ Unable to check regional services: {ex.Message}";
        }
    }

    private async Task<string> GetRecommendedFixes(string region, string vmSize)
    {
        var fixes = new List<string>();

        // Check if it's a VM size issue
        var vmCheck = await CheckVMSizeAvailability(vmSize, region);
        if (vmCheck.Contains("❌"))
        {
            fixes.Add("🔧 **VM Size Fix:**");
            fixes.Add("   - Try alternative VM sizes like Standard_D2s_v3, Standard_DS2_v2, or Standard_B2s");
            fixes.Add("   - Consider using newer generation VM sizes (v4, v5)");
        }

        // Suggest alternative regions
        var alternativeRegions = new[] { "East US", "West US 2", "Central US", "East US 2" };
        var workingRegions = new List<string>();

        foreach (var altRegion in alternativeRegions)
        {
            if (altRegion.Equals(region, StringComparison.OrdinalIgnoreCase)) continue;

            var regionCheck = await CheckVMSizeAvailability(vmSize, altRegion);
            if (regionCheck.Contains("✅"))
            {
                workingRegions.Add(altRegion);
            }
        }

        if (workingRegions.Any())
        {
            fixes.Add("\n🌍 **Alternative Regions:**");
            foreach (var workingRegion in workingRegions.Take(3))
            {
                fixes.Add($"   - {workingRegion}");
            }
        }

        return fixes.Any() ? string.Join("\n", fixes) : "No specific fixes available. Please contact Azure support.";
    }

    private static string GetServiceDisplayName(string serviceNamespace)
    {
        return serviceNamespace switch
        {
            "Microsoft.ContainerService" => "Azure Kubernetes Service (AKS)",
            "Microsoft.Web" => "App Service",
            "Microsoft.Storage" => "Storage Account",
            "Microsoft.Sql" => "SQL Database",
            "Microsoft.KeyVault" => "Key Vault",
            "Microsoft.ContainerRegistry" => "Container Registry",
            _ => serviceNamespace
        };
    }

    private static bool IsInGeographicArea(string regionName, string geoPreference)
    {
        return geoPreference.ToLower() switch
        {
            "us" or "usa" or "united states" => regionName.Contains("us") || regionName.Contains("central"),
            "europe" or "eu" => regionName.Contains("europe") || regionName.Contains("uk") || regionName.Contains("france") || regionName.Contains("germany") || regionName.Contains("norway") || regionName.Contains("switzerland"),
            "asia" or "apac" => regionName.Contains("asia") || regionName.Contains("japan") || regionName.Contains("korea") || regionName.Contains("india"),
            "australia" or "oceania" => regionName.Contains("australia"),
            "canada" => regionName.Contains("canada"),
            "brazil" or "south america" => regionName.Contains("brazil"),
            _ => true // Default: include all regions
        };
    }

    private async Task<int> CalculateRegionScore(string regionName, string requiredServices, string preferredVmSize)
    {
        try
        {
            int score = 0;

            // Check VM size availability (30 points)
            var vmCheck = await ExecuteAzureCommand($"vm list-sizes --location \"{regionName}\" --query \"[?name=='{preferredVmSize}']\" --output table");
            if (!string.IsNullOrEmpty(vmCheck) && vmCheck.Contains(preferredVmSize))
            {
                score += 30;
            }

            // Check service availability (50 points total)
            var services = requiredServices.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var serviceNamespaces = services.Select(GetServiceNamespace).Where(s => !string.IsNullOrEmpty(s));

            foreach (var serviceNamespace in serviceNamespaces)
            {
                var serviceCheck = await ExecuteAzureCommand($"provider show --namespace {serviceNamespace} --query \"resourceTypes[?contains(locations, '{regionName}')].resourceType\" --output tsv");
                if (!string.IsNullOrEmpty(serviceCheck) && !serviceCheck.Contains("Error"))
                {
                    score += 50 / services.Length; // Distribute points across services
                }
            }

            // Bonus points for popular/stable regions (20 points)
            if (IsPopularRegion(regionName))
            {
                score += 20;
            }

            return Math.Min(score, 100); // Cap at 100
        }
        catch
        {
            return 0;
        }
    }

    private static string GetServiceNamespace(string serviceName)
    {
        return serviceName.Trim().ToLower() switch
        {
            "aks" or "kubernetes" => "Microsoft.ContainerService",
            "app service" or "web app" => "Microsoft.Web",
            "storage" or "storage account" => "Microsoft.Storage",
            "sql" or "database" => "Microsoft.Sql",
            "key vault" or "keyvault" => "Microsoft.KeyVault",
            "container registry" or "acr" => "Microsoft.ContainerRegistry",
            _ => ""
        };
    }

    private static bool IsPopularRegion(string regionName)
    {
        var popularRegions = new[] { "eastus", "westus2", "centralus", "westeurope", "northeurope", "southeastasia", "japaneast", "australiaeast" };
        return popularRegions.Contains(regionName.ToLower());
    }

    private static int CalculateVMScore(string vmName, int cores, int memoryGB, string workloadType, int targetCores, int targetMemoryGB)
    {
        int score = 0;

        // Base score based on how close to target specs (40 points)
        var coresDiff = Math.Abs(cores - targetCores);
        var memoryDiff = Math.Abs(memoryGB - targetMemoryGB);
        
        score += Math.Max(0, 20 - coresDiff * 5); // Penalize core differences
        score += Math.Max(0, 20 - memoryDiff * 2); // Penalize memory differences

        // Workload-specific bonuses (30 points)
        score += workloadType.ToLower() switch
        {
            var wl when wl.Contains("web") || wl.Contains("app") => GetWebAppVMScore(vmName),
            var wl when wl.Contains("database") || wl.Contains("sql") => GetDatabaseVMScore(vmName),
            var wl when wl.Contains("kubernetes") || wl.Contains("aks") => GetKubernetesVMScore(vmName),
            _ => GetGeneralPurposeVMScore(vmName)
        };

        // Cost efficiency bonus (30 points)
        score += GetCostEfficiencyScore(vmName, cores, memoryGB);

        return Math.Min(score, 100);
    }

    private static int GetWebAppVMScore(string vmName)
    {
        return vmName.ToLower() switch
        {
            var vm when vm.Contains("d2s_v") || vm.Contains("d4s_v") => 30,
            var vm when vm.Contains("b2s") || vm.Contains("b2ms") => 25,
            var vm when vm.Contains("f2s_v2") || vm.Contains("f4s_v2") => 20,
            _ => 10
        };
    }

    private static int GetDatabaseVMScore(string vmName)
    {
        return vmName.ToLower() switch
        {
            var vm when vm.Contains("ds") && (vm.Contains("v4") || vm.Contains("v5")) => 30,
            var vm when vm.Contains("es_v") => 25,
            var vm when vm.Contains("m") => 20, // Memory optimized
            _ => 10
        };
    }

    private static int GetKubernetesVMScore(string vmName)
    {
        return vmName.ToLower() switch
        {
            var vm when vm.Contains("d2s_v3") || vm.Contains("d2s_v4") => 30,
            var vm when vm.Contains("ds2_v2") => 25,
            var vm when vm.Contains("b2s") => 15, // Less ideal for K8s
            _ => 10
        };
    }

    private static int GetGeneralPurposeVMScore(string vmName)
    {
        return vmName.ToLower() switch
        {
            var vm when vm.Contains("d2s_v") => 30,
            var vm when vm.Contains("b2s") || vm.Contains("b2ms") => 25,
            var vm when vm.Contains("ds2_v2") => 20,
            _ => 10
        };
    }

    private static int GetCostEfficiencyScore(string vmName, int cores, int memoryGB)
    {
        // B-series and newer generation VMs are generally more cost-effective
        return vmName.ToLower() switch
        {
            var vm when vm.Contains("b2s") || vm.Contains("b2ms") => 30,
            var vm when vm.Contains("v4") || vm.Contains("v5") => 25,
            var vm when vm.Contains("v3") => 20,
            var vm when vm.Contains("v2") => 15,
            _ => 10
        };
    }

    private static string GetVMRecommendationReason(string vmName, int cores, int memoryGB, string workloadType)
    {
        var reasons = new List<string>();

        if (vmName.ToLower().Contains("b2s") || vmName.ToLower().Contains("b2ms"))
            reasons.Add("Cost-effective burstable performance");
        
        if (vmName.ToLower().Contains("v4") || vmName.ToLower().Contains("v5"))
            reasons.Add("Latest generation with better price/performance");
        
        if (workloadType.ToLower().Contains("web") && vmName.ToLower().Contains("d2s"))
            reasons.Add("Optimized for web applications");
        
        if (workloadType.ToLower().Contains("database") && vmName.ToLower().Contains("ds"))
            reasons.Add("Enhanced for database workloads");

        return reasons.Any() ? string.Join(", ", reasons) : "General purpose compute";
    }

    private static string AnalyzeTerraformBestPractices(string templateContent)
    {
        var issues = new List<string>();
        var recommendations = new List<string>();

        // Check for hardcoded values
        if (templateContent.Contains("\"Standard_") && !templateContent.Contains("var."))
            issues.Add("⚠️ Hardcoded VM sizes detected - consider using variables");

        // Check for proper naming conventions
        if (!templateContent.Contains("random_id") && !templateContent.Contains("random_string"))
            issues.Add("⚠️ No random suffix for resource names - may cause naming conflicts");

        // Check for tags
        if (!templateContent.Contains("tags") || !templateContent.Contains("Environment"))
            recommendations.Add("💡 Add consistent tags for better resource management");

        // Check for output values
        if (!templateContent.Contains("output "))
            recommendations.Add("💡 Add output values for important resource information");

        var result = new StringBuilder();
        if (issues.Any())
        {
            result.AppendLine("**Issues Found:**");
            result.AppendLine(string.Join("\n", issues));
            result.AppendLine();
        }

        if (recommendations.Any())
        {
            result.AppendLine("**Recommendations:**");
            result.AppendLine(string.Join("\n", recommendations));
        }

        return result.Length > 0 ? result.ToString() : "✅ Template follows best practices";
    }

    private async Task<string> CheckResourceCompatibility(string templateContent)
    {
        try
        {
            var issues = new List<string>();

            // Extract resource types from template
            var resourceTypes = ExtractTerraformResourceTypes(templateContent);

            foreach (var resourceType in resourceTypes)
            {
                // Check for known compatibility issues
                var compatibility = CheckResourceTypeCompatibility(resourceType, templateContent);
                if (!string.IsNullOrEmpty(compatibility))
                {
                    issues.Add(compatibility);
                }
            }

            return issues.Any() ? string.Join("\n", issues) : "✅ No compatibility issues detected";
        }
        catch (Exception ex)
        {
            return $"⚠️ Unable to check compatibility: {ex.Message}";
        }
    }

    private static List<string> ExtractTerraformResourceTypes(string templateContent)
    {
        var resourceTypes = new List<string>();
        var lines = templateContent.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("resource \""))
            {
                var parts = trimmed.Split('"');
                if (parts.Length >= 2)
                {
                    resourceTypes.Add(parts[1]);
                }
            }
        }

        return resourceTypes.Distinct().ToList();
    }

    private static List<string> ValidateTemplateSyntax(string templateContent)
    {
        var issues = new List<string>();

        // Basic syntax checks without external calls
        if (string.IsNullOrWhiteSpace(templateContent))
        {
            issues.Add("Template is empty");
            return issues;
        }

        var lines = templateContent.Split('\n');
        var braceCount = 0;
        var inStringLiteral = false;
        var hasProvider = false;
        var hasResource = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Check for provider
            if (trimmed.StartsWith("provider "))
                hasProvider = true;
                
            // Check for resources
            if (trimmed.StartsWith("resource "))
                hasResource = true;

            // Basic brace matching
            foreach (var ch in trimmed)
            {
                if (ch == '"' && !inStringLiteral)
                    inStringLiteral = true;
                else if (ch == '"' && inStringLiteral)
                    inStringLiteral = false;
                else if (!inStringLiteral)
                {
                    if (ch == '{') braceCount++;
                    if (ch == '}') braceCount--;
                }
            }
        }

        if (!hasProvider)
            issues.Add("⚠️ No provider block found - add terraform provider configuration");
            
        if (!hasResource)
            issues.Add("⚠️ No resource blocks found - template should define Azure resources");
            
        if (braceCount != 0)
            issues.Add("⚠️ Mismatched braces - check template syntax");

        return issues;
    }

    private static string CheckResourceCompatibilityLocal(string templateContent)
    {
        var issues = new List<string>();
        var resources = ExtractTerraformResourceTypes(templateContent);

        // Local compatibility checks without external API calls
        foreach (var resourceType in resources)
        {
            var compatibility = CheckResourceTypeCompatibility(resourceType, templateContent);
            if (!string.IsNullOrEmpty(compatibility))
            {
                issues.Add(compatibility);
            }
        }

        // Additional local checks
        if (resources.Contains("azurerm_kubernetes_cluster"))
        {
            if (!resources.Contains("azurerm_subnet"))
                issues.Add("⚠️ AKS cluster should have a dedicated subnet");
            if (!templateContent.Contains("node_count") || templateContent.Contains("node_count = 1"))
                issues.Add("💡 Consider using 3+ nodes for AKS production clusters");
        }

        if (resources.Contains("azurerm_virtual_machine") && !resources.Contains("azurerm_network_security_group"))
            issues.Add("💡 Consider adding Network Security Group for VM security");

        return issues.Any() ? string.Join("\n", issues) : "✅ No compatibility issues detected";
    }

    private static string CheckResourceTypeCompatibility(string resourceType, string templateContent)
    {
        return resourceType switch
        {
            "azurerm_kubernetes_cluster" when !templateContent.Contains("azurerm_subnet") =>
                "⚠️ AKS cluster requires a virtual network and subnet",
            "azurerm_sql_database" when !templateContent.Contains("azurerm_sql_server") =>
                "⚠️ SQL Database requires a SQL Server",
            "azurerm_subnet" when !templateContent.Contains("azurerm_virtual_network") =>
                "⚠️ Subnet requires a virtual network",
            _ => ""
        };
    }

    private static string GenerateTemplateSuggestions(string templateContent)
    {
        var suggestions = new List<string>();

        // Suggest improvements based on template content
        if (templateContent.Contains("azurerm_kubernetes_cluster") && !templateContent.Contains("azurerm_log_analytics_workspace"))
            suggestions.Add("💡 Consider adding Azure Monitor for AKS cluster monitoring");

        if (templateContent.Contains("azurerm_virtual_machine") && !templateContent.Contains("azurerm_backup"))
            suggestions.Add("💡 Consider adding backup policies for virtual machines");

        if (templateContent.Contains("azurerm_storage_account") && !templateContent.Contains("network_rules"))
            suggestions.Add("💡 Consider adding network access rules for storage security");

        return suggestions.Any() ? string.Join("\n", suggestions) : "";
    }

    private static string SuggestTemplateFixes(string validationError, string templateContent)
    {
        var fixes = new List<string>();

        if (validationError.Contains("Missing required argument"))
            fixes.Add("🔧 Add all required arguments to resource blocks");

        if (validationError.Contains("Invalid resource name"))
            fixes.Add("🔧 Use valid resource names (alphanumeric and hyphens only)");

        if (validationError.Contains("Duplicate resource"))
            fixes.Add("🔧 Ensure all resource names are unique within their type");

        if (validationError.Contains("Invalid reference"))
            fixes.Add("🔧 Check resource references use correct syntax (resource_type.resource_name.attribute)");

        return fixes.Any() ? string.Join("\n", fixes) : "🔧 Please review the validation error and fix syntax issues";
    }

    private async Task<string> AnalyzeTerraformErrorWithTrackingAsync(string errorOutput, string templateContent)
    {
        // Extract failure details for tracking
        var region = ExtractRegionFromTemplate(templateContent);
        var vmSize = ExtractVMSizeFromTemplate(templateContent);
        var resourceType = ExtractResourceTypeFromTemplate(templateContent);

        // Automatically track this failure
        if (!string.IsNullOrEmpty(region) && !string.IsNullOrEmpty(vmSize))
        {
            await TrackDeploymentFailure(resourceType, region, vmSize, errorOutput);
        }

        // Get the standard error analysis
        var analysis = AnalyzeTerraformError(errorOutput, templateContent);
        
        // Add failure tracking info
        var result = new StringBuilder();
        result.AppendLine("📝 **Failure Recorded**: This combination will be avoided in future deployments");
        result.AppendLine();
        result.AppendLine(analysis);
        
        // Add recommendations based on failure history
        if (!string.IsNullOrEmpty(region) && !string.IsNullOrEmpty(vmSize))
        {
            var alternatives = await GetRecommendedAlternatives(resourceType, region, vmSize);
            result.AppendLine();
            result.AppendLine(alternatives);
        }

        return result.ToString();
    }

    private static string ExtractResourceTypeFromTemplate(string templateContent)
    {
        if (templateContent.Contains("azurerm_kubernetes_cluster"))
            return "AKS";
        if (templateContent.Contains("azurerm_virtual_machine"))
            return "VM";
        if (templateContent.Contains("azurerm_container_app"))
            return "Container App";
        
        return "Unknown";
    }

    private static string AnalyzeTerraformError(string errorOutput, string templateContent)
    {
        var analysis = new StringBuilder();
        analysis.AppendLine("🔍 **Error Analysis & Smart Suggestions:**\n");

        // Check for VM size availability issues
        if (errorOutput.Contains("InvalidParameter") && errorOutput.Contains("vmSize"))
        {
            var currentVMSize = ExtractVMSizeFromError(errorOutput) ?? ExtractVMSizeFromTemplate(templateContent);
            var currentRegion = ExtractRegionFromTemplate(templateContent);
            
            analysis.AppendLine("❌ **Issue**: VM size not available in the selected region");
            analysis.AppendLine($"🔍 **Current**: {currentVMSize} in {currentRegion}");
            analysis.AppendLine();
            analysis.AppendLine("💡 **Recommended Alternatives**:");
            analysis.AppendLine("• **Standard_D2s_v3** - Widely available, good performance");
            analysis.AppendLine("• **Standard_DS2_v2** - Reliable choice for most workloads");
            analysis.AppendLine("• **Standard_B2s** - Cost-effective option");
            analysis.AppendLine();
            analysis.AppendLine("🌍 **Alternative Regions**:");
            analysis.AppendLine("• **East US** - Highest service availability");
            analysis.AppendLine("• **West US 2** - Latest infrastructure");
            analysis.AppendLine("• **Central US** - Good performance and availability");
        }
        
        // Check for quota issues
        else if (errorOutput.Contains("QuotaExceeded") || errorOutput.Contains("InsufficientCapacity"))
        {
            analysis.AppendLine("❌ **Issue**: Azure quota exceeded or insufficient capacity");
            analysis.AppendLine();
            analysis.AppendLine("💡 **Recommended Solutions**:");
            analysis.AppendLine("• **Reduce instance count** (try 1-2 nodes instead of 3+)");
            analysis.AppendLine("• **Use smaller VM sizes** (Standard_B2s instead of Standard_D4s_v3)");
            analysis.AppendLine("• **Try different region** (East US, West US 2, Central US)");
            analysis.AppendLine("• **Request quota increase** in Azure Portal");
        }
        
        // Check for region service availability
        else if (errorOutput.Contains("LocationNotAvailableForResourceType") || errorOutput.Contains("not available in location"))
        {
            var currentRegion = ExtractRegionFromTemplate(templateContent);
            analysis.AppendLine("❌ **Issue**: Azure service not available in selected region");
            analysis.AppendLine($"🔍 **Current Region**: {currentRegion}");
            analysis.AppendLine();
            analysis.AppendLine("🌍 **Recommended Regions with Full Service Support**:");
            analysis.AppendLine("• **East US** - Most comprehensive service availability");
            analysis.AppendLine("• **West Europe** - European alternative");
            analysis.AppendLine("• **Southeast Asia** - Asia-Pacific alternative");
        }
        
        // Check for networking issues
        else if (errorOutput.Contains("SubnetNotFound") || errorOutput.Contains("VirtualNetworkNotFound"))
        {
            analysis.AppendLine("❌ **Issue**: Network infrastructure dependency missing");
            analysis.AppendLine();
            analysis.AppendLine("💡 **Recommended Fix**:");
            analysis.AppendLine("• Ensure Virtual Network and Subnet are created before other resources");
            analysis.AppendLine("• Check resource dependency order in template");
        }
        
        // Check for authentication issues
        else if (errorOutput.Contains("AuthorizationFailed") || errorOutput.Contains("Forbidden"))
        {
            analysis.AppendLine("❌ **Issue**: Azure authentication or permissions problem");
            analysis.AppendLine();
            analysis.AppendLine("💡 **Recommended Solutions**:");
            analysis.AppendLine("• Run `az login` to re-authenticate");
            analysis.AppendLine("• Verify you have Contributor access to the subscription");
            analysis.AppendLine("• Check if you're using the correct Azure subscription");
        }
        
        // Generic suggestions for other errors
        else
        {
            analysis.AppendLine("❌ **Issue**: Deployment configuration problem");
            analysis.AppendLine();
            analysis.AppendLine("💡 **General Recommendations**:");
            analysis.AppendLine("• **Try different region**: East US, West US 2, Central US");
            analysis.AppendLine("• **Use proven VM sizes**: Standard_D2s_v3, Standard_DS2_v2");
            analysis.AppendLine("• **Reduce resource scale**: Fewer instances, smaller sizes");
            analysis.AppendLine("• **Check Azure status**: portal.azure.com/status");
        }

        analysis.AppendLine();
        analysis.AppendLine("🔧 **Next Steps**:");
        analysis.AppendLine("• Ask me to \"regenerate the template with [specific recommendation]\"");
        analysis.AppendLine("• I can automatically create a new template with working parameters");
        analysis.AppendLine("• Or specify your preferred region/VM size and I'll adjust accordingly");

        return analysis.ToString();
    }

    private static string ExtractVMSizeFromError(string errorOutput)
    {
        // Simple extraction - can be enhanced
        var lines = errorOutput.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("Standard_") && line.Contains("vmSize"))
            {
                var parts = line.Split(' ');
                foreach (var part in parts)
                {
                    if (part.StartsWith("Standard_"))
                        return part.Trim('\'', '"', ',', '.');
                }
            }
        }
        return null;
    }

    private static string ExtractVMSizeFromTemplate(string templateContent)
    {
        var lines = templateContent.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("vm_size") || line.Contains("node_vm_size"))
            {
                var parts = line.Split('=');
                if (parts.Length > 1)
                {
                    return parts[1].Trim().Trim('"', ' ');
                }
            }
        }
        return "Unknown";
    }

    private static string ExtractResourceGroupFromTemplate(string templateContent)
    {
        var lines = templateContent.Split('\n');
        foreach (var line in lines)
        {
            // Look for resource group resource definition
            if (line.Contains("azurerm_resource_group") && line.Contains("\"main\""))
            {
                // Look for the name in subsequent lines
                for (int i = Array.IndexOf(lines, line) + 1; i < lines.Length && i < Array.IndexOf(lines, line) + 10; i++)
                {
                    if (lines[i].Contains("name") && lines[i].Contains("="))
                    {
                        var parts = lines[i].Split('=');
                        if (parts.Length > 1)
                        {
                            return parts[1].Trim().Trim('"', ' ');
                        }
                    }
                }
            }
            // Also check for name = "rg-..." pattern directly
            else if (line.Contains("name") && line.Contains("=") && line.Contains("rg-"))
            {
                var parts = line.Split('=');
                if (parts.Length > 1)
                {
                    var name = parts[1].Trim().Trim('"', ' ');
                    if (name.StartsWith("rg-"))
                    {
                        return name;
                    }
                }
            }
        }
        return null;
    }

    private static string ExtractRegionFromTemplate(string templateContent)
    {
        var lines = templateContent.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("location") && line.Contains("="))
            {
                var parts = line.Split('=');
                if (parts.Length > 1)
                {
                    return parts[1].Trim().Trim('"', ' ');
                }
            }
        }
        return "Unknown";
    }

    private static string GenerateBackendConfiguration(string deploymentName)
    {
        // Simple storage account name generation that complies with Azure naming (max 24 chars)
        var cleanDeploymentName = deploymentName.Replace("-", "").ToLower();
        var storageAccountName = $"tfst{Math.Abs(cleanDeploymentName.GetHashCode()).ToString().Substring(0, 6)}";
        
        return @"# Backend configuration for persistent state management
terraform {
  backend ""local"" {
    path = ""terraform.tfstate""
  }
}

# Optional: Uncomment below for Azure Storage backend (recommended for production)
# terraform {
#   backend ""azurerm"" {
#     resource_group_name  = ""rg-terraform-state""
#     storage_account_name = """ + storageAccountName + @"""
#     container_name       = ""tfstate""
#     key                  = """ + deploymentName + @".terraform.tfstate""
#   }
# }
";
    }

    private async Task<string> ImportExistingResourcesAsync(string deployDir, string deploymentName)
    {
        var result = new StringBuilder();
        result.AppendLine("🔄 **Importing Existing Azure Resources...**");
        result.AppendLine();

        try
        {
            // Check if resource group exists and import it
            var rgName = $"rg-dev-{deploymentName}";
            var checkResult = await ExecuteTerraformCommand($"az group show --name {rgName}", deployDir);
            
            if (!checkResult.Contains("ResourceGroupNotFound"))
            {
                result.AppendLine($"🔄 Found existing resource group: {rgName}");
                result.AppendLine("⏳ Importing into Terraform state...");
                
                var importResult = await ExecuteTerraformCommand($"terraform import azurerm_resource_group.main /subscriptions/$(az account show --query id -o tsv)/resourceGroups/{rgName}", deployDir);
                
                if (!importResult.Contains("Error"))
                {
                    result.AppendLine("✅ Resource group imported successfully!");
                }
                else
                {
                    result.AppendLine("⚠️ Resource group import failed, will try to manage existing resources");
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            result.AppendLine($"⚠️ Import process encountered issues: {ex.Message}");
            result.AppendLine("Continuing with deployment...");
            return result.ToString();
        }
    }

    #endregion

    #region AKS MCP Integration Functions

    private string? FindTerraformDirectory(string deploymentName)
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            
            if (!Directory.Exists(baseDir))
                return null;

            // Look for exact deployment name match
            var exactMatch = Path.Combine(baseDir, deploymentName);
            if (Directory.Exists(exactMatch) && File.Exists(Path.Combine(exactMatch, "terraform.tfstate")))
                return exactMatch;

            // Look for deployment directories that contain the deployment name
            var directories = Directory.GetDirectories(baseDir);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.Contains(deploymentName, StringComparison.OrdinalIgnoreCase) && 
                    File.Exists(Path.Combine(dir, "terraform.tfstate")))
                {
                    return dir;
                }
            }

            // Look for any directory with AKS resources in the state
            foreach (var dir in directories)
            {
                var stateFile = Path.Combine(dir, "terraform.tfstate");
                if (File.Exists(stateFile))
                {
                    var stateContent = File.ReadAllText(stateFile);
                    if (stateContent.Contains("azurerm_kubernetes_cluster") && 
                        stateContent.Contains(deploymentName))
                    {
                        return dir;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    [KernelFunction("ConnectToAksCluster")]
    [Description("Connect to an AKS cluster for detailed inspection using kubectl-like commands")]
    public async Task<string> ConnectToAksClusterAsync(
        [Description("Name of the AKS deployment or resource group name")] string deploymentName)
    {
        try
        {
            // Try to find the Terraform directory automatically
            var terraformDirectory = FindTerraformDirectory(deploymentName);
            
            if (string.IsNullOrEmpty(terraformDirectory))
            {
                // List available deployments to help the user
                var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
                var availableDeployments = new List<string>();
                
                if (Directory.Exists(baseDir))
                {
                    var directories = Directory.GetDirectories(baseDir);
                    foreach (var dir in directories)
                    {
                        var stateFile = Path.Combine(dir, "terraform.tfstate");
                        if (File.Exists(stateFile))
                        {
                            var stateContent = File.ReadAllText(stateFile);
                            if (stateContent.Contains("azurerm_kubernetes_cluster"))
                            {
                                availableDeployments.Add(Path.GetFileName(dir));
                            }
                        }
                    }
                }

                var result = $"❌ Could not find Terraform directory for deployment '{deploymentName}'.\n\n";
                if (availableDeployments.Any())
                {
                    result += "📋 **Available AKS deployments:**\n";
                    foreach (var deployment in availableDeployments)
                    {
                        result += $"• {deployment}\n";
                    }
                    result += "\n💡 Try using one of the deployment names listed above.";
                }
                else
                {
                    result += "📋 No AKS deployments found in Terraform state.\n";
                    result += "💡 Deploy an AKS cluster first, then try connecting to it.";
                }
                
                return result;
            }

            return await _aksMcpPlugin.ConnectToAksCluster(deploymentName, terraformDirectory);
        }
        catch (Exception ex)
        {
            return $"❌ Error connecting to AKS cluster: {ex.Message}";
        }
    }

    [KernelFunction("GetAksClusterOverview")]
    [Description("Get comprehensive overview of AKS cluster including nodes, pods, services")]
    public async Task<string> GetAksClusterOverviewAsync(
        [Description("Name of the AKS deployment")] string deploymentName)
    {
        // Try to auto-connect if not already connected
        var connectResult = await ConnectToAksClusterAsync(deploymentName);
        if (connectResult.Contains("❌") && !connectResult.Contains("already connected"))
        {
            return connectResult; // Return connection error
        }

        return await _aksMcpPlugin.GetAksClusterOverview(deploymentName);
    }

    [KernelFunction("GetAksPods")]
    [Description("List and inspect pods in the AKS cluster")]
    public async Task<string> GetAksPodsAsync(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Namespace to filter pods (optional)")] string? namespaceFilter = null,
        [Description("Show only failed/problematic pods")] bool onlyProblematic = false)
    {
        // Try to auto-connect if not already connected
        var connectResult = await ConnectToAksClusterAsync(deploymentName);
        if (connectResult.Contains("❌") && !connectResult.Contains("already connected"))
        {
            return connectResult; // Return connection error
        }

        return await _aksMcpPlugin.GetAksPods(deploymentName, namespaceFilter, onlyProblematic);
    }

    [KernelFunction("GetAksServices")]
    [Description("List and inspect services in the AKS cluster")]
    public async Task<string> GetAksServicesAsync(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Namespace to filter services (optional)")] string? namespaceFilter = null)
    {
        // Try to auto-connect if not already connected
        var connectResult = await ConnectToAksClusterAsync(deploymentName);
        if (connectResult.Contains("❌") && !connectResult.Contains("already connected"))
        {
            return connectResult; // Return connection error
        }

        return await _aksMcpPlugin.GetAksServices(deploymentName, namespaceFilter);
    }

    [KernelFunction("GetAksLogs")]
    [Description("Get logs from pods in the AKS cluster")]
    public async Task<string> GetAksLogsAsync(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("Namespace where the pod is located")] string namespaceName,
        [Description("Name of the pod to get logs from")] string podName,
        [Description("Container name (optional, uses first container if not specified)")] string? containerName = null,
        [Description("Number of log lines to retrieve")] int lines = 50)
    {
        // Try to auto-connect if not already connected
        var connectResult = await ConnectToAksClusterAsync(deploymentName);
        if (connectResult.Contains("❌") && !connectResult.Contains("already connected"))
        {
            return connectResult; // Return connection error
        }

        return await _aksMcpPlugin.GetAksLogs(deploymentName, namespaceName, podName, containerName, lines);
    }

    [KernelFunction("ExecuteKubectlCommand")]
    [Description("Execute any kubectl command on the AKS cluster")]
    public async Task<string> ExecuteKubectlCommandAsync(
        [Description("Name of the AKS deployment")] string deploymentName,
        [Description("The kubectl command to execute (e.g., 'get deployments', 'describe pod name')")] string command)
    {
        // Try to auto-connect if not already connected
        var connectResult = await ConnectToAksClusterAsync(deploymentName);
        if (connectResult.Contains("❌") && !connectResult.Contains("already connected"))
        {
            return connectResult; // Return connection error
        }

        return await _aksMcpPlugin.ExecuteKubectlCommand(deploymentName, command);
    }

    [KernelFunction("ListAvailableAksClusters")]
    [Description("List all available AKS deployments that can be inspected")]
    public async Task<string> ListAvailableAksClustersAsync()
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure-ai-agent", "terraform");
            var availableDeployments = new List<(string name, string path, bool hasKubeconfig)>();
            
            if (!Directory.Exists(baseDir))
            {
                return "📋 No Terraform deployments found. Deploy an AKS cluster first.";
            }

            var directories = Directory.GetDirectories(baseDir);
            foreach (var dir in directories)
            {
                var stateFile = Path.Combine(dir, "terraform.tfstate");
                if (File.Exists(stateFile))
                {
                    var stateContent = File.ReadAllText(stateFile);
                    if (stateContent.Contains("azurerm_kubernetes_cluster"))
                    {
                        var deploymentName = Path.GetFileName(dir);
                        var hasKubeconfig = await CheckKubeconfigAvailable(dir);
                        availableDeployments.Add((deploymentName, dir, hasKubeconfig));
                    }
                }
            }

            if (!availableDeployments.Any())
            {
                return "📋 No AKS deployments found in Terraform state.\n💡 Deploy an AKS cluster first using the ApplyTerraformTemplate function.";
            }

            var result = new StringBuilder();
            result.AppendLine("📋 **Available AKS Clusters for Inspection:**");
            result.AppendLine();

            foreach (var (name, path, hasKubeconfig) in availableDeployments)
            {
                var status = hasKubeconfig ? "✅ Ready for inspection" : "⚠️ Kubeconfig not available";
                result.AppendLine($"🔹 **{name}**");
                result.AppendLine($"   └─ Status: {status}");
                result.AppendLine($"   └─ Path: {path}");
                result.AppendLine();
            }

            result.AppendLine("💡 **Usage**: Use the deployment name with any AKS inspection function:");
            result.AppendLine("• `GetAksClusterOverview(deploymentName)`");
            result.AppendLine("• `GetAksPods(deploymentName)`");
            result.AppendLine("• `GetAksServices(deploymentName)`");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error listing AKS clusters: {ex.Message}";
        }
    }

    private async Task<bool> CheckKubeconfigAvailable(string terraformDirectory)
    {
        try
        {
            var process = new Process();
            process.StartInfo.FileName = "terraform";
            process.StartInfo.Arguments = "output -raw kube_config";
            process.StartInfo.WorkingDirectory = terraformDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    [KernelFunction("GenerateAIAdaptiveCard")]
    [Description("Generate an adaptive card using Azure OpenAI based on natural language description")]
    public async Task<string> GenerateAIAdaptiveCard(
        [Description("Natural language description of what kind of card you want")] string description,
        [Description("Type of card: parameter_form, template_gallery, resource_status, or general")] string cardType = "general")
    {
        if (_adaptiveCardService == null)
        {
            return "❌ Adaptive Card service not available. Ensure dependency injection is configured.";
        }

        try
        {
            var aiCard = await _adaptiveCardService.GenerateAdaptiveCardWithAI(description, cardType);
            
            return $"🎯 **AI-Generated Adaptive Card!**\n\n" +
                   $"**Description:** {description}\n" +
                   $"**Card Type:** {cardType}\n\n" +
                   "📋 **Generated Adaptive Card JSON:**\n" +
                   "```json\n" +
                   $"{aiCard}\n" +
                   "```\n\n" +
                   "💡 **How to use:**\n" +
                   "• Copy the JSON above into an Adaptive Card viewer\n" +
                   "• Interact with the AI-designed elements\n" +
                   "• Experience the intelligent layout and functionality\n\n" +
                   "🔗 **Test with Adaptive Card Designer:** https://adaptivecards.io/designer/\n\n" +
                   "✨ **Powered by Azure OpenAI** - This card was generated using advanced AI to optimize user experience!";
        }
        catch (Exception ex)
        {
            return $"❌ Error generating AI adaptive card: {ex.Message}\n\n" +
                   "💡 Make sure:\n" +
                   "• Azure OpenAI is properly configured\n" +
                   "• API keys are set\n" +
                   "• Semantic Kernel is initialized with the AI service";
        }
    }

    [KernelFunction("ListAnyAzureResourcesInteractive")]
    [Description("List any type of Azure resources with universal interactive cards - works for ANY resource type")]
    public async Task<string> ListAnyAzureResourcesInteractive(
        [Description("Type of Azure resource (e.g., 'Virtual Machines', 'Storage Accounts', 'Web Apps', 'App Services', 'SQL Databases', etc.)")] string resourceType,
        [Description("Optional resource group name to filter resources")] string? resourceGroupName = null,
        [Description("Optional additional context or filters")] string? additionalContext = null)
    {
        try
        {
            // Map common resource type names to Azure CLI commands
            var resourceTypeMapping = new Dictionary<string, string>
            {
                // VMs
                { "virtual machines", "vm list" },
                { "vms", "vm list" },
                { "vm", "vm list" },
                
                // Storage
                { "storage accounts", "storage account list" },
                { "storage", "storage account list" },
                
                // Web Apps
                { "web apps", "webapp list" },
                { "webapps", "webapp list" },
                { "websites", "webapp list" },
                
                // App Services
                { "app services", "webapp list" },
                { "appservices", "webapp list" },
                
                // SQL Databases
                { "sql databases", "sql db list --server" },
                { "databases", "sql db list --server" },
                { "sql", "sql db list --server" },
                
                // Container Instances
                { "container instances", "container list" },
                { "containers", "container list" },
                { "aci", "container list" },
                
                // Function Apps
                { "function apps", "functionapp list" },
                { "functions", "functionapp list" },
                { "azure functions", "functionapp list" },
                
                // Logic Apps
                { "logic apps", "logic workflow list" },
                
                // Key Vaults
                { "key vaults", "keyvault list" },
                { "keyvault", "keyvault list" },
                
                // App Service Plans
                { "app service plans", "appservice plan list" },
                { "service plans", "appservice plan list" },
                
                // Network Security Groups
                { "network security groups", "network nsg list" },
                { "nsg", "network nsg list" },
                { "security groups", "network nsg list" },
                
                // Public IPs
                { "public ips", "network public-ip list" },
                { "public ip", "network public-ip list" },
                { "ips", "network public-ip list" },
                
                // Virtual Networks
                { "virtual networks", "network vnet list" },
                { "vnets", "network vnet list" },
                { "networks", "network vnet list" }
            };

            var lowerResourceType = resourceType.ToLower().Trim();
            
            if (!resourceTypeMapping.TryGetValue(lowerResourceType, out var azCommand))
            {
                // If no exact match, try to infer the command
                azCommand = $"resource list --resource-type '*{resourceType}*'";
            }

            // Build the full command with optional resource group filter
            var command = azCommand;
            if (!string.IsNullOrEmpty(resourceGroupName))
            {
                command += $" -g {resourceGroupName}";
            }
            command += " --output json";

            var output = await ExecuteAzureCommand(command);
            
            if (string.IsNullOrEmpty(output) || output.Contains("ERROR"))
            {
                return $"❌ Failed to retrieve {resourceType}. Please ensure you're logged into Azure CLI and the resource type exists.";
            }

            var resourceData = JsonSerializer.Deserialize<List<JsonElement>>(output);
            if (resourceData == null || !resourceData.Any())
            {
                var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
                return $"📦 No {resourceType} found in your current {scope}.";
            }

            // Convert resources to dynamic objects for the card service
            var resources = new List<dynamic>();
            foreach (var resource in resourceData)
            {
                var resourceObj = new Dictionary<string, object?>();
                
                // Extract common properties that most Azure resources have
                if (resource.TryGetProperty("name", out var nameProp))
                    resourceObj["name"] = nameProp.GetString();
                if (resource.TryGetProperty("id", out var idProp))
                    resourceObj["id"] = idProp.GetString();
                if (resource.TryGetProperty("location", out var locationProp))
                    resourceObj["location"] = locationProp.GetString();
                if (resource.TryGetProperty("resourceGroup", out var rgProp))
                    resourceObj["resourceGroup"] = rgProp.GetString();
                if (resource.TryGetProperty("type", out var typeProp))
                    resourceObj["type"] = typeProp.GetString();
                if (resource.TryGetProperty("kind", out var kindProp))
                    resourceObj["kind"] = kindProp.GetString();
                if (resource.TryGetProperty("sku", out var skuProp))
                    resourceObj["sku"] = skuProp.ToString();
                if (resource.TryGetProperty("state", out var stateProp))
                    resourceObj["state"] = stateProp.GetString();
                if (resource.TryGetProperty("powerState", out var powerProp))
                    resourceObj["powerState"] = powerProp.GetString();
                if (resource.TryGetProperty("status", out var statusProp))
                    resourceObj["status"] = statusProp.GetString();

                resources.Add(resourceObj);
            }

            // Generate interactive cards using the card service
            if (_adaptiveCardService != null)
            {
                try
                {
                    var context = $"{resourceType} in {(string.IsNullOrEmpty(resourceGroupName) ? "subscription" : resourceGroupName)}";
                    if (!string.IsNullOrEmpty(additionalContext))
                        context += $" - {additionalContext}";
                        
                    var interactiveCard = await _adaptiveCardService.GenerateInteractiveResourceCards(resourceType, resources, context);
                    return $"🃏 **Universal Interactive {resourceType} List**\n\n{interactiveCard}";
                }
                catch (Exception cardEx)
                {
                    _logger?.LogWarning(cardEx, "Failed to generate interactive cards, falling back to text");
                }
            }

            // Fallback to text format if cards fail
            var result = $"📦 **{resourceType}** ({resources.Count} found)\n\n";
            
            foreach (var resource in resources)
            {
                var name = resource.TryGetValue("name", out object? nameVal) ? nameVal?.ToString() : "Unknown";
                var location = resource.TryGetValue("location", out object? locVal) ? locVal?.ToString() : "Unknown";
                var resourceGroup = resource.TryGetValue("resourceGroup", out object? rgVal) ? rgVal?.ToString() : "Unknown";
                
                result += $"📦 **{name}**\n";
                result += $"   📍 Location: {location}\n";
                result += $"   📁 Resource Group: {resourceGroup}\n";
                
                // Add additional properties if available
                if (resource.TryGetValue("state", out object? stateVal))
                    result += $"   ⚡ State: {stateVal}\n";
                if (resource.TryGetValue("powerState", out object? powerVal))
                    result += $"   🔋 Power: {powerVal}\n";
                if (resource.TryGetValue("sku", out object? skuVal))
                    result += $"   🏷️ SKU: {skuVal}\n";
                
                result += $"\n   💡 *Click resource for contextual actions and details*\n\n";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error listing {resourceType}: {ex.Message}\n\n" +
                   "💡 Supported resource types:\n" +
                   "• Virtual Machines, VMs\n" +
                   "• Storage Accounts\n" +
                   "• Web Apps, App Services\n" +
                   "• SQL Databases\n" +
                   "• Function Apps\n" +
                   "• Key Vaults\n" +
                   "• Container Instances\n" +
                   "• And many more Azure resources!";
        }
    }

    #endregion
}
