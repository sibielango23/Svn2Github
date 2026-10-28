# Start FastAPI Server
# Usage: .\start-api.ps1

Write-Host "Starting SVN to GitHub Migration API Server..." -ForegroundColor Cyan

# Check if Python is installed
try {
    $pythonVersion = python --version 2>&1
    Write-Host "✓ Python found: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ Python not found. Please install Python 3.8+" -ForegroundColor Red
    exit 1
}

# Check if in correct directory
if (-not (Test-Path "api\server.py")) {
    Write-Host "✗ Please run this script from the Svn2Github directory" -ForegroundColor Red
    exit 1
}

# Install dependencies if needed
if (-not (Test-Path "api\__pycache__")) {
    Write-Host "Installing API dependencies..." -ForegroundColor Yellow
    pip install -r api\requirements.txt
}

# Create data directory
New-Item -ItemType Directory -Force -Path "data" | Out-Null

# Initialize database
Write-Host "Initializing database..." -ForegroundColor Yellow
python -c "from api.database import init_db; init_db()"

# Start the server
Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "API Server Starting" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host "URL: http://localhost:8000" -ForegroundColor Green
Write-Host "Docs: http://localhost:8000/docs" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

cd api
python server.py
