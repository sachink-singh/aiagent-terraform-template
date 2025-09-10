using System.Collections.Concurrent;

namespace AzureAIAgent.Core.Services;

/// <summary>
/// Thread-safe manager for current session context accessible across the application
/// </summary>
public static class SessionContextManager
{
    private static readonly AsyncLocal<string?> _currentSessionId = new AsyncLocal<string?>();
    
    public static void SetCurrentSessionId(string? sessionId)
    {
        _currentSessionId.Value = sessionId;
    }
    
    public static string? GetCurrentSessionId()
    {
        return _currentSessionId.Value;
    }
    
    public static void ClearCurrentSessionId()
    {
        _currentSessionId.Value = null;
    }
}
