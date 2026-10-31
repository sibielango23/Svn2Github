$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$solution = Join-Path $root "wpf\SVN2GitHub.sln"
$apiDll = Join-Path $root "backend\SVN2GitHub.Api\bin\Debug\net8.0\SVN2GitHub.Api.dll"
$wpfProject = Join-Path $root "wpf\SVN2GitHub\SVN2GitHub.csproj"
$apiUrl = "http://127.0.0.1:5000"
$apiProcess = $null
$ownsApiProcess = $false

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 8 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/8.0"
}

$runningWpf = Get-Process -Name "SVN2GitHub" -ErrorAction SilentlyContinue
if ($runningWpf) {
    throw "Close the running SVN2GitHub desktop app before rebuilding and launching it."
}

$sdkVersion = dotnet --version
Write-Host "Using .NET SDK $sdkVersion"
Write-Host "Building WPF app and .NET API..."
dotnet build $solution --configuration Debug --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw "The solution build failed."
}

$health = $null
try {
    $health = Invoke-RestMethod -Uri "$apiUrl/api/health" -TimeoutSec 2
} catch {
    $health = $null
}

if ($health -and $health.runtime -ne "dotnet") {
    throw "Port 5000 is responding, but it is not the .NET API. Stop the old API process and run this script again."
}

if (-not $health) {
    $logDirectory = Join-Path $root "temp"
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $stdout = Join-Path $logDirectory "api-server.stdout.log"
    $stderr = Join-Path $logDirectory "api-server.stderr.log"
    $quotedDll = '"' + $apiDll + '"'
    $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList $quotedDll `
        -WorkingDirectory $root -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $ownsApiProcess = $true

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($apiProcess.HasExited) {
            throw "The .NET API stopped during startup. Check $stderr."
        }

        try {
            $health = Invoke-RestMethod -Uri "$apiUrl/api/health" -TimeoutSec 2
            if ($health.status -eq "healthy" -and $health.runtime -eq "dotnet") {
                break
            }
        } catch {
            Start-Sleep -Seconds 1
        }
    }

    if (-not $health -or $health.runtime -ne "dotnet") {
        if ($apiProcess -and -not $apiProcess.HasExited) {
            Stop-Process -Id $apiProcess.Id -Force
        }

        throw "The .NET API did not become healthy. Check $stderr."
    }
}

try {
    Write-Host "API ready at $apiUrl"
    $quotedProject = '"' + $wpfProject + '"'
    $wpfArguments = "run --no-build --project $quotedProject --configuration Debug"
    $wpfProcess = Start-Process -FilePath "dotnet" -ArgumentList $wpfArguments `
        -WorkingDirectory $root -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    if ($wpfProcess.HasExited) {
        throw "The WPF application stopped during startup."
    }

    Write-Host "WPF application launched."
    if ($ownsApiProcess) {
        Write-Host "Keep this window open while using the app. Press Ctrl+C to stop the local API."
        Wait-Process -Id $apiProcess.Id
    } else {
        Write-Host "Using the already-running .NET API."
    }
} finally {
    if ($ownsApiProcess -and $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
    }
}
