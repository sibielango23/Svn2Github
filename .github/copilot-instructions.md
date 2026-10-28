# GitHub Copilot Instructions for SVN2GitHub Migration Tool

## Project Overview

This is a multi-agent SVN to GitHub migration system with:
- **WPF Desktop Application** (.NET 8.0, Material Design)
- **FastAPI Backend** (Python 3.8+, SQLite)
- **6 Specialized Agents** (Repository analysis, author mapping, migration, cleaning, GitHub setup, verification)

## Code Style & Conventions

### C# / WPF (.NET 8.0)

**Naming:**
- `PascalCase` for classes, methods, properties, events
- `camelCase` for private fields, local variables, parameters
- `_camelCase` for private fields (with underscore prefix)
- `UPPER_CASE` for constants

**MVVM Pattern:**
```csharp
// ViewModels inherit from ObservableObject
public class DashboardViewModel : ObservableObject
{
    private int _totalMigrations;
    public int TotalMigrations
    {
        get => _totalMigrations;
        set => SetProperty(ref _totalMigrations, value);
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        // Use CommunityToolkit.Mvvm attributes
    }
}
```

**Async/Await:**
- Always use `async`/`await` for I/O operations
- Suffix async methods with `Async`
- Use `Task<T>` or `Task` return types
- Handle cancellation with `CancellationToken`

**XAML:**
- Material Design theme throughout
- Use `materialDesign:` prefix for MD controls
- Bind to ViewModels with `{Binding Property}`
- Use `{StaticResource}` for styles and colors
- Always include `x:Name` for controls accessed in code-behind

**Example:**
```xml
<Button Style="{StaticResource MaterialDesignRaisedButton}"
        Command="{Binding LoadCommand}"
        materialDesign:ButtonAssist.CornerRadius="8"
        Height="40"
        Margin="8">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Refresh" Margin="0,0,8,0"/>
        <TextBlock Text="Load Data"/>
    </StackPanel>
</Button>
```

### Python / FastAPI

**Naming:**
- `snake_case` for functions, variables, module names
- `PascalCase` for classes
- `UPPER_CASE` for constants
- Prefix private methods/attributes with single underscore `_private_method`

**Type Hints:**
Always use type hints:
```python
from typing import List, Dict, Optional, Any

def extract_authors(svn_url: str, limit: int = 5000) -> List[Dict[str, str]]:
    """Extract authors from SVN repository."""
    authors: List[str] = []
    # ...
    return authors
```

**FastAPI Routes:**
```python
from fastapi import APIRouter, HTTPException, status
from pydantic import BaseModel

router = APIRouter(prefix="/api/migrations", tags=["migrations"])

class MigrationCreate(BaseModel):
    svn_url: str
    github_org: str
    github_repo: str

@router.post("", response_model=Migration, status_code=status.HTTP_201_CREATED)
async def create_migration(migration: MigrationCreate) -> Migration:
    """Create a new migration."""
    # Implementation
```

**Database (SQLAlchemy):**
```python
from sqlalchemy import Column, String, Integer, Boolean, DateTime
from sqlalchemy.ext.declarative import declarative_base

Base = declarative_base()

class Migration(Base):
    __tablename__ = "migrations"
    
    id = Column(String, primary_key=True)
    svn_url = Column(String, nullable=False)
    status = Column(String, nullable=False)
    created_at = Column(DateTime, nullable=False)
```

**Error Handling:**
```python
try:
    result = await some_operation()
except SpecificError as e:
    logger.error(f"Operation failed: {e}")
    raise HTTPException(
        status_code=status.HTTP_400_BAD_REQUEST,
        detail=str(e)
    )
```

## Architecture Patterns

### WPF Architecture

```
Views (XAML)
    ↓ Binding
ViewModels (ObservableObject + RelayCommand)
    ↓ Calls
Services (ApiService, NotificationService)
    ↓ HTTP
FastAPI Backend
```

**Rules:**
- Views should only contain UI logic
- ViewModels contain presentation logic and state
- Services handle API calls and business logic
- Models are simple DTOs (Data Transfer Objects)
- Use dependency injection where possible

