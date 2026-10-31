# SVN2GitHub Repository Instructions

## Project architecture

- Desktop client: WPF on .NET 8, using CommunityToolkit.Mvvm and Material
  Design.
- Backend: ASP.NET Core minimal API in `backend/SVN2GitHub.Api`.
- Persistence: Microsoft.Data.Sqlite, reusing
  `data/local_migrations.sqlite3`.
- Migration workflow: C# services calling `svn`, `git`/`git-svn`, and `gh`
  through `ExternalCommandRunner`.
- The WPF API base URL is `http://127.0.0.1:5000`; preserve its camelCase JSON
  contract.

## C# conventions

- Use PascalCase for public types and members, camelCase for parameters and
  locals, and `_camelCase` for private fields.
- Use nullable reference types and explicit input validation.
- Use async/await for I/O and pass CancellationToken through process and file
  operations.
- Preserve MVVM: views handle UI concerns, view models own presentation state,
  services handle HTTP and provider calls.
- Use existing dependency injection, logging, DTOs, and validation patterns.

## WPF conventions

- Bind controls to view-model properties/commands.
- Use Material Design resources and existing localization patterns.
- Keep code-behind limited to view-specific interaction.
- Surface API failures to the user; do not convert failures into success-shaped
  empty results.

## API and migration safety

- Keep the API bound to loopback; it has no authentication.
- Never persist GitHub tokens, place them in command-line arguments, or log
  them. Pass secrets to child processes through environment variables and
  redact them from errors.
- Reject SVN URLs containing embedded credentials.
- Do not run external migrations in tests unless the test uses isolated,
  disposable SVN/GitHub repositories.
- Treat history cleaning as destructive because it rewrites commits.
- SQLite migration records and existing local user data must be preserved.
- An interrupted migration cannot be resumed safely; do not imply otherwise.

## Validation

Build both projects with:

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
```

Run focused tests or API smoke checks for changed behavior. A real migration is
an environment-dependent acceptance test and must not be claimed unless it was
actually performed.
