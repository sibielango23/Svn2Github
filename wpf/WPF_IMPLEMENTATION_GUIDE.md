# WPF Implementation Guide

The desktop application targets .NET 8 WPF and follows MVVM with
CommunityToolkit.Mvvm. Views contain presentation and interaction logic,
view models own UI state and commands, and services handle API/provider
communication.

## API client

`Services/ApiService.cs` calls the ASP.NET Core API at
`http://127.0.0.1:5000`. The client and API use camelCase JSON. Keep endpoint
and DTO changes aligned across both projects.

## AI assistant

The optional AI assistant runs in the WPF process and supports user-selected
OpenAI or Anthropic configuration. Provider credentials are encrypted with
Windows DPAPI for the current user. Before a request, the user reviews and
approves the migration context. AI responses are suggestions only and do not
execute commands.

## Build and launch

From the repository root:

```powershell
dotnet build SVN2GitHub.sln --configuration Debug
..\start.ps1
```

The solution includes the ASP.NET Core API project. The service is local-only,
does not authenticate callers, and must not be exposed to a network.
