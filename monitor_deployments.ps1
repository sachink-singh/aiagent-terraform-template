# Deployment Monitoring Script
Write-Host "🔍 Monitoring Azure AI Agent Deployments..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Yellow
Write-Host ""

$tempPath = $env:TEMP
$apiLogPath = "C:\Temp\PoC\infraAgent.NET\AzureAIAgent.Api"

Write-Host "📂 Monitoring locations:" -ForegroundColor Green
Write-Host "   - Temp directory: $tempPath" -ForegroundColor Gray
Write-Host "   - API directory: $apiLogPath" -ForegroundColor Gray
Write-Host ""

# Initial state
$initialTerraformDirs = Get-ChildItem -Path $tempPath -Directory -Name -ErrorAction SilentlyContinue | Where-Object { $_ -like "terraform-*" }
$lastCount = $initialTerraformDirs.Count

Write-Host "📊 Initial terraform directories: $lastCount" -ForegroundColor Blue
Write-Host ""

while ($true) {
    Start-Sleep -Seconds 2
    
    # Check for new terraform directories
    $currentTerraformDirs = Get-ChildItem -Path $tempPath -Directory -Name -ErrorAction SilentlyContinue | Where-Object { $_ -like "terraform-*" }
    $currentCount = $currentTerraformDirs.Count
    
    if ($currentCount -gt $lastCount) {
        $newDirs = $currentTerraformDirs | Where-Object { $_ -notin $initialTerraformDirs }
        Write-Host "🚀 NEW DEPLOYMENT DETECTED!" -ForegroundColor Green
        foreach ($dir in $newDirs) {
            Write-Host "   📁 $dir" -ForegroundColor Yellow
            
            # Check if main.tf exists
            $mainTfPath = Join-Path $tempPath $dir "main.tf"
            if (Test-Path $mainTfPath) {
                Write-Host "   ✅ main.tf found" -ForegroundColor Green
                $tfContent = Get-Content $mainTfPath -Raw
                $lines = ($tfContent -split "`n").Count
                Write-Host "   📝 Terraform file: $lines lines" -ForegroundColor Cyan
            } else {
                Write-Host "   ❌ main.tf missing" -ForegroundColor Red
            }
        }
        Write-Host ""
        $lastCount = $currentCount
        $initialTerraformDirs = $currentTerraformDirs
    }
    
    # Check for terraform processes
    $terraformProcesses = Get-Process -Name "terraform" -ErrorAction SilentlyContinue
    if ($terraformProcesses) {
        Write-Host "⚡ TERRAFORM PROCESS RUNNING!" -ForegroundColor Magenta
        foreach ($proc in $terraformProcesses) {
            Write-Host "   🔧 PID: $($proc.Id), CPU: $($proc.CPU)" -ForegroundColor Yellow
        }
        Write-Host ""
    }
}
