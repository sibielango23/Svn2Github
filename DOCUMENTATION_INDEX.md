# Documentation Index

## Start here

- [README](README.md): project overview, requirements, and limitations.
- [Setup](SETUP.md): install, build, and run instructions.
- [Getting started](GETTING_STARTED.md): first safe test migration.
- [Migration guide](MIGRATION_GUIDE.md): preflight, migration, and recovery.
- [Portable package](PORTABLE_PACKAGE.md): no-install Windows x64 packaging,
  bundled tools, data paths, publishing, and release verification.

## Technical references

- [Architecture](ARCHITECTURE.md)
- [WPF/API boundary](API_WPF_ARCHITECTURE.md)
- [.NET API endpoints](api/README.md)
- [WPF implementation](wpf/WPF_IMPLEMENTATION_GUIDE.md)
- [Contributing](CONTRIBUTING.md)
- [Migration flow](FLOW_DIAGRAM.md)

## User guides

- [Author mapping guide](docs/AUTHOR_MAPPING_GUIDE.md)
- [Author mapping implementation](docs/AUTHOR_MAPPING_IMPLEMENTATION.md)
- [Quick reference](docs/QUICK_REFERENCE.md)
- [Migration copilot guide](docs/AI_COPILOT_GUIDE.md): guided tasks, local
  checkpoint advice, snapshot refresh, diagnostics, explicit sharing/privacy.

The guides describe the current source: interactive dashboard filters/search,
live updates, aligned wizard navigation, right-side AI with saved chats and
clear-key controls, and manual checkpoint Resume. An older portable ZIP does
not include these latest changes; publish current source before distribution.
Local readiness and full diagnostics/report export are also included in the
current source; these checks do not constitute remote migration acceptance.

## Current validation

See [TEST_RESULTS.md](TEST_RESULTS.md) for the latest local build and smoke
checks. A real external SVN-to-GitHub migration remains to be validated.
Use [WPF/API test plan](wpf/TEST_PLAN.md) for current manual, failure-injection,
and portable acceptance scenarios. Compatibility status pages link here and
must not be interpreted as enterprise-production certification.
