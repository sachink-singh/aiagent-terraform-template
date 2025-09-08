using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AzureAIAgent.Core;
using AzureAIAgent.Core.Interfaces;
using AzureAIAgent.Core.Services;
using AzureAIAgent.Core.Models;

// Create host builder
var builder = Host.CreateApplicationBuilder(args);

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services
builder.Services.AddMemoryCache(); // Required for InMemorySessionManager
builder.Services.AddSingleton<ISessionManager, InMemorySessionManager>();
builder.Services.AddSingleton<IAzureCommandExecutor, AzureCommandExecutor>();
builder.Services.AddSingleton<ITemplateDeployer, BicepTemplateDeployer>();

// Add AKS context service for dynamic cluster management
builder.Services.AddSingleton<AzureAIAgent.Core.Services.IAksContextService, AzureAIAgent.Core.Services.AksContextService>();

// Add new GitHub-based services
builder.Services.AddHttpClient(); // Required for GitHubTemplateService
builder.Services.AddSingleton<AzureAIAgent.Core.Services.GitHubTemplateService>();
builder.Services.AddSingleton<AzureAIAgent.Core.Services.AdaptiveCardService>();
builder.Services.AddSingleton<AzureAIAgent.Core.Services.IUniversalInteractiveService, AzureAIAgent.Core.Services.UniversalInteractiveService>();

// Add port detection service for dynamic API URL detection  
builder.Services.AddHttpClient<AzureAIAgent.Core.Services.IPortDetectionService, AzureAIAgent.Core.Services.PortDetectionService>();

// Configure Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();

// Add services to the kernel builder too
kernelBuilder.Services.AddHttpClient();
kernelBuilder.Services.AddSingleton<AzureAIAgent.Core.Services.GitHubTemplateService>(provider =>
    new AzureAIAgent.Core.Services.GitHubTemplateService(
        provider.GetRequiredService<HttpClient>(),
        provider.GetRequiredService<ILogger<AzureAIAgent.Core.Services.GitHubTemplateService>>(),
        builder.Configuration));
kernelBuilder.Services.AddSingleton<AzureAIAgent.Core.Services.AdaptiveCardService>();

// Get AI configuration
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var azureOpenAiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var azureOpenAiApiKey = builder.Configuration["AzureOpenAI:ApiKey"];

if (!string.IsNullOrEmpty(openAiApiKey))
{
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: builder.Configuration["OpenAI:Model"] ?? "gpt-4o", 
        apiKey: openAiApiKey);
}
else if (!string.IsNullOrEmpty(azureOpenAiEndpoint) && !string.IsNullOrEmpty(azureOpenAiApiKey))
{
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4",
        endpoint: azureOpenAiEndpoint,
        apiKey: azureOpenAiApiKey);
}
else
{
    Console.WriteLine("❌ No AI configuration found. Please set OpenAI or Azure OpenAI configuration.");
    return;
}

// Build kernel with services first
var kernel = kernelBuilder.Build();

// Build the host first to get the service provider for session manager
var tempHost = builder.Build();

// Get services from host
var sessionManager = tempHost.Services.GetRequiredService<ISessionManager>();

// Create plugins with proper dependency injection
var templateService = kernel.Services.GetRequiredService<AzureAIAgent.Core.Services.GitHubTemplateService>();
var cardService = kernel.Services.GetRequiredService<AzureAIAgent.Core.Services.AdaptiveCardService>();

// Add plugins with injected dependencies
kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(new AzureAIAgent.Plugins.AzureResourcePlugin(templateService), "AzureResourcePlugin"));
kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(new AzureAIAgent.Plugins.GitHubTerraformPlugin(templateService, cardService, kernel.Services.GetRequiredService<ILogger<AzureAIAgent.Plugins.GitHubTerraformPlugin>>(), sessionManager), "GitHubTerraformPlugin"));

builder.Services.AddSingleton(kernel);

// Register AI Agent
builder.Services.AddSingleton<AzureAIAgent.Core.IAzureAIAgent, AzureAIAgent.Core.AzureAIAgent>();

// Build the host
var host = builder.Build();

// Get the AI agent
var aiAgent = host.Services.GetRequiredService<AzureAIAgent.Core.IAzureAIAgent>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

Console.WriteLine("🚀 Azure AI Agent Console");
Console.WriteLine("==========================");
Console.WriteLine("Type 'help' for available commands, 'exit' to quit");
Console.WriteLine();

// Generate a session ID for this console session
var sessionId = Guid.NewGuid().ToString();
Console.WriteLine($"📱 Session ID: {sessionId}");
Console.WriteLine();

while (true)
{
    Console.Write("You: ");
    var input = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(input))
        continue;
        
    var command = input.Trim().ToLowerInvariant();
    
    if (command == "exit" || command == "quit")
    {
        Console.WriteLine("👋 Goodbye!");
        break;
    }
    
    if (command == "help")
    {
        Console.WriteLine("📖 Available Commands:");
        Console.WriteLine("  help           - Show this help message");
        Console.WriteLine("  clear          - Clear conversation history");
        Console.WriteLine("  history        - Show conversation history");
        Console.WriteLine("  exit/quit      - Exit the application");
        Console.WriteLine();
        Console.WriteLine("💡 Examples:");
        Console.WriteLine("  'Create a resource group called test-rg in East US'");
        Console.WriteLine("  'Generate an AKS cluster deployment'");
        Console.WriteLine("  'List my Azure subscriptions'");
        Console.WriteLine();
        continue;
    }
    
    if (command == "clear")
    {
        try
        {
            await aiAgent.ClearChatHistoryAsync(sessionId);
            Console.WriteLine("🧹 Conversation history cleared.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error clearing conversation: {ex.Message}");
        }
        continue;
    }
    
    if (command == "history")
    {
        try
        {
            var history = await aiAgent.GetChatHistoryAsync(sessionId);
            Console.WriteLine("📚 Conversation History:");
            foreach (var message in history)
            {
                Console.WriteLine($"  [{message.Timestamp:HH:mm:ss}] {message.Role}: {message.Content}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting conversation history: {ex.Message}");
        }
        continue;
    }

    // Process the request
    Console.WriteLine("🤔 Processing your request...");
    
    try
    {
        var result = await aiAgent.ProcessRequestAsync(sessionId, input);
        Console.WriteLine($"🤖 AI Agent: {result}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error: {ex.Message}");
    }
    
    Console.WriteLine();
}
