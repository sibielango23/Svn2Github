# Project Summary

This file supersedes the historical implementation summary, which described
the retired Python backend. The current system consists of the WPF desktop
application and the ASP.NET Core API documented in [README.md](README.md) and
[ARCHITECTURE.md](ARCHITECTURE.md).

Do not use the historical test or production-readiness claims from the former
implementation. See [TEST_RESULTS.md](TEST_RESULTS.md) for current validation
and known limitations.

The current source adds an interactive live dashboard, aligned migration
wizard, persistent right-side AI chat with clear-key controls, and manual
stage-checkpoint Resume. No-install Windows x64 packaging is supported; see
[Portable package](PORTABLE_PACKAGE.md). The existing ZIP predates the latest
changes and requires republishing. Real migration acceptance and enterprise
production readiness are not claimed.
Local readiness, full diagnostics/report export, and guided copilot tasks
with explicit snapshot refresh are now documented in the
[Copilot guide](docs/AI_COPILOT_GUIDE.md); they do not execute AI-proposed actions.
