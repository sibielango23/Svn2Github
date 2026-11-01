# Architecture

## Components

```text
WPF desktop application
  ├── AI assistant (direct OpenAI/Anthropic HTTP, user-encrypted local files)
  └── ApiService (HTTP, camelCase JSON)
        └── ASP.NET Core API (loopback)
              ├── MigrationStateStore (SQLite)
              ├── MigrationQueue (single reader)
              ├── ReadinessService (local version/storage checks, no remote calls)
              └── Migration workflow services
                    ├── RepositoryAnalyzer
                    ├── AuthorMapper
                    ├── MigrationExecutor
                    ├── HistoryCleaner
                    ├── GitHubSetup
                    └── PushVerifier
```

The migration workflow is implemented in C# in
`backend/SVN2GitHub.Api/Services/MigrationAgents.cs`. SVN and Git operations
remain external command-line tools invoked through
`ExternalCommandRunner.cs`.

## Migration lifecycle

The API validates a request, persists the job without its GitHub token, and
queues the in-memory request for sequential execution. Each step updates
SQLite-backed progress and logs. Tokens are retained only in process memory
while queued/processing and are passed to GitHub CLI/Git through environment
variables. The queue clears the token after each attempt.

The workflow analyzes SVN structure and authors, creates an authors file,
imports history with `git-svn`, maps imported remote refs to local branches
and tags, optionally cleans history, creates/configures a GitHub repository,
pushes refs, and compares local and remote ref names and object IDs.

## Persistence and recovery

The .NET store uses the existing SQLite `migration_state` table and reads
legacy snake_case or camelCase records. `LastCompletedStage` and history-cleaning
settings are persisted in the migration JSON without changing the existing
table or storing GitHub credentials. Older records default to checkpoint
`none`. On restart, active jobs become failed/interrupted.

Manual Resume requires a fresh token and skips completed analysis, authors,
import, optional cleanup, and GitHub setup stages. The incomplete stage
repeats. An incomplete import is replaced; a checkpointed import requires
its saved `.git` repository. Late push retries use normal, non-force pushes.
This does not recover arbitrary external-process state or individual SVN
revisions. See [Migration guide](MIGRATION_GUIDE.md#failure-and-resume).

Source defaults are repository `data\local_migrations.sqlite3` and
`temp\workspace_<migration-id>`. Portable defaults are the Windows user's
`SVN2GitHub\data` and `SVN2GitHub\workspaces` under local application data.
Preserve the database and workspaces together.

## Presentation and deployment

The dashboard filters the full loaded job collection by status and repository
search. In-place reconciliation keeps live updates stable; polling is every
2 seconds when jobs are active and every 10 seconds when idle. Errors report
stale data and retry automatically. The wizard uses a scrollable form with a
fixed navigation footer.

In source mode, the launcher builds both projects and starts/reuses the
loopback API at port 5000. In portable mode, WPF starts the bundled
self-contained API on an available loopback port and passes the URL to
`ApiService` through `SVN2GITHUB_API_URL`. Closing WPF stops its owned API.
Bundled Git/git-svn, SVN, and gh directories are prepended to process PATH.
See [Portable package](PORTABLE_PACKAGE.md); existing ZIPs do not update
automatically when source changes.

## Security boundary

The API binds to loopback and has no authentication; it is not a network
service. Embedded credentials in SVN URLs are rejected. GitHub tokens are
excluded from the SQLite record and logs, but are necessarily present in
process memory while the job runs. The optional AI assistant runs in the WPF
client and sends only user-approved context directly to the configured
provider.
AI settings and chats use Windows-user DPAPI protection and always stay in
per-user local application data. Clear saved key retains provider/model and
chat history; it does not revoke provider credentials or cancel in-flight
requests. Sending is explicit approval; there is no acknowledgment checkbox.
Readiness uses a shared command-runner interface and the queue's workspace
resolution, respects portable paths, and bounds each version command to
15 seconds. Storage probes are delete-on-close. A report can contain failed
checks despite a successful HTTP response.

`MigrationSupportService` builds redacted diagnostics snapshots and generic
checkpoint recovery advice in WPF. Diagnostics offers full log search and
complete report copy/export. It is not tamper-proof auditing.
The copilot uses the same snapshot/advice, prepares task drafts locally,
and refreshes selected migration context through the API. Only explicit Send
calls a provider; suggested operations are never executed automatically.
See [Copilot guide](docs/AI_COPILOT_GUIDE.md).

## External dependencies and limitations

- Git for Windows with `git-svn`, Subversion CLI, and GitHub CLI are required
  for migrations.
- Optional history cleaning uses the separately installed `git-filter-repo`.
- A real SVN/GitHub migration remains an acceptance test.
- Jobs are sequential; safe user cancellation and automatic restart resume
  are not available. Manual checkpoint resume is implemented but external
  failure-injection acceptance remains outstanding.
- This is a local single-user pilot, not a production multi-user service.
