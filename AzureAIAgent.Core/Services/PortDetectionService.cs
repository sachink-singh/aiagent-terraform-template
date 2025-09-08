using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AzureAIAgent.Core.Services;

/// <summary>
/// Service to detect and manage API port configuration dynamically
/// </summary>
public interface IPortDetectionService
{
    /// <summary>
    /// Gets the current API base URL by detecting the running port
    /// </summary>
    Task<string> GetApiBaseUrlAsync();
    
    /// <summary>
    /// Finds an available port to start the API on
    /// </summary>
    int FindAvailablePort(int startPort = 5000);
    
    /// <summary>
    /// Checks if a specific port is available
    /// </summary>
    bool IsPortAvailable(int port);
    
    /// <summary>
    /// Detects the port that the current application is running on
    /// </summary>
    Task<int?> DetectCurrentPortAsync();
}

public class PortDetectionService : IPortDetectionService
{
    private readonly ILogger<PortDetectionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private string? _cachedApiBaseUrl;

    public PortDetectionService(
        ILogger<PortDetectionService> logger, 
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<string> GetApiBaseUrlAsync()
    {
        if (!string.IsNullOrEmpty(_cachedApiBaseUrl))
        {
            return _cachedApiBaseUrl;
        }

        // First try to detect the current running port
        var currentPort = await DetectCurrentPortAsync();
        if (currentPort.HasValue)
        {
            _cachedApiBaseUrl = $"http://localhost:{currentPort.Value}";
            _logger.LogInformation("Detected API running on port {Port}", currentPort.Value);
            return _cachedApiBaseUrl;
        }

        // Fallback to configuration
        var configuredUrl = _configuration["Deployment:ApiBaseUrl"];
        if (!string.IsNullOrEmpty(configuredUrl))
        {
            _cachedApiBaseUrl = configuredUrl;
            _logger.LogInformation("Using configured API URL: {Url}", configuredUrl);
            return _cachedApiBaseUrl;
        }

        // Last resort: try common ports
        var commonPorts = new[] { 5000, 5001, 7000, 7001, 8080, 3000 };
        foreach (var port in commonPorts)
        {
            var testUrl = $"http://localhost:{port}";
            if (await IsApiRunningAsync(testUrl))
            {
                _cachedApiBaseUrl = testUrl;
                _logger.LogInformation("Found API running on {Url}", testUrl);
                return _cachedApiBaseUrl;
            }
        }

        // Default fallback
        _cachedApiBaseUrl = "http://localhost:5000";
        _logger.LogWarning("Could not detect API port, using default: {Url}", _cachedApiBaseUrl);
        return _cachedApiBaseUrl;
    }

    public async Task<int?> DetectCurrentPortAsync()
    {
        try
        {
            // Try to get from environment variables first
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (!string.IsNullOrEmpty(urls))
            {
                var urlParts = urls.Split(';').FirstOrDefault();
                if (Uri.TryCreate(urlParts, UriKind.Absolute, out var uri))
                {
                    return uri.Port;
                }
            }

            // Try to detect from running processes/listeners
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            var webPorts = listeners
                .Where(l => l.Address.Equals(System.Net.IPAddress.Loopback) || l.Address.Equals(System.Net.IPAddress.Any))
                .Select(l => l.Port)
                .Where(p => p >= 5000 && p <= 8080) // Common web port range
                .OrderBy(p => p)
                .ToList();

            foreach (var port in webPorts)
            {
                var testUrl = $"http://localhost:{port}";
                if (await IsApiRunningAsync(testUrl))
                {
                    return port;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting current port");
            return null;
        }
    }

    public int FindAvailablePort(int startPort = 5000)
    {
        for (int port = startPort; port < startPort + 100; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }
        
        throw new InvalidOperationException($"No available ports found starting from {startPort}");
    }

    public bool IsPortAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private async Task<bool> IsApiRunningAsync(string baseUrl)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var response = await _httpClient.GetAsync($"{baseUrl}/", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
