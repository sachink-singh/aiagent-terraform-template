using AzureAIAgent.Core.Models;

namespace AzureAIAgent.Core.Interfaces;

/// <summary>
/// Interface for executing Azure CLI commands
/// </summary>
public interface IAzureCommandExecutor
{
    /// <summary>
    /// Execute an Azure CLI command
    /// </summary>
    Task<OperationResult<string>> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute multiple Azure CLI commands in sequence
    /// </summary>
    Task<OperationResult<List<string>>> ExecuteCommandsAsync(List<string> commands, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if Azure CLI is available and user is logged in
    /// </summary>
    Task<OperationResult<bool>> CheckAzureCliStatusAsync();
    
    /// <summary>
    /// Get current Azure subscription info
    /// </summary>
    Task<OperationResult<object>> GetCurrentSubscriptionAsync();
    
    /// <summary>
    /// Set the current subscription context
    /// </summary>
    Task<OperationResult<bool>> SetSubscriptionAsync(string subscriptionId);
    
    /// <summary>
    /// List available subscriptions
    /// </summary>
    Task<OperationResult<List<object>>> ListSubscriptionsAsync();
    
    /// <summary>
    /// Validate a command before execution
    /// </summary>
    Task<OperationResult<bool>> ValidateCommandAsync(string command);
}
