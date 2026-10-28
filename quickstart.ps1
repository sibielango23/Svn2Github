# Quick start script for SVN to GitHub migration (Windows)
# Usage: .\quickstart.ps1 -SvnUrl <url> -GitHubOrg <org> -GitHubRepo <repo>

param(
    [Parameter(Mandatory=$true)]
    [string]$SvnUrl,

    [Parameter(Mandatory=$true)]
    [string]$GitHubOrg,

    [Parameter(Mandatory=$true)]
    [string]$GitHubRepo
)

$ErrorActionPreference = "Stop"

Write-Host "=== SVN to GitHub Migration ===" -ForegroundColor Cyan
Write-Host "SVN URL: $SvnUrl"
Write-Host "GitHub: $GitHubOrg/$GitHubRepo"
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

$commands = @{
    "git" = "Git"
    "svn" = "SVN client"
    "gh" = "GitHub CLI"
    "python" = "Python 3"
}

foreach ($cmd in $commands.Keys) {
    try {
        $null = Get-Command $cmd -ErrorAction Stop
        Write-Host "✓ $($commands[$cmd]) found" -ForegroundColor Green
    } catch {
        Write-Host "✗ $($commands[$cmd]) not found" -ForegroundColor Red
        exit 1
    }
}

# Check git-svn
try {
    $null = git svn --version 2>&1
    Write-Host "✓ git-svn available" -ForegroundColor Green
} catch {
    Write-Host "✗ git-svn not available" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test SVN access
Write-Host "Testing SVN access..." -ForegroundColor Yellow
try {
    $null = svn info $SvnUrl 2>&1
    Write-Host "✓ SVN repository accessible" -ForegroundColor Green
} catch {
    Write-Host "✗ Cannot access SVN repository" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test GitHub authentication
Write-Host "Testing GitHub authentication..." -ForegroundColor Yellow
try {
    $null = gh auth status 2>&1
    Write-Host "✓ GitHub CLI authenticated" -ForegroundColor Green
} catch {
    Write-Host "✗ GitHub CLI not authenticated. Run: gh auth login" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Generate config
Write-Host "Generating configuration..." -ForegroundColor Yellow

$config = @{
    svn = @{
        url = $SvnUrl
        structure = "standard"
    }
    github = @{
        org = $GitHubOrg
        repo = $GitHubRepo
        private = $true
    }
    options = @{
        author_domain = "example.com"
        clean_history = $false
    }
    workspace = "./migration-workspace"
} | ConvertTo-Json -Depth 10

$config | Out-File -FilePath "migration-config.json" -Encoding UTF8

Write-Host "✓ Configuration created: migration-config.json" -ForegroundColor Green
Write-Host ""

# Run migration
Write-Host "Starting migration..." -ForegroundColor Cyan
Write-Host "This may take several hours for a large repository." -ForegroundColor Yellow
Write-Host "Progress will be logged to migration.log" -ForegroundColor Yellow
Write-Host ""

python orchestrator.py --config migration-config.json 2>&1 | Tee-Object -FilePath migration.log

Write-Host ""
Write-Host "=== Migration Complete ===" -ForegroundColor Green
Write-Host "Repository: https://github.com/$GitHubOrg/$GitHubRepo"
Write-Host "Report: migration-workspace/migration-report.json"
