using System.Text.Json;

// Test the API response format directly
var httpClient = new HttpClient();

var request = new
{
    message = "I need a Kubernetes cluster",
    sessionId = "test-debug-001"
};

var json = JsonSerializer.Serialize(request);
var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

try 
{
    var response = await httpClient.PostAsync("http://localhost:5050/api/agent/chat", content);
    var responseBody = await response.Content.ReadAsStringAsync();
    
    Console.WriteLine("=== FULL API RESPONSE ===");
    Console.WriteLine(responseBody);
    Console.WriteLine("=========================");
    
    // Try to parse as JSON for better formatting
    try 
    {
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseBody);
        Console.WriteLine("\n=== FORMATTED RESPONSE ===");
        Console.WriteLine(JsonSerializer.Serialize(responseObj, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"JSON parsing failed: {ex.Message}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Request failed: {ex.Message}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
