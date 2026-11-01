# Setup

## Choose a deployment

- **Portable user:** obtain a freshly published Windows x64 package, extract
  the entire ZIP, and double-click `SVN2GitHub.exe`. No SDK, runtime, or tool
  installation is required for the bundled workflow. History cleanup is an
  exception; see [Portable package](PORTABLE_PACKAGE.md).
- **Source/development user:** install the prerequisites below and use the
  repository launch scripts.

The existing ZIP predates the current source changes. Rebuild it before
expecting the redesigned dashboard, chat history, clear-key action, or resume.

## Install source/build prerequisites

1. Install the .NET 8 SDK.
2. Install Git for Windows and ensure `git svn --version` works.
3. Install the Subversion command-line client and check `svn --version`.
4. Install GitHub CLI and check `gh --version`. The migration form requires a
   GitHub token; a separate `gh auth login` is needed only for CLI usage that
   relies on your normal user authentication configuration.
5. Prepare a GitHub token authorized to create and push to the target
   repository. Organization policy may require an approved token.
6. If history cleaning is enabled, install `git-filter-repo` and verify
   `git filter-repo --version`. This optional third-party program may require
   Python independently of this repository.

## Build

From the repository root:

```powershell
dotnet restore wpf\SVN2GitHub.sln
dotnet build wpf\SVN2GitHub.sln --configuration Debug
```

## Run

Start the API and WPF application together:

```powershell
.\start.ps1
```

To run only the API:

```powershell
.\start-api.ps1
```

The API listens on `127.0.0.1:5000`. It has no authentication and must not be
exposed to a network.
Close the running WPF app before rebuilding. Keep the launcher window open
while using the API it started; do not stop either process during migration.
Portable startup uses an available loopback port instead of a fixed port.

## Local data

Migration records and author mappings are stored at
`data/local_migrations.sqlite3`; workspaces are stored under
`temp\workspace_<migration-id>`. Back up
the database and any workspaces that matter before changing or deleting them.
GitHub tokens are held in memory only and are not written to the database.
Unfinished jobs are marked failed/interrupted on backend restart. Resume is
manual: select **Resume** on the dashboard and provide a fresh GitHub token.
Completed stages are reused only while their workspace artifacts remain
available. Interrupted SVN import must repeat; see [Recovery](MIGRATION_GUIDE.md#failure-and-resume).

Portable API data defaults to `%LOCALAPPDATA%\SVN2GitHub\data` and workspaces
to `%LOCALAPPDATA%\SVN2GitHub\workspaces`. Source and portable databases are
not automatically merged. Back up both locations if you use both modes.

## AI assistant

AI support is optional and configured in the WPF app. Provider keys are
protected for the current Windows user with DPAPI. Review the context preview
before sending a request to OpenAI or Anthropic. Configure **AI settings**
with the provider, an available model ID, and API key. Open the right-side
assistant from the shell or a migration row. Enter sends; Shift+Enter adds a
newline. Saved conversations can be selected and continued.

Use **AI settings > Clear saved key** and confirm to remove the local key;
provider/model preferences and chat history are retained. A new key is
required for future chats. Clearing does not revoke the provider key or
cancel an in-flight request. AI responses do not run migration commands.

AI files always stay under `%LOCALAPPDATA%\SVN2GitHub` for the Windows user,
even with `SVN2GITHUB_PORTABLE_DATA_ROOT`. See the
[portable data guidance](PORTABLE_PACKAGE.md#data-and-moving-the-app).

## Readiness and support

Use dashboard **Check readiness**, or the same action in the wizard's Review
step, before a long import. This verifies local tool versions, writable
database/workspace directories, and available workspace volume space.
The 5 GiB caution threshold is not an estimate of the repository's needs.
Cleanup checks include `git-filter-repo` only when requested from the wizard.
Local checks do not contact SVN/GitHub, validate credentials, prove permission
to create repositories, or cover all historical authors.

Job **Details** opens full searchable recorded logs and checkpoint recovery
guidance, with copy/export of a support report. Review confidential metadata
and logs before sharing; automatic redaction is best-effort.
The [Copilot guide](docs/AI_COPILOT_GUIDE.md) explains guided task drafts and
local snapshot refresh. Neither calls AI until the user presses Send.
