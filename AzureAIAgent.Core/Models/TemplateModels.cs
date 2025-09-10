namespace AzureAIAgent.Core.Models;

public class TemplateMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string GitHubUrl { get; set; } = string.Empty;
    public TemplateParameter[] Parameters { get; set; } = Array.Empty<TemplateParameter>();
}

public class TemplateParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? Default { get; set; }
    public bool Sensitive { get; set; }
}

public class DeploymentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Placeholder { get; set; }
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsSecret { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string, number, boolean, choice, multiline
    public string[]? AllowedValues { get; set; } // For choice type
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? ValidationPattern { get; set; } // Regex pattern
    public string? ValidationMessage { get; set; }
    public string? Group { get; set; } // For organizing parameters into groups
    public int Order { get; set; } = 0; // For parameter ordering
    public bool IsAdvanced { get; set; } = false; // For advanced/expert parameters
}
