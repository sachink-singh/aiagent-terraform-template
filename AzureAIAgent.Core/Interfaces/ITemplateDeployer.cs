using AzureAIAgent.Core.Models;

namespace AzureAIAgent.Core.Interfaces;

/// <summary>
/// Interface for deploying Bicep templates
/// </summary>
public interface ITemplateDeployer
{
    /// <summary>
    /// Deploy a Bicep template
    /// </summary>
    Task<OperationResult<DeploymentState>> DeployTemplateAsync(
        string templateContent, 
        string? parametersContent, 
        string deploymentName,
        string resourceGroupName,
        string subscriptionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate a Bicep template from natural language description
    /// </summary>
    Task<OperationResult<string>> GenerateTemplateAsync(string description, Dictionary<string, object>? parameters = null);
    
    /// <summary>
    /// Validate a Bicep template
    /// </summary>
    Task<OperationResult<bool>> ValidateTemplateAsync(string templateContent, string? parametersContent = null);
    
    /// <summary>
    /// Get deployment status
    /// </summary>
    Task<OperationResult<DeploymentState>> GetDeploymentStatusAsync(string deploymentName, string resourceGroupName, string subscriptionId);
    
    /// <summary>
    /// Delete a deployment
    /// </summary>
    Task<OperationResult<bool>> DeleteDeploymentAsync(string deploymentName, string resourceGroupName, string subscriptionId);
    
    /// <summary>
    /// Estimate costs for a template deployment
    /// </summary>
    Task<OperationResult<object>> EstimateTemplateDeploymentCostAsync(string templateContent, string? parametersContent = null);
    
    /// <summary>
    /// Preview what resources will be created/modified by a template
    /// </summary>
    Task<OperationResult<object>> PreviewTemplateDeploymentAsync(
        string templateContent, 
        string? parametersContent, 
        string resourceGroupName,
        string subscriptionId);
}
