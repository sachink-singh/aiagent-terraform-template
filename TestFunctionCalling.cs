using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using AzureAIAgent.Plugins;

// Simple test to verify function calling is working
class TestFunctionCalling
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 Testing Function Calling...");
        
        // Get API key from environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("❌ Please set OPENAI_API_KEY environment variable");
            return;
        }

        // Create kernel
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("gpt-4o", apiKey);
        
        // Add the Azure Resource Plugin
        builder.Plugins.AddFromType<AzureResourcePlugin>("AzureResourcePlugin");
        
        var kernel = builder.Build();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Create a simple test scenario
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage("You are a helpful assistant. When the user asks you to deploy terraform, call the ApplyTerraformTemplate function with some sample terraform content.");
        chatHistory.AddUserMessage("Please deploy this terraform template: resource \"azurerm_resource_group\" \"test\" { name = \"test-rg\"; location = \"East US\" }");

        Console.WriteLine("🤖 Sending request to AI...");
        
        try
        {
            var result = await chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new OpenAIPromptExecutionSettings()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0.1
                },
                kernel: kernel);

            Console.WriteLine($"✅ Response: {result.Content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
