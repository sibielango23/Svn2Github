# Setup

## Install prerequisites

1. Install the .NET 8 SDK.
2. Install Git for Windows and ensure `git svn --version` works.
3. Install the Subversion command-line client and check `svn --version`.
4. Install GitHub CLI and authenticate it with `gh auth login`.
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

## Local data

Migration records and author mappings are stored at
`data/local_migrations.sqlite3`; workspaces are stored under `temp/`. Back up
the database and any workspaces that matter before changing or deleting them.
GitHub tokens are held in memory only and are not written to the database.
Unfinished jobs are marked interrupted on backend restart and cannot resume
automatically.

## AI assistant

AI support is optional and configured in the WPF app. Provider keys are
protected for the current Windows user with DPAPI. Review the context preview
before approving any request to OpenAI or Anthropic. AI responses are
suggestions; they do not run migration commands.
