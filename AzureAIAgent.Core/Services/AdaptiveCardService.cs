using System.Text.Json;
using Microsoft.Extensions.Logging;
using AzureAIAgent.Core.Models;
using Microsoft.SemanticKernel;

namespace AzureAIAgent.Core.Services;

public class AdaptiveCardService
{
    private readonly ILogger<AdaptiveCardService> _logger;
    private readonly Kernel? _kernel;

    // Comprehensive Azure regions for consistent use across all adaptive cards
    private static readonly object[] AzureRegions = new object[]
    {
        new { title = "East US", value = "East US" },
        new { title = "East US 2", value = "East US 2" },
        new { title = "West US", value = "West US" },
        new { title = "West US 2", value = "West US 2" },
        new { title = "West US 3", value = "West US 3" },
        new { title = "Central US", value = "Central US" },
        new { title = "North Central US", value = "North Central US" },
        new { title = "South Central US", value = "South Central US" },
        new { title = "West Central US", value = "West Central US" },
        new { title = "Canada Central", value = "Canada Central" },
        new { title = "Canada East", value = "Canada East" },
        new { title = "Brazil South", value = "Brazil South" },
        new { title = "North Europe", value = "North Europe" },
        new { title = "West Europe", value = "West Europe" },
        new { title = "UK South", value = "UK South" },
        new { title = "UK West", value = "UK West" },
        new { title = "France Central", value = "France Central" },
        new { title = "Germany West Central", value = "Germany West Central" },
        new { title = "Norway East", value = "Norway East" },
        new { title = "Switzerland North", value = "Switzerland North" },
        new { title = "Sweden Central", value = "Sweden Central" },
        new { title = "UAE North", value = "UAE North" },
        new { title = "South Africa North", value = "South Africa North" },
        new { title = "East Asia", value = "East Asia" },
        new { title = "Southeast Asia", value = "Southeast Asia" },
        new { title = "Australia East", value = "Australia East" },
        new { title = "Australia Southeast", value = "Australia Southeast" },
        new { title = "Central India", value = "Central India" },
        new { title = "South India", value = "South India" },
        new { title = "West India", value = "West India" },
        new { title = "Japan East", value = "Japan East" },
        new { title = "Japan West", value = "Japan West" },
        new { title = "Korea Central", value = "Korea Central" },
        new { title = "Korea South", value = "Korea South" }
    };

    public AdaptiveCardService(ILogger<AdaptiveCardService> logger, Kernel? kernel = null)
    {
        _logger = logger;
        _kernel = kernel;
    }

    #region Template Gallery Cards

