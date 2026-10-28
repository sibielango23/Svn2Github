"""
Configuration and testing endpoints for the API.
"""

from fastapi import APIRouter, HTTPException
import subprocess
import json

from api.schemas import (
    SVNConnectionTest, GitHubConnectionTest, ConnectionTestResponse,
    MigrationConfigCreate
)

router = APIRouter()


@router.get("/template")
async def get_config_template():
    """Get a configuration template."""
    return {
        "svn": {
            "url": "https://svn.example.com/repo",
            "structure": "standard",
            "trunk": "trunk",
            "branches": "branches",
            "tags": "tags"
        },
        "github": {
            "org": "your-org",
            "repo": "your-repo",
            "private": True
        },
        "options": {
            "author_domain": "example.com",
            "clean_history": False,
            "exclude_paths": ["*.dll", "*.exe", "*.so", "build/*"],
            "max_file_size_mb": 100
        }
    }


@router.post("/validate")
async def validate_config(config: MigrationConfigCreate):
    """Validate a migration configuration."""
    errors = []

    # Validate SVN URL
    if not config.svn_url.startswith(("http://", "https://", "svn://")):
        errors.append("SVN URL must start with http://, https://, or svn://")

    # Validate GitHub org/repo
    if "/" in config.github_org:
        errors.append("GitHub organization cannot contain /")

    if "/" in config.github_repo or " " in config.github_repo:
        errors.append("GitHub repository name cannot contain / or spaces")

    # Validate structure
    if config.svn_structure not in ["standard", "non-standard"]:
        errors.append("SVN structure must be 'standard' or 'non-standard'")

    # Validate file size
    if config.max_file_size_mb < 1 or config.max_file_size_mb > 10000:
        errors.append("Max file size must be between 1 and 10000 MB")

    if errors:
        return {
            "valid": False,
            "errors": errors
        }

    return {
        "valid": True,
        "message": "Configuration is valid"
    }


@router.post("/test-svn", response_model=ConnectionTestResponse)
async def test_svn_connection(test: SVNConnectionTest):
    """Test SVN repository connection."""
    try:
        # Try to get SVN info
        result = subprocess.run(
            ["svn", "info", test.svn_url],
            capture_output=True,
            text=True,
            timeout=30,
            check=True
        )

        # Parse output for details
        details = {}
        for line in result.stdout.split('\n'):
            if ':' in line:
                key, value = line.split(':', 1)
                details[key.strip()] = value.strip()

        return ConnectionTestResponse(
            success=True,
            message="Successfully connected to SVN repository",
            details=details
        )

    except subprocess.TimeoutExpired:
        return ConnectionTestResponse(
            success=False,
            message="Connection timeout - SVN repository not responding"
        )

    except subprocess.CalledProcessError as e:
        return ConnectionTestResponse(
            success=False,
            message=f"Failed to connect to SVN repository: {e.stderr}"
        )

    except FileNotFoundError:
        return ConnectionTestResponse(
            success=False,
            message="SVN client not found - please install Subversion"
        )

    except Exception as e:
        return ConnectionTestResponse(
            success=False,
            message=f"Unexpected error: {str(e)}"
        )


@router.post("/test-github", response_model=ConnectionTestResponse)
async def test_github_connection(test: GitHubConnectionTest):
    """Test GitHub authentication and organization access."""
    try:
        # Check if gh CLI is authenticated
        result = subprocess.run(
            ["gh", "auth", "status"],
            capture_output=True,
            text=True,
            timeout=10,
            check=False
        )

        if result.returncode != 0:
            return ConnectionTestResponse(
                success=False,
                message="GitHub CLI not authenticated - run 'gh auth login'"
            )

        # Try to check if org exists (or if we have access)
        result = subprocess.run(
            ["gh", "api", f"orgs/{test.github_org}"],
            capture_output=True,
            text=True,
            timeout=10,
            check=False
        )

        if result.returncode == 0:
            org_data = json.loads(result.stdout)
            return ConnectionTestResponse(
                success=True,
                message=f"Successfully connected to GitHub organization: {test.github_org}",
                details={
                    "org_name": org_data.get("name"),
                    "org_login": org_data.get("login"),
                    "org_type": org_data.get("type")
                }
            )
        else:
            return ConnectionTestResponse(
                success=False,
                message=f"Cannot access organization '{test.github_org}' - check permissions"
            )

    except FileNotFoundError:
        return ConnectionTestResponse(
            success=False,
            message="GitHub CLI not found - please install from https://cli.github.com"
        )

    except json.JSONDecodeError:
        return ConnectionTestResponse(
            success=False,
            message="Invalid response from GitHub API"
        )

    except Exception as e:
        return ConnectionTestResponse(
            success=False,
            message=f"Unexpected error: {str(e)}"
        )


@router.get("/prerequisites")
async def check_prerequisites():
    """Check if all required tools are installed."""
    tools = {
        "git": False,
        "git-svn": False,
        "svn": False,
        "gh": False,
        "python": False
    }

    messages = {}

    # Check Git
    try:
        result = subprocess.run(["git", "--version"], capture_output=True, timeout=5)
        tools["git"] = result.returncode == 0
        messages["git"] = result.stdout.decode() if tools["git"] else "Not found"
    except:
        messages["git"] = "Not found"

    # Check git-svn
    try:
        result = subprocess.run(["git", "svn", "--version"], capture_output=True, timeout=5)
        tools["git-svn"] = result.returncode == 0
        messages["git-svn"] = "Available" if tools["git-svn"] else "Not found"
    except:
        messages["git-svn"] = "Not found"

    # Check SVN
    try:
        result = subprocess.run(["svn", "--version"], capture_output=True, timeout=5)
        tools["svn"] = result.returncode == 0
        messages["svn"] = result.stdout.decode().split('\n')[0] if tools["svn"] else "Not found"
    except:
        messages["svn"] = "Not found"

    # Check GitHub CLI
    try:
        result = subprocess.run(["gh", "--version"], capture_output=True, timeout=5)
        tools["gh"] = result.returncode == 0
        messages["gh"] = result.stdout.decode().split('\n')[0] if tools["gh"] else "Not found"
    except:
        messages["gh"] = "Not found"

    # Check Python
    try:
        result = subprocess.run(["python", "--version"], capture_output=True, timeout=5)
        tools["python"] = result.returncode == 0
        messages["python"] = result.stdout.decode() if tools["python"] else "Not found"
    except:
        messages["python"] = "Not found"

    all_ready = all(tools.values())

    return {
        "ready": all_ready,
        "tools": tools,
        "messages": messages
    }
