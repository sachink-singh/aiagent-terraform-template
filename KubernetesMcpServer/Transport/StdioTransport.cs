using System.Text.Json;
using Microsoft.Extensions.Logging;
using KubernetesMcpServer.Models;

namespace KubernetesMcpServer.Transport;

/// <summary>
/// Standard Input/Output transport for MCP Server
/// Handles JSON-RPC communication over stdin/stdout
/// </summary>
public class StdioTransport
{
    private readonly KubernetesMcpServer _server;
    private readonly ILogger<StdioTransport> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public StdioTransport(KubernetesMcpServer server, ILogger<StdioTransport> logger)
    {
        _server = server;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false // Compact JSON for transport
        };
    }

    /// <summary>
    /// Start the MCP server and handle incoming requests
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Kubernetes MCP Server on stdio transport");

        try
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) break; // EOF

                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    // Parse incoming JSON-RPC message
                    var request = JsonSerializer.Deserialize<McpMessage>(line, _jsonOptions);
                    if (request == null) continue;

                    _logger.LogDebug("Received request: {Method}", request.Method);

                    // Route the request to appropriate handler
                    var response = await RouteRequestAsync(request);

                    // Send response
                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                    await writer.WriteLineAsync(responseJson);
                    _logger.LogDebug("Sent response for request ID: {Id}", response.Id);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON request: {Line}", line);
                    // Send parse error response
                    var errorResponse = new McpMessage
                    {
                        Id = null,
                        Error = new McpError { Code = -32700, Message = "Parse error" }
                    };
                    var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                    await writer.WriteLineAsync(errorJson);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing request");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MCP server transport");
        }

        _logger.LogInformation("Kubernetes MCP Server stopped");
    }

    private async Task<McpMessage> RouteRequestAsync(McpMessage request)
    {
        try
        {
            return request.Method switch
            {
                "initialize" => await _server.HandleInitializeAsync(request),
                "tools/list" => await _server.HandleToolsListAsync(request),
                "tools/call" => await _server.HandleToolsCallAsync(request),
                "ping" => HandlePing(request),
                _ => CreateMethodNotFoundResponse(request)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request method: {Method}", request.Method);
            return new McpMessage
            {
                Id = request.Id,
                Error = new McpError { Code = -32603, Message = $"Internal error: {ex.Message}" }
            };
        }
    }

    private static McpMessage HandlePing(McpMessage request)
    {
        return new McpMessage
        {
            Id = request.Id,
            Result = JsonSerializer.SerializeToElement(new { pong = true, timestamp = DateTimeOffset.UtcNow })
        };
    }

    private static McpMessage CreateMethodNotFoundResponse(McpMessage request)
    {
        return new McpMessage
        {
            Id = request.Id,
            Error = new McpError { Code = -32601, Message = $"Method not found: {request.Method}" }
        };
    }
}
