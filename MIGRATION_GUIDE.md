# Migration Guide

## Before starting

- Use a disposable destination repository for your first migration.
- Verify the SVN URL is reachable and does not embed credentials.
- Check the SVN layout (trunk, branches, tags) and required tools.
- Ensure the GitHub token can create and push to the destination organization.
- Review author mappings and confirm all required SVN usernames are covered.
- Check disk space for the imported history and temporary repository.
- Back up important local data. The migration database is
  `data/local_migrations.sqlite3` in source mode; each job has a workspace at
  `temp\workspace_<migration-id>`. In portable mode they live under
  `%LOCALAPPDATA%\SVN2GitHub\data` and `workspaces`, respectively.

## Run

Use **Check readiness** on the dashboard or Review step for local tooling,
storage writability, and disk-space checks. This advisory report makes no
SVN/GitHub network calls and is not a permissions, author-coverage, or
capacity guarantee. Missing prerequisites must be fixed; the wizard is not
automatically gated by the report.

1. Source mode: `.\start.ps1`. Portable mode: extract the entire package and
   double-click `SVN2GitHub.exe`. See [Portable package](PORTABLE_PACKAGE.md).
2. Use **New migration**. Step 1 collects the SVN URL and layout; step 2
   collects the GitHub owner, repository, and token. Click **Next** to advance.
   New GitHub destinations are created private; review an existing
   destination's visibility and contents separately.
3. In step 3, extract and correct author mappings before starting. **Back**
   returns to the preceding step without clearing entered values.
4. Enable history cleaning only when required. It removes configured file
   patterns and blobs larger than 100 MiB, and rewrites commit IDs.
5. Review, click **Start migration**, and confirm. Use dashboard filters,
   repository search, and **Details** to monitor progress. Active jobs refresh
   every 2 seconds; idle refresh is every 10 seconds. Percentages describe
   workflow stages, not exact revision counts or remaining time.
6. Confirm the final verification shows matching branch and tag names and
   object IDs on GitHub.

Jobs are processed sequentially. Do not close the app/API or power off during
migration. Portable app shutdown stops its bundled API.

## Failure and resume

Dashboard **Conversion time** shows cumulative recorded processing across
attempts, from queue pickup through verification. It updates with live
dashboard refresh and stops on completion/failure. Queue waits and time
between retries are excluded. Diagnostics and AI snapshots include the same
duration. This is elapsed time, not a forecast of time remaining.

Timing is saved at stage updates and normally every 10 seconds during
processing. An abrupt backend stop retains only the last saved sample, so
the unsaved tail is not counted. Historical jobs without timing are shown
as "not recorded"; resuming an older job records only new processing time.

After failure, fix the reported cause, choose **Resume** on the dashboard,
enter a fresh GitHub token, and confirm the checkpoint/next stage in the
dialog. After a backend restart, previously active/queued jobs are marked
failed rather than automatically started.

| Last completed checkpoint | What a retry reuses | Next work |
|---|---|---|
| None | Original job settings | SVN analysis |
| SVN analysis | Completed analysis | Author mapping |
| Author mapping | Analysis and authors | SVN import |
| SVN import | Saved Git repository | Optional cleanup, then GitHub setup |
| History cleanup | Cleaned Git repository | GitHub setup |
| GitHub setup | Imported/cleaned repository and destination | Push and verification |

This is **stage-based resume**, not arbitrary-percent or revision-level
resume. A failure inside a stage repeats that stage. An interrupted
`git-svn` import discards its incomplete generated `migrated-repo` and
reimports; this may still take hours. A failed late push reuses the import
and retries normal branch/tag pushes, without force-pushing. Remote
divergence is rejected rather than overwritten.

Keep the database and the job's entire workspace together. A missing saved
Git repository or required authors file causes an explicit resume failure;
restore the workspace from a suitable backup or start a new migration.
Older records without checkpoints begin at the first stage. Cancel stops
queued/running jobs and their active child processes outside destructive
history cleanup (which must finish). Wait for the failed state before Resume;
the incomplete stage repeats. Already-created destinations and pushed refs
are not rolled back. Unattended automatic resume is not implemented.

## Assessment and verification reports

An accessible existing GitHub destination is reused without a creation call
or settings changes. Enter the actual owner/repository from its GitHub URL,
not `git` as a tool name. If lookup reports missing/inaccessible, setup attempts
private creation and surfaces any permission failure. For the first test,
prefer an empty repository without initialized README/license commits.
Normal pushes reject divergent history; no merge/force-overwrite is performed.
Resume retains the original destination; changing owner/repository requires
a new migration.

