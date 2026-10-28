# SVN to GitHub Migration System with REST API & WPF UI
# Architecture Document

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    WPF Desktop Application                   │
│  • Modern UI (Material Design / Fluent)                     │
│  • Real-time progress tracking                              │
│  • Configuration editor                                      │
│  • Log viewer with filtering                                │
│  • Author mapping editor                                     │
│  • Audit trail viewer                                        │
└───────────────────────┬─────────────────────────────────────┘
                        │ HTTP/REST
                        │ (JSON)
┌───────────────────────▼─────────────────────────────────────┐
│              Python REST API (FastAPI)                       │
│  • /api/migrations (GET, POST)                              │
│  • /api/migrations/{id}/status (GET)                        │
│  • /api/migrations/{id}/logs (GET, WebSocket)               │
│  • /api/config (GET, POST)                                  │
│  • /api/authors (GET, POST)                                 │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                   SQLite Database                            │
│  Tables:                                                     │
│  • migrations (id, status, start_time, config)              │
│  • logs (migration_id, timestamp, level, message)           │
│  • authors (migration_id, svn_user, git_name, email)        │
│  • audit_trail (id, action, user, timestamp, details)       │
│  • verification (migration_id, check_name, status, result)  │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│              Python Orchestrator & Agents                    │
│  • repository_analyzer.py                                    │
│  • author_mapper.py                                          │
│  • migration_executor.py                                     │
│  • history_cleaner.py                                        │
│  • github_setup.py                                           │
│  • push_verifier.py                                          │
└─────────────────────────────────────────────────────────────┘
```

## Technology Stack

### Backend
- **Python FastAPI** - REST API framework
- **SQLite** - Embedded database (single file, no server needed)
- **SQLAlchemy** - ORM for database operations
- **Uvicorn** - ASGI server
- **WebSockets** - Real-time log streaming

### Frontend
- **WPF (.NET 8)** - Desktop UI framework
- **MaterialDesignThemes** - Modern UI components
- **LiveCharts2** - Charts and graphs
- **RestSharp** - HTTP client for API calls
- **SignalR** - Real-time updates via WebSocket

### Database
- **SQLite** - Advantages:
  - No server required
  - Single file database
  - Zero configuration
  - Cross-platform
  - Perfect for desktop apps
  - Built-in to Python

## Database Schema

```sql
-- Migrations table
CREATE TABLE migrations (
    id TEXT PRIMARY KEY,
    svn_url TEXT NOT NULL,
    github_org TEXT NOT NULL,
    github_repo TEXT NOT NULL,
    status TEXT NOT NULL,  -- pending, running, completed, failed, paused
    start_time DATETIME,
    end_time DATETIME,
    config_json TEXT,
    current_phase TEXT,
    progress_percent REAL,
    total_revisions INTEGER,
    processed_revisions INTEGER,
    error_message TEXT
);

-- Logs table
CREATE TABLE logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_id TEXT NOT NULL,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    level TEXT NOT NULL,  -- INFO, WARNING, ERROR, DEBUG
    agent TEXT,
    message TEXT,
    details_json TEXT,
    FOREIGN KEY (migration_id) REFERENCES migrations(id)
);

-- Authors table
CREATE TABLE authors (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_id TEXT NOT NULL,
    svn_username TEXT NOT NULL,
    git_name TEXT,
    git_email TEXT,
    commit_count INTEGER DEFAULT 0,
    verified BOOLEAN DEFAULT 0,
    FOREIGN KEY (migration_id) REFERENCES migrations(id)
);

-- Audit trail table
CREATE TABLE audit_trail (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    migration_id TEXT,
    action TEXT NOT NULL,  -- start, stop, pause, config_change, etc.
    user TEXT,
    details_json TEXT,
    FOREIGN KEY (migration_id) REFERENCES migrations(id)
);

-- Verification results table
CREATE TABLE verification_results (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_id TEXT NOT NULL,
    check_name TEXT NOT NULL,
    status TEXT NOT NULL,  -- passed, failed, warning
    expected_value TEXT,
    actual_value TEXT,
    message TEXT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (migration_id) REFERENCES migrations(id)
);

