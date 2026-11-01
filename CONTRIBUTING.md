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
- `scripts/Publish-Portable.ps1`: self-contained Windows x64 packaging and
  bundled-tool validation. See [Portable package](PORTABLE_PACKAGE.md).

## Implementation conventions

- Preserve the WPF/API camelCase JSON contract and existing endpoint paths.
- Use async APIs and pass cancellation tokens to I/O and child-process calls.
- Never persist GitHub tokens or include them in logs/errors.
- Keep the API loopback-only unless authentication and transport security are
  implemented and reviewed.
- Treat destructive history rewriting and external repository operations as
  opt-in; do not run them against a user's repository during tests.
- Add focused tests or reproducible smoke checks for behavior changes.
- Preserve checkpoint ordering and workspace artifacts. Manual Resume skips
  only completed stages, repeats interrupted import, and requires a fresh
  token. Do not introduce automatic or force-push retries.
- Keep AI credentials user-encrypted and separate from migration records.
  Clear-key must not erase chats/model preferences or be advertised as
  revoking a provider key/cancelling in-flight requests.
- Use Material Design 3 resource names; test dashboard and fixed wizard
  navigation in a narrow content area with chat open.
- Keep generated portable README text and root guides aligned with source.
  Rebuild/smoke-test a fresh package before claiming release coverage.

Before submitting changes, build the full solution, run relevant tests, and
review `git diff --check`.
From the repository root:

```powershell
dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj --configuration Debug
dotnet test wpf\SVN2GitHub.Tests\SVN2GitHub.Tests.csproj --configuration Debug
```

Record exact validation commands and results, separating automated/local UI
checks from real migration and portable acceptance. See
[Test plan](wpf/TEST_PLAN.md) and [Validation results](TEST_RESULTS.md).

Readiness must stay local-only, bounded, and explicit about remote permissions
and author-coverage limits. Report export must redact known secrets without
claiming perfect sanitization or immutable audit. Copilot drafts/refresh must
not call providers or execute suggested actions until the explicit Send
boundary (Send calls the provider, not commands).
