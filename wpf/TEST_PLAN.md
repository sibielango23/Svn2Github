# WPF and API Test Plan

## Automated validation

The backend tests are in `backend/SVN2GitHub.Api.Tests` and run with:

```powershell
dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj
```

The solution build compiles both the WPF application and API:

```powershell
dotnet build SVN2GitHub.sln --configuration Debug
```

## Manual WPF acceptance

- Start with `..\start.ps1` from this directory or `.\start.ps1` from the
  repository root.
- Confirm dashboard statistics and migration list load from the local API.
- Open New Migration and verify URL, destination, and token validation.
- Open author mapping, extract authors from a disposable SVN repository, edit,
  save, export, and confirm the mapping is included in the migration.
- Submit only a disposable test migration and confirm status/log progression.
- Verify final branch/tag names and object IDs on the test GitHub repository.
- Confirm the AI assistant displays its reviewed context and does not send
  anything before explicit approval.

## Environment-dependent acceptance

A real SVN-to-GitHub migration has not yet been validated. Use disposable
repositories and a non-production token. Test history cleaning separately
because it rewrites commits.

## Limitations to verify

- The API stays bound to loopback.
- Interrupted jobs do not resume after restart.
- Safe cancellation is not implemented.
- The AI feature is optional and its provider calls run from the WPF client.