-- Statistics table
CREATE TABLE statistics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    migration_id TEXT NOT NULL,
    metric_name TEXT NOT NULL,  -- commits, branches, tags, size_mb, etc.
    metric_value TEXT,
    phase TEXT,  -- analysis, migration, verification
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (migration_id) REFERENCES migrations(id)
);
```

## REST API Endpoints

### Migrations
- `POST /api/migrations` - Start new migration
- `GET /api/migrations` - List all migrations
- `GET /api/migrations/{id}` - Get migration details
- `GET /api/migrations/{id}/status` - Get current status
- `POST /api/migrations/{id}/pause` - Pause migration
- `POST /api/migrations/{id}/resume` - Resume migration
- `POST /api/migrations/{id}/stop` - Stop migration
- `DELETE /api/migrations/{id}` - Delete migration record

### Logs
- `GET /api/migrations/{id}/logs` - Get logs (paginated)
- `GET /api/migrations/{id}/logs/stream` - WebSocket for live logs
- `GET /api/migrations/{id}/logs/export` - Export logs to file

### Authors
- `GET /api/migrations/{id}/authors` - Get author mappings
- `POST /api/migrations/{id}/authors` - Update author mappings
- `GET /api/migrations/{id}/authors/export` - Export authors.txt

### Configuration
- `GET /api/config/template` - Get config template
- `POST /api/config/validate` - Validate configuration
- `POST /api/config/test-svn` - Test SVN connection
- `POST /api/config/test-github` - Test GitHub connection

### Statistics
- `GET /api/migrations/{id}/statistics` - Get migration statistics
- `GET /api/migrations/{id}/verification` - Get verification results

### Audit
- `GET /api/audit` - Get audit trail (filtered)

## Real-time Updates

### WebSocket for Live Logs
```
WPF connects to: ws://localhost:8000/api/migrations/{id}/logs/stream

Server pushes:
{
  "timestamp": "2026-10-28T14:30:45",
  "level": "INFO",
  "agent": "migration_executor",
  "message": "Processing revision 28450/45000",
  "progress": 63.2
}
```

## WPF UI Screens

### 1. Dashboard
- Active migrations grid
- Quick statistics (total migrations, success rate)
- Recent activity timeline

### 2. New Migration Wizard
- Step 1: SVN Configuration
- Step 2: GitHub Configuration
- Step 3: Options (author domain, cleaning, etc.)
- Step 4: Test Connections
- Step 5: Review & Start

### 3. Migration Monitor (Main Screen)
- Large progress bar showing overall progress
- Phase indicators (6 circular progress indicators)
- Live log viewer (auto-scroll, filterable)
- Current statistics panel
- Pause/Resume/Stop controls
- Estimated time remaining

### 4. Author Mapping Editor
- Editable data grid
- SVN Username → Git Name → Git Email
- Bulk edit capabilities
- Import/Export authors.txt
- Validation (email format, duplicates)

### 5. Verification Results
- Checklist view with status icons
- Detailed comparison (expected vs actual)
- Export verification report

### 6. Audit Log Viewer
- Filterable table
- Date range picker
- Action type filter
- Export to CSV/PDF

### 7. Settings
- API endpoint configuration
- UI theme selection
- Log retention settings
- Database location

## Implementation Plan

I'll create the following components:

### Backend (Python)
1. `api/server.py` - FastAPI application
2. `api/database.py` - SQLite database setup & models
3. `api/routes/` - API endpoint handlers
4. `api/websocket.py` - Real-time log streaming
5. `api/orchestrator_wrapper.py` - Wraps existing orchestrator

### Frontend (WPF)
1. `MigrationApp.sln` - Visual Studio solution
2. `ViewModels/` - MVVM view models
3. `Views/` - WPF XAML views
4. `Services/ApiClient.cs` - REST API client
5. `Services/DatabaseService.cs` - Local SQLite queries (if needed)
6. `Models/` - Data models
7. `Converters/` - XAML value converters
8. `Styles/` - Material Design styling

## Why SQLite?

✅ **Zero Configuration** - No database server to install
✅ **Single File** - Entire database in one .db file
✅ **Cross-Platform** - Works on Windows, Linux, macOS
✅ **Fast** - Perfect for this use case
✅ **Reliable** - Battle-tested, used by millions
✅ **Built-in** - Python has sqlite3 module included
✅ **Low Resource** - Minimal memory/CPU usage
✅ **ACID Compliant** - Transactions work properly
✅ **Backup-Friendly** - Just copy the .db file

For a migration tool, SQLite is perfect because:
- Single user (one person running migrations)
- Moderate data volume
- Desktop application
- No network database needed
- Easy to backup and move

## Next Steps

Would you like me to start building:
1. **Backend first** - Python FastAPI + SQLite
2. **Frontend first** - WPF application shell
3. **Both together** - Full stack implementation

I can start with any component you prefer!
