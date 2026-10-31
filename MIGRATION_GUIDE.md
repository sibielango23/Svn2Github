# Migration Guide

## Before starting

- Use a disposable destination repository for your first migration.
- Verify the SVN URL is reachable and does not embed credentials.
- Check the SVN layout (trunk, branches, tags) and required tools.
- Ensure the GitHub token can create and push to the destination organization.
- Review author mappings and confirm all required SVN usernames are covered.
- Check disk space for the imported history and temporary repository.
- Back up important local data. The migration database is
  `data/local_migrations.sqlite3`; each job has a workspace under `temp/`.

## Run

1. Start the application with `.\start.ps1`.
2. Use **New Migration** to enter the source and GitHub destination.
3. Extract and correct author mappings before starting.
4. Enable history cleaning only when required. It removes configured file
   patterns and blobs larger than 100 MiB, and rewrites commit IDs.
5. Submit the job and monitor its progress.
6. Confirm the final verification shows matching branch and tag names and
   object IDs on GitHub.

Jobs are processed sequentially. Do not close the API while a migration is
running. Restart recovery marks an active job interrupted; it cannot safely
resume and should be restarted as a new migration after reviewing the partial
workspace.

## Troubleshooting

- **SVN access:** test with `svn info <repository-url>`.
- **Git-SVN:** test with `git svn --version`; Git for Windows installations
  may require additional SVN components.
- **GitHub setup:** test `gh auth status` and confirm organization policy
  permits repository creation.
- **History cleaner:** verify `git filter-repo --version` if cleaning is
  enabled.
- **API unavailable:** check `http://127.0.0.1:5000/api/health` and the
  launcher output logs under `temp/`.
- **Interrupted job:** inspect its workspace under `temp/`; the app does not
  resume external Git/SVN processes.

## Data handling

The API stores job details and author mappings in SQLite, but not GitHub
tokens. Tokens are held in process memory and passed to child processes through
their environment. Keep the API bound to loopback; authentication and
multi-user access are not implemented.
