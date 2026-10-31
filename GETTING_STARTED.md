# Getting Started

## 1. Prepare the machine

Install the prerequisites in [SETUP.md](SETUP.md). For an actual migration,
verify `git svn --version`, `svn --version`, and `gh --version` before opening
the application.

## 2. Build and launch

From PowerShell at the repository root:

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
.\start.ps1
```

The launcher starts the local .NET API at `http://127.0.0.1:5000`, checks its
health endpoint, and opens the WPF application.

## 3. Prepare a safe test migration

Use a disposable SVN repository and a GitHub repository created specifically
for testing. Confirm that the token has the required access, review the target
repository visibility, and check available disk space before starting. Do not
use a production repository for the first end-to-end test.

In the WPF app:

1. Enter the SVN URL and GitHub destination.
2. Extract and review author mappings; correct names and email addresses.
3. Choose whether to clean history. Cleaning rewrites commit IDs and is
   irreversible for the migrated copy.
4. Review the migration details and submit the job.
5. Watch the dashboard and verify the final remote branches and tags.

## 4. Know the current limitations

- The API is a local, single-user pilot and must remain on loopback.
- Running jobs cannot currently be cancelled safely or resumed after a
  backend restart.
- The migration workflow depends on external SVN, Git, and GitHub CLI tools.
- Real external migration acceptance testing is still required.

See the [migration guide](MIGRATION_GUIDE.md) for preflight and recovery
guidance.
