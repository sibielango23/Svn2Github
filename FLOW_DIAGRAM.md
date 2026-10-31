# Migration Flow

```text
WPF UI
  -> ASP.NET Core API (loopback)
    -> Validate request and store migration state
      -> Sequential migration queue
        1. Analyze SVN layout, revisions, branches, tags, and authors
        2. Create or apply the reviewed authors mapping
        3. Run git-svn clone and convert imported refs to Git branches/tags
        4. Optionally clean history (rewrites commit IDs)
        5. Create/configure the GitHub repository
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
app does not resume a migration or safely cancel a running process tree.

## Data locations

- Job state and author maps: `data/local_migrations.sqlite3`
- Per-job working files and imported repositories: `temp/`

GitHub tokens are kept in process memory and are not written to SQLite. See
[ARCHITECTURE.md](ARCHITECTURE.md) and [MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)
for security and operational limitations.
