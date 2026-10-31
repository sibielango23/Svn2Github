# API and WPF Architecture

The WPF client and ASP.NET Core service communicate over HTTP at
`http://127.0.0.1:5000`. The API retains the existing routes and camelCase JSON
contract so the desktop client does not require a separate backend protocol.

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

- Health, statistics, and migration CRUD/status routes.
- Temporary author sessions for extracting and editing mappings before a
  migration.
- Migration analysis, execution, optional cleaning, GitHub setup, push, and
  final ref verification.

See [api/README.md](api/README.md) for endpoint details and
[ARCHITECTURE.md](ARCHITECTURE.md) for workflow behavior.

## Reliability and trust boundaries

The API binds to loopback and has no authentication. It must not be exposed to
a network. Migration status and author maps persist in SQLite; GitHub tokens
do not. Active jobs become interrupted on service restart and cannot resume.
Subprocess cancellation is not yet supported.
