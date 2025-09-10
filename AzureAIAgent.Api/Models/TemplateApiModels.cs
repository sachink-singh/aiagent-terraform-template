// Additional API Models for Adaptive Cards and Templates
namespace AzureAIAgent.Api.Models;

public class TemplateGalleryRequest
{
    public string? SessionId { get; set; }
    public string? Category { get; set; } // Optional filter: compute, containers, web, storage, database
}

public class TemplateGalleryResponse
{
    public string AdaptiveCard { get; set; } = string.Empty;
    public List<TemplateInfo> Templates { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class TemplateParameterFormRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}

public class TemplateParameterFormResponse
{
    public string AdaptiveCard { get; set; } = string.Empty;
    public TemplateInfo Template { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class TemplateDeploymentRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string? SessionId { get; set; }
}

public class TemplateDeploymentResponse
{
    public string DeploymentId { get; set; } = string.Empty;
    public string AdaptiveCard { get; set; } = string.Empty; // Status card
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class TemplateInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<TemplateParameterInfo> Parameters { get; set; } = new();
}

public class TemplateParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Default { get; set; }
    public bool Required { get; set; }
    public bool Sensitive { get; set; }
}
