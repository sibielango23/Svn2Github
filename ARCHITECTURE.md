# Architecture

## Components

```text
WPF desktop application
  └── ApiService (HTTP, camelCase JSON)
        └── ASP.NET Core API (127.0.0.1:5000)
              ├── MigrationStateStore (SQLite)
              ├── MigrationQueue (single reader)
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
for the active job and are passed to GitHub CLI/Git through environment
variables.

The workflow analyzes SVN structure and authors, creates an authors file,
imports history with `git-svn`, maps imported remote refs to local branches
and tags, optionally cleans history, creates/configures a GitHub repository,
pushes refs, and compares local and remote ref names and object IDs.

## Persistence and recovery

The .NET store uses the existing SQLite `migration_state` table and reads
legacy snake_case or camelCase records. On restart, active jobs are marked
interrupted because external process recovery and safe resume are not
implemented. The database is local application data and should be backed up
before manual edits.

## Security boundary

The API binds to loopback and has no authentication; it is not a network
service. Embedded credentials in SVN URLs are rejected. GitHub tokens are
excluded from the SQLite record and logs, but are necessarily present in
process memory while the job runs. The optional AI assistant runs in the WPF
client and sends only user-approved context directly to the configured
provider.

## External dependencies and limitations

- Git for Windows with `git-svn`, Subversion CLI, and GitHub CLI are required
  for migrations.
- Optional history cleaning uses the separately installed `git-filter-repo`.
- A real SVN/GitHub migration remains an acceptance test.
- Jobs are sequential; safe cancellation and restart resume are not available.
- This is a local single-user pilot, not a production multi-user service.
