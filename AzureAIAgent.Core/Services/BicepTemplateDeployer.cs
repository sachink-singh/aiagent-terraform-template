using AzureAIAgent.Core.Interfaces;
using AzureAIAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using System.Text.Json;

namespace AzureAIAgent.Core.Services;

/// <summary>
/// Bicep template deployment service using Azure SDK
/// </summary>
public class BicepTemplateDeployer : ITemplateDeployer
{
    private readonly ILogger<BicepTemplateDeployer> _logger;
    private readonly ArmClient _armClient;

    public BicepTemplateDeployer(ILogger<BicepTemplateDeployer> logger)
    {
        _logger = logger;
        _armClient = new ArmClient(new DefaultAzureCredential());
    }

    public async Task<OperationResult<DeploymentState>> DeployTemplateAsync(
        string templateContent, 
        string? parametersContent, 
        string deploymentName,
        string resourceGroupName,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting deployment {DeploymentName} in resource group {ResourceGroup}", 
                deploymentName, resourceGroupName);

            var subscription = await _armClient.GetDefaultSubscriptionAsync();
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken);

            if (!resourceGroup.HasValue)
            {
                return OperationResult<DeploymentState>.Failure($"Resource group {resourceGroupName} not found");
            }

            // Parse parameters if provided
            var parameters = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(parametersContent))
            {
                try
                {
                    var parameterJson = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersContent);
                    if (parameterJson != null)
                    {
                        parameters = parameterJson;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse parameters JSON");
                    return OperationResult<DeploymentState>.Failure($"Invalid parameters JSON: {ex.Message}");
                }
            }

            // Create deployment content
            var deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
            {
                Template = BinaryData.FromString(templateContent),
                Parameters = BinaryData.FromObjectAsJson(parameters)
            });

            // Start deployment
            var deploymentOperation = await resourceGroup.Value.GetArmDeployments()
                .CreateOrUpdateAsync(Azure.WaitUntil.Started, deploymentName, deploymentContent, cancellationToken);

            var deploymentState = new DeploymentState
            {
                Id = Guid.NewGuid().ToString(),
                DeploymentName = deploymentName,
                TemplateContent = templateContent,
                ParametersContent = parametersContent,
                ResourceGroupName = resourceGroupName,
                SubscriptionId = subscriptionId,
                Status = DeploymentStatus.InProgress,
                StartedAt = DateTime.UtcNow,
                CorrelationId = deploymentOperation.Id
            };

            _logger.LogInformation("Deployment {DeploymentName} started successfully", deploymentName);
            return OperationResult<DeploymentState>.Success(deploymentState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying template {DeploymentName}", deploymentName);
            return OperationResult<DeploymentState>.Failure($"Deployment failed: {ex.Message}");
        }
    }

    public async Task<OperationResult<string>> GenerateTemplateAsync(string description, Dictionary<string, object>? parameters = null)
    {
        try
        {
            _logger.LogInformation("Generating Bicep template from description: {Description}", description);

            // This is a simplified template generator
            // In a real implementation, you might use AI to generate more sophisticated templates
            
            var template = await Task.Run(() => GenerateBasicTemplate(description, parameters));
            
            return OperationResult<string>.Success(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating template from description: {Description}", description);
            return OperationResult<string>.Failure($"Template generation failed: {ex.Message}");
        }
    }

    public async Task<OperationResult<bool>> ValidateTemplateAsync(string templateContent, string? parametersContent = null)
    {
        try
        {
            _logger.LogInformation("Validating Bicep template");

            // Basic JSON validation
            await Task.Run(() =>
            {
                try
                {
                    JsonSerializer.Deserialize<object>(templateContent);
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException("Template contains invalid JSON");
                }

                if (!string.IsNullOrEmpty(parametersContent))
                {
                    try
                    {
                        JsonSerializer.Deserialize<object>(parametersContent);
                    }
                    catch (JsonException)
                    {
                        throw new InvalidOperationException("Parameters contain invalid JSON");
                    }
                }
            });

            // Additional template validation would go here
            // For now, we'll just return success if JSON is valid
            return OperationResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template");
            return OperationResult<bool>.Failure($"Template validation failed: {ex.Message}");
        }
    }

    public async Task<OperationResult<DeploymentState>> GetDeploymentStatusAsync(string deploymentName, string resourceGroupName, string subscriptionId)
    {
        try
        {
            var subscription = await _armClient.GetDefaultSubscriptionAsync();
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            if (!resourceGroup.HasValue)
            {
                return OperationResult<DeploymentState>.Failure($"Resource group {resourceGroupName} not found");
            }

            var deployment = await resourceGroup.Value.GetArmDeploymentAsync(deploymentName);
            
            if (!deployment.HasValue)
            {
                return OperationResult<DeploymentState>.Failure($"Deployment {deploymentName} not found");
            }

            var deploymentData = deployment.Value.Data;
            var status = MapProvisioningStateToDeploymentStatus(deploymentData.Properties?.ProvisioningState);

            var deploymentState = new DeploymentState
            {
                Id = deploymentData.Id?.ToString() ?? string.Empty,
                DeploymentName = deploymentName,
                ResourceGroupName = resourceGroupName,
                SubscriptionId = subscriptionId,
                Status = status,
                StartedAt = deploymentData.Properties?.Timestamp?.DateTime ?? DateTime.UtcNow,
                CompletedAt = status == DeploymentStatus.Succeeded || status == DeploymentStatus.Failed 
                    ? deploymentData.Properties?.Timestamp?.DateTime 
                    : null
            };

            return OperationResult<DeploymentState>.Success(deploymentState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployment status for {DeploymentName}", deploymentName);
            return OperationResult<DeploymentState>.Failure($"Failed to get deployment status: {ex.Message}");
        }
    }

    public async Task<OperationResult<bool>> DeleteDeploymentAsync(string deploymentName, string resourceGroupName, string subscriptionId)
    {
        try
        {
            var subscription = await _armClient.GetDefaultSubscriptionAsync();
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            if (!resourceGroup.HasValue)
            {
                return OperationResult<bool>.Failure($"Resource group {resourceGroupName} not found");
            }

            await resourceGroup.Value.GetArmDeploymentAsync(deploymentName).Result.Value.DeleteAsync(Azure.WaitUntil.Completed);
            
            _logger.LogInformation("Deployment {DeploymentName} deleted successfully", deploymentName);
            return OperationResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting deployment {DeploymentName}", deploymentName);
            return OperationResult<bool>.Failure($"Failed to delete deployment: {ex.Message}");
        }
    }

    public Task<OperationResult<object>> EstimateTemplateDeploymentCostAsync(string templateContent, string? parametersContent = null)
    {
        // Cost estimation would require integration with Azure Cost Management APIs
        // For now, return a placeholder
        _logger.LogWarning("Cost estimation not implemented - returning placeholder");
        return Task.FromResult(OperationResult<object>.Success(new { message = "Cost estimation not implemented" }));
    }

    public async Task<OperationResult<object>> PreviewTemplateDeploymentAsync(
        string templateContent, 
        string? parametersContent, 
        string resourceGroupName,
        string subscriptionId)
    {
        try
        {
            var subscription = await _armClient.GetDefaultSubscriptionAsync();
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName);

            if (!resourceGroup.HasValue)
            {
                return OperationResult<object>.Failure($"Resource group {resourceGroupName} not found");
            }

            // For now, return a simplified preview
            // The actual what-if operation requires more complex setup
            var preview = new
            {
                message = "Preview functionality requires Azure CLI integration",
                suggestion = "Use 'az deployment group what-if' command for detailed preview"
            };
            
            return OperationResult<object>.Success(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing template deployment");
            return OperationResult<object>.Failure($"Preview failed: {ex.Message}");
        }
    }

    private static DeploymentStatus MapProvisioningStateToDeploymentStatus(ResourcesProvisioningState? provisioningState)
    {
        if (provisioningState == null) return DeploymentStatus.Preparing;
        
        if (provisioningState.ToString() == "Accepted") return DeploymentStatus.Preparing;
        if (provisioningState.ToString() == "Running") return DeploymentStatus.InProgress;
        if (provisioningState.ToString() == "Succeeded") return DeploymentStatus.Succeeded;
        if (provisioningState.ToString() == "Failed") return DeploymentStatus.Failed;
        if (provisioningState.ToString() == "Canceled") return DeploymentStatus.Cancelled;
        
        return DeploymentStatus.Preparing;
    }

    private static string GenerateBasicTemplate(string description, Dictionary<string, object>? parameters)
    {
        // Simple template generation based on keywords in description
        var template = new
        {
            @schema = "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
            contentVersion = "1.0.0.0",
            parameters = new Dictionary<string, object>(),
            variables = new Dictionary<string, object>(),
            resources = new List<object>(),
            outputs = new Dictionary<string, object>()
        };

        // Add basic resources based on keywords in description
        if (description.Contains("storage", StringComparison.OrdinalIgnoreCase))
        {
            // Add storage account resource
        }
        
        if (description.Contains("web app", StringComparison.OrdinalIgnoreCase) || 
            description.Contains("app service", StringComparison.OrdinalIgnoreCase))
        {
            // Add app service resources
        }

        return JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
    }
}
