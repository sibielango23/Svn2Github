# Contributing to SVN2GitHub Migration Tool

Thank you for your interest in contributing! This document provides guidelines and instructions for contributing to the project.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Documentation](#documentation)
- [Pull Request Process](#pull-request-process)
- [Community](#community)

## 📜 Code of Conduct

- Be respectful and inclusive
- Welcome newcomers
- Focus on constructive feedback
- Keep discussions professional and on-topic

## 🚀 Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally
   ```powershell
   git clone https://github.com/YOUR-USERNAME/Svn2Github.git
   cd Svn2Github
   ```
3. **Add upstream remote**
   ```powershell
   git remote add upstream https://github.com/ORIGINAL-OWNER/Svn2Github.git
   ```

## 🛠️ Development Setup

### Prerequisites

- Python 3.8+
- .NET 8.0 SDK
- Git for Windows (includes git-svn)
- Visual Studio Code or Visual Studio 2022

### Initial Setup

1. **Install Python dependencies:**
   ```powershell
   pip install -r api/requirements.txt
   pip install -r api/requirements-dev.txt  # Development tools
   ```

2. **Restore .NET packages:**
   ```powershell
   cd wpf/SVN2GitHub
   dotnet restore
   ```

3. **Run the application:**
   ```powershell
   # Terminal 1 - API
   python api/main.py
   
   # Terminal 2 - WPF
   cd wpf/SVN2GitHub
   dotnet run
   ```

## 🤝 How to Contribute

### Reporting Bugs

Use the [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.md):
1. Search existing issues first
2. Provide detailed reproduction steps
3. Include logs and screenshots
4. Specify your environment

### Suggesting Features

Use the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.md):
1. Describe the use case
2. Explain the expected behavior
3. Consider alternatives
4. Assess the impact

### Contributing Code

1. **Find an issue** to work on or create a new one
2. **Comment** on the issue to claim it
3. **Create a branch**
   ```powershell
   git checkout -b feature/your-feature-name
   ```
4. **Make your changes** following coding standards
5. **Write tests** for new functionality
6. **Update documentation** as needed
7. **Commit** with a conventional commit message
8. **Push** to your fork
9. **Create a Pull Request** using the template

## 📏 Coding Standards

### General Principles

- **DRY** (Don't Repeat Yourself)
- **SOLID** principles
- **KISS** (Keep It Simple, Stupid)
- Write self-documenting code
- Add comments for complex logic only

### C# / WPF Style

Follow the guidelines in [.github/copilot-instructions.md](.github/copilot-instructions.md):

**Naming:**
```csharp
// Classes, Methods, Properties: PascalCase
public class MigrationService { }
public async Task StartMigrationAsync() { }
public string MigrationId { get; set; }

// Private fields: _camelCase
private readonly ApiService _apiService;

// Local variables, parameters: camelCase
var migrationId = "abc-123";
```

**MVVM Pattern:**
```csharp
// Use CommunityToolkit.Mvvm
public class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Implementation
    }
}
```

**Async/Await:**
```csharp
// Always suffix with Async
public async Task<Migration> GetMigrationAsync(string id)
{
    // Use ConfigureAwait(false) in libraries
    var response = await _httpClient.GetAsync($"/api/migrations/{id}")
        .ConfigureAwait(false);
    return await response.Content.ReadFromJsonAsync<Migration>();
}
```

### Python / FastAPI Style

Follow PEP 8 and the guidelines in [.github/copilot-instructions.md](.github/copilot-instructions.md):

**Naming:**
```python
# Functions, variables, modules: snake_case
def extract_authors(svn_url: str) -> List[str]:
    author_list = []
    return author_list

# Classes: PascalCase
class MigrationService:
    pass

# Constants: UPPER_CASE
MAX_RETRIES = 3
```

**Type Hints:**
```python
from typing import List, Dict, Optional

def process_migration(
    migration_id: str,
    options: Optional[Dict[str, Any]] = None
) -> Migration:
    """Process a migration with optional configuration."""
    pass
```

**Docstrings:**
```python
def extract_authors(svn_url: str, limit: int = 5000) -> List[Dict[str, str]]:
    """
    Extract unique authors from SVN repository.
    
    Args:
        svn_url: The SVN repository URL
        limit: Maximum revisions to scan (default: 5000)
        
    Returns:
        List of author dictionaries with svn_username, git_name, git_email
        
    Raises:
        subprocess.CalledProcessError: If svn command fails
    """
    pass
```

### Git Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**
```
feat(author-mapping): add bulk edit functionality

Adds ability to edit multiple author mappings at once through
a new bulk edit dialog in the WPF UI.

Closes #123
```

```
fix(api): handle null github token gracefully

Previously would throw NullReferenceException when token was
missing. Now returns proper 400 error with helpful message.

Fixes #456
```

## 🧪 Testing Guidelines

### Unit Tests (Required)

**C# Tests (xUnit):**
```csharp
public class ApiServiceTests
{
    [Fact]
    public async Task GetMigrations_WhenSuccessful_ReturnsListOfMigrations()
    {
        // Arrange
        var mockHandler = CreateMockHandler();
        var service = new ApiService(mockHandler);
        
        // Act
        var result = await service.GetMigrationsAsync();
        
        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, m => Assert.NotNull(m.Id));
    }
}
```

**Python Tests (pytest):**
```python
import pytest
from httpx import AsyncClient

@pytest.mark.asyncio
async def test_create_migration_success(client: AsyncClient):
    # Arrange
    payload = {
        "svn_url": "https://svn.example.com/repo",
        "github_org": "test-org",
        "github_repo": "test-repo"
    }
    
    # Act
    response = await client.post("/api/migrations", json=payload)
    
    # Assert
    assert response.status_code == 201
    data = response.json()
    assert data["svn_url"] == payload["svn_url"]
```

### Integration Tests (Recommended)

Test the full stack when possible:
```python
@pytest.mark.integration
async def test_end_to_end_migration(client: AsyncClient, db: Session):
    # Create migration
    response = await client.post("/api/migrations", json=migration_data)
    migration_id = response.json()["id"]
    
    # Extract authors
    response = await client.post(f"/api/authors/{migration_id}/extract")
    assert response.status_code == 200
    
    # Verify authors were created
    authors = db.query(Author).filter_by(migration_id=migration_id).all()
    assert len(authors) > 0
```

### Manual Testing Checklist

Before submitting a PR:
- [ ] WPF app starts without errors
- [ ] API server starts and responds to health check
- [ ] Create new migration flow works
- [ ] Author mapping UI opens and loads data
- [ ] Dashboard displays correct statistics
- [ ] Error messages are clear and helpful
- [ ] UI is responsive and doesn't freeze

## 📚 Documentation

### Code Documentation

**Always document:**
- Public APIs (XML comments for C#, docstrings for Python)
- Complex algorithms or business logic
- Non-obvious design decisions
- Workarounds for known issues

**Example (C#):**
```csharp
/// <summary>
/// Extracts authors from SVN repository history.
/// </summary>
/// <param name="migrationId">The migration identifier.</param>
/// <returns>A list of author mappings.</returns>
/// <exception cref="HttpRequestException">Thrown when API call fails.</exception>
/// <remarks>
/// This method may take several minutes for large repositories.
/// It scans the last 5000 revisions by default for performance.
/// </remarks>
public async Task<List<Author>> ExtractAuthorsAsync(string migrationId)
{
    // Implementation
}
```

### User Documentation

Update relevant documentation files when adding features:
- `README.md` - Project overview and quick start
- `MIGRATION_GUIDE.md` - User-facing migration steps
- `SETUP.md` - Installation and configuration
- Feature-specific guides in `docs/`

## 🔄 Pull Request Process

### Before Submitting

1. **Sync with upstream:**
   ```powershell
   git fetch upstream
   git rebase upstream/main
   ```

2. **Run tests:**
   ```powershell
   # Python tests
   pytest
   
   # C# tests
   dotnet test
   ```

3. **Run linters:**
   ```powershell
   # Python
   black agents/ api/
   flake8 agents/ api/
   
   # C# (if you have tools)
   dotnet format
   ```

4. **Update documentation**

5. **Self-review your code**

### PR Checklist

Use the [Pull Request template](.github/PULL_REQUEST_TEMPLATE.md) and ensure:

- [ ] Code follows project style guidelines
- [ ] Tests added/updated and passing
- [ ] Documentation updated
- [ ] Commit messages follow conventions
- [ ] No merge conflicts
- [ ] PR description is clear and complete

### Review Process

1. **Automated checks** run (CI/CD when configured)
2. **Maintainers review** your code
3. **Address feedback** by pushing new commits
4. **Maintainer merges** when approved

### After Merge

1. **Delete your branch:**
   ```powershell
   git branch -d feature/your-feature
   git push origin --delete feature/your-feature
   ```

2. **Sync your fork:**
   ```powershell
   git checkout main
   git pull upstream main
   git push origin main
   ```

## 🎯 Areas for Contribution

### High Priority

- [ ] Additional migration source support (GitLab, Bitbucket)
- [ ] Real-time progress updates via WebSocket
- [ ] Docker containerization
- [ ] CI/CD pipeline configuration
- [ ] More comprehensive test coverage

### Medium Priority

- [ ] Migration analytics and reporting
- [ ] Batch migration support
- [ ] Custom agent templates
- [ ] Migration scheduling
- [ ] Email notifications

### Documentation

- [ ] Video tutorials
- [ ] Troubleshooting guides
- [ ] Architecture decision records
- [ ] API usage examples
- [ ] Blog posts / articles

### Good First Issues

Look for issues labeled `good first issue` - these are perfect for newcomers:
- Documentation improvements
- UI refinements
- Bug fixes with clear reproduction steps
- Test coverage improvements

## 💬 Community

### Communication

- **GitHub Issues:** Bug reports and feature requests
- **Pull Requests:** Code contributions and discussions
- **Discussions:** Questions and general conversations

### Getting Help

1. Check existing documentation
2. Search closed issues
3. Ask in GitHub Discussions
4. Create a new issue with details

### Recognition

Contributors are recognized in:
- `CONTRIBUTORS.md` file (coming soon)
- Release notes
- GitHub contributor graph

## 🙏 Thank You!

Every contribution, no matter how small, is valuable and appreciated. Thank you for helping make this project better!

---

**Questions?** Feel free to ask in GitHub Discussions or open an issue.
