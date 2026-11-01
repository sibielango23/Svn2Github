# Author Mapping Guide

The author mapping editor lets you translate SVN usernames into Git author
names and email addresses before the migration begins.

## Workflow

1. Use dashboard **New migration**, enter a reachable SVN URL/layout in
   Source, then complete Destination.
2. In step 3 (Review), open author mapping and extract usernames from the SVN log.
3. Review each suggested name and email address.
4. Correct values and mark mappings verified when appropriate.
5. Save the mapping and start the migration.

The API keeps temporary mappings in memory until the migration is created.
Once attached to a migration, the author map is persisted with the migration
record in `data/local_migrations.sqlite3`.
Portable mode stores the database under `%LOCALAPPDATA%\SVN2GitHub\data`;
see [Portable package](../PORTABLE_PACKAGE.md). Temporary sessions are not
preserved across API restarts.

## Mapping format

`git-svn` consumes lines in this format:

```text
svn_username = Git Display Name <email@example.com>
```

For example:

```text
jane.doe = Jane Doe <jane.doe@example.com>
```

Each SVN username must be unique. Check capitalization, spelling, and
organization-approved email addresses before starting a large import. The
automatically generated `example.com` email is a placeholder, not a verified
identity.

## Troubleshooting

- Extraction requires the `svn` command-line client and read access to the
  repository.
- If an author is missing, verify SVN access and retry extraction.
- Extraction examines up to 5,000 SVN log entries; it is not proof that all
  historical authors were found. Confirm coverage before a long import.
- The final `authors.txt` file is stored in the migration workspace.
- Update mappings before migration; changing them after import requires
  rewriting commit history.
- If the SVN URL changes after mappings are prepared, review the mappings
  again for that source before starting.
- Resume reuses a completed author checkpoint and its persisted mappings.
  Preserve the workspace; editing authors is not a revision-level retry.

The editor works in source mode and a freshly built portable package with
bundled SVN. See [Migration guide](../MIGRATION_GUIDE.md) for preflight,
safe testing, and checkpoint recovery.
Local readiness cautions about full author coverage rather than asserting it.
Use the [copilot readiness draft](AI_COPILOT_GUIDE.md) for a checklist, but
independently verify identities and coverage before import.
