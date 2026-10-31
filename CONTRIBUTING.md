# Contributing

## Development setup

- Install the .NET 8 SDK.
- Restore and build from the repository root:

  ```powershell
  dotnet restore wpf\SVN2GitHub.sln
  dotnet build wpf\SVN2GitHub.sln --configuration Debug
  ```

- For migration integration testing, install Git with `git-svn`, the SVN CLI,
  and GitHub CLI. Use disposable repositories and tokens.

## Project layout

- `wpf/SVN2GitHub/`: WPF views, view models, models, and HTTP client.
- `backend/SVN2GitHub.Api/`: ASP.NET Core API, SQLite store, process runner,
  migration queue, and workflow services.
- `docs/`: user and author mapping guides.

## Implementation conventions

- Preserve the WPF/API camelCase JSON contract and existing endpoint paths.
- Use async APIs and pass cancellation tokens to I/O and child-process calls.
- Never persist GitHub tokens or include them in logs/errors.
- Keep the API loopback-only unless authentication and transport security are
  implemented and reviewed.
- Treat destructive history rewriting and external repository operations as
  opt-in; do not run them against a user's repository during tests.
- Add focused tests or reproducible smoke checks for behavior changes.

Before submitting changes, build the full solution, run relevant tests, and
review `git diff --check`.
