# Author Mapping Implementation

The WPF author editor uses temporary author-session routes in the .NET API.
The session is keyed by a `temp_` ID and stores the SVN URL and edited mapping
only in service memory while the dialog is open.

On extraction, the API runs `svn log --quiet --limit 5000`, parses SVN
revision-author records, and merges new usernames with the current mapping.
Bulk save validates required values and rejects duplicate SVN usernames. The
WPF flow attaches the mappings to the migration request and closes the
temporary session.

When a migration begins, the queue writes the verified/edited mappings to the
workspace `authors.txt` file and passes it to `git-svn`. If no mappings were
provided, the service generates placeholder names and `example.com` emails.
Users should review this fallback before relying on imported commit identity.

Persisted author maps are stored alongside migration state in
`data/local_migrations.sqlite3`; GitHub tokens are not part of that record.
Portable API storage instead uses `%LOCALAPPDATA%\SVN2GitHub\data` by default.
The queue checkpoints completed author mapping, recreates the workspace
authors file from persisted mappings during Resume when possible, and fails
explicitly if required checkpoint artifacts cannot be recovered.

The three-step wizard presents author review on step 3 and rejects a saved
mapping for a changed SVN URL. Extraction's 5,000-entry limit should not be
described as complete full-history author coverage.
See [Author guide](AUTHOR_MAPPING_GUIDE.md),
[API reference](../api/README.md), and [Portable package](../PORTABLE_PACKAGE.md).
Local readiness does not run author extraction or validate full-history
coverage; its warning preserves the distinction between tool availability
and actual migration readiness.
