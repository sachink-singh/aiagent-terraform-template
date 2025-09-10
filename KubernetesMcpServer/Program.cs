using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KubernetesMcpServer;
using KubernetesMcpServer.Services;
using KubernetesMcpServer.Transport;

// Create host builder for dependency injection and logging
var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        // Only log to stderr to avoid interfering with MCP stdio transport
        logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Warning; // Only log warnings and errors to stderr
        });
        logging.SetMinimumLevel(LogLevel.Warning); // Reduce log verbosity
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<KubernetesService>();
        services.AddSingleton<KubernetesMcpServer.KubernetesMcpServer>();
        services.AddSingleton<StdioTransport>();
    });

var host = hostBuilder.Build();

// Get the transport and start the MCP server
var transport = host.Services.GetRequiredService<StdioTransport>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Enterprise Kubernetes MCP Server starting...");

// Handle graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    logger.LogInformation("Shutdown requested");
};

try
{
    await transport.StartAsync(cts.Token);
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error in MCP server");
    Environment.ExitCode = 1;
}
