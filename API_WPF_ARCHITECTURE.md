# API and WPF Architecture

The WPF client and ASP.NET Core service communicate over HTTP at
`http://127.0.0.1:5000`. The API retains the existing routes and camelCase JSON
contract so the desktop client does not require a separate backend protocol.
Portable WPF launches its bundled API on an available loopback port; both
processes receive the selected URL via `SVN2GITHUB_API_URL`.

```text
Views (XAML)
  -> ViewModels (CommunityToolkit.Mvvm)
    -> ApiService
      -> ASP.NET Core minimal API
        -> MigrationStateStore (SQLite)
        -> MigrationQueue
          -> C# migration workflow services
            -> svn / git-svn / git / gh
```

The API project is `backend/SVN2GitHub.Api`; the WPF application and API are
both included in `wpf/SVN2GitHub.sln`.

## Main API surfaces

- Health, statistics, migration creation/status, and checkpoint Resume routes.
  DELETE requests cancellation, not history deletion. It stops queued/running
  work outside destructive history cleanup; checkpoints remain.
- Local readiness report for tooling/storage/free-space checks without
  accepting credentials, contacting repositories, or creating migrations.
- Temporary author sessions for extracting and editing mappings before a
  migration.
- Migration analysis, execution, optional cleaning, GitHub setup, push, and
  final ref verification.

See [api/README.md](api/README.md) for endpoint details and
[ARCHITECTURE.md](ARCHITECTURE.md) for workflow behavior.

## Reliability and trust boundaries

The API binds to loopback and has no authentication. It must not be exposed to
a network. Migration status and author maps persist in SQLite; GitHub tokens
do not. Active jobs become failed/interrupted on service restart. Manual
Resume requires a new token and durable workspace artifacts, reuses completed
stages, and repeats the incomplete stage. User cancellation propagates to child
processes outside destructive cleanup. Automatic resume is not supported.

The dashboard filters loaded jobs locally and polls without resetting the
list. WPF's AI assistant calls OpenAI/Anthropic directly, not through the
API. Its encrypted settings/chats are separate from SQLite migration data.
**Clear saved key** changes only the AI credential; it does not affect tokens
for queued/running migrations.
Diagnostics/report export and checkpoint-aware local advice are WPF services.
The copilot's explicit snapshot refresh reads the migration API but does not
call AI; guided task drafts stay in the composer until Send.

See [Portable package](PORTABLE_PACKAGE.md) for data locations and packaging
boundaries, and [Migration guide](MIGRATION_GUIDE.md) for recovery.
