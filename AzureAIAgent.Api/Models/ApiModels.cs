namespace AzureAIAgent.Api.Models;

// Model classes for API requests/responses
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public object? AdaptiveCard { get; set; } // New: Support for adaptive cards
    public string? ContentType { get; set; } = "text"; // New: "text", "adaptive-card", or "mixed"
}
