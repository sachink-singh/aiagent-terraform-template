using AzureAIAgent.Core.Models;

namespace AzureAIAgent.Core.Interfaces;

/// <summary>
/// Interface for managing conversation sessions
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Get an existing session by ID
    /// </summary>
    Task<ConversationSession?> GetSessionAsync(string sessionId);
    
    /// <summary>
    /// Get or create a session with the specified ID
    /// </summary>
    Task<ConversationSession> GetOrCreateSessionAsync(string sessionId, string? userId = null);
    
    /// <summary>
    /// Update an existing session
    /// </summary>
    Task UpdateSessionAsync(ConversationSession session);
    
    /// <summary>
    /// Delete a session
    /// </summary>
    Task DeleteSessionAsync(string sessionId);
    
    /// <summary>
    /// Get all sessions for a user
    /// </summary>
    Task<List<ConversationSession>> GetUserSessionsAsync(string userId);
    
    /// <summary>
    /// Cleanup expired sessions
    /// </summary>
    Task CleanupExpiredSessionsAsync(TimeSpan expirationTime);
    
    /// <summary>
    /// Manually sync Terraform state for a session (on-demand)
    /// </summary>
    Task SyncTerraformStateAsync(string sessionId);
}
