# Quick Reference

Commands assume PowerShell at the repository root.

## Run

```powershell
.\start.ps1
```

API-only:

```powershell
.\start-api.ps1
```

Health: `http://127.0.0.1:5000/api/health`

Portable: extract a newly published whole ZIP and double-click
`SVN2GitHub.exe`. The app starts its owned API at an available loopback port;
port 5000 is only the source-mode default. The existing ZIP is older than
the latest source UI/chat/resume changes.

## Build

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj --configuration Debug
dotnet test wpf\SVN2GitHub.Tests\SVN2GitHub.Tests.csproj --configuration Debug
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
- Per-job workspaces: `temp\workspace_<migration-id>`
- API logs when started by `start.ps1`: `temp/api-server.*.log`

- Portable API data: `%LOCALAPPDATA%\SVN2GitHub\data` and `workspaces`
- AI files (both modes): `%LOCALAPPDATA%\SVN2GitHub\ai-settings.json` and
  `ai-chat-history.dat`, protected for the Windows user

## UI shortcuts

- Dashboard summary cards filter status; search matches GitHub destination or
  SVN URL; Reset filters restores all jobs.
- New migration: Source -> Next -> Destination -> Next -> Review ->
  Start migration. Back/Next stay in a fixed footer.
- Failed job: Details -> resolve cause -> Resume -> fresh GitHub token.
  Completed stages are reused; interrupted SVN import repeats.
- AI: Enter sends; Shift+Enter adds a newline; select history to continue.
- AI settings -> Clear saved key keeps model/provider and chats, does not
  revoke the provider key or cancel in-flight requests.
- Check readiness -> local tooling/storage report, not remote permissions.
- Details -> log search, checkpoint advice, full support report copy/export.
- Copilot -> guided task drafts and local snapshot refresh -> review -> Send.

Keep the API on loopback. GitHub tokens are memory-only; automatic resume is
not implemented. Cancel stops queued/running work outside destructive cleanup.
Preserve workspaces for Resume; incomplete stages repeat.
See [Portable guide](../PORTABLE_PACKAGE.md) and
[Migration guide](../MIGRATION_GUIDE.md).
For guided help and privacy, see [Copilot guide](AI_COPILOT_GUIDE.md).
