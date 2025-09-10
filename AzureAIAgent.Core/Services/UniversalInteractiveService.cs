using System.Text.Json;
using Microsoft.Extensions.Logging;
using AzureAIAgent.Core.Models;

namespace AzureAIAgent.Core.Services;

/// <summary>
/// Universal service for making any command result clickable and interactive
/// </summary>
public interface IUniversalInteractiveService
{
    Task<string> MakeResultInteractiveAsync(string command, string result, string? context = null);
    Task<string> GenerateInteractiveListAsync(string resourceType, IEnumerable<dynamic> items, string? context = null);
    Task<string> GenerateParameterFormAsync(string operation, string resourceType, Dictionary<string, object>? existingParams = null);
    bool ShouldMakeInteractive(string command, string result);
}

public class UniversalInteractiveService : IUniversalInteractiveService
{
    private readonly AdaptiveCardService _cardService;
    private readonly ILogger<UniversalInteractiveService> _logger;

    public UniversalInteractiveService(AdaptiveCardService cardService, ILogger<UniversalInteractiveService> logger)
    {
        _cardService = cardService;
        _logger = logger;
    }

    /// <summary>
    /// Determines if a command result should be made interactive
    /// </summary>
    public bool ShouldMakeInteractive(string command, string result)
    {
        var lowerCommand = command.ToLowerInvariant();
        var lowerResult = result.ToLowerInvariant();

        // Check for list-type commands
        var listKeywords = new[] { "list", "get", "show", "describe", "find", "search" };
        var hasListKeyword = listKeywords.Any(keyword => lowerCommand.Contains(keyword));

        // Check if result contains multiple items (indicates a list)
        var hasMultipleLines = result.Split('\n').Length > 3;
        var hasTableFormat = lowerResult.Contains("name") && (lowerResult.Contains("location") || lowerResult.Contains("status") || lowerResult.Contains("resource"));

        // Check for specific resource types
        var resourceTypes = new[] { 
            "pod", "vm", "storage", "app", "database", "network", "service", "deployment", 
            "container", "cluster", "resource", "group", "key", "vault", "function"
        };
        var hasResourceType = resourceTypes.Any(type => lowerCommand.Contains(type) || lowerResult.Contains(type));

        return hasListKeyword && (hasMultipleLines || hasTableFormat) && hasResourceType;
    }

