# Migration Flow

```text
WPF UI
  -> ASP.NET Core API (loopback)
    -> Validate request and store migration state
      -> Sequential migration queue
        1. Analyze SVN layout, revisions, branches, tags, and authors [checkpoint]
        2. Create or apply the reviewed authors mapping [checkpoint]
        3. Run git-svn clone and convert imported refs [checkpoint]
        4. Optionally clean history (rewrites commit IDs) [checkpoint]
        5. Create/configure the GitHub repository [checkpoint]
        6. Push branches and tags
        7. Compare local and remote ref names and object IDs
      -> Persist progress and final status in SQLite
```

The six workflow services are C# classes in the ASP.NET Core backend. They
invoke `svn`, `git`, and `gh` through a shared external-command runner.

## Failure and restart behavior

The failing step records a redacted error and changes the migration to failed.
Jobs are processed one at a time. If the backend stops while a subprocess is
running, the job is marked interrupted/failed during shutdown or startup. The
user can choose Resume, supply a fresh GitHub token, and skip completed
checkpoint stages. The incomplete stage repeats; an interrupted SVN import
must run again. Push retries do not force-overwrite divergent remote refs.
Automatic resume is not implemented. User cancellation stops queued/running
jobs outside destructive history cleanup, retaining completed checkpoints.

## Data locations

- Job state and author maps: `data/local_migrations.sqlite3`
- Per-job working files and imported repositories: `temp\workspace_<migration-id>`
- Portable defaults: `%LOCALAPPDATA%\SVN2GitHub\data` and `workspaces`;
  keep both for checkpoint reuse.

## Desktop interactions

```text
Dashboard -> New migration -> Source -> Destination -> Review -> Queue
          -> Status filter + repository search -> Details / Ask AI / Resume
          -> Check readiness -> Local version/storage report (no remote calls)
          -> Details -> Search logs -> Copy/export support report
          <- Live API refresh (2 seconds active / 10 seconds idle)

Ask AI -> Right-side chat -> Review shared context -> Send (Enter)
       -> OpenAI or Anthropic -> Reply and locally encrypted chat history
AI settings -> Save/replace key OR Clear saved key (keeps model and chats)
Copilot tasks -> Draft diagnosis/recovery/readiness/handover -> Review -> Send
Refresh snapshot -> Local migration API -> Updated context (no provider call)

Portable EXE -> Bundled API on available loopback port + bundled CLI tools
             -> App closes -> Owned API stops
```

GitHub tokens are kept in process memory and are not written to SQLite. See
[ARCHITECTURE.md](ARCHITECTURE.md) and [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)
for security and operational limitations.
See [PORTABLE_PACKAGE.md](PORTABLE_PACKAGE.md) for publishing current binaries;
the earlier ZIP does not contain the newest source changes.
