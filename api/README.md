# .NET Migration API

The supported local API is the ASP.NET Core application in
`backend/SVN2GitHub.Api`. The former Python/FastAPI implementation has been
retired. The WPF client continues to use the loopback HTTP boundary at
`http://127.0.0.1:5000`.

## Run

From the repository root:

```powershell
dotnet run --project backend\SVN2GitHub.Api\SVN2GitHub.Api.csproj
```

Or run `.\start-api.ps1`. The health route is
`GET http://127.0.0.1:5000/api/health`. OpenAPI/Swagger is not configured.

## Core endpoints

- `GET /api/health`
- `GET /api/stats`
- `GET /api/migrations`
- `POST /api/migrations`
- `GET /api/migrations/{migrationId}`
- `DELETE /api/migrations/{migrationId}` (running-job cancellation returns
  `501 Not Implemented`)
- `GET /api/authors`
- `POST /api/authors`
- `GET /api/authors/{migrationId}`
- `POST /api/authors/{migrationId}/session`
- `DELETE /api/authors/{migrationId}/session`
- `POST /api/authors/{migrationId}/extract`
- `POST /api/authors/{migrationId}/bulk`
- `POST /api/authors/{migrationId}`
- `PUT /api/authors/{migrationId}/{authorId}`
- `DELETE /api/authors/{migrationId}/{authorId}`
- `GET /api/authors/{migrationId}/export`

Requests and responses use camelCase JSON to match the WPF client.

## Data and limitations

- The API reuses `data/local_migrations.sqlite3` and the existing
  `migration_state` table, preserving migration history and author maps.
- GitHub tokens are memory-only and omitted from SQLite records. Restarted
  in-progress jobs are marked failed/interrupted; they are not resumed.
- Jobs are processed one at a time. Safe cancellation is not implemented.
- The service binds to loopback and has no authentication; do not expose it to
  a network.
- SVN, Git with `git-svn`, GitHub CLI, and optionally `git-filter-repo` remain
  external command dependencies.
