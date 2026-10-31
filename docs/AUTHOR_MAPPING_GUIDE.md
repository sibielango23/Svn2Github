# Author Mapping Guide

The author mapping editor lets you translate SVN usernames into Git author
names and email addresses before the migration begins.

## Workflow

1. Start a new migration and enter a reachable SVN URL.
2. Open author mapping and extract usernames from the SVN log.
3. Review each suggested name and email address.
4. Correct values and mark mappings verified when appropriate.
5. Save the mapping and start the migration.

The API keeps temporary mappings in memory until the migration is created.
Once attached to a migration, the author map is persisted with the migration
record in `data/local_migrations.sqlite3`.

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
- The final `authors.txt` file is stored in the migration workspace.
- Update mappings before migration; changing them after import requires
  rewriting commit history.
