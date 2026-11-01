# WPF and API Test Plan

## Automated validation

Run all commands below from the repository root.

The backend tests are in `backend/SVN2GitHub.Api.Tests` and run with:

```powershell
dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj --configuration Debug
dotnet test wpf\SVN2GitHub.Tests\SVN2GitHub.Tests.csproj --configuration Debug
```

The solution build compiles both the WPF application and API:

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
```

## Manual WPF acceptance

- Start with `.\start.ps1` from the repository root.
- Confirm dashboard statistics and migration list load from the local API.
- Click each summary card and combine its filter with case-insensitive
  GitHub destination/SVN URL search. Check counts, Reset filters, and the
  distinct no-history/no-match states. Verify jobs older than ten are reachable.
- Confirm live updates retain the active filter/search and do not flash/reset
  the whole list; API outages retain data and show retry/stale status.
- Use the dashboard's New migration action. Verify source/destination/token
  validation, Back/Next behavior, and Start migration on step 3.
- Open AI chat and resize the app. On every wizard step, confirm Back and
  Next/Start have equal height and remain separated and visible below the
  scrollable form, even with long validation text. Verify active step colors.
- Open author mapping, extract authors from a disposable SVN repository, edit,
  save, export, and confirm the mapping is included in the migration.
- Submit only a disposable test migration and confirm status/log progression.
- Verify final branch/tag names and object IDs on the test GitHub repository.
- Confirm the right-side AI assistant sends only on Send/Enter; Shift+Enter
  adds a newline. There is no separate acknowledgment checkbox.
- Verify auto-scroll, saved-chat reopening/continuation, deletion, and
  conversation switching during a pending reply. The reply must remain in
  the initiating chat; deletion stays disabled while busy.
- Save/replace a test AI key; clear it with confirmation, then close settings.
  Check provider/model and chat history remain, while future chat requires a
  new key. Clearing must not be described as provider revocation or cancellation.
- After a disposable migration fails, confirm Resume displays its checkpoint,
  requires a fresh token, and continues at the next incomplete stage.
- Run readiness with/without cleanup. Missing commands must be failed checks,
  storage problems explicit, and unknown remote permissions a warning. A 200
  report response must not be interpreted as readiness success.
- Confirm conversion time updates on a running job even when progress/step
  stays unchanged, stops on terminal status, and resumes accumulating without
  counting queue/retry waits. Old jobs must show not recorded, not zero.
  Abrupt restart must keep saved timing without counting offline time.
- Open Details, filter recorded logs case-insensitively, copy/export the full
  report, and verify filtering does not mutate/export-truncate the logs.
  Check secret redaction, confidentiality warning, and file/clipboard errors.
- Draft each copilot task; no message/provider call must occur until Send.
  Refresh a migration snapshot while the job changes, ensure it uses the
  initiating chat and resets optional logs, and check stale-context errors.
- Reopen a legacy chat: preserve messages but use current Resume capability
  guidance; a chat without a migration ID must not pretend to refresh live data.

## Environment-dependent acceptance

A real SVN-to-GitHub migration has not yet been validated. Use disposable
repositories and a non-production token. Test history cleaning separately
because it rewrites commits.
Inject failures at different stages: an interrupted import must repeat,
whereas a failed late push must reuse the saved import/setup. Check divergent
remote refs fail without force-overwrite and missing workspace artifacts
fail explicitly. These real migration scenarios are not yet acceptance-tested.

## Portable acceptance

- Publish to a fresh output folder using the
  [portable guide](../PORTABLE_PACKAGE.md). Do not overwrite the older ZIP.
- On Windows x64, extract the complete folder/ZIP and launch the EXE without
  installing .NET or the bundled CLI tools. Verify bundled versions execute.
- Confirm WPF starts its owned API only on loopback at an available port and
  closes it when WPF exits while idle.
- Verify database/workspaces default to per-user `data`/`workspaces`; test a
  writable `SVN2GITHUB_PORTABLE_DATA_ROOT`. AI files must remain per-user.
- Back up/restore database and workspaces together. Do not assume copying
  DPAPI-encrypted AI files to another identity makes them readable.
- Verify latest UI, clear-key, chat history, and Resume are in the new package.
  The earlier smoke-tested ZIP predates these changes.
- Optional `git-filter-repo` is not bundled; test cleaning separately with
  its requirements installed.

## Limitations to verify

- The API stays bound to loopback.
- Interrupted jobs do not automatically resume after restart; manual
  checkpoint resume is available and requires preserved artifacts.
- Cancel queued and running disposable jobs; no commands may start for a
  cancelled queued job, and active processes must stop before Resume.
  Verify destructive history cleanup cannot be user-cancelled, completed
  checkpoints remain, token is cleared, duration stops, and the next queued
  job still runs. No remote rollback may be implied.
- In wizard Review, run confirmed remote assessment against disposable
  repositories. Test missing mappings, layout errors, unconfirmed identities,
  current-trunk files above 100 MiB, invalid tokens, and absent destinations.
  Copy/export results; no repository creation/import/push may occur.
- Inspect Details > Verification and exported support reports. Mismatched or
  extra refs and Git integrity failures must prevent completion; older reports
  must remain explicitly unavailable, not a successful verification.
- The AI feature is optional and its provider calls run from the WPF client.