### API Architecture

```
Routes (FastAPI endpoints)
    ↓
Services (Business logic)
    ↓
Database (SQLAlchemy ORM)
    ↓
Agents (Python scripts)
```

**Rules:**
- Routes only handle HTTP concerns (validation, status codes)
- Services contain business logic
- Database layer handles persistence
- Agents are standalone executables (stdin/stdout JSON)

### Agent Communication

Agents communicate via JSON on stdin/stdout:

**Input:**
```json
{
  "svn_url": "https://svn.example.com/repo",
  "github_token": "ghp_xxx",
  "options": {}
}
```

**Output:**
```json
{
  "status": "success",
  "data": {},
  "error": null
}
```

## Common Patterns

### WPF: Loading Data from API

```csharp
[RelayCommand]
private async Task LoadMigrationsAsync()
{
    try
    {
        IsLoading = true;
        var migrations = await _apiService.GetMigrationsAsync();
        Migrations.Clear();
        foreach (var migration in migrations)
        {
            Migrations.Add(migration);
        }
    }
    catch (Exception ex)
    {
        _notificationService.ShowError("Failed to load migrations", ex.Message);
    }
    finally
    {
        IsLoading = false;
    }
}
```

### WPF: Opening Dialogs

```csharp
private void OpenAuthorMapping()
{
    var dialog = new AuthorMappingWindow
    {
        DataContext = new AuthorMappingViewModel(_migrationId, _apiService),
        Owner = Application.Current.MainWindow
    };
    
    if (dialog.ShowDialog() == true)
    {
        // Dialog was saved
        await RefreshDataAsync();
    }
}
```

### FastAPI: Creating New Resources

```python
@router.post("")
async def create_migration(
    migration: MigrationCreate,
    db: Session = Depends(get_db)
) -> Migration:
    # Generate ID
    migration_id = str(uuid.uuid4())
    
    # Create database record
    db_migration = MigrationModel(
        id=migration_id,
        svn_url=migration.svn_url,
        status="pending",
        created_at=datetime.utcnow()
    )
    db.add(db_migration)
    db.commit()
    db.refresh(db_migration)
    
    # Start background agent
    asyncio.create_task(run_migration_agent(migration_id))
    
    return Migration.from_orm(db_migration)
```

### Agent Pattern

```python
import sys
import json
import logging

def main():
    # Read config from stdin
    config = json.load(sys.stdin)
    
    # Setup logging
    logging.basicConfig(level=logging.INFO)
    logger = logging.getLogger(__name__)
    
    try:
        # Perform work
        result = process(config)
        
        # Output result to stdout
        output = {
            "status": "success",
            "data": result,
            "error": None
        }
    except Exception as e:
        logger.exception("Agent failed")
        output = {
            "status": "error",
            "data": None,
            "error": str(e)
        }
    
    # Write JSON to stdout
    json.dump(output, sys.stdout, indent=2)

if __name__ == "__main__":
    main()
```

## Material Design Guidelines

### Colors

Use semantic color names from Material Design:
- `MaterialDesignBody` - Primary text color
- `MaterialDesignPaper` - Background color
- `PrimaryHueMidBrush` - Primary accent
- `SecondaryHueMidBrush` - Secondary accent
- `MaterialDesignValidationErrorBrush` - Error state

### Common Controls

**Buttons:**
```xml
<!-- Primary action -->
<Button Style="{StaticResource MaterialDesignRaisedButton}"/>

<!-- Secondary action -->
<Button Style="{StaticResource MaterialDesignFlatButton}"/>

<!-- Icon button -->
<Button Style="{StaticResource MaterialDesignIconButton}">
    <materialDesign:PackIcon Kind="Delete"/>
</Button>
```

**TextBoxes:**
```xml
<TextBox materialDesign:HintAssist.Hint="Enter SVN URL"
         materialDesign:TextFieldAssist.HasClearButton="True"
         Style="{StaticResource MaterialDesignOutlinedTextBox}"/>
```

