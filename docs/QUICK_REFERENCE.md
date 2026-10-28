# 📘 Quick Reference Card - SVN to GitHub Migration

**Version:** 1.0 | **Date:** October 2026 | **Status:** Current

---

## 🚀 Quick Start (60 seconds)

```powershell
# 1. Start API (Terminal 1)
cd D:\Svn2Github
python api/main.py

# 2. Launch WPF App (Terminal 2)
cd D:\Svn2Github\wpf\SVN2GitHub
dotnet run
```

✅ **API:** http://localhost:8000  
✅ **Docs:** http://localhost:8000/docs

---

## 📖 Documentation Quick Links

| Need | Document | Time |
|------|----------|------|
| **Get Started** | [README.md](../README.md) | 5 min |
| **Install Everything** | [SETUP.md](../SETUP.md) | 20 min |
| **Run Migration** | [MIGRATION_GUIDE.md](../MIGRATION_GUIDE.md) | Full guide |
| **Map Authors** | [AUTHOR_MAPPING_GUIDE.md](AUTHOR_MAPPING_GUIDE.md) | 10 min |
| **Visual Guide** | [author-mapping-workflow.html](author-mapping-workflow.html) | Interactive |
| **All Docs** | [DOCUMENTATION_INDEX.md](../DOCUMENTATION_INDEX.md) | Index |

---

## 🎯 Common Tasks

### **Create New Migration**
1. Open WPF app → "New Migration"
2. Enter SVN URL and GitHub details
3. Click "Configure" in Author Mapping section
4. Click "Load From SVN" → Review mappings → Save
5. Click "START MIGRATION"

### **Edit Author Mappings**
1. From migration form: Click "Configure"
2. OR from dashboard: Click migration → "Edit Authors"
3. Double-click cells to edit
4. Check "Verified" when correct
5. Click "Save & Close"

### **Monitor Progress**
1. Dashboard shows all active migrations
2. Click any migration for detailed logs
3. Progress bar shows completion %
4. Auto-refreshes every 10 seconds

### **Export Author Mappings**
1. Open Author Mapping Window
2. Click "Export to File"
3. Saves as `authors_<migration-id>.txt`
4. Use with git-svn command line tools

---

## 🔧 API Endpoints (Quick Reference)

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/health` | Check API status |
| GET | `/stats` | Migration statistics |
| GET | `/migrations` | List all migrations |
| POST | `/migrations` | Create new migration |
| GET | `/migrations/{id}` | Get migration details |
| GET | `/migrations/{id}/logs` | Get migration logs |
| GET | `/authors/{id}` | Get author mappings |
| **POST** | `/authors/{id}/extract` | **Extract from SVN** |
| POST | `/authors/{id}/bulk` | Bulk update authors |
| GET | `/authors/{id}/export` | Export authors.txt |

---

## ⏱️ Time Estimates

| Repository Size | Time | Disk Space |
|----------------|------|------------|
| Small (< 10K revisions) | 1-4 hours | 1-5 GB |
| Medium (10K-50K) | 4-12 hours | 5-20 GB |
| Large (50K-100K) | 12-24 hours | 20-50 GB |
| Very Large (> 100K) | 1-3 days | 50+ GB |

**Your 15-year repo:** Likely 50K-100K revisions = **12-24 hours**

---

## 🎨 Author Mapping Examples

| SVN Username | Generated Git Format |
|--------------|---------------------|
| `jdoe` | Jdoe <jdoe@example.com> |
| `john.doe` | John Doe <john.doe@example.com> |
| `alice.m.smith` | Alice M Smith <alice.m.smith@example.com> |

**Edit these in the WPF UI before migration!**

---

## ⚠️ Common Issues

### **"API Not Available"**
```powershell
# Check if API is running
curl http://localhost:8000/health

# Restart API
cd D:\Svn2Github
python api/main.py
```

### **"git-svn not found"**
```powershell
# Check installation
git svn --version

# Install if missing
winget install Git.Git
```

### **"SVN connection timeout"**
```bash
# Test SVN access first
svn info https://your-svn-url

