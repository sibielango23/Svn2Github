# Python FastAPI Backend - README

## Overview

This is the REST API backend for the SVN to GitHub Migration system, built with FastAPI and SQLite.

## Features

- **RESTful API** - Complete REST endpoints for all migration operations
- **SQLite Database** - Single-file database for easy deployment
- **WebSocket Support** - Real-time log streaming
- **Interactive Documentation** - Auto-generated API docs at `/docs`
- **CORS Enabled** - Allows WPF client to connect
- **Background Tasks** - Async migration execution

## Installation

1. Install dependencies:
   ```bash
   pip install -r api/requirements.txt
   ```

2. Initialize database:
   ```bash
   python -c "from api.database import init_db; init_db()"
   ```

## Running the Server

### Windows:
```powershell
.\start-api.ps1
```

### Linux/macOS:
```bash
./start-api.sh
```

### Manual:
```bash
cd api
python server.py
```

The server will start at: **http://localhost:8000**

## API Documentation

Interactive API documentation is available at:
- **Swagger UI**: http://localhost:8000/docs
- **ReDoc**: http://localhost:8000/redoc

## Endpoints

### Migrations
- `POST /api/migrations` - Create and start new migration
- `GET /api/migrations` - List all migrations
- `GET /api/migrations/{id}` - Get migration details
- `GET /api/migrations/{id}/status` - Get current status
- `POST /api/migrations/{id}/pause` - Pause migration
- `POST /api/migrations/{id}/resume` - Resume migration
- `POST /api/migrations/{id}/stop` - Stop migration
- `DELETE /api/migrations/{id}` - Delete migration
- `WS /api/migrations/{id}/stream` - WebSocket for real-time logs

### Logs
- `GET /api/logs/{migration_id}` - Get logs (paginated, filterable)
- `GET /api/logs/{migration_id}/export` - Export logs (txt/json/csv)
- `GET /api/logs/{migration_id}/summary` - Get log summary

### Authors
- `GET /api/authors/{migration_id}` - Get all authors
- `POST /api/authors/{migration_id}` - Create author mapping
- `PUT /api/authors/{migration_id}/{author_id}` - Update author
- `POST /api/authors/{migration_id}/bulk` - Bulk update authors
- `GET /api/authors/{migration_id}/export` - Export authors.txt

### Configuration
- `GET /api/config/template` - Get config template
- `POST /api/config/validate` - Validate configuration
- `POST /api/config/test-svn` - Test SVN connection
- `POST /api/config/test-github` - Test GitHub connection
- `GET /api/config/prerequisites` - Check installed tools

### Statistics
- `GET /api/statistics/{migration_id}` - Get statistics
- `GET /api/statistics/{migration_id}/summary` - Get summary

### Verification
- `GET /api/verification/{migration_id}` - Get verification results
- `GET /api/verification/{migration_id}/summary` - Get verification summary

### Audit
- `GET /api/audit` - Get audit trail (filterable)
- `GET /api/audit/actions` - Get all action types
- `GET /api/audit/users` - Get all users

## Database Schema

The SQLite database (`data/migration.db`) contains:
- **migrations** - Migration tracking
- **logs** - Log entries
- **authors** - Author mappings
- **audit_trail** - Audit log
- **verification_results** - Verification checks
- **statistics** - Migration metrics

## Example Usage

### Start a Migration

```bash
curl -X POST "http://localhost:8000/api/migrations" \
  -H "Content-Type: application/json" \
  -d '{
    "svn_url": "https://svn.example.com/repo",
    "github_org": "myorg",
    "github_repo": "myrepo",
    "svn_structure": "standard",
    "github_private": true
  }'
```

### Get Migration Status

```bash
curl "http://localhost:8000/api/migrations/{id}/status"
```

### Get Logs

```bash
curl "http://localhost:8000/api/logs/{migration_id}?level=ERROR&limit=50"
```

### WebSocket Connection (JavaScript)

```javascript
const ws = new WebSocket('ws://localhost:8000/api/migrations/{id}/stream');

ws.onmessage = (event) => {
  const log = JSON.parse(event.data);
  console.log(`[${log.timestamp}] ${log.level}: ${log.message}`);
};
```

## Architecture

```
FastAPI Server
    ↓
Routes (REST endpoints)
    ↓
Database (SQLite via SQLAlchemy)
    ↓
Orchestrator Wrapper
    ↓
Agent Scripts (existing Python agents)
```

## Development

### Hot Reload
The server runs with `reload=True` by default, so changes are picked up automatically.

### Adding New Endpoints
1. Create route file in `api/routes/`
2. Add router to `api/server.py`
3. Define schemas in `api/schemas.py`

## Database Management

### View Database
```bash
sqlite3 data/migration.db
```

### Reset Database
```bash
rm data/migration.db
python -c "from api.database import init_db; init_db()"
```

### Backup Database
```bash
cp data/migration.db data/migration_backup.db
```

## Troubleshooting

### Port Already in Use
Change port in `api/server.py`:
```python
uvicorn.run("server:app", port=8001)
```

### Database Locked
SQLite doesn't handle concurrent writes well. Ensure only one migration runs at a time.

### CORS Issues
If connecting from a different origin, update CORS settings in `api/server.py`:
```python
allow_origins=["http://localhost:3000"]
```

## Next Steps

1. ✓ Backend API complete
2. → Build WPF frontend (next step)
3. → Connect WPF to API via HTTP client

The API is now ready for the WPF application to connect to!
