# Test Azure AI Agent API with real Azure operations
$baseUrl = "http://localhost:5050"
$sessionId = "test-session-" + (Get-Date -Format "yyyyMMdd-HHmmss")

Write-Host "🚀 Testing Azure AI Agent API" -ForegroundColor Green
Write-Host "Session ID: $sessionId" -ForegroundColor Yellow
Write-Host ""

# Test 1: Check agent status
Write-Host "1️⃣ Checking agent status..." -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/agent/status" -Method GET
    Write-Host "✅ Agent Status: $($response.status)" -ForegroundColor Green
    Write-Host "Available Commands: $($response.availableCommands -join ', ')" -ForegroundColor White
} catch {
    Write-Host "❌ Failed to get status: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 2: List existing resource groups (safe operation)
Write-Host "2️⃣ Listing existing resource groups..." -ForegroundColor Cyan
$listRgRequest = @{
    message = "List all my resource groups"
    sessionId = $sessionId
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/agent/chat" -Method POST -Body $listRgRequest -ContentType "application/json"
    Write-Host "✅ Response: $($response.response)" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to list resource groups: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 3: Create a test resource group (real operation)
Write-Host "3️⃣ Creating a test resource group..." -ForegroundColor Cyan
$createRgRequest = @{
    message = "Create a resource group called 'ai-agent-test-rg' in East US"
    sessionId = $sessionId
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/agent/chat" -Method POST -Body $createRgRequest -ContentType "application/json"
    Write-Host "✅ Response: $($response.response)" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create resource group: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 4: Check session history
Write-Host "4️⃣ Checking session history..." -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/agent/history/$sessionId" -Method GET
    Write-Host "✅ Session has $($response.messages.Count) messages" -ForegroundColor Green
    foreach ($msg in $response.messages) {
        Write-Host "  - $($msg.role): $($msg.content.Substring(0, [Math]::Min(100, $msg.content.Length)))..." -ForegroundColor White
    }
} catch {
    Write-Host "❌ Failed to get history: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "🎉 Test completed! Check the results above." -ForegroundColor Green
Write-Host "💡 You can now use Swagger UI at http://localhost:5050/swagger for more testing." -ForegroundColor Yellow
