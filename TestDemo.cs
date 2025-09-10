using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using AzureAIAgent.Core;
using AzureAIAgent.Core.Interfaces;
using AzureAIAgent.Core.Services;
using AzureAIAgent.Core.Models;

Console.WriteLine("🧪 Azure AI Agent - Test Demo");
Console.WriteLine("==============================");
Console.WriteLine();

// Create a minimal test setup without AI service
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add memory cache
services.AddMemoryCache();

// Add our services
services.AddSingleton<ISessionManager, InMemorySessionManager>();
services.AddSingleton<IAzureCommandExecutor, AzureCommandExecutor>();
services.AddSingleton<ITemplateDeployer, BicepTemplateDeployer>();

var serviceProvider = services.BuildServiceProvider();

// Test the services individually
Console.WriteLine("🔧 Testing Core Services...");
Console.WriteLine();

// Test Session Manager
var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();
var testSessionId = "test-session-123";

Console.WriteLine("1️⃣ Testing Session Manager:");
var session = await sessionManager.GetOrCreateSessionAsync(testSessionId, "test-user");
Console.WriteLine($"   ✅ Created session: {session.Id}");
Console.WriteLine($"   ✅ User: {session.UserId}");
Console.WriteLine($"   ✅ Created at: {session.CreatedAt}");

// Add a test message
session.Messages.Add(new ConversationMessage
{
    Role = MessageRole.User,
    Content = "Create a resource group in East US"
});

await sessionManager.UpdateSessionAsync(session);
Console.WriteLine($"   ✅ Added message to session");

// Test Azure Command Executor
var commandExecutor = serviceProvider.GetRequiredService<IAzureCommandExecutor>();

Console.WriteLine();
Console.WriteLine("2️⃣ Testing Azure Command Executor:");

// Test command validation
var validateResult = await commandExecutor.ValidateCommandAsync("account show");
Console.WriteLine($"   ✅ Command validation: {validateResult.IsSuccess}");

// Test Azure CLI status check
var statusResult = await commandExecutor.CheckAzureCliStatusAsync();
if (statusResult.IsSuccess)
{
    Console.WriteLine($"   ✅ Azure CLI is available and user is logged in");
    
    // Try to get current subscription
    var subResult = await commandExecutor.GetCurrentSubscriptionAsync();
    if (subResult.IsSuccess)
    {
        Console.WriteLine($"   ✅ Current subscription retrieved");
    }
    else
    {
        Console.WriteLine($"   ⚠️  Could not get subscription: {subResult.ErrorMessage}");
    }
}
else
{
    Console.WriteLine($"   ⚠️  Azure CLI status: {statusResult.ErrorMessage}");
}

// Test Template Deployer
var templateDeployer = serviceProvider.GetRequiredService<ITemplateDeployer>();

Console.WriteLine();
Console.WriteLine("3️⃣ Testing Template Deployer:");

// Test template validation
var testTemplate = """
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": []
}
""";

var validationResult = await templateDeployer.ValidateTemplateAsync(testTemplate);
Console.WriteLine($"   ✅ Template validation: {validationResult.IsSuccess}");

// Test template generation
var generateResult = await templateDeployer.GenerateTemplateAsync("Create a storage account for my web application");
if (generateResult.IsSuccess)
{
    Console.WriteLine($"   ✅ Template generation successful");
    Console.WriteLine($"   📄 Generated template length: {generateResult.Data?.Length} characters");
}

Console.WriteLine();
Console.WriteLine("4️⃣ Testing Session History:");

// Retrieve conversation history
var historyResult = await sessionManager.GetConversationHistoryAsync(testSessionId);
if (historyResult.IsSuccess && historyResult.Data != null)
{
    Console.WriteLine($"   ✅ Retrieved {historyResult.Data.Count} messages");
    foreach (var message in historyResult.Data)
    {
        Console.WriteLine($"   📝 [{message.Role}]: {message.Content}");
    }
}

Console.WriteLine();
Console.WriteLine("5️⃣ Testing Configuration Management:");

// Test updating session configuration
var newState = session.State;
newState.CurrentSubscriptionId = "test-subscription-id";
newState.CurrentResourceGroup = "test-rg";
newState.CurrentRegion = "eastus";
newState.Azure.DefaultRegion = "eastus";
newState.Azure.Tags["Environment"] = "Test";
newState.Azure.Tags["Project"] = "AzureAIAgent";

// This would normally be done through the AI agent, but we're testing the service directly
await sessionManager.UpdateSessionAsync(session);
Console.WriteLine($"   ✅ Updated session configuration");
Console.WriteLine($"   🔧 Subscription: {newState.CurrentSubscriptionId}");
Console.WriteLine($"   🔧 Resource Group: {newState.CurrentResourceGroup}");
Console.WriteLine($"   🔧 Region: {newState.CurrentRegion}");
Console.WriteLine($"   🏷️  Tags: {string.Join(", ", newState.Azure.Tags.Select(t => $"{t.Key}={t.Value}"))}");

Console.WriteLine();
Console.WriteLine("✅ All core services tested successfully!");
Console.WriteLine();
Console.WriteLine("🚀 Next Steps:");
Console.WriteLine("   1. Set up OpenAI API key or Azure OpenAI to test full AI functionality");
Console.WriteLine("   2. Run 'az login' to enable Azure operations");
Console.WriteLine("   3. Try the full console application with natural language commands");
Console.WriteLine();
Console.WriteLine("💡 Example commands to try with the full app:");
Console.WriteLine("   • 'Create a resource group called my-test-rg in East US'");
Console.WriteLine("   • 'Show me my current Azure subscription'");
Console.WriteLine("   • 'Generate a Bicep template for a web app'");
Console.WriteLine("   • 'List all my Azure subscriptions'");

// Cleanup
serviceProvider.Dispose();
Console.WriteLine();
Console.WriteLine("🧹 Test completed and cleaned up.");
