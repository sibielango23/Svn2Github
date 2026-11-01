# .NET Migration API

The supported local API is the ASP.NET Core application in
`backend/SVN2GitHub.Api`. The former Python/FastAPI implementation has been
retired. The WPF client continues to use the loopback HTTP boundary at
`http://127.0.0.1:5000`.
Portable startup selects an available loopback port and sets
`SVN2GITHUB_API_URL` for the API and WPF client.

## Run

From the repository root:

```powershell
dotnet run --project backend\SVN2GitHub.Api\SVN2GitHub.Api.csproj
```

Or run `.\start-api.ps1`. The health route is
`GET http://127.0.0.1:5000/api/health`. OpenAPI/Swagger is not configured.

## Core endpoints

- `GET /api/health`
- `POST /api/readiness`
- `POST /api/assessment`
- `GET /api/stats`
- `GET /api/migrations`
- `POST /api/migrations`
- `GET /api/migrations/{migrationId}`
- `POST /api/migrations/{migrationId}/resume`
- `DELETE /api/migrations/{migrationId}` (202 accepted cancellation; 404
  missing job; 400 terminal job; 409 stopping/not cancellable, including cleanup)
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

## Migration contract and resume

Creation requires `svnUrl` (HTTP/HTTPS/SVN, no embedded credentials),
`githubOrg`, `githubRepo`, and `githubToken`. Optional setup includes
`trunkPath`, `branchesPath`, `tagsPath`, `cleanHistory`, and `authors`.
The wizard supplies the reviewed authors in the request. Invalid input
returns 422; accepted creation returns 200 with ID/status/timestamp.
Migration creation, assessment and author sessions normalize valid URLs to
escaped absolute URLs (`Source Code` becomes `Source%20Code`) without
double-encoding existing escapes. Backslashes, UNC/filesystem inputs,
credentials, query strings and fragments are rejected with HTTP 422.

## Local readiness contract

`POST /api/readiness` takes `{ "cleanHistory": false }` (or true for optional
cleanup checks) and returns 200 with:

- `checkedAt`: timestamp of the check.
- `checks`: `{ name, status, detail }` entries; status is `passed`, `warning`,
  or `failed`.
- `hasBlockingIssues`: true when any local check failed.

HTTP 200 means a report was produced, **not** that the machine is ready.
Checks run bounded local version commands for git, git-svn, svn, gh, and
optional git-filter-repo, probe configured storage with delete-on-close
temporary files, and measure workspace free space. Under 5 GiB is a warning,
not a size estimate. No migration is created, no credentials are accepted,
and SVN/GitHub are not contacted. Access, permissions, author coverage, and
true capacity remain explicit unverified cautions. Request cancellation is
passed to command/file operations.

## Resume request

`POST /api/assessment` accepts the migration creation body and validates it
identically (422 invalid input). It returns the readiness report shape, with
additional read-only remote checks. It scans the full visible SVN log, layout
access, and current trunk inventory (>100 MiB file threshold), plus github.com
token authentication and existing-repository push permission. Each remote
command is bounded to five minutes and cancellation propagates. No job, import,
repository creation, or push occurs; the request token is not persisted.
Unknown creation permissions/historical sizes are warnings, not invented passes.
Assessment is advisory. HTTP 200 is report creation, not a successful assessment.

Details also include `canCancel` and nullable `verificationReport` using the
readiness-report shape. Reports record imported/destination ref object IDs,
Git integrity, author inventory, and explicit verification limits. Report
failures prevent completion. Resume clears the previous report; older jobs
without a report return null.

Cancellation immediately stops a queued job without starting commands. For
an active job it transitions to `cancelling` until child processes stop, then
`failed` with its last completed checkpoint and frozen duration. Cancellation
is rejected during destructive in-place history cleanup. No remote rollback
or arbitrary-percent resume is provided; do not change the workspace while stopping.

Migration details include `id`, source/destination, `status`, `progress`,
`currentStep`, `logs`, timestamps, `error`, `cleanHistory`,
`lastCompletedStage`, and `canResume`. Status values are `pending`,
`analyzing`, `mapping_authors`, `migrating`, `cleaning`, `pushing`,
`cancelling`, `completed`, and `failed`. Percentages are workflow milestones, not a
live per-revision completion estimate.
`elapsedSeconds` is nullable cumulative recorded processing time, excluding
queue/retry/offline waits. `runningSince` is nullable UTC timing-sample start;
the API's elapsed value already includes time up to the response snapshot.
Do not add that interval again on the client. Timing stops on terminal
status and resumes accumulating on queue pickup. Older records have null
timing; abrupt restart retains saved seconds, not downtime. Samples normally
persist every 10 seconds and at workflow updates.

Resume request shape:

```json
{
  "githubToken": "<fresh token supplied by the user>"
}
```

Do not save a real token in a request example, script, or command history.

| Resume result | HTTP status |
|---|---|
| Queued, with migration details and status URL | 202 Accepted |
| Missing/blank token | 422 Unprocessable Entity |
| Unknown migration | 404 Not Found |
| Migration is not failed | 409 Conflict |
| Queue unavailable | 503 Service Unavailable |

Checkpoint values are `none`, `analysis`, `authors`, `import`,
`history_cleanup`, and `github_setup`. Completed stages are skipped;
incomplete stages repeat. Required saved workspace files must still exist.
The API does not automatically resume jobs after restart and does not force
push divergent remote refs. See [Recovery](../MIGRATION_GUIDE.md#failure-and-resume).

## Data and limitations

- The API reuses `data/local_migrations.sqlite3` and the existing
  `migration_state` table, preserving migration history and author maps.
- GitHub tokens are memory-only and omitted from SQLite records; cleared after
  a queue attempt. Restarted active/queued jobs are marked failed/interrupted,
  retaining their checkpoint for manual Resume with a fresh token.
- Jobs are processed one at a time. Cancellation is supported outside destructive
  history cleanup; wait for the stopped job before resuming.
- The service binds to loopback and has no authentication; do not expose it to
  a network.
- SVN, Git with `git-svn`, GitHub CLI, and optionally `git-filter-repo` remain
  external command dependencies.
- Source defaults: repository `data` and `temp`; portable defaults:
  `%LOCALAPPDATA%\SVN2GitHub\data` and `workspaces`.
  `SVN2GITHUB_DATA_DIRECTORY` and `SVN2GITHUB_WORKSPACE_DIRECTORY` override
  API storage paths. Portable WPF sets these from
  `SVN2GITHUB_PORTABLE_DATA_ROOT` or the default per-user root.
- DELETE returns 404 for unknown jobs, 400 for terminal jobs, or 501 for
  nonterminal jobs. It neither safely cancels work nor deletes history.
- AI provider keys and chat history belong to WPF and have no API routes.
  For distribution details, see [Portable package](../PORTABLE_PACKAGE.md).