# Check credentials
svn list https://your-svn-url --username your-user
```

### **"Migration stuck"**
1. Check Dashboard logs
2. Look for last processed revision
3. May need to restart from checkpoint
4. Check disk space (need 20-50 GB)

---

## 🔐 GitHub Token Setup

1. Go to: https://github.com/settings/tokens
2. Click "Generate new token (classic)"
3. Select permissions:
   - ✅ `repo` (all)
   - ✅ `admin:repo_hook`
4. Generate and copy token
5. Paste in WPF app (never logged)

---

## 📁 Key Files & Directories

| Path | Purpose |
|------|---------|
| `D:\Svn2Github\` | Project root |
| `api/main.py` | FastAPI server |
| `api/migrations.db` | SQLite database |
| `wpf/SVN2GitHub/` | WPF application |
| `agents/` | Migration agents |
| `docs/` | Documentation |
| `migration-workspace/` | Temporary working dir |

---

## 🛠️ Tech Stack Quick Info

| Component | Technology | Version |
|-----------|-----------|---------|
| **Frontend** | WPF + Material Design | .NET 8.0 |
| **Backend** | FastAPI | Python 3.8+ |
| **Database** | SQLite | 3.x |
| **Migration** | git-svn | Latest |
| **API Client** | System.Net.Http | .NET 8.0 |

---

## 📊 Migration Workflow (High Level)

```
1. Repository Analysis     (5-10 min)
   ↓
2. Author Mapping         (2-5 min) ← YOU REVIEW HERE
   ↓
3. Migration Execution    (12-24 hours) ← LONGEST STEP
   ↓
4. History Cleaning       (Optional, 30-120 min)
   ↓
5. GitHub Setup          (1-2 min)
   ↓
6. Push & Verification   (15-60 min)
   ↓
✅ DONE!
```

---

## 🎯 Best Practices

### ✅ Do:
- Review ALL author mappings before migration
- Use real email addresses (not example.com)
- Test SVN access before starting
- Monitor disk space during migration
- Keep API running in background
- Check "Verified" for each author

### ❌ Don't:
- Skip author mapping review
- Close terminal running API
- Interrupt migration (unless error)
- Modify workspace files manually
- Use fake/random GitHub tokens
- Start multiple migrations of same repo

---

## 🆘 Get Help

| Issue | Where to Look |
|-------|--------------|
| Setup problems | [SETUP.md](../SETUP.md) |
| Migration errors | [MIGRATION_GUIDE.md](../MIGRATION_GUIDE.md) |
| Author mapping | [AUTHOR_MAPPING_GUIDE.md](AUTHOR_MAPPING_GUIDE.md) |
| API issues | Console logs + `/docs` endpoint |
| WPF crashes | Application event log |
| General questions | [DOCUMENTATION_INDEX.md](../DOCUMENTATION_INDEX.md) |

---

## 📝 Useful Commands

### **Check Prerequisites**
```powershell
python --version          # Should be 3.8+
dotnet --version          # Should be 8.0+
git svn --version         # Should show git-svn
svn --version             # Should show Subversion
gh --version              # Should show GitHub CLI
```

### **API Management**
```powershell
# Start API
python api/main.py

# Check health
curl http://localhost:8000/health

# View OpenAPI docs
start http://localhost:8000/docs

# Check database
sqlite3 api/migrations.db "SELECT * FROM migrations;"
```

### **WPF Build**
```powershell
# Clean build
dotnet clean
dotnet restore
dotnet build

# Run debug
dotnet run

# Publish release
dotnet publish -c Release -r win-x64 --self-contained
```

### **git-svn Commands** (Manual)
```bash
# Clone entire repository
git svn clone https://svn-url \
  --authors-file=authors.txt \
  --stdlayout

# Clone recent history only
git svn clone -r 50000:HEAD https://svn-url

# Resume interrupted clone
cd repo-dir
git svn fetch
```

---

## 🎉 Quick Checklist

Before starting migration:

- [ ] API server is running
- [ ] WPF app is connected
- [ ] SVN URL is accessible
- [ ] GitHub token is valid
- [ ] Author mappings are reviewed
- [ ] Disk space > 50 GB available
- [ ] Network is stable
- [ ] Coffee is ready ☕ (this takes hours!)

---

## 📞 Support Resources

- **Project Docs:** `D:\Svn2Github\docs\`
- **API Docs:** http://localhost:8000/docs
- **FastAPI:** https://fastapi.tiangolo.com/
- **git-svn:** https://git-scm.com/docs/git-svn
- **Material Design:** https://materialdesigninxaml.net/

---

**Print this card and keep it handy during migration! 📌**

*Last updated: October 2026*