**Cards:**
```xml
<materialDesign:Card Margin="8" Padding="16">
    <StackPanel>
        <TextBlock Text="Card Title" Style="{StaticResource MaterialDesignHeadline6TextBlock}"/>
        <TextBlock Text="Card content" TextWrapping="Wrap"/>
    </StackPanel>
</materialDesign:Card>
```

**DataGrid:**
```xml
<DataGrid ItemsSource="{Binding Items}"
          AutoGenerateColumns="False"
          CanUserAddRows="False"
          materialDesign:DataGridAssist.CellPadding="8">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Name" Binding="{Binding Name}"/>
    </DataGrid.Columns>
</DataGrid>
```

## Database Schema

### Migrations Table
```sql
CREATE TABLE migrations (
    id TEXT PRIMARY KEY,
    svn_url TEXT NOT NULL,
    github_org TEXT NOT NULL,
    github_repo TEXT NOT NULL,
    status TEXT NOT NULL,
    current_phase TEXT,
    progress_percent REAL,
    total_revisions INTEGER,
    processed_revisions INTEGER,
    error_message TEXT,
    workspace_path TEXT,
    created_at TIMESTAMP NOT NULL,
    started_at TIMESTAMP,
    completed_at TIMESTAMP
);
```

### Authors Table
```sql
CREATE TABLE authors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_id TEXT NOT NULL,
    svn_username TEXT NOT NULL,
    git_name TEXT NOT NULL,
    git_email TEXT NOT NULL,
    verified BOOLEAN DEFAULT 0,
    FOREIGN KEY (migration_id) REFERENCES migrations(id),
    UNIQUE(migration_id, svn_username)
);
```

### Logs Table
```sql
CREATE TABLE logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_id TEXT NOT NULL,
    timestamp TIMESTAMP NOT NULL,
    level TEXT NOT NULL,
    message TEXT NOT NULL,
    FOREIGN KEY (migration_id) REFERENCES migrations(id)
);
```

## API Response Formats

### Success Response
```json
{
  "id": "abc-123",
  "svn_url": "https://svn.example.com/repo",
  "status": "running",
  "progress_percent": 45.2,
  "created_at": "2026-10-28T10:00:00Z"
}
```

### Error Response
```json
{
  "detail": "Migration not found",
  "error_code": "MIGRATION_NOT_FOUND"
}
```

### List Response
```json
{
  "items": [...],
  "total": 50,
  "page": 1,
  "page_size": 20
}
```

## Testing Guidelines

### C# Unit Tests (xUnit)
```csharp
public class ApiServiceTests
{
    [Fact]
    public async Task GetMigrations_ReturnsListOfMigrations()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var apiService = new ApiService(mockHandler);
        
        // Act
        var result = await apiService.GetMigrationsAsync();
        
        // Assert
        Assert.NotEmpty(result);
    }
}
```

### Python Tests (pytest)
```python
import pytest
from httpx import AsyncClient

@pytest.mark.asyncio
async def test_create_migration(client: AsyncClient):
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

## Security Considerations

### Sensitive Data
- **GitHub tokens**: Never log, always stored encrypted
- **SVN credentials**: Prompt user, never persist
- **Database**: Use parameterized queries (SQLAlchemy handles this)

### Input Validation
```python
from pydantic import BaseModel, validator, HttpUrl

class MigrationCreate(BaseModel):
    svn_url: HttpUrl
    github_org: str
    github_repo: str
    
    @validator('github_org')
    def validate_org(cls, v):
        if not v or not v.strip():
            raise ValueError('GitHub org cannot be empty')
        return v.strip()