    public string GenerateTemplateGalleryCard(IEnumerable<TemplateMetadata> templates)
    {
        var templateList = templates.ToList();
        
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = "🏗️ Azure Infrastructure Templates",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = "Choose from production-ready infrastructure templates:",
                    wrap = true,
                    spacing = "Medium"
                }
            }.Concat(templateList.Select(template => new
            {
                type = "Container",
                style = "emphasis",
                selectAction = new
                {
                    type = "Action.Submit",
                    data = new { action = "select_template", templateId = template.Id }
                },
                items = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = $"{GetCategoryIcon(template.Category)} {template.Name}",
                        weight = "Bolder",
                        size = "Medium"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = template.Description,
                        wrap = true,
                        size = "Small",
                        isSubtle = true
                    }
                }
            })).ToArray()
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    public string GenerateParameterFormCard(TemplateMetadata template)
    {
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = $"⚙️ Configure {template.Name}",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = template.Description,
                    wrap = true,
                    spacing = "Medium",
                    isSubtle = true
                }
            }.Concat(template.Parameters.Select<TemplateParameter, object>(param => 
            {
                // Special handling for location/region parameters
                if (param.Name.ToLower().Contains("location") || param.Name.ToLower().Contains("region"))
                {
                    return new
                    {
                        type = "Input.ChoiceSet",
                        id = param.Name,
                        label = param.Description + (param.Required ? " *" : ""),
                        value = param.Default ?? "East US",
                        isRequired = param.Required,
                        choices = AzureRegions
                    };
                }
                // Special handling for environment parameters
                else if (param.Name.ToLower().Contains("environment") || param.Name.ToLower().Contains("env"))
                {
                    return new
                    {
                        type = "Input.ChoiceSet",
                        id = param.Name,
                        label = param.Description + (param.Required ? " *" : ""),
                        value = param.Default ?? "dev",
                        isRequired = param.Required,
                        choices = new object[]
                        {
                            new { title = "Development", value = "dev" },
                            new { title = "Testing", value = "test" },
                            new { title = "Staging", value = "staging" },
                            new { title = "Production", value = "prod" }
                        }
                    };
                }
                // Special handling for VM size parameters
                else if (param.Name.ToLower().Contains("vm_size") || param.Name.ToLower().Contains("vmsize") || param.Name.ToLower().Contains("size"))
                {
                    return new
                    {
                        type = "Input.ChoiceSet",
                        id = param.Name,
                        label = param.Description + (param.Required ? " *" : ""),
                        value = param.Default ?? "Standard_DS2_v2",
                        isRequired = param.Required,
                        choices = new object[]
                        {
                            new { title = "Standard_B2s (2 vCPU, 4GB RAM) - Burstable", value = "Standard_B2s" },
                            new { title = "Standard_D2s_v3 (2 vCPU, 8GB RAM)", value = "Standard_D2s_v3" },
                            new { title = "Standard_DS2_v2 (2 vCPU, 7GB RAM)", value = "Standard_DS2_v2" },
                            new { title = "Standard_D4s_v3 (4 vCPU, 16GB RAM)", value = "Standard_D4s_v3" },
                            new { title = "Standard_DS3_v2 (4 vCPU, 14GB RAM)", value = "Standard_DS3_v2" },
                            new { title = "Standard_D8s_v3 (8 vCPU, 32GB RAM)", value = "Standard_D8s_v3" },
                            new { title = "Standard_D16s_v3 (16 vCPU, 64GB RAM)", value = "Standard_D16s_v3" },
                            new { title = "Standard_E4s_v3 (4 vCPU, 32GB RAM) - Memory Optimized", value = "Standard_E4s_v3" },
                            new { title = "Standard_E8s_v3 (8 vCPU, 64GB RAM) - Memory Optimized", value = "Standard_E8s_v3" }
                        }
                    };
                }
                // Special handling for node count parameters
                else if (param.Name.ToLower().Contains("node_count") || param.Name.ToLower().Contains("nodecount") || param.Name.ToLower().Contains("count"))
                {
                    return new
                    {
                        type = "Input.ChoiceSet",
                        id = param.Name,
                        label = param.Description + (param.Required ? " *" : ""),
                        value = param.Default ?? "3",
                        isRequired = param.Required,
                        choices = new object[]
                        {
                            new { title = "1 node", value = "1" },
                            new { title = "2 nodes", value = "2" },
                            new { title = "3 nodes", value = "3" },
                            new { title = "5 nodes", value = "5" },
                            new { title = "10 nodes", value = "10" }
                        }
                    };
                }
                // Default text input for other parameters
                else
                {
                    return new
                    {
                        type = "Input.Text",
                        id = param.Name,
                        label = param.Description + (param.Required ? " *" : ""),
                        placeholder = param.Default ?? $"Enter {param.Description.ToLower()}",
                        value = param.Default,
                        isRequired = param.Required,
                        style = param.Sensitive ? "Password" : "Text"
                    };
                }
            })).Concat(new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = "* Required fields",
                    size = "Small",
                    isSubtle = true,
                    spacing = "Medium"
                }
            }).ToArray(),
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "🚀 Deploy Infrastructure",
                    style = "positive",
                    data = new { action = "deploy_template", templateId = template.Id }
                },
                new
                {
                    type = "Action.Submit",
                    title = "← Back to Gallery",
                    data = new { action = "show_gallery" }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    public string GenerateDeploymentStatusCard(string deploymentId, string status, string? message = null)
    {
        var statusColor = status.ToLower() switch
        {
            "running" or "deploying" => "warning",
            "completed" or "success" => "good",
            "failed" or "error" => "attention",
            _ => "default"
        };

        var statusIcon = status.ToLower() switch
        {
            "running" or "deploying" => "⏳",
            "completed" or "success" => "✅",
            "failed" or "error" => "❌",
            _ => "ℹ️"
        };

        var bodyItems = new List<object>
        {
            new
            {
                type = "TextBlock",
                text = $"{statusIcon} Deployment Status",
                weight = "Bolder",
                size = "Large",
                color = "Accent"
            },
            new
            {
                type = "TextBlock",
                text = $"Deployment ID: {deploymentId}",
                wrap = true,
                weight = "Bolder"
            },
            new
            {
                type = "Container",
                style = statusColor,
                items = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = $"Status: {status}",
                        weight = "Bolder",
                        size = "Medium"
                    }
                }
            }
        };

        if (!string.IsNullOrEmpty(message))
        {
            bodyItems.Add(new
            {
                type = "TextBlock",
                text = message,
                wrap = true,
                spacing = "Small"
            });
        }

        var actionsList = new List<object>
        {
            new
            {
                type = "Action.Submit",
                title = "🔄 Refresh Status",
                data = new { action = "refresh_status", deploymentId }
            }
        };

        if (status.ToLower() == "completed")
        {
            actionsList.Add(new
            {
                type = "Action.Submit",
                title = "🗑️ Destroy Resources",
                style = "destructive",
                data = new { action = "destroy_deployment", deploymentId }
            });
        }

        actionsList.Add(new
        {
            type = "Action.Submit",
            title = "📋 View All Deployments",
            data = new { action = "list_deployments" }
        });

        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = bodyItems.ToArray(),
            actions = actionsList.ToArray()
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    public string GenerateDeploymentListCard(IEnumerable<DeploymentInfo> deployments)
    {
        var deploymentList = deployments.ToList();
        
        var bodyItems = new List<object>
        {
            new
            {
                type = "TextBlock",
                text = "📋 Your Deployments",
                weight = "Bolder",
                size = "Large",
                color = "Accent"
            }
        };

        if (deploymentList.Any())
        {
            bodyItems.AddRange(deploymentList.Select(deployment => new
            {
                type = "Container",
                style = "emphasis",
                selectAction = new
                {
                    type = "Action.Submit",
                    data = new { action = "show_deployment_status", deploymentId = deployment.Id }
                },
                items = new object[]
                {
                    new
                    {
                        type = "TextBlock",
                        text = $"{GetStatusIcon(deployment.Status)} {deployment.Name}",
                        weight = "Bolder"
                    },
                    new
                    {
                        type = "TextBlock",
                        text = $"Template: {deployment.TemplateId} | Status: {deployment.Status}",
                        size = "Small",
                        isSubtle = true
                    }
                }
            }));
        }
        else
        {
            bodyItems.Add(new
            {
                type = "TextBlock",
                text = "No deployments found. Use 'show templates' to create your first deployment.",
                wrap = true,
                horizontalAlignment = "Center",
                spacing = "Medium"
            });
        }

        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = bodyItems.ToArray(),
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "🏗️ Browse Templates",
                    style = "positive",
                    data = new { action = "show_gallery" }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    private string GetCategoryIcon(string category) => category.ToLower() switch
    {
        "compute" => "🖥️",
        "containers" => "🐳",
        "web" => "🌐",
        "storage" => "💾",
        "database" => "🗄️",
        "security" => "🔒",
        "networking" => "🌐",
        _ => "📦"
    };

    private string GetStatusIcon(string status) => status.ToLower() switch
    {
        "running" or "deploying" => "⏳",
        "completed" or "success" => "✅",
        "failed" or "error" => "❌",
        _ => "ℹ️"
    };

    #endregion

    #region Universal Azure Resource Cards

    /// <summary>
    /// Generates an interactive card for listing Azure Resource Groups with actions
    /// </summary>
    public string GenerateResourceGroupListCard(IEnumerable<dynamic> resourceGroups)
{
    var rgList = resourceGroups.ToList();
    
    var card = new
    {
        type = "AdaptiveCard",
        version = "1.5",
        body = new object[]
        {
            new
            {
                type = "TextBlock",
                text = "📁 Azure Resource Groups",
                weight = "Bolder",
                size = "Large",
                color = "Accent"
            },
            new
            {
                type = "TextBlock",
                text = $"Found {rgList.Count} resource groups. Click on any resource group for actions:",
                wrap = true,
                spacing = "Medium"
            }
        }.Concat(rgList.Select(rg => new
        {
            type = "Container",
            style = "emphasis",
            selectAction = new
            {
                type = "Action.ShowCard",
                card = new
                {
                    type = "AdaptiveCard",
                    body = new object[]
                    {
                        new
                        {
                            type = "TextBlock",
                            text = $"Actions for: {rg.name}",
                            weight = "Bolder"
                        },
                        new
                        {
                            type = "ActionSet",
                            actions = new object[]
                            {
                                new
                                {
                                    type = "Action.Submit",
                                    title = "📋 List Resources",
                                    data = new { action = "list_resources", resourceGroupName = rg.name }
                                },
                                new
                                {
                                    type = "Action.Submit", 
                                    title = "📊 View Metrics",
                                    data = new { action = "view_metrics", resourceGroupName = rg.name }
                                },
                                new
                                {
                                    type = "Action.Submit",
                                    title = "🏷️ Manage Tags",
                                    data = new { action = "manage_tags", resourceGroupName = rg.name }
                                },
                                new
                                {
                                    type = "Action.Submit",
                                    title = "🗑️ Delete RG",
                                    data = new { action = "delete_rg", resourceGroupName = rg.name },
                                    style = "destructive"
                                }
                            }
                        }
                    }
                }
            },
            items = new object[]
            {
                new
                {
                    type = "ColumnSet",
                    columns = new object[]
                    {
                        new
                        {
                            type = "Column",
                            width = "stretch",
                            items = new object[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    text = $"**{rg.name}**",
                                    weight = "Bolder",
                                    size = "Medium"
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = $"📍 {rg.location}",
                                    spacing = "None",
                                    color = "Good"
                                }
                            }
                        },
                        new
                        {
                            type = "Column",
                            width = "auto",
                            items = new object[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    text = "🔽",
                                    horizontalAlignment = "Right"
                                }
                            }
                        }
                    }
                }
            },
            spacing = "Medium"
        })).ToArray()
    };

    return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Generates a universal parameter input form for any Azure operation
