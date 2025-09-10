using Microsoft.SemanticKernel;
using System.ComponentModel;
using AzureAIAgent.Core.Services;

namespace AzureAIAgent.Plugins;

/// <summary>
/// Test minimal Azure Resource Plugin
/// </summary>
public class AzureResourcePluginTest
{
    private readonly GitHubTemplateService? _templateService;

    public AzureResourcePluginTest(GitHubTemplateService? templateService = null)
    {
        _templateService = templateService;
    }

    [KernelFunction("TestFunction")]
    [Description("A simple test function")]
    public async Task<string> TestFunction()
    {
        return "Test successful!";
    }
}
