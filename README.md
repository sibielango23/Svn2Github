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

- Interactive WPF dashboard with status filters, repository search, live
  progress, and quick migration actions; guided three-step migration setup.
- .NET 8 API and sequential background migration pipeline.
- SVN analysis, author mapping, `git-svn` import, optional history cleaning,
  GitHub repository setup, push, and ref verification.
  Accessible existing GitHub repositories are reused without changing settings;
  otherwise setup attempts private repository creation. Use an empty test
  destination: existing divergent history is not force-overwritten.
- Local SQLite job history; GitHub tokens are kept in API process memory and
  are not persisted.
- Optional WPF AI support using a user-configured OpenAI or Anthropic
  provider. The assistant is available in a right-side panel for general
  product questions and migration troubleshooting. Chat history is saved
  locally with Windows DPAPI protection and can be reopened or deleted. Sending
  a message shares the conversation and displayed context with the provider;
  recent migration logs are optional. AI suggestions are read-only.
- AI settings include **Clear saved key**, which removes the locally saved
  API key while keeping the provider and model. It does not revoke the key at
  the provider or delete chat history.
- Self-contained Windows x64 folder/ZIP deployment with the .NET runtime,
  Git/git-svn, SVN CLI, and GitHub CLI. See [Portable package](PORTABLE_PACKAGE.md)
  for building and using the package.
- Local readiness report: Git, Git-SVN, SVN, gh, optional history cleaner,
  writable data/workspace paths, and disk-space cautions. No SVN/GitHub network
  calls, token validation, or repository changes are made by this check.
- Live **conversion time** on each dashboard job and in diagnostics/copilot
  snapshots. Recorded processing accumulates across Resume attempts, excluding
  queue time and waits between retries. Completed/failed durations stop.
  Old jobs without timing show "not recorded"; abrupt-stop recovery keeps the
  last saved timing sample (normally every 10 seconds), not offline time.
- Full migration diagnostics with searchable recorded logs, checkpoint-aware
  local recovery guidance, and copy/export of a redacted support report.
- Read-only migration assessment in the wizard Review step: visible SVN
  author coverage, layout access, current-trunk files above 100 MiB, and
  GitHub authentication/existing destination push permission. Reports can be
  copied/exported. Requires explicit approval to contact SVN/GitHub; it does
  not prove historical size risk, repository creation permission, or cutover readiness.
- Persisted post-push verification report in Details > Verification: imported
  branch/tag object IDs versus destination, local Git integrity, and an author
  inventory for review. This is not proof of complete SVN-to-Git parity.
- Confirmed Cancel for queued/running jobs, stopping child processes and
  retaining completed checkpoints. Cancellation is unavailable during destructive
  history cleanup; already-pushed refs/repositories are not rolled back.
- Migration copilot guided drafts for diagnosis, recovery, readiness, and
  handover; explicit local snapshot refresh. See
  [Copilot guide](docs/AI_COPILOT_GUIDE.md).

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

For a no-install Windows x64 folder/ZIP package that includes the .NET runtime,
Git/git-svn, SVN CLI, and GitHub CLI, see [Portable package](PORTABLE_PACKAGE.md).
Extract the whole package before double-clicking `SVN2GitHub.exe`; the app
starts and stops its bundled API using an available loopback port.

The existing portable ZIP predates the latest chat, live-dashboard,
checkpoint-resume, clear-key, and dashboard-redesign changes. Updating source
or guides does not update that ZIP; publish and smoke-test a new package to
distribute these features.
The new readiness, diagnostics, and guided copilot features also require a
fresh publish.

The API is bound to loopback and has no user authentication. Do not expose it
to a network. The `/api/health` endpoint provides a local health check; Swagger
and `/docs` are not configured.

## Migration workflow

1. In the guided setup, enter the SVN URL and repository layout.
2. Enter the GitHub owner, repository, and token.
3. Optionally review author mappings, then inspect the final migration summary.
4. Confirm to queue the job; the service analyzes SVN, imports history with
   `git-svn`, and optionally cleans history (which rewrites commit IDs).
5. The service creates or configures the GitHub repository, pushes branches
   and tags, and verifies the remote refs.

The dashboard shows job status and progress, refreshing every 2 seconds while
jobs are active and every 10 seconds when idle. Click a summary card to filter
all jobs by status, search by GitHub destination or SVN URL, and use Reset
filters to return to the full list. Start a new migration directly from the
dashboard; each job offers Details, AI help, and Resume when eligible.
**Details** opens a diagnostics window with overview/recovery and recorded-log
tabs. Filter logs locally or copy/export the complete support report. Exports
contain repository metadata and logs; review confidential details before
sharing. Redaction is best-effort, and the report is a snapshot, not an
immutable enterprise audit.
The migration wizard keeps Back and Next (or Start migration) in a fixed
footer below the scrollable form.

