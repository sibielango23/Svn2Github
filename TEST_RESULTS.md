# Validation Results

## Verified locally

- Full .NET solution build, including the WPF client, ASP.NET Core API, and
  backend test project.
- Backend automated tests cover SVN log author parsing, local `git-svn` ref
  conversion, temporary author-session mapping, SQLite recovery, and
  GitHub-token exclusion from persisted records.
- HTTP smoke checks cover health, existing SQLite migration loading,
  statistics, legacy author round-trip, temporary author create/read/close,
  and rejection of SVN URLs containing embedded credentials.
- PowerShell launch scripts pass parser validation.

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

The application is suitable for a controlled local pilot after the external
tool prerequisites are installed. It is not a hardened production or
multi-user service: the API has no authentication, jobs cannot safely be
cancelled or resumed, and the API must remain bound to loopback.
