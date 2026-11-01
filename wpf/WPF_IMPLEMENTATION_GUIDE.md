# WPF Implementation Guide

The desktop application targets .NET 8 WPF and follows MVVM with
CommunityToolkit.Mvvm. Views contain presentation and interaction logic,
view models own UI state and commands, and services handle API/provider
communication.

## Migration workspace

The main window provides persistent navigation to the dashboard and a guided
three-step migration form. The wizard collects the SVN URL and layout, GitHub
destination and token, then offers author mapping and a final review before
queueing a job. Back and Next/Start migration share a fixed, equal-height
footer aligned with the form; validation messages wrap above them. The step
indicators use the Material Design 3 primary brush for active/completed steps.

The dashboard provides:

- A direct **New migration** action and AI settings/refresh controls.
- Clickable summary cards for All, Queued/running, Completed, and Needs
  attention, with selected-filter highlighting.
- Case-insensitive repository/SVN URL search combined with the status filter.
- Reset filters, a matching count, and distinct first-use/no-match states.
- The complete loaded history, not only the newest ten items.
- Wrapping cards/actions for the narrow content area when AI chat is open.
- Details, Ask AI, and Resume for eligible failed jobs.
- Local readiness dialog checking API-side tool/storage prerequisites.
- Full diagnostics with searchable recorded logs, checkpoint recovery advice,
  and complete redacted support-report copy/export.

`DashboardViewModel` uses an `ICollectionView` for local filters and reconciles
job items in place. Polling is every 2 seconds active and 10 seconds idle;
failed background refresh retains stale data and reports retry status.
`MainViewModel` handles dashboard-to-wizard navigation.
Wizard Review provides separately confirmed remote **Assess SVN and GitHub**
alongside local readiness. `ReadinessWindow` renders/copies/exports the shared
report shape; remote assessment accepts the reviewed request only in memory.
Diagnostics adds a **Verification** tab and includes persisted evidence in
support exports/copilot snapshots. Dashboard **Cancel** calls the existing
DELETE surface with explicit errors and confirmation; `canCancel` excludes
destructive cleanup. A stopping job cannot Resume until process draining ends.
Each row displays cumulative API-recorded conversion time. Timing snapshots
change even while stage/progress remains unchanged, so reconciliation compares
`ElapsedSeconds`/`RunningSince` as well. Diagnostics/copilot snapshots share
the same formatted hours/minutes/seconds (days for long conversions). Queue
and retry/offline waits are excluded; historical unavailable timing is explicit.

## API client

`Services/ApiService.cs` calls the ASP.NET Core API at
`http://127.0.0.1:5000`. The client and API use camelCase JSON. Keep endpoint
and DTO changes aligned across both projects.
`POST /api/migrations/{id}/resume` requires a fresh GitHub token.
The Resume dialog explains the saved checkpoint and next stage. Required
workspace artifacts must survive; an interrupted import repeats rather than
resuming individual revisions.

## AI assistant

The optional AI assistant runs in the WPF process and supports user-selected
OpenAI or Anthropic configuration. Provider credentials are encrypted with
Windows DPAPI for the current user. The assistant is docked on the right side
of the main window and can be opened from the application shell for general
product help or from a migration for troubleshooting. Chat conversations are
stored in the current user's local application data, encrypted with Windows
DPAPI, and remain until deleted using the history controls. Pressing Send
sends the conversation and displayed context to the selected provider. The
context preview can be reviewed and migration logs can be disabled before
sending. Press Enter to send a message or Shift+Enter to insert a new line;
the conversation scrolls to the latest message as messages arrive. AI
responses are suggestions only and do not execute commands.
Switching chats during a pending reply keeps the response with its originating
conversation. Chat deletion is disabled while a response is pending.

AI settings support save/replace and confirmed **Clear saved key**, retaining
provider/model preferences and chat history. The cleared key cannot be reused
for new chats; opening the assistant prompts for a new key. After the settings
dialog closes, changed settings reset the current assistant view model.
Already-issued requests are not cancelled and provider keys are not revoked.
Settings use `ai-settings.json` and chats `ai-chat-history.dat` under
`%LOCALAPPDATA%\SVN2GitHub`, protected for the Windows user. Cross-user/PC
credential or chat transfer is not supported by simply copying those files.
The guided copilot provides Diagnose/Recovery/Readiness/Handover composer
drafts and shares `MigrationSupportService` checkpoint advice with diagnostics.
Drafting does not send; Refresh migration snapshot calls only the local API.
Logs are opt-in and disabled again after snapshot refresh. Legacy chats are
upgraded to current capability guidance without claiming their metadata is
live. See [Copilot guide](../docs/AI_COPILOT_GUIDE.md).

## Build and launch

From the repository root:

```powershell
dotnet build wpf\SVN2GitHub.sln --configuration Debug
.\start.ps1
dotnet test wpf\SVN2GitHub.Tests\SVN2GitHub.Tests.csproj --configuration Debug
```

The solution includes the ASP.NET Core API project. The service is local-only,
does not authenticate callers, and must not be exposed to a network.

## Portable deployment

Publish with [Publish-Portable.ps1](../scripts/Publish-Portable.ps1); see
[Portable package](../PORTABLE_PACKAGE.md) for prerequisites and output.
WPF starts the bundled self-contained API on an available loopback port,
sets `SVN2GITHUB_API_URL`, and prepends bundled CLI directories to PATH.
`ApiService` reads that URL before falling back to source-mode port 5000.
WPF stops the API it owns when it exits.

API data/workspaces can be relocated with `SVN2GITHUB_PORTABLE_DATA_ROOT`;
AI files always remain per-user. The existing ZIP is older than the current
features; publishing and smoke-testing current source is a separate step.
See [Test plan](TEST_PLAN.md) for layout, chat, and portable acceptance.
Desktop regression tests now permanently cover report redaction/recovery,
clear-key retention, local-only task drafts, legacy-chat guidance, dashboard
filters, and fixed navigation at multiple widths.