In wizard Review, choose **Assess SVN and GitHub (read-only)** and confirm
network access. This reads visible SVN history for explicit author coverage,
configured layout access, and current-trunk files above 100 MiB. It authenticates
the token against github.com and checks push permission for accessible existing
destinations. An absent/inaccessible destination does not prove permission to
create it. Full historical file sizes, restricted history, rulesets, build and
cutover readiness require a disposable trial and manual review.
Missing mappings are failed checks; prepared but unconfirmed identities warn.
Assessment is advisory and does not automatically block Start.
Copy/export the report before closing; tokens are not stored in the report.

After push, **Details > Verification** shows persisted imported/destination
branch/tag object IDs, Git integrity, and imported author inventory. Mismatches,
extra refs, absent branches, and integrity failures prevent completion.
Matching imported Git refs do not certify SVN history/file/author completeness;
cleanup intentionally changes history. Reports are snapshots of the last
verification attempt and reset when Resume starts a new attempt.

## Troubleshooting

### Disposable local SVN sample

If you have no SVN source, create a test fixture with the bundled SVN tools:

```powershell
.\scripts\New-TestSvnRepository.ps1 -OutputDirectory D:\Svn2Github\temp\svn-sample
```

The generator refuses existing output directories and creates six revisions,
three sample authors, standard trunk/branches/tags, a feature branch, a tag,
and file update/rename/delete history. It exports verbose text/XML history,
an example authors file, and expected results. It does not contact GitHub.
Use `-SvnBinDirectory` if your complete SVN tools are installed elsewhere.

Serve the sample read-only from a PowerShell window (keep it open):

```powershell
& .\artifacts\SVN2GitHub-portable-win-x64\tools\svn\bin\svnserve.exe --daemon --foreground --read-only --listen-host 127.0.0.1 --listen-port 3690 --root D:\Svn2Github\temp\svn-sample --log-file D:\Svn2Github\temp\svn-sample\logs\svnserve.log
```

Enter `svn://127.0.0.1:3690/sample` in the app, with `trunk`, `branches`,
and `tags` as layout paths. Review/confirm mappings for `alice`, `bob`,
and `carol`; the generated authors file gives sample names/emails.
No production credentials or data are needed. For end-to-end migration,
use a disposable GitHub destination and enter a valid token only in the app.
The sample is read-only over SVN; logs may contain local host/user metadata.
Stop the server with Ctrl+C. If the port is occupied, choose another port and
use it in the SVN URL. Do not expose this test server to the network.

- **SVN access:** test with `svn info <repository-url>`.
- **Git-SVN:** test with `git svn --version`; Git for Windows installations
  may require additional SVN components.
- **GitHub setup:** test `gh auth status` and confirm organization policy
  permits repository creation.
- **History cleaner:** verify `git filter-repo --version` if cleaning is
  enabled.
- **API unavailable:** check `http://127.0.0.1:5000/api/health` and the
  source launcher output logs under `temp\api-server.*.log`. Portable mode
  uses a different available loopback port; do not assume port 5000 there.
- **Interrupted job:** review Details and the saved checkpoint, then use
  Resume. Do not delete the workspace to fix a late-stage error.
- **No job visible:** reset dashboard filters and clear repository search.
  Check the correct source/portable data location.
- **Need help:** use a row's **Ask AI**, review the shared context, and
  optionally include logs. Suggestions do not execute commands; independently
  review any history-rewriting or repository-changing recommendation.
- **Support report:** Details offers Overview/recovery and Recorded logs
  tabs. Filter logs for diagnosis, then copy/export the complete recorded
  report. Filtering does not remove entries from the export. It includes
  names/URLs/errors, uses best-effort redaction, and is not an immutable audit.
- **Guided copilot:** draft Diagnose or Recovery and refresh the local snapshot
  before sending advice requests about a changing job. Drafts, local guidance,
  and refresh do not call AI; only Send does. See
  [Copilot guide](docs/AI_COPILOT_GUIDE.md).

## Data handling

The API stores job details and author mappings in SQLite, but not GitHub
tokens. Tokens are held in process memory and passed to child processes through
their environment. Keep the API bound to loopback; authentication and
multi-user access are not implemented.
AI keys and chat history are Windows-user-encrypted local files. Clearing the
AI key does not clear the GitHub token used by a migration, revoke the AI
provider key, or delete chats. See [Setup](SETUP.md#ai-assistant).
