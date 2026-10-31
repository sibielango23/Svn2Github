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
