# SVN2GitHub Migration Tool - Complete Setup Guide

## 🎯 Overview

This is a complete end-to-end SVN to GitHub migration solution with:
- **FastAPI Backend** - REST API for orchestrating multi-agent migrations
- **WPF Desktop App** - Modern Material Design UI for Windows
- **Multi-Agent System** - 6 specialized agents for different migration tasks

## 📋 Prerequisites

### For API (Backend)
- Python 3.8+ 
- pip

### For WPF App (Frontend)
- .NET 8.0 SDK
- Windows 10/11

### For Migration
- git-svn (comes with Git for Windows)
- SVN command line tools
- GitHub personal access token

## 🚀 Quick Start

### 1. Start the API Server

```powershell
# Navigate to project root
cd D:\Svn2Github

# Install Python dependencies
pip install -r api/requirements.txt

# Start the API server
python api/main.py
```

The API will start at `http://localhost:8000`
- API Documentation: `http://localhost:8000/docs`
- Health Check: `http://localhost:8000/health`

### 2. Launch the WPF Application

```powershell
# Navigate to WPF project
cd D:\Svn2Github\wpf\SVN2GitHub

# Build and run
dotnet run
```

Or run the compiled executable:
```powershell
.\bin\Release\net8.0-windows\SVN2GitHub.exe
```

## 📊 Features Implemented

### ✅ Backend API

**Endpoints:**
- `GET /health` - Health check
- `GET /stats` - Migration statistics
- `GET /migrations` - List all migrations
- `GET /migrations/{id}` - Get migration details
- `POST /migrations` - Create new migration
- `GET /migrations/{id}/logs` - Get migration logs

**Database:**
- SQLite database (`migrations.db`)
- Automatic schema creation
- Migration tracking and logging

### ✅ WPF Application

**Dashboard View:**
- Real-time statistics (Total, Active, Completed, Failed)
- Recent migrations list with progress bars
- Click to view detailed logs
- Auto-refresh capability
- API connection status

**New Migration View:**
- SVN configuration (URL, trunk/branches/tags paths)
- GitHub configuration (Org, Repo, Token)
- History cleaning option
- Form validation
- Progress feedback

**Services:**
- `ApiService` - HTTP client for backend communication
- `NotificationService` - User notifications and confirmations

## 🔧 Configuration

### API Configuration
Edit `api/main.py`:
```python
DATABASE_URL = "sqlite:///./migrations.db"
HOST = "0.0.0.0"
PORT = 8000
```

### Agent Configuration
Agents are located in `agents/` directory:
- `repository_analyzer.py` - Analyzes SVN structure
- `author_mapper.py` - Maps SVN users to Git authors
- `migration_executor.py` - Executes git-svn clone
- `history_cleaner.py` - Cleans repository history
- `github_setup.py` - Creates GitHub repository
- `push_verifier.py` - Verifies migration integrity

### Custom Agent Template
See `agents/custom_agent_template.py` for creating your own agents.

## 📖 Usage Workflow

### Step 1: Start API Server
```powershell
python api/main.py
```

### Step 2: Launch WPF App
```powershell
cd wpf\SVN2GitHub
dotnet run
```

### Step 3: Create Migration
1. Click "NEW MIGRATION" button
2. Enter SVN repository URL
3. Configure SVN paths (trunk/branches/tags)
4. Enter GitHub organization and repository name
5. Provide GitHub personal access token
6. (Optional) Enable history cleaning
7. Click "START MIGRATION"

### Step 4: Monitor Progress
1. Return to Dashboard
2. Click "Refresh" to update status
3. Click "VIEW" on any migration to see detailed logs
4. Monitor progress percentage

## 🔐 Security Notes

### GitHub Token
The token needs these permissions:
- `repo` - Full repository access
- `admin:repo_hook` - Repository webhooks (if needed)

**Never commit your GitHub token to version control!**

### Token Storage
Tokens are:
- Stored in SQLite database
- Used only for GitHub API calls
- Not displayed in the UI after submission

## ⏱️ Migration Timeline

For a 15-year repository:
- **Analysis**: 5-10 minutes
- **Author Mapping**: 2-5 minutes
- **git-svn Clone**: 12-24 hours (longest step)
- **History Cleaning**: 30-120 minutes (optional)
- **GitHub Setup**: 1-2 minutes
- **Push & Verify**: 15-60 minutes

**Total: 13-26 hours**

## 🐛 Troubleshooting

### API won't start
```powershell
# Check if port 8000 is in use
netstat -ano | findstr :8000

# Kill the process if needed
taskkill /PID <PID> /F
```

### WPF App shows "API Not Available"
1. Ensure API is running: `http://localhost:8000/health`
2. Check firewall settings
3. Verify API URL in `ApiService.cs` (default: `http://localhost:8000`)

### Migration fails
1. Check API logs in console
2. View migration logs via Dashboard > VIEW button
3. Check `migrations.db` for error details
4. Ensure git-svn is installed: `git svn --version`

### Build errors
```powershell
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## 📁 Project Structure

```
D:\Svn2Github\
├── api/
│   ├── main.py              # FastAPI application
│   ├── requirements.txt     # Python dependencies
│   └── migrations.db        # SQLite database (created on first run)
├── agents/
│   ├── repository_analyzer.py
│   ├── author_mapper.py
│   ├── migration_executor.py
│   ├── history_cleaner.py
│   ├── github_setup.py
│   ├── push_verifier.py
│   └── custom_agent_template.py
├── wpf/
│   └── SVN2GitHub/
│       ├── Models/          # Data models
│       ├── ViewModels/      # MVVM view models
│       ├── Views/           # XAML views
│       ├── Services/        # API and notification services
│       └── Converters/      # XAML converters
└── docs/
    ├── SETUP.md            # This file
    ├── GUIDE_BEGINNERS.md
    └── GUIDE_ADVANCED.md
```

## 🔄 Development Workflow

### Making Changes to API
```powershell
# Edit api/main.py
python api/main.py  # Restart server
```

### Making Changes to WPF
```powershell
cd wpf\SVN2GitHub
dotnet build
dotnet run
```

### Adding New Agent
1. Copy `agents/custom_agent_template.py`
2. Implement the agent logic
3. Update API orchestrator to call new agent
4. Test via API endpoint

## 🚢 Deployment

### API Server
```powershell
# Install as Windows Service using NSSM
nssm install SVN2GitHubAPI "python" "D:\Svn2Github\api\main.py"
nssm start SVN2GitHubAPI
```

### WPF Application
```powershell
# Build release version
cd wpf\SVN2GitHub
dotnet publish -c Release -r win-x64 --self-contained

# Executable will be in:
# bin\Release\net8.0-windows\win-x64\publish\SVN2GitHub.exe
```

## 📞 Support

For issues, check:
1. API logs in console
2. WPF application event viewer
3. SQLite database: `migrations.db`
4. GitHub API status page

## 🎯 Next Steps

1. ✅ API server running
2. ✅ WPF app connected
3. ✅ Create first migration
4. ✅ Monitor progress
5. ✅ View results on GitHub

---

**Built with:**
- FastAPI (Backend)
- WPF + Material Design (Frontend)
- SQLite (Database)
- Multi-Agent Architecture (Orchestration)