/// </summary>
public string GenerateParameterInputCard(string title, string actionName, IEnumerable<ParameterDefinition> parameters)
{
    var paramList = parameters.ToList();
    
    // Create body with title and description
    var bodyItems = new List<object>
    {
        new
        {
            type = "TextBlock",
            text = title,
            weight = "Bolder",
            size = "Large",
            color = "Accent"
        },
        new
        {
            type = "TextBlock",
            text = "Please provide the required parameters:",
            wrap = true,
            spacing = "Medium"
        }
    };

    // Add input elements based on parameter type
    foreach (var param in paramList)
    {
        if (param.AllowedValues?.Any() == true)
        {
            // Create choice set for parameters with allowed values
            bodyItems.Add(new
            {
                type = "Input.ChoiceSet",
                id = param.Name,
                label = param.DisplayName,
                isRequired = param.Required,
                value = param.DefaultValue,
                choices = param.AllowedValues.Select(value => new
                {
                    title = value,
                    value = value
                }).ToArray()
            });
        }
        else
        {
            // Create text input for other parameters
            bodyItems.Add(new
            {
                type = "Input.Text",
                id = param.Name,
                label = param.DisplayName,
                placeholder = param.Placeholder ?? $"Enter {param.DisplayName.ToLower()}",
                isRequired = param.Required,
                value = param.DefaultValue,
                style = param.IsSecret ? "Password" : "Text"
            });
        }
    }
    
    var card = new
    {
        type = "AdaptiveCard",
        version = "1.5",
        body = bodyItems.ToArray(),
        actions = new object[]
        {
            new
            {
                type = "Action.Submit",
                title = "Submit",
                style = "positive"
            },
            new
            {
                type = "Action.Submit",
                title = "Cancel",
                style = "destructive",
                data = new { action = "cancel" }
            }
        }
    };

    return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
}

    #endregion

    #region AI-Powered Adaptive Card Generation

    /// <summary>
    /// Generate adaptive cards using Azure OpenAI based on natural language description
    /// </summary>
    public async Task<string> GenerateAdaptiveCardWithAI(string description, string cardType = "parameter_form")
    {
        if (_kernel == null)
        {
            _logger.LogWarning("Kernel not available for AI card generation, falling back to static card");
            return GenerateFallbackCard(description, cardType);
        }

        try
        {
            var prompt = GetAdaptiveCardPrompt(description, cardType);
            var response = await _kernel.InvokePromptAsync(prompt);
            
            var cardJson = response.GetValue<string>();
            
            // Validate JSON and return
            if (IsValidAdaptiveCard(cardJson))
            {
                _logger.LogInformation("AI generated adaptive card successfully for: {Description}", description);
                return cardJson;
            }
            else
            {
                _logger.LogWarning("AI generated invalid adaptive card, falling back to static card");
                return GenerateFallbackCard(description, cardType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating adaptive card with AI for: {Description}", description);
            return GenerateFallbackCard(description, cardType);
        }
    }

    /// <summary>
    /// Generate parameter input form using AI based on resource type and operation
    /// </summary>
    public async Task<string> GenerateParameterFormWithAI(string resourceType, string operation, List<ParameterDefinition>? existingParams = null)
    {
        if (_kernel == null)
        {
            return GenerateParameterInputCard($"Configure {resourceType}", operation, existingParams ?? new List<ParameterDefinition>());
        }

        try
        {
            var context = BuildParameterContext(resourceType, operation, existingParams);
            var prompt = GetParameterFormPrompt(context);
            
            var response = await _kernel.InvokePromptAsync(prompt);
            var cardJson = response.GetValue<string>();
            
            if (IsValidAdaptiveCard(cardJson))
            {
                _logger.LogInformation("AI generated parameter form for {ResourceType} {Operation}", resourceType, operation);
                return cardJson;
            }
            else
            {
                _logger.LogWarning("AI generated invalid parameter form, falling back to static form");
                return GenerateParameterInputCard($"Configure {resourceType}", operation, existingParams ?? new List<ParameterDefinition>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating parameter form with AI for {ResourceType}", resourceType);
            return GenerateParameterInputCard($"Configure {resourceType}", operation, existingParams ?? new List<ParameterDefinition>());
        }
    }

    /// <summary>
    /// Generate template gallery using AI to categorize and present templates intelligently
    /// </summary>
    public async Task<string> GenerateTemplateGalleryWithAI(IEnumerable<TemplateMetadata> templates, string userIntent = "")
    {
        if (_kernel == null)
        {
            return GenerateTemplateGalleryCard(templates);
        }

        try
        {
            var templateContext = BuildTemplateContext(templates, userIntent);
            var prompt = GetTemplateGalleryPrompt(templateContext);
            
            var response = await _kernel.InvokePromptAsync(prompt);
            var cardJson = response.GetValue<string>();
            
            if (IsValidAdaptiveCard(cardJson))
            {
                _logger.LogInformation("AI generated template gallery with intelligent categorization");
                return cardJson;
            }
            else
            {
                _logger.LogWarning("AI generated invalid template gallery, falling back to static gallery");
                return GenerateTemplateGalleryCard(templates);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating template gallery with AI");
            return GenerateTemplateGalleryCard(templates);
        }
    }

    #endregion

    #region AI Prompt Generation

    private string GetAdaptiveCardPrompt(string description, string cardType)
    {
        return $"""
        Generate an Adaptive Card (version 1.5) JSON for the following request:

        **Description**: {description}
        **Card Type**: {cardType}

        Requirements:
        1. Generate valid Adaptive Card JSON (v1.5 schema)
        2. Include appropriate visual elements (TextBlocks, Images, Containers)
        3. Use modern styling with emphasis, spacing, and colors
        4. Include relevant actions (Submit buttons with meaningful data)
        5. Make it visually appealing and functional
        6. Use Azure-themed colors (Accent, Good, Warning, Attention)
        7. Include icons using emoji or symbols where appropriate
        8. For Submit actions: use consistent button title "Submit" with action data object
        9. For Cancel actions: use consistent button title "Cancel" with action="cancel" in data object
        10. Ensure consistent UI elements (same button types, same styling patterns)

        Return ONLY the JSON object, no additional text or markdown formatting.
        """;
    }

    private string GetParameterFormPrompt(string context)
    {
        return $"""
        Generate an Adaptive Card JSON for a parameter input form based on this context:

        {context}

        Requirements:
        1. Create a functional parameter input form as an Adaptive Card (v1.5)
        2. Include appropriate input controls (Input.Text, Input.Number, Input.ChoiceSet, etc.)
        3. For location/region parameters, use comprehensive Azure regions list: East US, East US 2, West US, West US 2, West US 3, Central US, North Central US, South Central US, West Central US, Canada Central, Canada East, Brazil South, North Europe, West Europe, UK South, UK West, France Central, Germany West Central, Norway East, Switzerland North, Sweden Central, UAE North, South Africa North, East Asia, Southeast Asia, Australia East, Australia Southeast, Central India, South India, West India, Japan East, Japan West, Korea Central, Korea South
        4. Group related parameters using Containers with emphasis styling
        5. Add validation hints and placeholder text
        6. Include consistent Submit action with data and Cancel action (use "Submit" and "Cancel" as titles)
        7. Use Azure-themed styling and colors
        8. Make required fields clearly marked with asterisk (*) in the label ONLY
        9. Use appropriate input types for each parameter
        10. For Submit action: use data object with action="submit", for Cancel: use data object with action="cancel"
        11. CRITICAL: Do NOT create TextBlock headers above input fields - use ONLY the input field "label" property
        12. Structure: Main title → grouped input fields with labels → action buttons
        13. Avoid any redundant field headers or duplicate text
        14. IMPORTANT: Cancel button MUST include proper action data for cancellation handling

        Format:
        - One main title TextBlock for the entire form
        - Input fields with descriptive labels (no separate header TextBlocks)
        - Action buttons: Submit button with action="submit", Cancel button with action="cancel"

        Return ONLY the Adaptive Card JSON, no additional text.
        """;
    }

    private string GetTemplateGalleryPrompt(string templateContext)
    {
        return $"""
        Generate an Adaptive Card JSON for a template gallery based on this context:

        {templateContext}

        Requirements:
        1. Create an attractive template gallery as an Adaptive Card (v1.5)
        2. Intelligently categorize and organize templates
        3. Highlight recommended templates based on user intent
        4. Use visual elements like icons, colors, and emphasis
        5. Include template cards with descriptions and actions
        6. Add search/filter capabilities if appropriate
        7. Use Azure-themed styling
        8. Make templates easily selectable with clear actions

        Return ONLY the Adaptive Card JSON, no additional text.
        """;
    }

    #endregion

    #region Context Building

    private string BuildParameterContext(string resourceType, string operation, List<ParameterDefinition>? existingParams)
    {
        var context = $"Resource Type: {resourceType}\nOperation: {operation}\n\n";
        
        if (existingParams?.Any() == true)
        {
            context += "Existing Parameters:\n";
            foreach (var param in existingParams)
            {
                context += $"- {param.Name} ({param.DisplayName}): {param.Description}";
                if (param.Required) context += " [REQUIRED]";
                if (param.AllowedValues?.Any() == true) context += $" Options: [{string.Join(", ", param.AllowedValues)}]";
                context += "\n";
            }
        }
        else
        {
            context += "Generate appropriate parameters for this Azure resource type and operation.";
        }

        return context;
    }

    private string BuildTemplateContext(IEnumerable<TemplateMetadata> templates, string userIntent)
    {
        var templateList = templates.ToList();
        var context = $"User Intent: {userIntent}\n\nAvailable Templates:\n\n";
        
        foreach (var template in templateList)
        {
            context += $"- {template.Name} ({template.Category})\n";
            context += $"  Description: {template.Description}\n";
            context += $"  ID: {template.Id}\n\n";
        }

        return context;
    }

    #endregion

    #region Validation and Fallback

    private bool IsValidAdaptiveCard(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        
        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("type", out var typeElement) && 
                   typeElement.GetString() == "AdaptiveCard";
        }
        catch
        {
            return false;
        }
    }

    private string GenerateFallbackCard(string description, string cardType)
    {
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = "⚙️ Configuration Required",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = description,
                    wrap = true,
                    spacing = "Medium"
                },
                new
                {
                    type = "TextBlock",
                    text = "AI assistance temporarily unavailable. Please provide parameters manually or try again later.",
                    wrap = true,
                    size = "Small",
                    isSubtle = true,
                    color = "Warning"
                }
            },
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "Continue",
                    data = new { action = "manual_config" }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion

    #region Universal Interactive Resource Cards

    /// <summary>
    /// Generate interactive cards for ANY Azure resource list with contextual actions
    /// </summary>
    public async Task<string> GenerateInteractiveResourceCards(
        string resourceType, 
        IEnumerable<dynamic> resources, 
        string? context = null)
    {
        var resourceList = resources.ToList();
        
        // Try AI-powered generation first
        if (_kernel != null)
        {
            try
            {
                var aiCard = await GenerateResourceCardsWithAI(resourceType, resourceList, context);
                if (IsValidAdaptiveCard(aiCard))
                {
                    return aiCard;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI card generation failed, using static cards");
            }
        }

        // Fallback to static generation
        return GenerateStaticResourceCards(resourceType, resourceList, context);
    }

    /// <summary>
    /// Generate interactive cards for Kubernetes pods with actions
    /// </summary>
    public string GenerateAksPodCards(IEnumerable<dynamic> pods, string deploymentName)
    {
        var podList = pods.ToList();
        
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = $"🐳 AKS Pods - {deploymentName}",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = $"Found {podList.Count} pods. Click any pod for actions:",
                    wrap = true,
                    spacing = "Medium"
                }
            }.Concat(podList.Select(pod => {
                // Safely extract properties from Dictionary<string, object>
                var podDict = pod as Dictionary<string, object> ?? new Dictionary<string, object>();
                string podName = podDict.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? "unknown" : "unknown";
                string podNamespace = podDict.TryGetValue("namespace", out var nsObj) ? nsObj?.ToString() ?? "default" : "default";
                string podStatus = podDict.TryGetValue("status", out var statusObj) ? statusObj?.ToString() ?? "Unknown" : "Unknown";
                string podReady = podDict.TryGetValue("ready", out var readyObj) ? readyObj?.ToString() ?? "0/0" : "0/0";
                
                return new
                {
                    type = "Container",
                    style = GetPodContainerStyle(podStatus),
                    selectAction = new
                    {
                        type = "Action.ShowCard",
                        card = new
                        {
                            type = "AdaptiveCard",
                            body = new object[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    text = $"Actions for Pod: {podName}",
                                    weight = "Bolder",
                                    wrap = true
                                },
                                new
                                {
                                    type = "ActionSet",
                                    actions = new object[]
                                    {
                                        new
                                        {
                                            type = "Action.Submit",
                                            title = "📄 View Logs",
                                            data = new { 
                                                action = "get_pod_logs", 
                                                deploymentName, 
                                                podName,
                                                namespaceName = podNamespace
                                            }
                                        },
                                        new
                                        {
                                            type = "Action.Submit",
                                            title = "🔍 Describe Pod",
                                            data = new { 
                                                action = "describe_pod", 
                                                deploymentName, 
                                                podName,
                                                namespaceName = podNamespace
                                            }
                                        },
                                        new
                                        {
                                            type = "Action.Submit",
                                            title = "📊 Pod Metrics",
                                            data = new { 
                                                action = "get_pod_metrics", 
                                                deploymentName, 
                                                podName,
                                                namespaceName = podNamespace
                                            }
                                        },
                                        new
                                        {
                                            type = "Action.Submit",
                                            title = "🔄 Restart Pod",
                                            data = new { 
                                                action = "restart_pod", 
                                                deploymentName, 
                                                podName,
                                                namespaceName = podNamespace
                                            },
                                            style = "destructive"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    items = new object[]
                    {
                        new
                        {
                            type = "ColumnSet",
                            columns = new object[]
                            {
                                new
                                {
                                    type = "Column",
                                    width = "stretch",
                                    items = new object[]
                                    {
                                        new
                                        {
                                            type = "TextBlock",
                                            text = $"**{podName}**",
                                            weight = "Bolder",
                                            size = "Medium"
                                        },
                                        new
                                        {
                                            type = "TextBlock",
                                            text = $"📍 {podNamespace} | {GetPodStatusIcon(podStatus)} {podStatus}",
                                            spacing = "None",
                                            color = GetPodStatusColor(podStatus)
                                        }
                                    }
                                },
                                new
                                {
                                    type = "Column",
                                    width = "auto",
                                    items = new object[]
                                    {
                                        new
                                        {
                                            type = "TextBlock",
                                            text = "🔽",
                                            horizontalAlignment = "Right"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    spacing = "Medium"
                };
            })).ToArray(),
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "🔄 Refresh Pods",
                    data = new { action = "refresh_pods", deploymentName }
                },
                new
                {
                    type = "Action.Submit",
                    title = "🌐 View Services",
                    data = new { action = "get_services", deploymentName }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Generate interactive cards for Virtual Machines
    /// </summary>
    public string GenerateVirtualMachineCards(IEnumerable<dynamic> vms)
    {
        var vmList = vms.ToList();
        
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = "🖥️ Virtual Machines",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = $"Found {vmList.Count} virtual machines. Click any VM for actions:",
                    wrap = true,
                    spacing = "Medium"
                }
            }.Concat(vmList.Select(vm => new
            {
                type = "Container",
                style = GetVMContainerStyle(vm.powerState?.ToString()),
                selectAction = new
                {
                    type = "Action.ShowCard",
                    card = new
                    {
                        type = "AdaptiveCard",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = $"Actions for VM: {vm.name}",
                                weight = "Bolder",
                                wrap = true
                            },
                            new
                            {
                                type = "ActionSet",
                                actions = new object[]
                                {
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "▶️ Start VM",
                                        data = new { 
                                            action = "start_vm", 
                                            vmName = vm.name,
                                            resourceGroup = vm.resourceGroup
                                        },
                                        style = "positive"
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "⏹️ Stop VM",
                                        data = new { 
                                            action = "stop_vm", 
                                            vmName = vm.name,
                                            resourceGroup = vm.resourceGroup
                                        },
                                        style = "destructive"
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "🔄 Restart VM",
                                        data = new { 
                                            action = "restart_vm", 
                                            vmName = vm.name,
                                            resourceGroup = vm.resourceGroup
                                        }
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "🔍 VM Details",
                                        data = new { 
                                            action = "get_vm_details", 
                                            vmName = vm.name,
                                            resourceGroup = vm.resourceGroup
                                        }
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "📊 VM Metrics",
                                        data = new { 
                                            action = "get_vm_metrics", 
                                            vmName = vm.name,
                                            resourceGroup = vm.resourceGroup
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                items = new object[]
                {
                    new
                    {
                        type = "ColumnSet",
                        columns = new object[]
                        {
                            new
                            {
                                type = "Column",
                                width = "stretch",
                                items = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = $"**{vm.name}**",
                                        weight = "Bolder",
                                        size = "Medium"
                                    },
                                    new
                                    {
                                        type = "TextBlock",
                                        text = $"📍 {vm.location} | {GetVMStatusIcon(vm.powerState?.ToString())} {vm.powerState} | 💻 {vm.vmSize}",
                                        spacing = "None",
                                        color = GetVMStatusColor(vm.powerState?.ToString())
                                    }
                                }
                            },
                            new
                            {
                                type = "Column",
                                width = "auto",
                                items = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = "🔽",
                                        horizontalAlignment = "Right"
                                    }
                                }
                            }
                        }
                    }
                },
                spacing = "Medium"
            })).ToArray(),
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "🔄 Refresh VMs",
                    data = new { action = "refresh_vms" }
                },
                new
                {
                    type = "Action.Submit",
                    title = "➕ Create New VM",
                    data = new { action = "create_vm" }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Generate interactive cards for Storage Accounts
    /// </summary>
    public string GenerateStorageAccountCards(IEnumerable<dynamic> storageAccounts)
    {
        var storageList = storageAccounts.ToList();
        
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = "💾 Storage Accounts",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = $"Found {storageList.Count} storage accounts. Click any account for actions:",
                    wrap = true,
                    spacing = "Medium"
                }
            }.Concat(storageList.Select(storage => new
            {
                type = "Container",
                style = "emphasis",
                selectAction = new
                {
                    type = "Action.ShowCard",
                    card = new
                    {
                        type = "AdaptiveCard",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = $"Actions for Storage: {storage.name}",
                                weight = "Bolder",
                                wrap = true
                            },
                            new
                            {
                                type = "ActionSet",
                                actions = new object[]
                                {
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "📂 Browse Containers",
                                        data = new { 
                                            action = "browse_containers", 
                                            storageAccountName = storage.name,
                                            resourceGroup = storage.resourceGroup
                                        }
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "🔑 Manage Keys",
                                        data = new { 
                                            action = "manage_storage_keys", 
                                            storageAccountName = storage.name,
                                            resourceGroup = storage.resourceGroup
                                        }
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "📊 View Metrics",
                                        data = new { 
                                            action = "get_storage_metrics", 
                                            storageAccountName = storage.name,
                                            resourceGroup = storage.resourceGroup
                                        }
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "🏷️ Manage Tags",
                                        data = new { 
                                            action = "manage_storage_tags", 
                                            storageAccountName = storage.name,
                                            resourceGroup = storage.resourceGroup
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                items = new object[]
                {
                    new
                    {
                        type = "ColumnSet",
                        columns = new object[]
                        {
                            new
                            {
                                type = "Column",
                                width = "stretch",
                                items = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = $"**{storage.name}**",
                                        weight = "Bolder",
                                        size = "Medium"
                                    },
                                    new
                                    {
                                        type = "TextBlock",
                                        text = $"📍 {storage.location} | ⚡ {storage.sku} | 🔒 {storage.accessTier}",
                                        spacing = "None",
                                        color = "Good"
                                    }
                                }
                            },
                            new
                            {
                                type = "Column",
                                width = "auto",
                                items = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = "🔽",
                                        horizontalAlignment = "Right"
                                    }
                                }
                            }
                        }
                    }
                },
                spacing = "Medium"
            })).ToArray(),
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "🔄 Refresh Storage",
                    data = new { action = "refresh_storage" }
                },
                new
                {
                    type = "Action.Submit",
                    title = "➕ Create Storage Account",
                    data = new { action = "create_storage" }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Universal resource card generator using AI
    /// </summary>
    private async Task<string> GenerateResourceCardsWithAI(string resourceType, List<dynamic> resources, string? context)
    {
        var prompt = $"""
        Generate an Adaptive Card JSON for displaying a list of Azure {resourceType} resources with interactive actions.

        Resource Count: {resources.Count}
        Context: {context ?? "General resource listing"}

        Requirements:
        1. Create clickable cards for each resource
        2. Include relevant actions based on resource type
        3. Show key resource properties
        4. Use appropriate icons and colors
        5. Include expandable action menus
        6. Make cards visually distinct based on resource status
        7. Add refresh and create actions at the bottom

        Resource Type Guidelines:
        - Pods: Show logs, describe, metrics, restart actions
        - VMs: Show start, stop, restart, details, metrics actions  
        - Storage: Show browse, keys, metrics, tags actions
        - Web Apps: Show browse, logs, scale, restart actions
        - Databases: Show query, backup, scale, metrics actions

        Return ONLY the Adaptive Card JSON, no additional text.
        """;

        var response = await _kernel!.InvokePromptAsync(prompt);
        return response.GetValue<string>() ?? "";
    }

    /// <summary>
    /// Static fallback for resource cards
    /// </summary>
    private string GenerateStaticResourceCards(string resourceType, List<dynamic> resources, string? context)
    {
        return resourceType.ToLower() switch
        {
            "pods" or "pod" => GenerateAksPodCards(resources, context ?? "default"),
            "virtualmachines" or "vm" or "vms" => GenerateVirtualMachineCards(resources),
            "storageaccounts" or "storage" => GenerateStorageAccountCards(resources),
            _ => GenerateGenericResourceCards(resourceType, resources, context)
        };
    }

    /// <summary>
    /// Generic resource cards for any Azure resource type
    /// </summary>
    private string GenerateGenericResourceCards(string resourceType, List<dynamic> resources, string? context)
    {
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = $"📦 {resourceType}",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = $"Found {resources.Count} {resourceType.ToLower()}. Click any resource for actions:",
                    wrap = true,
                    spacing = "Medium"
                }
            }.Concat(resources.Select(resource => new
            {
                type = "Container",
                style = "emphasis",
                selectAction = new
                {
                    type = "Action.ShowCard",
                    card = new
                    {
                        type = "AdaptiveCard",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = $"Actions for: {resource.name ?? resource.id ?? "Resource"}",
                                weight = "Bolder",
                                wrap = true
                            },
                            new
                            {
                                type = "ActionSet",
                                actions = new object[]
                                {
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "🔍 View Details",
                                        data = new { 
                                            action = "view_details", 
                                            resourceType,
                                            resourceName = resource.name ?? resource.id,
                                            resourceGroup = resource.resourceGroup
                                        }
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "📊 View Metrics",
                                        data = new { 
                                            action = "view_metrics", 
                                            resourceType,
                                            resourceName = resource.name ?? resource.id,
                                            resourceGroup = resource.resourceGroup
                                        }
                                    },
                                    new
                                    {
                                        type = "Action.Submit",
                                        title = "🏷️ Manage Tags",
                                        data = new { 
                                            action = "manage_tags", 
                                            resourceType,
                                            resourceName = resource.name ?? resource.id,
                                            resourceGroup = resource.resourceGroup
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                items = new object[]
                {
                    new
                    {
                        type = "ColumnSet",
                        columns = new object[]
                        {
                            new
                            {
                                type = "Column",
                                width = "stretch",
                                items = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = $"**{resource.name ?? resource.id ?? "Unknown"}**",
                                        weight = "Bolder",
                                        size = "Medium"
                                    },
                                    new
                                    {
                                        type = "TextBlock",
                                        text = $"📍 {resource.location ?? "Unknown"} | 📁 {resource.resourceGroup ?? "Unknown"}",
                                        spacing = "None",
                                        color = "Good"
                                    }
                                }
                            },
                            new
                            {
                                type = "Column",
                                width = "auto",
                                items = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = "🔽",
                                        horizontalAlignment = "Right"
                                    }
                                }
                            }
                        }
                    }
                },
                spacing = "Medium"
            })).ToArray(),
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = $"🔄 Refresh {resourceType}",
                    data = new { action = "refresh_resources", resourceType }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion

    #region Helper Methods for Resource Cards

    private string GetPodStatusIcon(string? status) => status?.ToLower() switch
    {
        "running" => "✅",
        "pending" => "⏳",
        "failed" => "❌",
        "succeeded" => "✅",
        "unknown" => "❓",
        _ => "⚪"
    };

    private string GetPodStatusColor(string? status) => status?.ToLower() switch
    {
        "running" or "succeeded" => "Good",
        "pending" => "Warning",
        "failed" => "Attention",
        _ => "Default"
    };

    private string GetPodContainerStyle(string? status) => status?.ToLower() switch
    {
        "running" or "succeeded" => "good",
        "pending" => "warning", 
        "failed" => "attention",
        _ => "emphasis"
    };

    private string GetVMStatusIcon(string? powerState) => powerState?.ToLower() switch
    {
        "running" or "vm running" => "✅",
        "stopped" or "vm stopped" => "⏹️",
        "starting" or "vm starting" => "⏳",
        "stopping" or "vm stopping" => "⏳",
        "deallocated" or "vm deallocated" => "💤",
        _ => "❓"
    };

    private string GetVMStatusColor(string? powerState) => powerState?.ToLower() switch
    {
        "running" or "vm running" => "Good",
        "starting" or "vm starting" or "stopping" or "vm stopping" => "Warning",
        "stopped" or "vm stopped" or "deallocated" or "vm deallocated" => "Attention",
        _ => "Default"
    };

    private string GetVMContainerStyle(string? powerState) => powerState?.ToLower() switch
    {
        "running" or "vm running" => "good",
        "starting" or "vm starting" or "stopping" or "vm stopping" => "warning",
        "stopped" or "vm stopped" or "deallocated" or "vm deallocated" => "attention",
        _ => "emphasis"
    };

    #endregion

    #region AKS Parameter Collection Cards

    public string GenerateAksParameterCollectionCard()
    {
        var card = new
        {
            type = "AdaptiveCard",
            version = "1.5",
            body = new object[]
            {
                new
                {
                    type = "TextBlock",
                    text = "🚀 Azure Kubernetes Service (AKS) Configuration",
                    weight = "Bolder",
                    size = "Large",
                    color = "Accent"
                },
                new
                {
                    type = "TextBlock",
                    text = "Let's configure your AKS cluster with the required parameters:",
                    wrap = true,
                    spacing = "Medium",
                    isSubtle = true
                },
                new
                {
                    type = "Input.Text",
                    id = "workload_name",
                    label = "Workload Name *",
                    placeholder = "e.g., myapp-aks",
                    isRequired = true,
                    spacing = "Medium"
                },
                new
                {
                    type = "Input.Text",
                    id = "project_name",
                    label = "Project Name *",
                    placeholder = "e.g., finance-app",
                    isRequired = true
                },
                new
                {
                    type = "Input.Text",
                    id = "owner",
                    label = "Owner *",
                    placeholder = "Your name or team",
                    isRequired = true
                },
                new
                {
                    type = "Input.ChoiceSet",
                    id = "environment",
                    label = "Environment",
                    value = "dev",
                    choices = new object[]
                    {
                        new { title = "Development", value = "dev" },
                        new { title = "Test", value = "test" },
                        new { title = "Staging", value = "staging" },
                        new { title = "Production", value = "prod" }
                    }
                },
                new
                {
                    type = "Input.ChoiceSet",
                    id = "location",
                    label = "Azure Region",
                    value = "eastus",
                    choices = new object[]
                    {
                        new { title = "East US", value = "eastus" },
                        new { title = "West US 2", value = "westus2" },
                        new { title = "West Europe", value = "westeurope" },
                        new { title = "Southeast Asia", value = "southeastasia" },
                        new { title = "UK South", value = "uksouth" }
                    }
                },
                new
                {
                    type = "Input.ChoiceSet",
                    id = "node_count",
                    label = "Initial Node Count",
                    value = "2",
                    choices = new object[]
                    {
                        new { title = "1 node", value = "1" },
                        new { title = "2 nodes", value = "2" },
                        new { title = "3 nodes", value = "3" },
                        new { title = "5 nodes", value = "5" }
                    }
                },
                new
                {
                    type = "Input.ChoiceSet",
                    id = "vm_size",
                    label = "Node VM Size",
                    value = "Standard_DS2_v2",
                    choices = new object[]
                    {
                        new { title = "Standard_DS2_v2 (2 vCPU, 7GB RAM)", value = "Standard_DS2_v2" },
                        new { title = "Standard_DS3_v2 (4 vCPU, 14GB RAM)", value = "Standard_DS3_v2" },
                        new { title = "Standard_D4s_v3 (4 vCPU, 16GB RAM)", value = "Standard_D4s_v3" },
                        new { title = "Standard_D8s_v3 (8 vCPU, 32GB RAM)", value = "Standard_D8s_v3" }
                    }
                },
                new
                {
                    type = "TextBlock",
                    text = "* Required fields",
                    size = "Small",
                    isSubtle = true,
                    spacing = "Medium"
                }
            },
            actions = new object[]
            {
                new
                {
                    type = "Action.Submit",
                    title = "🚀 Create AKS Cluster",
                    style = "positive",
                    data = new { action = "create_aks_cluster" }
                },
                new
                {
                    type = "Action.Submit",
                    title = "Use Defaults",
                    data = new { action = "create_aks_defaults" }
                },
                new
                {
                    type = "Action.Submit",
                    title = "Cancel",
                    style = "destructive",
                    data = new { action = "cancel" }
                }
            }
        };

        return JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion
}