    /// <summary>
    /// Make any command result interactive with clickable elements
    /// </summary>
    public async Task<string> MakeResultInteractiveAsync(string command, string result, string? context = null)
    {
        try
        {
            _logger.LogInformation("Making result interactive for command: {Command}", command);

            // Check if this is a parameter input request (keep cards for these)
            var parameterKeywords = new[] { "create", "deploy", "configure", "setup", "provision" };
            var isParameterRequest = parameterKeywords.Any(keyword => command.ToLowerInvariant().Contains(keyword));
            
            if (isParameterRequest)
            {
                // For parameter inputs, generate adaptive cards
                var (resourceType, operation) = ParseCommand(command);
                var items = ExtractItemsFromResult(result, resourceType);
                
                if (!items.Any())
                {
                    _logger.LogWarning("No items extracted from result, returning original");
                    return result;
                }

                // Generate interactive card for parameter inputs
                var interactiveCard = await _cardService.GenerateInteractiveResourceCards(resourceType, items, context);
                
                return $"🃏 **Interactive {resourceType} List**\n\n" +
                       $"Found {items.Count()} {resourceType.ToLower()}. Click on any item for available actions:\n\n" +
                       $"{interactiveCard}\n\n" +
                       $"💡 *Interactive interface generated automatically from command: `{command}`*";
            }
            else
            {
                // For result listings, return simple text with clickable names
                _logger.LogInformation("Returning simple text with clickable names for result listing");
                return result; // Return the original simple text from AksMcpPlugin
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making result interactive for command: {Command}", command);
            return result; // Return original result on error
        }
    }

    /// <summary>
    /// Generate interactive list for specific resource type and items
    /// </summary>
    public async Task<string> GenerateInteractiveListAsync(string resourceType, IEnumerable<dynamic> items, string? context = null)
    {
        try
        {
            var itemList = items.ToList();
            _logger.LogInformation("Generating interactive list for {Count} {ResourceType} items", itemList.Count, resourceType);

            var interactiveCard = await _cardService.GenerateInteractiveResourceCards(resourceType, itemList, context);
            
            return $"🃏 **Interactive {resourceType} List**\n\n" +
                   $"Found {itemList.Count} {resourceType.ToLower()}. Click on any item for available actions:\n\n" +
                   $"{interactiveCard}\n\n" +
                   $"💡 *Click on any {resourceType.ToLower()} to see available actions and operations.*";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating interactive list for {ResourceType}", resourceType);
            throw;
        }
    }

    /// <summary>
    /// Generate parameter input form for any operation
    /// </summary>
    public async Task<string> GenerateParameterFormAsync(string operation, string resourceType, Dictionary<string, object>? existingParams = null)
    {
        try
        {
            _logger.LogInformation("Generating parameter form for {Operation} on {ResourceType}", operation, resourceType);

            // Convert existing params to parameter definitions
            var paramDefs = ConvertToParameterDefinitions(operation, resourceType, existingParams);
            
            var parameterCard = await _cardService.GenerateParameterFormWithAI(resourceType, operation, paramDefs);
            
            return $"🃏 **{operation} {resourceType} Configuration**\n\n" +
                   $"Please provide the required parameters for this operation:\n\n" +
                   $"{parameterCard}\n\n" +
                   $"💡 *Fill out the form above to execute: {operation} on {resourceType}*";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating parameter form for {Operation} {ResourceType}", operation, resourceType);
            throw;
        }
    }

    #region Command Parsing

    /// <summary>
    /// Parse command to extract resource type and operation
    /// </summary>
    private (string resourceType, string operation) ParseCommand(string command)
    {
        var lowerCommand = command.ToLowerInvariant();
        
        // Extract operation
        var operation = ExtractOperation(lowerCommand);
        
        // Extract resource type
        var resourceType = ExtractResourceType(lowerCommand);
        
        _logger.LogDebug("Parsed command '{Command}' as operation='{Operation}', resourceType='{ResourceType}'", 
            command, operation, resourceType);
        
        return (resourceType, operation);
    }

    private string ExtractOperation(string lowerCommand)
    {
        var operations = new Dictionary<string, string>
        {
            { "list", "list" },
            { "get", "get" },
            { "show", "show" },
            { "describe", "describe" },
            { "create", "create" },
            { "delete", "delete" },
            { "update", "update" },
            { "start", "start" },
            { "stop", "stop" },
            { "restart", "restart" },
            { "scale", "scale" },
            { "deploy", "deploy" }
        };

        foreach (var (keyword, op) in operations)
        {
            if (lowerCommand.Contains(keyword))
                return op;
        }

        return "view";
    }

    private string ExtractResourceType(string lowerCommand)
    {
        var resourceTypes = new Dictionary<string, string>
        {
            { "pod", "pods" },
            { "service", "services" },
            { "deployment", "deployments" },
            { "vm", "virtualmachines" },
            { "virtualmachine", "virtualmachines" },
            { "storage", "storageaccounts" },
            { "app", "webapps" },
            { "function", "functionapps" },
            { "database", "databases" },
            { "sql", "databases" },
            { "cosmos", "cosmosdb" },
            { "keyvault", "keyvaults" },
            { "vault", "keyvaults" },
            { "network", "networks" },
            { "vnet", "virtualnetworks" },
            { "subnet", "subnets" },
            { "nsg", "networksecuritygroups" },
            { "lb", "loadbalancers" },
            { "aks", "clusters" },
            { "cluster", "clusters" },
            { "container", "containers" },
            { "registry", "containerregistries" },
            { "group", "resourcegroups" },
            { "resource", "resources" },
            { "subscription", "subscriptions" }
        };

        foreach (var (keyword, resourceType) in resourceTypes)
        {
            if (lowerCommand.Contains(keyword))
                return resourceType;
        }

        return "resources";
    }

    #endregion

    #region Result Parsing

    /// <summary>
    /// Extract structured items from command result text
    /// </summary>
    private IEnumerable<dynamic> ExtractItemsFromResult(string result, string resourceType)
    {
        var items = new List<dynamic>();

        try
        {
            // Try JSON parsing first
            if (result.Trim().StartsWith("[") || result.Trim().StartsWith("{"))
            {
                items.AddRange(ParseJsonResult(result));
            }
            else
            {
                // Parse table/text format
                items.AddRange(ParseTableResult(result, resourceType));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing result for resource type {ResourceType}", resourceType);
        }

        return items;
    }

    private IEnumerable<dynamic> ParseJsonResult(string jsonResult)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResult);
            
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Select(element => ParseJsonElement(element))
                    .ToList();
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return new[] { ParseJsonElement(doc.RootElement) };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse as JSON, will try table parsing");
        }

