using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AzureAIAgent.Core.Services;

// Simple test to verify port detection functionality
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add configuration
var configBuilder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();
var configuration = configBuilder.Build();
services.AddSingleton<IConfiguration>(configuration);

// Add HttpClient
services.AddHttpClient<IPortDetectionService, PortDetectionService>();
services.AddSingleton<IPortDetectionService, PortDetectionService>();

var serviceProvider = services.BuildServiceProvider();

try
{
    var portDetectionService = serviceProvider.GetRequiredService<IPortDetectionService>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("🔍 Testing port detection functionality...");
    
    // Test finding available port
    var availablePort = portDetectionService.FindAvailablePort();
    logger.LogInformation("✅ Found available port: {Port}", availablePort);
    
    // Test port availability check
    var isPortAvailable = portDetectionService.IsPortAvailable(5000);
    logger.LogInformation("🔧 Port 5000 available: {Available}", isPortAvailable);
    
    // Test current port detection
    var currentPort = await portDetectionService.DetectCurrentPortAsync();
    logger.LogInformation("🎯 Current running port: {Port}", currentPort?.ToString() ?? "Not detected");
    
    // Test API base URL detection
    var apiBaseUrl = await portDetectionService.GetApiBaseUrlAsync();
    logger.LogInformation("🌐 Detected API base URL: {Url}", apiBaseUrl);
    
    logger.LogInformation("✨ Port detection test completed successfully!");
}
catch (Exception ex)
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "❌ Port detection test failed");
}
