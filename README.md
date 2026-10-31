# SVN2GitHub Migration Tool

SVN2GitHub is a Windows WPF desktop application backed by an ASP.NET Core
service. It migrates Subversion history, authors, branches, and tags to GitHub
using Git for Windows' `git-svn` support.

## Current status

The WPF/API integration and local persistence are validated for a local pilot.
A full migration against a real SVN server and GitHub repository has not yet
been exercised. Test the complete workflow with a disposable repository before
migrating production history.

## Features

- WPF dashboard and migration setup UI.
- .NET 8 API and sequential background migration pipeline.
- SVN analysis, author mapping, `git-svn` import, optional history cleaning,
  GitHub repository setup, push, and ref verification.
- Local SQLite job history; GitHub tokens are kept in API process memory and
  are not persisted.
- Optional WPF AI support using a user-configured OpenAI or Anthropic
  provider. AI suggestions are read-only and require review and approval before
  migration context is sent.

## Requirements

- Windows 10/11 and the .NET 8 SDK.
- Git for Windows with `git-svn` available (`git svn --version`).
- Subversion command-line client (`svn --version`).
- GitHub CLI (`gh --version`) and a token authorized to create and push to the
  destination repository.
- Optional: `git-filter-repo` for history cleaning. This is a third-party tool;
  its installation may require Python, but no Python application source or
  Python package manifest is used by this repository.

## Start the application

From the repository root in PowerShell:

```powershell
.\start.ps1
```

The launcher builds the WPF and API projects, starts the API on
`http://127.0.0.1:5000`, checks its health, and opens the WPF app. Keep the
launcher window open while using the app. To run only the API:

```powershell
.\start-api.ps1
```

The API is bound to loopback and has no user authentication. Do not expose it
to a network. The `/api/health` endpoint provides a local health check; Swagger
and `/docs` are not configured.

## Migration workflow

1. Review the SVN URL and repository layout.
2. Extract and verify the SVN-to-Git author mappings.
3. Import history with `git-svn`.
4. Optionally clean history (this rewrites commit IDs).
5. Create or configure the GitHub repository.
6. Push branches and tags and compare local and remote refs.

The service processes jobs sequentially. In-progress jobs are marked
interrupted if the backend stops; they do not resume automatically.
Cancellation of a running external command is not currently supported.

## Data and security

- Migration state and author mappings are stored in
  `data/local_migrations.sqlite3`.
- GitHub tokens remain in memory during the migration and are passed to
  GitHub tooling through the child-process environment.
- SVN URLs containing embedded username/password credentials are rejected.
- Keep the API on loopback. This is a single-user local pilot, not a hardened
  multi-user or network service.
- The AI assistant runs in the WPF app, not the migration API. Review the
  redacted context preview before sharing it with a provider.

## Build and validate

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
```

The solution includes the WPF app and the ASP.NET Core API. A real external
migration remains an environment-dependent acceptance test.

## Documentation

- [Setup](SETUP.md)
- [Getting started](GETTING_STARTED.md)
- [Migration guide](MIGRATION_GUIDE.md)
- [Architecture](ARCHITECTURE.md)
- [API and WPF boundary](API_WPF_ARCHITECTURE.md)
- [API reference](api/README.md)
- [Author mapping guide](docs/AUTHOR_MAPPING_GUIDE.md)
- [WPF implementation guide](wpf/WPF_IMPLEMENTATION_GUIDE.md)
- [Documentation index](DOCUMENTATION_INDEX.md)