        return new List<dynamic>();
    }

    private dynamic ParseJsonElement(JsonElement element)
    {
        var obj = new Dictionary<string, object>();
        
        foreach (var prop in element.EnumerateObject())
        {
            obj[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? "",
                JsonValueKind.Number => prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.ToString()
            };
        }

        return obj;
    }

    private IEnumerable<dynamic> ParseTableResult(string tableResult, string resourceType)
    {
        var lines = tableResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var items = new List<dynamic>();

        if (lines.Length < 2) return items;

        // Find header line (usually contains column names)
        var headerLine = lines.FirstOrDefault(line => 
            line.ToLowerInvariant().Contains("name") && 
            (line.ToLowerInvariant().Contains("location") || 
             line.ToLowerInvariant().Contains("status") || 
             line.ToLowerInvariant().Contains("resource")));

        if (headerLine == null) 
        {
            // Try to parse without headers
            return ParseTableWithoutHeaders(lines, resourceType);
        }

        var headerIndex = Array.IndexOf(lines, headerLine);
        var headers = SplitTableRow(headerLine);

        // Parse data rows
        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("-")) continue;

            var values = SplitTableRow(line);
            if (values.Length == 0) continue;

            var item = new Dictionary<string, object>();
            
            for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
            {
                var key = CleanHeaderName(headers[j]);
                var value = values[j].Trim();
                
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    item[key] = value;
                }
            }

            if (item.Any())
            {
                items.Add(item);
            }
        }

        return items;
    }

    private IEnumerable<dynamic> ParseTableWithoutHeaders(string[] lines, string resourceType)
    {
        var items = new List<dynamic>();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("-")) continue;

            // Try to extract meaningful information based on patterns
            var item = new Dictionary<string, object>();
            
            // Extract name (usually the first word)
            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                item["name"] = parts[0];
                item["id"] = parts[0];
                
                // Try to find location patterns
                var locationPattern = System.Text.RegularExpressions.Regex.Match(trimmed, @"\b(eastus|westus|westus2|centralus|northeurope|westeurope|southeastasia|eastasia)\b");
                if (locationPattern.Success)
                {
                    item["location"] = locationPattern.Value;
                }
                
                // Try to find status patterns
                var statusPattern = System.Text.RegularExpressions.Regex.Match(trimmed, @"\b(running|stopped|succeeded|failed|pending|creating|deleting)\b");
                if (statusPattern.Success)
                {
                    item["status"] = statusPattern.Value;
                }
                
                // Add resource group if found
                var rgPattern = System.Text.RegularExpressions.Regex.Match(trimmed, @"\brg-[\w-]+\b");
                if (rgPattern.Success)
                {
                    item["resourceGroup"] = rgPattern.Value;
                }
            }

            if (item.Any())
            {
                items.Add(item);
            }
        }

        return items;
    }

    private string[] SplitTableRow(string row)
    {
        // Handle different table formats (spaces, tabs, pipes)
        if (row.Contains('|'))
        {
            return row.Split('|', StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim())
                     .ToArray();
        }
        else
        {
            // Split on multiple spaces or tabs
            return System.Text.RegularExpressions.Regex.Split(row, @"\s{2,}|\t+")
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Select(s => s.Trim())
                     .ToArray();
        }
    }

    private string CleanHeaderName(string header)
    {
        return header.ToLowerInvariant()
                     .Replace(" ", "")
                     .Replace("-", "")
                     .Replace("_", "");
    }

    #endregion

    #region Parameter Definitions

    private List<ParameterDefinition> ConvertToParameterDefinitions(string operation, string resourceType, Dictionary<string, object>? existingParams)
    {
        var parameters = new List<ParameterDefinition>();

        // Add common parameters based on operation and resource type
        if (operation == "create" || operation == "deploy")
        {
            parameters.AddRange(GetCreationParameters(resourceType));
        }
        else if (operation == "update" || operation == "scale")
        {
            parameters.AddRange(GetUpdateParameters(resourceType));
        }

        // Add existing parameters
        if (existingParams != null)
        {
            foreach (var (key, value) in existingParams)
            {
                if (!parameters.Any(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)))
                {
                    parameters.Add(new ParameterDefinition
                    {
                        Name = key,
                        DisplayName = FormatDisplayName(key),
                        Description = $"Parameter for {key}",
                        Required = true,
                        DefaultValue = value?.ToString()
                    });
                }
            }
        }

        return parameters;
    }

    private List<ParameterDefinition> GetCreationParameters(string resourceType)
    {
        return resourceType.ToLower() switch
        {
            "clusters" or "aks" => new List<ParameterDefinition>
            {
                new() { Name = "workload_name", DisplayName = "Workload Name", Description = "Name for the workload", Required = true, Placeholder = "e.g., myapp-aks" },
                new() { Name = "project_name", DisplayName = "Project Name", Description = "Project identifier", Required = true, Placeholder = "e.g., finance-app" },
                new() { Name = "owner", DisplayName = "Owner", Description = "Owner or team name", Required = true, Placeholder = "Your name or team" },
                new() { Name = "environment", DisplayName = "Environment", Description = "Deployment environment", Required = false, DefaultValue = "dev", AllowedValues = new[] { "dev", "test", "staging", "prod" } },
                new() { Name = "location", DisplayName = "Location", Description = "Azure region", Required = false, DefaultValue = "eastus", AllowedValues = new[] { "eastus", "westus2", "westeurope", "southeastasia" } }
            },
            "virtualmachines" => new List<ParameterDefinition>
            {
                new() { Name = "vm_name", DisplayName = "VM Name", Description = "Virtual machine name", Required = true },
                new() { Name = "vm_size", DisplayName = "VM Size", Description = "VM SKU size", Required = true, DefaultValue = "Standard_B2s" },
                new() { Name = "admin_username", DisplayName = "Admin Username", Description = "Administrator username", Required = true },
                new() { Name = "location", DisplayName = "Location", Description = "Azure region", Required = true, DefaultValue = "eastus" }
            },
            "storageaccounts" => new List<ParameterDefinition>
            {
                new() { Name = "storage_name", DisplayName = "Storage Name", Description = "Storage account name", Required = true },
                new() { Name = "sku", DisplayName = "SKU", Description = "Storage SKU", Required = true, DefaultValue = "Standard_LRS", AllowedValues = new[] { "Standard_LRS", "Standard_GRS", "Premium_LRS" } },
                new() { Name = "location", DisplayName = "Location", Description = "Azure region", Required = true, DefaultValue = "eastus" }
            },
            _ => new List<ParameterDefinition>
            {
                new() { Name = "name", DisplayName = "Resource Name", Description = "Name of the resource", Required = true },
                new() { Name = "location", DisplayName = "Location", Description = "Azure region", Required = true, DefaultValue = "eastus" }
            }
        };
    }

    private List<ParameterDefinition> GetUpdateParameters(string resourceType)
    {
        return resourceType.ToLower() switch
        {
            "virtualmachines" => new List<ParameterDefinition>
            {
                new() { Name = "vm_name", DisplayName = "VM Name", Description = "Virtual machine to update", Required = true },
                new() { Name = "new_size", DisplayName = "New VM Size", Description = "New VM SKU size", Required = false }
            },
            _ => new List<ParameterDefinition>
            {
                new() { Name = "resource_name", DisplayName = "Resource Name", Description = "Name of resource to update", Required = true }
            }
        };
    }

    private string FormatDisplayName(string name)
    {
        return System.Text.RegularExpressions.Regex.Replace(name, @"[_-]", " ")
            .Split(' ')
            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower())
            .Aggregate((a, b) => a + " " + b);
    }

    #endregion
}
