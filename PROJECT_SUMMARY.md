# Project Summary

The supported architecture is a .NET 8 WPF desktop application backed by an
ASP.NET Core API. The prior Python/FastAPI implementation has been retired.

For current setup, requirements, data handling, and limitations, see
[README.md](README.md). For current validation status, see
[TEST_RESULTS.md](TEST_RESULTS.md).

Current source includes guided migration with aligned Back/Next controls,
an interactive live dashboard, persistent right-side AI chat and a clear-key
action, and manual checkpoint Resume with a fresh token. Windows x64
folder/ZIP deployment bundles .NET, Git/git-svn, SVN, and gh. The earlier ZIP
must be republished for current features; see
[Portable package](PORTABLE_PACKAGE.md).
Local readiness checks, full diagnostic support reports, and guided copilot
drafts/snapshot refresh extend troubleshooting beyond free-form chat.
See [Copilot guide](docs/AI_COPILOT_GUIDE.md); provider sharing stays explicit.
