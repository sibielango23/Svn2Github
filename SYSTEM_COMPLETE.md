# Historical Status Notice

The completion claims in this older document refer to an earlier application
version and are not current production-readiness claims. The supported backend
is now .NET; see [README.md](README.md) and [TEST_RESULTS.md](TEST_RESULTS.md)
for current requirements and verified behavior.

Current features and limits include stage-based manual Resume (not
revision-level/automatic resume), a searchable live dashboard, aligned
wizard controls, user-encrypted AI chats/settings, and a local clear-key
action. Windows x64 portable support is documented in
[PORTABLE_PACKAGE.md](PORTABLE_PACKAGE.md); the existing ZIP is stale.
The API is loopback-only; safe user cancellation and real migration
acceptance remain outstanding.
Current diagnostics and local readiness reports are advisory snapshots, not
enterprise audit/compliance guarantees. The
[copilot](docs/AI_COPILOT_GUIDE.md) drafts structured help tasks and refreshes
context without automatically sending or executing operations.