Use the app-shell AI
assistant for general setup or workflow questions, or open AI help on a
migration to review its diagnostic context. Pressing Send shares the message
and selected context with the provider; migration logs can be excluded.
Expand Guided tasks to draft Diagnose, Recovery, Readiness, or Handover
requests. Local checkpoint advice and Refresh migration snapshot work without
calling AI; drafts still require Send. Run **Check readiness** in the
dashboard or wizard Review step to inspect local tooling/storage. These checks
are advisory, do not block the wizard automatically, and do not prove remote
access or full author coverage.

The service processes jobs sequentially and records durable checkpoints after
SVN analysis, author mapping, SVN import, optional history cleanup, and GitHub
repository setup. Failed or interrupted jobs can be resumed from the last
completed checkpoint in the dashboard; enter a GitHub token again because
tokens are never stored in migration history. A failure during a stage repeats
that stage. In particular, an interrupted `git-svn` import must be run again
and may take hours; completed earlier stages are retained. A failed push
resumes at push and safely retries branch/tag updates without force-pushing.
Use Cancel to request a stop outside destructive history cleanup. Wait for
the job to become failed before Resume; the incomplete stage repeats.

## Data and security

- Source-mode migration state and author mappings are stored in
  `data/local_migrations.sqlite3`, with job workspaces under
  `temp\workspace_<migration-id>`.
- Portable mode uses `%LOCALAPPDATA%\SVN2GitHub\data\local_migrations.sqlite3`
  and `%LOCALAPPDATA%\SVN2GitHub\workspaces\workspace_<migration-id>`.
  `SVN2GITHUB_PORTABLE_DATA_ROOT` relocates portable API data and workspaces,
  not the AI files. Preserve both the database and workspaces for resume.
- AI settings are stored in `%LOCALAPPDATA%\SVN2GitHub\ai-settings.json`
  (encrypted API key); chat history uses `ai-chat-history.dat` in the same
  directory (encrypted conversations). DPAPI protection is Windows-user
  specific; copying files to another PC/user is not a supported way to
  transfer credentials or readable chats. Configure a new AI key there.
- GitHub tokens remain in memory during the migration and are passed to
  GitHub tooling through the child-process environment.
- SVN URLs containing embedded username/password credentials are rejected.
- Keep the API on loopback. This is a single-user local pilot, not a hardened
  multi-user or network service.
- The AI assistant runs in the WPF app, not the migration API. Review the
  redacted context preview before sharing it with a provider.
- Enter sends a chat message; Shift+Enter inserts a newline. Sending is the
  approval action; no separate acknowledgment button is required. Previously
  saved chats can be selected and continued, or deleted locally.
- Clearing an AI key closes the current assistant when the settings dialog
  closes. It does not cancel a provider request already in flight or revoke
  the provider's credential. Revoke compromised keys in the provider account.

## Build and validate

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj --configuration Debug
dotnet test wpf\SVN2GitHub.Tests\SVN2GitHub.Tests.csproj --configuration Debug
```

The solution includes the WPF app and the ASP.NET Core API. A real external
migration remains an environment-dependent acceptance test.
See [Validation results](TEST_RESULTS.md) for verified checks and
[WPF test plan](wpf/TEST_PLAN.md) for UI and portable acceptance scenarios.

## Product-readiness direction

The current improvements prioritize migration risk reduction, diagnostics,
and explicit verification over unattended AI actions. They are informed by
GitHub's guidance to
[review migration logs and warnings](https://docs.github.com/en/migrations/using-github-enterprise-importer/completing-your-migration-with-github-enterprise-importer/accessing-your-migration-logs-for-github-enterprise-importer).
SVN2GitHub is not GitHub Enterprise Importer and does not migrate its wider
issue/PR/organization metadata or implement its audit infrastructure.
GitHub's [importer overview](https://docs.github.com/en/migrations/using-github-enterprise-importer/understanding-github-enterprise-importer/about-github-enterprise-importer)
also highlights trial migrations, history ownership, and distinct migration
permissions. Here, disposable rehearsal, author review, checkpoint reuse,
and support reports support safer local workflows; central migration roles
and high-fidelity non-Git metadata migration are not implemented.

Market/enterprise launch still needs real large-repository and interruption
acceptance, independently reviewed security, signed/reproducible release
artifacts, update/distribution policy, licensing review, and operational
support. Central SSO/RBAC, tenant isolation, immutable audit, and centralized
AI/data policy are not implemented. The loopback single-user API must not
be exposed as an enterprise network service.

## Documentation

- [Setup](SETUP.md)
- [Getting started](GETTING_STARTED.md)
- [Migration guide](MIGRATION_GUIDE.md)
- [Architecture](ARCHITECTURE.md)
- [API and WPF boundary](API_WPF_ARCHITECTURE.md)
- [API reference](api/README.md)
- [Author mapping guide](docs/AUTHOR_MAPPING_GUIDE.md)
- [Migration copilot guide](docs/AI_COPILOT_GUIDE.md)
- [WPF implementation guide](wpf/WPF_IMPLEMENTATION_GUIDE.md)
- [Documentation index](DOCUMENTATION_INDEX.md)
