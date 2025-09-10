using System.Text.Json;
using System.Text.Json.Serialization;

namespace KubernetesMcpServer.Models;

/// <summary>
/// MCP Protocol message types and models
/// Based on Model Context Protocol specification
/// </summary>
public record McpMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; init; }
    
    [JsonPropertyName("method")]
    public string? Method { get; init; }
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
    
    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }
    
    [JsonPropertyName("error")]
    public McpError? Error { get; init; }
}

public record McpError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
    
    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}

public record McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
    
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; init; }
}

public record McpResource
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
}

public record McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "Kubernetes MCP Server";
    
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = "Enterprise-grade Kubernetes MCP Server for secure cluster management";
    
    [JsonPropertyName("authors")]
    public string[] Authors { get; init; } = ["Enterprise Team"];
    
    [JsonPropertyName("license")]
    public string License { get; init; } = "Enterprise";
}

public record McpCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; init; }
    
    [JsonPropertyName("resources")]
    public ResourcesCapability? Resources { get; init; }
}

public record ToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; } = false;
}

public record ResourcesCapability
{
    [JsonPropertyName("subscribe")]
    public bool Subscribe { get; init; } = false;
    
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; } = false;
}
