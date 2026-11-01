# Validation Results

## git-svn URL minimization fix validation

- Build passed with zero warnings/errors; **65 tests passed** (42 API,
  23 desktop):

  ```powershell
  dotnet build wpf\SVN2GitHub.sln --configuration Debug -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\git-svn-fix-validation\
  dotnet test wpf\SVN2GitHub.sln --configuration Debug --no-build -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\git-svn-fix-validation\
  ```

- Actual git-svn clone against a disposable loopback SVN server passed:

  ```powershell
  git svn clone --no-minimize-url --stdlayout --authors-file D:\Svn2Github\temp\git-svn-space-repro\authors.txt --no-metadata svn://127.0.0.1:3692/Source%20Code D:\Svn2Github\temp\git-svn-space-repro\fixed-import
  ```

  Six revisions were imported with trunk, feature-demo and v1.0 remote refs.
  Stored SVN URL remained `%20` (not `%2520`), fetch mappings were valid,
  imported settings content matched trunk/branch/tag expectations, and
  `git fsck --full` passed.
- Tests assert `--no-minimize-url` for standard and custom layouts while
  preserving encoded URLs. No company SVN server or GitHub push was accessed.
  This verifies a local git-svn import, not full enterprise acceptance.
- The previously uploaded URL-spaces DLL patch predates this additional fix.

## SVN URL spaces validation

- Build passed with zero warnings/errors; **63 tests passed** (40 API,
  23 desktop), using:

  ```powershell
  dotnet build wpf\SVN2GitHub.sln --configuration Debug -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\svn-url-validation\
  dotnet test wpf\SVN2GitHub.sln --configuration Debug --no-build -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\svn-url-validation\
  ```

- Tests cover raw/encoded spaces, idempotence (no `%2520` double encoding),
  escaped path separators, unsupported filesystem/backslash inputs,
  credentials, query/fragment rejection, command argument shape, and WPF
  navigation/author-mapping association.
- Real disposable SVN server checks on loopback port 3691 passed for
  `svn://127.0.0.1:3691/Source%20Code`: info, full visible log, and trunk list.
  Temporary server/state were cleaned; existing sample/user data preserved.
- No GitHub operation or full git-svn migration was performed. The published
  `portable-f7dd04b` ZIP predates this change.

## Existing GitHub destination validation

