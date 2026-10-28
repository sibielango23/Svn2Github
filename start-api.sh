#!/bin/bash
# Start FastAPI Server
# Usage: ./start-api.sh

echo "Starting SVN to GitHub Migration API Server..."

# Check if Python is installed
if ! command -v python3 &> /dev/null; then
    echo "✗ Python 3 not found. Please install Python 3.8+"
    exit 1
fi

echo "✓ Python found: $(python3 --version)"

# Check if in correct directory
if [ ! -f "api/server.py" ]; then
    echo "✗ Please run this script from the Svn2Github directory"
    exit 1
fi

# Install dependencies if needed
if [ ! -d "api/__pycache__" ]; then
    echo "Installing API dependencies..."
    pip3 install -r api/requirements.txt
fi

# Create data directory
mkdir -p data

# Initialize database
echo "Initializing database..."
python3 -c "from api.database import init_db; init_db()"

# Start the server
echo ""
echo "================================"
echo "API Server Starting"
echo "================================"
echo "URL: http://localhost:8000"
echo "Docs: http://localhost:8000/docs"
echo "Press Ctrl+C to stop"
echo ""

cd api
python3 server.py
