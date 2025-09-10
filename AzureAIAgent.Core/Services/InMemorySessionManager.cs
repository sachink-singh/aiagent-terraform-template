using AzureAIAgent.Core.Interfaces;
using AzureAIAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace AzureAIAgent.Core.Services;

/// <summary>
/// In-memory implementation of session management
/// For production, this should be replaced with a persistent storage implementation
/// </summary>
public class InMemorySessionManager : ISessionManager
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemorySessionManager> _logger;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(24);

    public InMemorySessionManager(IMemoryCache cache, ILogger<InMemorySessionManager> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<ConversationSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            var session = _cache.Get<ConversationSession>($"session:{sessionId}");
            _logger.LogDebug("Retrieved session {SessionId}: {Found}", sessionId, session != null);
            return Task.FromResult(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", sessionId);
            return Task.FromResult<ConversationSession?>(null);
        }
    }

    public async Task<ConversationSession> GetOrCreateSessionAsync(string sessionId, string? userId = null)
    {
        try
        {
            var session = _cache.Get<ConversationSession>($"session:{sessionId}");
            
            if (session == null)
            {
                session = new ConversationSession
                {
                    Id = sessionId,
                    UserId = userId ?? "anonymous",
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    State = new ConversationState
                    {
                        SessionId = sessionId,
                        CurrentLocation = "eastus",
                        Azure = new AzureConfiguration
                        {
                            DefaultRegion = "eastus",
                            Naming = new ResourceNamingConvention()
                        }
                    },
                    Messages = new List<ConversationMessage>()
                };

                // Note: Terraform state sync is now on-demand only for better performance
                // await SyncExistingTerraformStateAsync(session);

                _cache.Set($"session:{sessionId}", session, _defaultExpiration);
                _logger.LogInformation("Created new session {SessionId} for user {UserId}", sessionId, userId);
            }
            else
            {
                // Note: Terraform state sync is now on-demand only for better performance
                // await SyncExistingTerraformStateAsync(session);
                session.LastActivity = DateTime.UtcNow;
                _cache.Set($"session:{sessionId}", session, _defaultExpiration);
                _logger.LogDebug("Retrieved existing session {SessionId}", sessionId);
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting or creating session {SessionId}", sessionId);
            throw;
        }
    }

    private async Task SyncExistingTerraformStateAsync(ConversationSession session)
    {
        try
        {
            // Scan for existing Terraform directories in temp folder
            var tempPath = Path.GetTempPath();
            var terraformDirs = Directory.GetDirectories(tempPath, "terraform-*", SearchOption.TopDirectoryOnly);

            foreach (var dir in terraformDirs)
            {
                try
                {
                    var deploymentId = Path.GetFileName(dir);
                    
                    // Check if this deployment is already tracked
                    if (session.Deployments.Any(d => d.DeploymentId == deploymentId))
                    {
                        // Update existing deployment status
                        var existingDeployment = session.Deployments.First(d => d.DeploymentId == deploymentId);
                        await existingDeployment.SyncStatusWithTerraformAsync();
                        continue;
                    }

                    // Check if this directory has valid Terraform state
                    var deployment = new DeploymentReference
                    {
                        DeploymentId = deploymentId,
                        TerraformDirectory = dir,
                        TemplateName = "recovered-deployment",
                        CreatedAt = Directory.GetCreationTime(dir),
                        Status = DeploymentStatus.Pending
                    };

                    // Sync with actual Terraform state
                    await deployment.SyncStatusWithTerraformAsync();

                    // Only add if it has valid state
                    if (await deployment.HasValidStateAsync())
                    {
                        session.Deployments.Add(deployment);
                        _logger.LogInformation("Recovered Terraform deployment {DeploymentId} for session {SessionId}", 
                            deploymentId, session.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync Terraform directory {Directory}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing existing Terraform state for session {SessionId}", session.Id);
        }
    }

    public Task UpdateSessionAsync(ConversationSession session)
    {
        try
        {
            session.LastActivity = DateTime.UtcNow;
            _cache.Set($"session:{session.Id}", session, _defaultExpiration);
            _logger.LogDebug("Updated session {SessionId}", session.Id);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session {SessionId}", session.Id);
            throw;
        }
    }

    public Task DeleteSessionAsync(string sessionId)
    {
        try
        {
            _cache.Remove($"session:{sessionId}");
            _logger.LogInformation("Deleted session {SessionId}", sessionId);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
            throw;
        }
    }

    public Task<List<ConversationSession>> GetUserSessionsAsync(string userId)
    {
        // Note: MemoryCache doesn't support enumeration by design
        // For a real implementation, use a persistent store that supports querying
        _logger.LogWarning("GetUserSessionsAsync is not fully supported with in-memory cache");
        return Task.FromResult(new List<ConversationSession>());
    }

    public Task CleanupExpiredSessionsAsync(TimeSpan expirationTime)
    {
        // MemoryCache handles expiration automatically
        _logger.LogDebug("Cleanup requested - MemoryCache handles expiration automatically");
        return Task.CompletedTask;
    }

    public async Task SyncTerraformStateAsync(string sessionId)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session != null)
            {
                _logger.LogInformation("Manual Terraform state sync requested for session {SessionId}", sessionId);
                await SyncExistingTerraformStateAsync(session);
                await UpdateSessionAsync(session);
                _logger.LogInformation("Terraform state sync completed for session {SessionId}", sessionId);
            }
            else
            {
                _logger.LogWarning("Cannot sync Terraform state - session {SessionId} not found", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Terraform state for session {SessionId}", sessionId);
        }
    }
}
