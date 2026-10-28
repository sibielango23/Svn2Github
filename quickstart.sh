#!/bin/bash
# Quick start script for SVN to GitHub migration
# Usage: ./quickstart.sh <svn-url> <github-org> <github-repo>

set -e

# Check arguments
if [ $# -lt 3 ]; then
    echo "Usage: $0 <svn-url> <github-org> <github-repo>"
    echo "Example: $0 https://svn.example.com/repo myorg myrepo"
    exit 1
fi

SVN_URL="$1"
GITHUB_ORG="$2"
GITHUB_REPO="$3"

echo "=== SVN to GitHub Migration ==="
echo "SVN URL: $SVN_URL"
echo "GitHub: $GITHUB_ORG/$GITHUB_REPO"
echo

# Check prerequisites
echo "Checking prerequisites..."

command -v git >/dev/null 2>&1 || { echo "Error: git not found"; exit 1; }
command -v git-svn >/dev/null 2>&1 || { echo "Error: git-svn not found"; exit 1; }
command -v svn >/dev/null 2>&1 || { echo "Error: svn not found"; exit 1; }
command -v gh >/dev/null 2>&1 || { echo "Error: GitHub CLI (gh) not found"; exit 1; }
command -v python3 >/dev/null 2>&1 || { echo "Error: python3 not found"; exit 1; }

echo "✓ All prerequisites met"
echo

# Test SVN access
echo "Testing SVN access..."
if svn info "$SVN_URL" >/dev/null 2>&1; then
    echo "✓ SVN repository accessible"
else
    echo "✗ Cannot access SVN repository"
    exit 1
fi
echo

# Test GitHub authentication
echo "Testing GitHub authentication..."
if gh auth status >/dev/null 2>&1; then
    echo "✓ GitHub CLI authenticated"
else
    echo "✗ GitHub CLI not authenticated. Run: gh auth login"
    exit 1
fi
echo

# Generate config
echo "Generating configuration..."
cat > migration-config.json <<EOF
{
  "svn": {
    "url": "$SVN_URL",
    "structure": "standard"
  },
  "github": {
    "org": "$GITHUB_ORG",
    "repo": "$GITHUB_REPO",
    "private": true
  },
  "options": {
    "author_domain": "example.com",
    "clean_history": false
  },
  "workspace": "./migration-workspace"
}
EOF

echo "✓ Configuration created: migration-config.json"
echo

# Run migration
echo "Starting migration..."
echo "This may take several hours for a large repository."
echo "Progress will be logged to migration.log"
echo

python3 orchestrator.py --config migration-config.json 2>&1 | tee migration.log

echo
echo "=== Migration Complete ==="
echo "Repository: https://github.com/$GITHUB_ORG/$GITHUB_REPO"
echo "Report: migration-workspace/migration-report.json"
