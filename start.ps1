# SVN2GitHub Complete Migration Tool Launcher
# This script starts both the API server and WPF application

Write-Host "=================================" -ForegroundColor Cyan
Write-Host "SVN2GitHub Migration Tool" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "[1/5] Checking prerequisites..." -ForegroundColor Yellow

# Check Python
$pythonCmd = $null
$pythonPaths = @(
    "python",
    "python3",
    "C:\Users\sibi2\AppData\Local\Python\bin\python3.exe",
    "C:\Python312\python.exe",
    "C:\Python311\python.exe",
    "C:\Python310\python.exe"
)

foreach ($path in $pythonPaths) {
    try {
        $version = & $path --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $pythonCmd = $path
            Write-Host "  ✓ Python found: $version" -ForegroundColor Green
            break
        }
    } catch {
        continue
    }
}

if (-not $pythonCmd) {
    Write-Host "  ✗ Python not found! Please install Python 3.8+" -ForegroundColor Red
    exit 1
}

# Check .NET
try {
    $dotnetVersion = dotnet --version 2>&1
    Write-Host "  ✓ .NET found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  ✗ .NET not found! Please install .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}

# Check git-svn
try {
    $gitSvnVersion = git svn --version 2>&1 | Select-Object -First 1
    Write-Host "  ✓ git-svn found: $gitSvnVersion" -ForegroundColor Green
} catch {
    Write-Host "  ⚠ git-svn not found. It will be needed for actual migrations." -ForegroundColor Yellow
}

Write-Host ""

# Install Python dependencies
Write-Host "[2/5] Installing Python dependencies..." -ForegroundColor Yellow
if (Test-Path "api\requirements.txt") {
    & $pythonCmd -m pip install -r api\requirements.txt -q
    Write-Host "  ✓ Python dependencies installed" -ForegroundColor Green
} else {
    Write-Host "  ✗ api\requirements.txt not found!" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Build WPF application
Write-Host "[3/5] Building WPF application..." -ForegroundColor Yellow
Push-Location wpf\SVN2GitHub
try {
    $buildOutput = dotnet build --verbosity quiet 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ WPF application built successfully" -ForegroundColor Green
    } else {
        Write-Host "  ✗ WPF build failed!" -ForegroundColor Red
        Write-Host $buildOutput
        Pop-Location
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""

# Start API server in background
Write-Host "[4/5] Starting API server..." -ForegroundColor Yellow
$apiJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    & $using:pythonCmd api/main.py
}

# Wait for API to be ready
$maxAttempts = 10
$attempt = 0
$apiReady = $false

while ($attempt -lt $maxAttempts -and -not $apiReady) {
    Start-Sleep -Seconds 2
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8000/health" -UseBasicParsing -TimeoutSec 2 2>$null
        if ($response.StatusCode -eq 200) {
            $apiReady = $true
            Write-Host "  ✓ API server started at http://localhost:8000" -ForegroundColor Green
        }
    } catch {
        $attempt++
    }
}

if (-not $apiReady) {
    Write-Host "  ✗ API server failed to start!" -ForegroundColor Red
    Stop-Job $apiJob
    Remove-Job $apiJob
    exit 1
}

Write-Host ""

# Launch WPF application
Write-Host "[5/5] Launching WPF application..." -ForegroundColor Yellow
Push-Location wpf\SVN2GitHub
Start-Process "dotnet" -ArgumentList "run" -WindowStyle Hidden
Pop-Location
Start-Sleep -Seconds 3
Write-Host "  ✓ WPF application launched" -ForegroundColor Green

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "✓ All systems running!" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "API Server:  http://localhost:8000" -ForegroundColor White
Write-Host "API Docs:    http://localhost:8000/docs" -ForegroundColor White
Write-Host "WPF App:     Running on desktop" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop the API server" -ForegroundColor Yellow
Write-Host ""

# Keep API server running
try {
    Wait-Job $apiJob
} finally {
    Stop-Job $apiJob
    Remove-Job $apiJob
    Write-Host ""
    Write-Host "API server stopped." -ForegroundColor Yellow
}
