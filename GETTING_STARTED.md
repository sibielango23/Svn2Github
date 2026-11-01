# Getting Started

## 1. Prepare the machine

Choose source or portable deployment in [SETUP.md](SETUP.md).
Portable users extract the complete current package and double-click
`SVN2GitHub.exe`; it launches the API and uses bundled tools.
Source users install the prerequisites. For an actual source-mode migration,
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

1. Click **New migration** on the dashboard. In **Source**, enter the SVN URL
   and layout, then **Next**.
2. In **Destination**, enter the GitHub owner, repository name, and token,
   then **Next**. New destinations are created private by the backend.
3. In **Review**, extract and review author mappings; correct names and email
   addresses. Choose whether to clean history. Cleaning rewrites commit IDs and is
   irreversible for the migrated copy.
4. Use **Back** to correct previous steps, or **Start migration** and confirm
   to queue the job. Back/Next stay below the scrollable form.
5. Watch the dashboard. Click a summary card to filter queued/running,
   completed, or failed jobs. Search GitHub destinations or SVN URLs and use
   **Reset filters** to see all jobs. Open **Details** for recent logs.
6. Verify the final remote branches and tags. **Resume** is available on
   failed jobs; it requires a fresh token and reuses completed stages.

## 4. Optional AI help

Open **AI settings** and configure OpenAI or Anthropic with a model ID
available to your account. Use **Ask AI assistant** for general product
questions or a job's **Ask AI** action for migration help. Review displayed
context; enable logs only when sharing them is permitted. Enter sends,
Shift+Enter adds a newline. Use chat history to reopen previous conversations.
**Clear saved key** removes the local credential, not chats or the key in
your provider account.
Expand guided tasks to draft Diagnose, Recovery, Readiness, or Handover.
The local checkpoint advice is not AI-generated; Refresh migration snapshot
updates job context from the API without sharing it. Drafts require Send.
See [Copilot guide](docs/AI_COPILOT_GUIDE.md).

Before starting, run **Check readiness** on the dashboard or wizard Review
step. Resolve local failed checks, then independently verify remote access,
permissions, and authors. Use **Details** to search full recorded logs and
copy/export a support report if help is needed. Review confidential
information before sharing reports.

## 5. Know the current limitations

- The API is a local, single-user pilot and must remain on loopback.
- Cancel stops queued/running work outside destructive history cleanup; it
  does not roll back remote changes. Restarted jobs require manual
  stage-based Resume; an incomplete SVN import must run again.
- The migration workflow depends on external SVN, Git, and GitHub CLI tools.
- Real external migration acceptance testing is still required.
- The existing portable ZIP must be rebuilt to include the latest source
  features. Portable mode does not bundle optional `git-filter-repo`/Python.

See the [migration guide](MIGRATION_GUIDE.md) for preflight and recovery
guidance.
