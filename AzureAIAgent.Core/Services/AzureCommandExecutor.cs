using AzureAIAgent.Core.Interfaces;
using AzureAIAgent.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AzureAIAgent.Core.Services;

/// <summary>
/// Azure CLI command executor implementation
/// </summary>
public class AzureCommandExecutor : IAzureCommandExecutor
{
    private readonly ILogger<AzureCommandExecutor> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AzureCommandExecutor(ILogger<AzureCommandExecutor> logger)
    {
        _logger = logger;
    }

    public async Task<OperationResult<string>> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Executing Azure CLI command: {Command}", command);

            // Validate command before execution
            var validationResult = await ValidateCommandAsync(command);
            if (!validationResult.IsSuccess)
            {
                return OperationResult<string>.Failure($"Command validation failed: {validationResult.ErrorMessage}");
            }

            using var process = new Process();
            process.StartInfo.FileName = "az";
            process.StartInfo.Arguments = command.StartsWith("az ") ? command[3..] : command;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Command executed successfully: {Command}", command);
                return OperationResult<string>.Success(output);
            }
            else
            {
                _logger.LogError("Command failed with exit code {ExitCode}: {Command}\nError: {Error}", 
                    process.ExitCode, command, error);
                return OperationResult<string>.Failure($"Command failed: {error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", command);
            return OperationResult<string>.Failure($"Error executing command: {ex.Message}");
        }
    }

    public async Task<OperationResult<List<string>>> ExecuteCommandsAsync(List<string> commands, CancellationToken cancellationToken = default)
    {
        var results = new List<string>();
        var warnings = new List<string>();

        foreach (var command in commands)
        {
            var result = await ExecuteCommandAsync(command, cancellationToken);
            
            if (result.IsSuccess)
            {
                results.Add(result.Data ?? string.Empty);
                warnings.AddRange(result.Warnings);
            }
            else
            {
                return OperationResult<List<string>>.Failure($"Command failed: {command} - {result.ErrorMessage}");
            }
        }

        return warnings.Any() 
            ? OperationResult<List<string>>.SuccessWithWarnings(results, warnings)
            : OperationResult<List<string>>.Success(results);
    }

    public async Task<OperationResult<bool>> CheckAzureCliStatusAsync()
    {
        try
        {
            // Check if Azure CLI is installed
            var versionResult = await ExecuteCommandAsync("--version");
            if (!versionResult.IsSuccess)
            {
                return OperationResult<bool>.Failure("Azure CLI is not installed or not available in PATH");
            }

            // Check if user is logged in
            var accountResult = await ExecuteCommandAsync("account show");
            if (!accountResult.IsSuccess)
            {
                return OperationResult<bool>.Failure("User is not logged into Azure CLI. Please run 'az login'");
            }

            return OperationResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Azure CLI status");
            return OperationResult<bool>.Failure($"Error checking Azure CLI status: {ex.Message}");
        }
    }

    public async Task<OperationResult<object>> GetCurrentSubscriptionAsync()
    {
        try
        {
            var result = await ExecuteCommandAsync("account show --output json");
            if (!result.IsSuccess)
            {
                return OperationResult<object>.Failure(result.ErrorMessage ?? "Failed to get current subscription");
            }

            var subscription = JsonSerializer.Deserialize<object>(result.Data ?? "{}");
            return OperationResult<object>.Success(subscription ?? new object());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current subscription");
            return OperationResult<object>.Failure($"Error getting current subscription: {ex.Message}");
        }
    }

    public async Task<OperationResult<bool>> SetSubscriptionAsync(string subscriptionId)
    {
        try
        {
            var result = await ExecuteCommandAsync($"account set --subscription {subscriptionId}");
            return OperationResult<bool>.Success(result.IsSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting subscription {SubscriptionId}", subscriptionId);
            return OperationResult<bool>.Failure($"Error setting subscription: {ex.Message}");
        }
    }

    public async Task<OperationResult<List<object>>> ListSubscriptionsAsync()
    {
        try
        {
            var result = await ExecuteCommandAsync("account list --output json");
            if (!result.IsSuccess)
            {
                return OperationResult<List<object>>.Failure(result.ErrorMessage ?? "Failed to list subscriptions");
            }

            var subscriptions = JsonSerializer.Deserialize<List<object>>(result.Data ?? "[]");
            return OperationResult<List<object>>.Success(subscriptions ?? new List<object>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing subscriptions");
            return OperationResult<List<object>>.Failure($"Error listing subscriptions: {ex.Message}");
        }
    }

    public Task<OperationResult<bool>> ValidateCommandAsync(string command)
    {
        try
        {
            // Basic validation - ensure it's an Azure CLI command
            if (string.IsNullOrWhiteSpace(command))
            {
                return Task.FromResult(OperationResult<bool>.Failure("Command cannot be empty"));
            }

            // Remove 'az ' prefix if present for validation
            var cleanCommand = command.StartsWith("az ") ? command[3..] : command;

            // List of potentially dangerous commands
            var dangerousCommands = new[]
            {
                "account clear",
                "logout",
                "group delete",
                "vm delete",
                "delete"
            };

            if (dangerousCommands.Any(dangerous => cleanCommand.Contains(dangerous, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Potentially dangerous command detected: {Command}", command);
                return Task.FromResult(OperationResult<bool>.SuccessWithWarnings(true, 
                    new List<string> { "This command may delete or modify existing resources" }));
            }

            return Task.FromResult(OperationResult<bool>.Success(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating command: {Command}", command);
            return Task.FromResult(OperationResult<bool>.Failure($"Error validating command: {ex.Message}"));
        }
    }
}
