# Quick Reference

## Run

```powershell
.\start.ps1
```

API-only:

```powershell
.\start-api.ps1
```

Health: `http://127.0.0.1:5000/api/health`

## Build

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
```

## Migration dependencies

```powershell
git svn --version
svn --version
gh --version
```

History cleaning is optional and requires `git-filter-repo`.

## Data locations

- Local history: `data/local_migrations.sqlite3`
- Per-job workspaces: `temp/workspace_<migration-id>`
- API logs when started by `start.ps1`: `temp/api-server.*.log`

Keep the API bound to loopback. Tokens are memory-only; interrupted jobs
cannot resume automatically.
