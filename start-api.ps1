$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "backend\SVN2GitHub.Api\SVN2GitHub.Api.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 8 SDK is required."
}

Write-Host "Starting the .NET SVN2GitHub API at http://127.0.0.1:5000"
Write-Host "Press Ctrl+C to stop."
Push-Location $PSScriptRoot
try {
    dotnet run --project $project --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw "The .NET API exited with code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}
