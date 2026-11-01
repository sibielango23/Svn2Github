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
- Portable startup selects an available loopback port and sets
  `SVN2GITHUB_API_URL`. Respect configured data/workspace locations; see
  [portable guide](../PORTABLE_PACKAGE.md).

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
- Use Material Design 3 brush keys; keep fixed wizard navigation aligned and
  dashboard actions usable with the right-side AI panel open.
- AI keys/chats are Windows-user encrypted; clear-key retains model/provider
  and chats, and does not revoke a provider key or cancel in-flight requests.
- Copilot task drafts and snapshot refresh stay local; only Send contacts an
  AI provider. Never execute AI-proposed repository actions automatically.
- Readiness is local tooling/storage validation, not remote permission or
  author-coverage verification. Reports redact known secrets but are not
  immutable audit or guaranteed confidential-data sanitization.

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
- Resume is manual and stage-based: skip only durable completed checkpoints,
  require a fresh GitHub token, and validate saved workspace artifacts.
  An interrupted SVN import must repeat; do not promise arbitrary-percent,
  revision-level, or automatic resume. Never force-overwrite remote refs.

## Validation

Build both projects with:

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj --configuration Debug
dotnet test wpf\SVN2GitHub.Tests\SVN2GitHub.Tests.csproj --configuration Debug
```

Run focused tests or API smoke checks for changed behavior. A real migration is
an environment-dependent acceptance test and must not be claimed unless it was
actually performed.
Portable source support is not proof that an existing ZIP contains new
changes; publish and smoke-test fresh binaries before claiming inclusion.