```

## Git Commit Messages

Follow conventional commits:
```
feat(author-mapping): add interactive UI for editing mappings
fix(api): handle null values in migration status
docs(readme): update installation instructions
refactor(wpf): extract common dialog logic to base class
test(agents): add unit tests for author mapper
chore(deps): update MaterialDesignThemes to 5.1.0
```

## Documentation

### XML Comments (C#)
```csharp
/// <summary>
/// Retrieves all migrations from the API.
/// </summary>
/// <returns>A list of migration objects.</returns>
/// <exception cref="HttpRequestException">Thrown when API is unavailable.</exception>
public async Task<List<Migration>> GetMigrationsAsync()
{
    // Implementation
}
```

### Docstrings (Python)
```python
def extract_authors(svn_url: str, limit: int = 5000) -> List[Dict[str, str]]:
    """
    Extract unique authors from SVN repository history.
    
    Args:
        svn_url: The SVN repository URL
        limit: Maximum number of revisions to scan (default: 5000)
    
    Returns:
        List of author dictionaries with svn_username, git_name, git_email
        
    Raises:
        subprocess.CalledProcessError: If svn command fails
        ValueError: If svn_url is invalid
    """
    # Implementation
```

## Performance Best Practices

### WPF
- Use `VirtualizingStackPanel` for long lists
- Implement `INotifyPropertyChanged` efficiently
- Avoid UI thread blocking (use `Task.Run` for CPU work)
- Dispose resources (implement `IDisposable`)

### FastAPI
- Use async endpoints for I/O operations
- Enable connection pooling for database
- Implement pagination for large result sets
- Use background tasks for long operations

### Agent Performance
- Stream large SVN logs instead of loading into memory
- Use `subprocess` with `capture_output=True` for isolation
- Implement checkpointing for resumable operations
- Monitor disk space before operations

## Environment Variables

```bash
# API Configuration
API_HOST=localhost
API_PORT=8000
DATABASE_URL=sqlite:///./migrations.db

# GitHub
GITHUB_TOKEN=ghp_xxx  # Never commit this!

# Logging
LOG_LEVEL=INFO
LOG_FILE=migration.log
```

## Common Issues & Solutions

### Issue: "API Not Available" in WPF
**Check:** Is FastAPI server running on port 8000?
```powershell
netstat -ano | findstr :8000
```

### Issue: "git-svn not found"
**Solution:** Install Git for Windows (includes git-svn)
```powershell
winget install Git.Git
```

### Issue: Author mapping slow
**Solution:** Limit SVN log scan to recent commits
```python
# Instead of scanning all history
result = subprocess.run(['svn', 'log', '--quiet', '--limit', '5000', svn_url])
```

### Issue: WPF UI freezes during long operations
**Solution:** Use async and ConfigureAwait
```csharp
await Task.Run(() => LongRunningOperation()).ConfigureAwait(false);
```

## File Structure Reference

```
D:\Svn2Github\
├── api/
│   ├── main.py              # FastAPI app
│   ├── routes/
│   │   ├── migrations.py    # Migration endpoints
│   │   └── authors.py       # Author endpoints
│   ├── models.py            # SQLAlchemy models
│   └── requirements.txt
├── agents/
│   ├── repository_analyzer.py
│   ├── author_mapper.py
│   ├── migration_executor.py
│   ├── history_cleaner.py
│   ├── github_setup.py
│   └── push_verifier.py
├── wpf/
│   └── SVN2GitHub/
│       ├── Models/          # DTOs
│       ├── ViewModels/      # MVVM ViewModels
│       ├── Views/           # XAML views
│       └── Services/        # API client
└── docs/                    # Documentation
```

## When Adding New Features

1. **Create a new branch**: `git checkout -b feature/new-feature`
2. **Add tests first** (TDD when possible)
3. **Implement feature**
4. **Update documentation** (README, API docs, etc.)
5. **Test end-to-end** (WPF → API → Agent)
6. **Commit with conventional commit message**
7. **Create pull request**

## Key Dependencies

### .NET / WPF
- MaterialDesignThemes 5.0.0
- CommunityToolkit.Mvvm 8.2.2
- System.Net.Http.Json (built-in)

### Python / FastAPI
- FastAPI 0.109+
- SQLAlchemy 2.0+
- Pydantic 2.0+
- uvicorn (ASGI server)

---

**When generating code for this project, follow these patterns and conventions strictly. The goal is consistency across the entire codebase.**
