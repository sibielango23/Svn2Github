# Portable Windows package

The portable package runs the WPF desktop client and its .NET API without
installing the .NET runtime. It also bundles Git for Windows with `git-svn`,
the Subversion CLI, and GitHub CLI. The app starts the bundled API on an
available loopback port and stops it when the desktop app closes.

## Current distribution status

Portable support is implemented in source and an earlier folder/ZIP was built
and startup-smoke-tested. The existing
`artifacts\SVN2GitHub-portable-win-x64.zip` predates the latest chat history,
live dashboard, checkpoint resume, clear-AI-key action, and dashboard/wizard
redesign. It has **not** been rebuilt with these changes. Publishing a fresh
package is required; updated documentation alone does not update binaries.

## Build the package

On a Windows x64 build machine with the .NET 8 SDK, prepare these tools:

- An extracted PortableGit distribution that contains `cmd\git.exe` and
  `usr\bin\perl.exe`. Use an official Git for Windows release and retain its
  license and notice files.
- An extracted portable SVN CLI distribution with `bin\svn.exe` and its
  required DLLs. Retain the SVN distribution's license and notices.
- An extracted official GitHub CLI release with `bin\gh.exe` and `LICENSE`
  beside the `bin` folder. The script requires and copies that license.

Then run from the repository root:

```powershell
.\scripts\Publish-Portable.ps1 `
  -GitPortablePath "C:\Tools\PortableGit" `
  -SvnPortablePath "C:\Tools\SvnPortable" `
  -GhExecutablePath "C:\Tools\gh\bin\gh.exe" `
  -CreateZip
```

The script publishes self-contained `win-x64` app and API executables, copies
the tool distributions into the package, checks that `git svn`, `svn`, and
`gh` execute, and optionally creates a ZIP. It will not overwrite an existing
output directory or ZIP. To retain the earlier package and publish current
source, choose a fresh output path:

```powershell
.\scripts\Publish-Portable.ps1 `
  -GitPortablePath "C:\Tools\PortableGit" `
  -SvnPortablePath "C:\Tools\SvnPortable" `
  -GhExecutablePath "C:\Tools\gh\bin\gh.exe" `
  -OutputDirectory "D:\Packages\SVN2GitHub-portable-current" `
  -CreateZip
```

The output contains `SVN2GitHub.exe`, a `backend` folder with
`SVN2GitHub.Api.exe`, and `tools\git`, `tools\svn`, and `tools\gh`.
It includes `PORTABLE-README.md`, `THIRD-PARTY-NOTICES.txt`, and
`tools\VERSIONS.txt`. The script validates command execution and required
files, but does not run a real migration or UI acceptance test.
Review and comply with each bundled tool's license before redistribution.

## Use

Extract the ZIP and double-click `SVN2GitHub.exe`. The app and API must remain
in the same extracted folder structure. No administrator rights or .NET
installation is needed.

The current source UI includes clickable dashboard status filters, repository
search, live progress, Details/Ask AI/Resume actions, and a three-step wizard
with a fixed Back/Next footer. The AI assistant remains docked on the right:
Enter sends, Shift+Enter adds a newline, and saved chats can be reopened.
New current-source features also include local **Check readiness**, searchable
diagnostics and support-report copy/export, and a migration copilot with
guided diagnosis/recovery/readiness/handover drafts, local checkpoint advice,
and local snapshot refresh. These are not in the earlier ZIP.
See [Copilot guide](docs/AI_COPILOT_GUIDE.md).
Current source also records live/final conversion duration across attempts,
excluding queue/offline waits; preserve the database for duration history.
The earlier ZIP does not include this timing feature either.
The current source adds confirmed read-only remote assessment, persisted
verification reports, and Cancel outside destructive history cleanup.
These also require a fresh package publish; the existing ZIP is unchanged.

## Data and moving the app

Migration database and working directories are written to
`%LOCALAPPDATA%\SVN2GitHub`, rather than the package folder:

| Data | Default portable location |
|---|---|
| API database and author maps | `%LOCALAPPDATA%\SVN2GitHub\data\local_migrations.sqlite3` |
| Job workspace | `%LOCALAPPDATA%\SVN2GitHub\workspaces\workspace_<migration-id>` |
| AI provider/model and encrypted key | `%LOCALAPPDATA%\SVN2GitHub\ai-settings.json` |
| Encrypted chat conversations | `%LOCALAPPDATA%\SVN2GitHub\ai-chat-history.dat` |

The app is no-install, but not stateless and not fully confined to its
extracted folder. Moving only the package does not move migration history.
Back up the database and complete workspaces while the app/API is stopped;
restore them together for checkpoint reuse. Do not share one writable data
root between concurrently running instances.

AI encryption is scoped to the Windows user. Copying the encrypted AI files
to another PC/user does not guarantee they can be decrypted. Reconfigure the
AI API key on the destination; chat transfer across Windows identities is not
implemented.

For managed portable deployments, set `SVN2GITHUB_PORTABLE_DATA_ROOT` to a
writable folder before launching the app to relocate the API database and
migration workspaces. AI settings and chat history continue to use Windows
per-user application data.

```powershell
$env:SVN2GITHUB_PORTABLE_DATA_ROOT = "D:\SVN2GitHubData"
& "D:\Packages\SVN2GitHub-portable-current\SVN2GitHub.exe"
```

Source mode uses repository `data` and `temp` by default and does not
automatically merge with portable data. The migration form and Resume dialog
require a GitHub token, passed to tooling through process environment
variables. Normal `gh auth login` is only needed for independent CLI use that
relies on per-user authentication. Never embed credentials in SVN URLs.

Use **AI settings > Clear saved key** to remove the saved local AI key.
Provider/model and chats remain. It does not revoke the provider key or
cancel a request already sent to the provider.

## Verify a fresh package

1. Extract into a fresh writable folder, then double-click `SVN2GitHub.exe`.
2. Confirm the dashboard loads and the owned API binds only to loopback;
   the chosen port varies and is not necessarily 5000.
3. Test bundled `git svn --version`, `svn --version`, and `gh --version`
   using the packaged executables/PATH, not just tools installed on the PC.
4. Check filters/search, wizard Back/Next alignment with the AI panel open,
   and AI save/clear/reopen behavior with a test provider account.
   Run readiness with bundled tooling and verify report export and guided
   drafts. Readiness must not contact repositories; drafts/refresh must not
   invoke an AI provider before Send.
5. Close the app while idle and verify its owned API exits.
6. Use disposable SVN/GitHub repositories for migration, injected failure,
   and Resume acceptance. Never use production history for this test.

See [Test plan](wpf/TEST_PLAN.md) and [Validation results](TEST_RESULTS.md).
An earlier startup smoke check is not acceptance evidence for new binaries.

## Boundaries

- The API binds to loopback only. Do not expose it to a network.
- Closing the app stops its bundled API; avoid closing it during a migration.
  If the app or backend stops, the dashboard offers Resume for failed jobs.
  Completed stage checkpoints are reused, and a fresh GitHub token is required
  because tokens are never saved in migration history. A failure during
  `git-svn` import repeats that import, which may take hours; a failed push
  resumes at push and retries branch/tag updates without force-pushing.
- History cleaning with `git-filter-repo` remains optional and may require a
  separate Python installation; it is not included in this package yet.
- This is a folder/ZIP deployment, not a single-file executable. Windows
  SmartScreen may show a warning for unsigned downloads.
