# Implementation Status

This status page replaces an earlier checklist written for the retired
Python/FastAPI implementation. The supported backend is now the ASP.NET Core
service in `backend/SVN2GitHub.Api`, and the WPF client remains the desktop
front end.

Current build and smoke-test results are recorded in
[TEST_RESULTS.md](TEST_RESULTS.md). The complete external migration workflow
still requires acceptance testing against a disposable SVN repository and
GitHub destination.