- Solution build passed with zero warnings/errors; 48 tests passed (27 API,
  21 desktop) using `dotnet build wpf\SVN2GitHub.sln --configuration Debug
  -m:1 -t:Rebuild -p:UseAppHost=false
  -p:OutputPath=D:\Svn2Github\temp\existing-destination-validation\`, followed
  by `dotnet test wpf\SVN2GitHub.sln --configuration Debug --no-build -m:1
  -p:UseAppHost=false
  -p:OutputPath=D:\Svn2Github\temp\existing-destination-validation\`.
- Fake command tests prove existing accessible repositories skip creation and
  settings edits, missing repositories attempt private creation, and unexpected
  lookup failures are surfaced without creation. No real GitHub push was made.

## Migration safety milestone validation

- Build passed with **0 warnings/errors**; **45/45 tests passed**
  (24 API, 21 desktop):

  ```powershell
  dotnet build wpf\SVN2GitHub.sln --configuration Debug -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\safety-validation\
  dotnet test wpf\SVN2GitHub.sln --configuration Debug --no-build -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\safety-validation\
  ```

- Fake-runner assessment tests verify read-only commands, tokens absent from
  arguments/SVN environment, visible-history author coverage, and the exact
  current-trunk threshold: 100 MiB warns; 100 MiB + 1 byte fails.
- Verification tests cover full local ref names, branch/tag object equality
  and mismatch evidence, empty imports, Git integrity failure, persistence
  across restart, and report reset on Resume. Fixed the directly related
  local-ref parsing bug and propagated token environment to remote ref reads.
- Cancellation tests cover queued jobs, stale queue entries after Resume,
  active-token propagation, retained checkpoints/timing, prevention of Resume
  during process draining, continued queue processing, cleanup rejection,
  queued-token cleanup on shutdown, and no start for pre-cancelled commands.
  A disposable sleeping PowerShell process was cancelled; no migration
  repositories were used.
- Existing WPF layout/chat regression tests passed, including upgraded older
  saved chats using current cancellation/assessment/verification guidance.
- Isolated API smoke passed on loopback port 5037 with disposable storage:
  healthy API, bundled local readiness, camelCase report shape, assessment
  validation HTTP 422, missing-job cancellation HTTP 404, and unchanged `[]`
  migration state. No SVN/GitHub/AI network operation or real migration was
  performed. The portable ZIP was not rebuilt.
- Destructive in-place history cleanup deliberately rejects user cancellation;
  this milestone does not claim atomic cleanup recovery, full SVN parity,
  production cutover certification, or enterprise security certification.

## First-message chat regression validation

- Reproduced the first-send bug with the real bound WPF chat controls: renaming
  and moving/resetting the conversation list cleared `SelectedConversation`
  to null. The regression assertion failed before the fix and passed after
  removing the unnecessary collection changes (titles already notify bindings).
- Build passed with **0 warnings/errors**; **33/33 tests passed**
  (13 API, 20 desktop):

  ```powershell
  dotnet build wpf\SVN2GitHub.sln --configuration Debug -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\chat-fix-validation\
  dotnet test wpf\SVN2GitHub.sln --configuration Debug --no-build -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\chat-fix-validation\
  ```

- Local HTTP fakes verify immediate question visibility while waiting, replies
  routed to the initiating chat after switching, visible/persisted failed turns,
  retry filtering, HTTP errors, timeouts, and empty provider replies. The WPF
  test verifies selected-chat identity and rendered reply text on first send.
- No actual AI provider request was made; live credentials/provider availability
  remain user acceptance checks. The portable ZIP has not been rebuilt.

## Conversion timing validation

- Solution build passed with **0 warnings/errors**; **28/28 tests passed**
  (13 API, 15 desktop):

  ```powershell
  dotnet build wpf\SVN2GitHub.sln --configuration Debug -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\conversion-timing-build\
  dotnet test wpf\SVN2GitHub.sln --configuration Debug --no-build -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\conversion-timing-build\
  ```

- Deterministic-clock tests verified live timing, frozen terminal duration,
  accumulated retries, excluded queue/retry waits, persistence, and unclean
  restart recovery without counting downtime. Display tests verify hours/day
  formatting and unknown historical timing rather than false zero duration.
- Timing is sampled during processing (normally every 10 seconds) and on
  workflow updates. Abrupt shutdown loses only unsaved timing; after storage
  write failures, sampling retries and logs the failure explicitly.
- Diff checks and portable publisher parser validation passed. No real
  external migration or updated portable ZIP was produced for this change.

## Product improvements validation: 2026-10-31

- Readiness, diagnostics, and guided copilot compiled successfully with zero
  warnings/errors in a dedicated build.
- Backend suite: **11/11 passed**, including local-only readiness commands,
  optional cleanup checks, blocked storage, cleanup of probes, and propagated
  cancellation.
- Desktop suite: **11/11 passed**, including support-report secret redaction,
  all recorded logs retained, checkpoint advice, log filtering, encrypted-key
  clearing/preferences, guided drafts with no provider call, legacy-chat
  capability upgrade, and permanent STA/WPF dashboard/wizard/copilot layout
  checks at 400/760/1100 pixels.
  Snapshot-refresh tests also verify updated checkpoint context with logs
  disabled and retention of old context with explicit staleness on API error.

  ```powershell
  dotnet test wpf\SVN2GitHub.sln --configuration Debug -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\product-tests\
  ```

  Tests passed. A shared-output dependency-copy warning occurred while the
  API test host and WPF build overlapped. Use separate outputs or build first
  and then run tests with `--no-build` to avoid this artifact contention.
- Final uncontended validation passed: **0 build warnings/errors, 22/22 tests**:

  ```powershell
  dotnet build wpf\SVN2GitHub.sln --configuration Debug -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\product-final\
  dotnet test wpf\SVN2GitHub.sln --configuration Debug --no-build -m:1 -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\product-final\
  ```

  Both commands passed.
- Isolated live API smoke: health/readiness camelCase report shape passed,
  no probe files remained, and migration list remained exactly `[]`.
  Missing source-PATH SVN/gh correctly appeared as blocking checks; this was
  expected, not converted to a success-shaped readiness result.
- With CLI directories from the earlier portable distribution prepended to
  PATH, the latest readiness API passed Git, git-svn, SVN, and gh checks.
  Remote access/author coverage remained a warning and no job was created.
  This validates new readiness with bundled tools, not a republished package.
- All **30 source Markdown files** passed local-link checks and the portable
  publisher passed PowerShell parser validation. Generated portable README
  text now describes current features and explicit limits.
- No provider request, repository migration, or republished portable package
  acceptance was performed for these product changes.

## Earlier dashboard source validation: 2026-10-31

- Full solution build passed with **0 warnings and 0 errors**:

  ```powershell
  dotnet build wpf\SVN2GitHub.sln --configuration Debug -p:UseAppHost=false -p:OutputPath=D:\Svn2Github\temp\dashboard-ui-final-build\
  ```

- All **6/6 backend tests passed**:

  ```powershell
  dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj --configuration Debug --no-build -p:OutputPath=D:\Svn2Github\temp\dashboard-ui-final-build\
  ```

- An isolated STA/WPF validation harness passed dashboard status filters,
  combined case-insensitive search, SVN URL search, reset/no-match state,
  live item replacement, access to more than ten jobs, and New migration
  navigation. It instantiated the actual WPF views at **400, 760, and
  1100 pixels** and checked all three wizard steps for equal **44-pixel**
  button heights, matching top positions, nonoverlap, footer visibility, and
  Back command behavior with long validation text. Active step colors and
  selected-filter highlighting passed. Narrow-view screenshots were reviewed.
  This was a temporary harness, not a committed permanent test suite.
- `git diff --check` passed.

Isolated outputs were used to avoid running-app executable locks. A concurrent
build attempt hit a shared intermediate-file lock; sequential validation
then passed. Temporary build/harness files were cleaned up. These checks do
not constitute a real external migration or new portable release test.

## Verified locally

- Full .NET solution build, including the WPF client, ASP.NET Core API, and
  backend test project.
- Backend automated tests cover SVN log author parsing, local `git-svn` ref
  conversion, temporary author-session mapping, SQLite recovery, and
  GitHub-token exclusion from persisted records.
- HTTP smoke checks cover health, existing SQLite migration loading,
  statistics, legacy author round-trip, temporary author create/read/close,
  and rejection of SVN URLs containing embedded credentials.
- Resume smoke checks cover health, 422 for a missing token, and 404 for an
  unknown job. Store tests cover checkpoint preservation, restart recovery,
  fresh-token requirements/rejections, and clearing/non-persistence of tokens.
- Clear-AI-key source change built successfully with zero warnings/errors;
  storage and future-chat guards were reviewed. Real provider calls and
  failure cases still require manual acceptance with a test account.
- PowerShell launch scripts pass parser validation.
- An earlier self-contained Windows x64 portable folder/ZIP was built and
  smoke-tested for WPF/API startup, loopback health, bundled Git/git-svn/SVN/gh
  execution, and owned API shutdown. **That ZIP predates the newest chat,
  dashboard, resume, and clear-key changes and has not been republished.**

Run the automated backend tests with:

```powershell
dotnet test backend\SVN2GitHub.Api.Tests\SVN2GitHub.Api.Tests.csproj
```

Build the complete solution with:

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
```

## Not yet verified

- A complete migration against a real SVN server and GitHub repository.
- GitHub token permissions and organization-specific repository policies.
- Long-running behavior on a large SVN history.
- Optional `git-filter-repo` cleanup against a disposable migrated repository.
- Restart behavior during an active external migration beyond local state
  recovery tests.
- Mid-stage failure injection and end-to-end checkpoint Resume against
  disposable SVN/GitHub repositories.
- A newly republished portable package containing the current source changes.
- Cross-PC/user AI chat transfer (not implemented; encryption is user-scoped).

The application is suitable for a controlled local pilot after the external
tool prerequisites are installed. It is not a hardened production or
multi-user service: the API has no authentication, jobs cannot safely be
user-cancelled, and the API must remain bound to loopback. Manual stage-based
resume is implemented; it is not revision-level or automatic resume.
Use [TEST_PLAN.md](wpf/TEST_PLAN.md) and
[PORTABLE_PACKAGE.md](PORTABLE_PACKAGE.md) for outstanding acceptance checks.
