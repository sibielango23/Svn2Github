# SVN2GitHub Migration Tool


> **Complete end-to-end solution for migrating SVN repositories to GitHub with 15+ years of history**

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)
![Python](https://img.shields.io/badge/Python-3.8+-3776AB.svg)
![FastAPI](https://img.shields.io/badge/FastAPI-0.109+-009688.svg)

## 🎯 Features

- **Multi-Agent Architecture** - 6 specialized agents handle different aspects of migration
- **Modern WPF UI** - Material Design dark theme with real-time progress tracking
- **Interactive Author Mapping** - Visual editor to review and correct SVN → Git author mappings
- **REST API Backend** - FastAPI server orchestrates the entire workflow
- **Complete History** - Preserves all 15+ years of commits, branches, and tags
- **Smart Author Generation** - Automatically converts usernames (john.doe → John Doe)
- **Progress Monitoring** - Real-time dashboard with detailed logs
- **History Cleaning** - Optional removal of large binaries and build artifacts

## 🚀 Quick Start

### One-Command Launch

```powershell
.\start.ps1
```

This will:
1. ✅ Check prerequisites (Python, .NET, git-svn)
2. ✅ Install dependencies
3. ✅ Build WPF application
4. ✅ Start API server
5. ✅ Launch desktop app

### Manual Start

**Terminal 1 - API Server:**
```powershell
pip install -r api/requirements.txt
python api/main.py
```

**Terminal 2 - WPF App:**
```powershell
cd wpf\SVN2GitHub
dotnet run
```

## 📋 Prerequisites

- **Windows 10/11**
- **Python 3.8+**
- **.NET 8.0 SDK**
- **Git for Windows** (includes git-svn)
- **GitHub Personal Access Token**

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────┐
│          WPF Desktop Application                │
│  (Material Design UI + Real-time Updates)       │
└───────────────────┬─────────────────────────────┘
                    │ REST API
┌───────────────────▼─────────────────────────────┐
│          FastAPI Backend Server                 │
│   (Orchestration + Database + Logging)          │
└───────────────────┬─────────────────────────────┘
                    │
        ┌───────────┼───────────┐
        │           │           │
┌───────▼──────┐ ┌──▼──────┐ ┌─▼────────────┐
│ Repository   │ │ Author  │ │  Migration   │
│ Analyzer     │ │ Mapper  │ │  Executor    │
└──────────────┘ └─────────┘ └──────────────┘
        │           │           │
┌───────▼──────┐ ┌──▼──────┐ ┌─▼────────────┐
│ History      │ │ GitHub  │ │  Push &      │
│ Cleaner      │ │ Setup   │ │  Verifier    │
└──────────────┘ └─────────┘ └──────────────┘
```

## 📊 Migration Workflow

1. **Repository Analysis** (5-10 min)
   - Scans SVN structure
   - Identifies branches and tags
   - Calculates repository size

2. **Author Mapping** (2-5 min)
   - Automatically extracts SVN usernames from history
   - Interactive WPF UI for reviewing and editing mappings
   - Smart name generation (john.doe → John Doe)
   - Generates authors.txt for git-svn

3. **Migration Execution** (12-24 hours)
   - Performs git-svn clone
   - Converts all revisions
   - Preserves complete history

4. **History Cleaning** (30-120 min, optional)
   - Removes large binaries
   - Cleans build artifacts
   - Optimizes repository size

5. **GitHub Setup** (1-2 min)
   - Creates repository
   - Configures settings
   - Sets up branch protection

6. **Push & Verification** (15-60 min)
   - Pushes all branches and tags
   - Verifies commit count
   - Generates migration report

## 🖥️ Screenshots

### Dashboard
### Dashboard
Real-time statistics, active migrations, and progress tracking

### New Migration
Simple form with validation and configuration options

## 📖 Documentation

- **[SETUP.md](SETUP.md)** - Complete setup and configuration guide
- **[MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)** - Step-by-step migration process
- **[AUTHOR_MAPPING_GUIDE.md](docs/AUTHOR_MAPPING_GUIDE.md)** - Complete author mapping workflow
- **[WPF_IMPLEMENTATION_GUIDE.md](wpf/WPF_IMPLEMENTATION_GUIDE.md)** - WPF application architecture
- **[API Documentation](http://localhost:8000/docs)** - Interactive API docs (when server is running)
- **[author-mapping-workflow.html](docs/author-mapping-workflow.html)** - Visual workflow guide (open in browser)

## 🔐 Security

### GitHub Token
Create a personal access token with these permissions:
- `repo` - Full repository access
- `admin:repo_hook` - Repository webhooks

### Token Storage
- Stored securely in SQLite database
- Never logged or displayed after submission
- Used only for GitHub API authentication

## ⏱️ Performance

For a typical 15-year repository:
- **Total Time**: 13-26 hours
- **Main Migration**: 12-24 hours (git-svn clone)
- **Disk Space**: 20-50 GB temporary storage
- **Memory**: 2-4 GB RAM recommended

## 🛠️ Technology Stack

### Frontend
- **WPF** (.NET 8.0)
- **MaterialDesignThemes** - Modern UI components
- **CommunityToolkit.Mvvm** - MVVM pattern
- **System.Net.Http.Json** - API communication

### Backend
- **FastAPI** - Modern Python web framework
- **SQLAlchemy** - Database ORM
- **SQLite** - Embedded database
- **Uvicorn** - ASGI server

### Migration Tools
- **git-svn** - SVN to Git conversion
- **git-filter-repo** - History cleaning (optional)
- **GitHub REST API** - Repository creation

## 📦 Project Structure

```
D:\Svn2Github\
├── api/
│   ├── main.py                    # FastAPI application
│   ├── requirements.txt           # Python dependencies
│   └── migrations.db              # SQLite database
├── agents/
│   ├── repository_analyzer.py     # SVN structure analysis
│   ├── author_mapper.py           # User mapping
│   ├── migration_executor.py      # git-svn execution
│   ├── history_cleaner.py         # Repository cleaning
│   ├── github_setup.py            # GitHub API integration
│   └── push_verifier.py           # Verification logic
├── wpf/
│   └── SVN2GitHub/
│       ├── Models/                # Data transfer objects
│       ├── ViewModels/            # MVVM view models
│       ├── Views/                 # XAML views
│       ├── Services/              # API and notifications
│       └── Converters/            # XAML converters
├── docs/                          # Documentation
├── start.ps1                      # Quick launcher
├── SETUP.md                       # Setup guide
└── README.md                      # This file
```

## 🐛 Troubleshooting

### API won't start
```powershell
# Check port availability
netstat -ano | findstr :8000

# Kill process if needed
taskkill /PID <PID> /F
```

### WPF shows "API Not Available"
1. Ensure API is running: http://localhost:8000/health
2. Check Windows Firewall
3. Restart both API and WPF app

### Migration fails
1. Check git-svn is installed: `git svn --version`
2. Verify SVN URL is accessible
3. Check GitHub token permissions
4. View detailed logs in Dashboard

### Build errors
```powershell
# Clean and rebuild
cd wpf\SVN2GitHub
dotnet clean
dotnet restore
dotnet build
```

## 🚢 Deployment

### As Windows Service (API)
```powershell
# Using NSSM (Non-Sucking Service Manager)
nssm install SVN2GitHubAPI python "D:\Svn2Github\api\main.py"
nssm start SVN2GitHubAPI
```

### Standalone Executable (WPF)
```powershell
cd wpf\SVN2GitHub
dotnet publish -c Release -r win-x64 --self-contained
# Output: bin\Release\net8.0-windows\win-x64\publish\
```

## 🤝 Contributing

Contributions are welcome! Areas for improvement:
- Additional agent types (BitBucket, GitLab support)
- Real-time WebSocket updates
- Progress visualization charts
- Multi-repository batch migrations
- Docker containerization

## 📄 License

MIT License - see LICENSE file for details

## 🙏 Acknowledgments

- **MaterialDesignInXAML** - Beautiful WPF components
- **FastAPI** - Modern Python web framework
- **git-svn** - SVN to Git bridge

## 📞 Support

For issues or questions:
1. Check [SETUP.md](SETUP.md) for configuration help
2. Review API logs in console
3. Check migrations.db for error details
4. View application logs in WPF

---

**Built for organizations migrating legacy SVN repositories to modern GitHub workflows**

Made with ❤️ for developers who value history preservation

