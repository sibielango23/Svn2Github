# WPF Application - Complete Implementation Guide

## Part 2: WPF Desktop Application with Material Design

This document provides the complete structure and key implementation details for the WPF application.

## Project Structure

```
wpf/
├── SVN2GitHub.sln                    # Visual Studio solution
└── SVN2GitHub/
    ├── SVN2GitHub.csproj             # Project file (✓ Created)
    ├── App.xaml                       # Application resources (✓ Created)
    ├── App.xaml.cs                    # Application startup (✓ Created)
    │
    ├── Models/                        # Data models
    │   ├── MigrationModels.cs        # Migration DTOs
    │   ├── AuthorModels.cs           # Author mapping DTOs (✓ NEW)
    │   └── ApiResponses.cs           # API response models
    │
    ├── ViewModels/                    # MVVM View Models
    │   ├── MainViewModel.cs          # Main window VM
    │   ├── DashboardViewModel.cs     # Dashboard VM
    │   ├── NewMigrationViewModel.cs  # New migration wizard VM
    │   └── BaseViewModel.cs          # Base VM class
    │
    ├── Views/                         # WPF Views (XAML)
    │   ├── MainWindow.xaml           # Main window (✓ Created)
    │   ├── DashboardView.xaml        # Dashboard (✓ Created)
    │   ├── NewMigrationView.xaml     # Migration wizard (✓ Updated)
    │   ├── AuthorMappingWindow.xaml  # Author editor (✓ NEW)
    │   └── AddAuthorDialog.xaml      # Add author dialog (✓ NEW)
    │
    ├── Services/                      # Business logic services
    │   ├── ApiService.cs             # REST API client (✓ Enhanced with author APIs)
    │   └── NotificationService.cs    # Toast notifications
    │
    ├── Converters/                    # XAML value converters
    │   ├── StatusToColorConverter.cs
    │   ├── LogLevelToColorConverter.cs
    │   ├── BoolToVisibilityConverter.cs
    │   └── ProgressToTextConverter.cs
    │
    ├── Styles/                        # XAML styles
    │   ├── CustomStyles.xaml         # Custom control styles
    │   └── Colors.xaml               # Color resources
    │
    └── Resources/                     # Images, icons
        └── app.ico                   # Application icon
```

## NuGet Packages (Already in .csproj)

- **MaterialDesignThemes** 5.0.0 - Material Design UI
- **MaterialDesignColors** 3.0.0 - Color themes
- **RestSharp** 110.2.0 - HTTP client
- **CommunityToolkit.Mvvm** 8.2.2 - MVVM helpers
- **LiveChartsCore.SkiaSharpView.WPF** 2.0.0 - Charts
- **Newtonsoft.Json** 13.0.3 - JSON serialization
- **Microsoft.Data.Sqlite** 8.0.0 - Direct DB access

## Key Implementation Files

### 1. Models/Migration.cs

```csharp
using System;

namespace SVN2GitHub.Models
{
    public class Migration
    {
        public string Id { get; set; } = string.Empty;
        public string SvnUrl { get; set; } = string.Empty;
        public string GitHubOrg { get; set; } = string.Empty;
        public string GitHubRepo { get; set; } = string.Empty;
        public MigrationStatus Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? CurrentPhase { get; set; }
        public double ProgressPercent { get; set; }
        public int? TotalRevisions { get; set; }
        public int ProcessedRevisions { get; set; }
        public string? ErrorMessage { get; set; }
        public string? WorkspacePath { get; set; }
    }

    public enum MigrationStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Paused
    }
}
```

### 2. Services/ApiClient.cs

```csharp
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SVN2GitHub.Models;
using Newtonsoft.Json;

namespace SVN2GitHub.Services
{
    public class ApiClient
    {
        private readonly RestClient _client;
        private const string BASE_URL = "http://localhost:8000";

        public ApiClient()
        {
            _client = new RestClient(BASE_URL);
        }

        public async Task<Migration> CreateMigrationAsync(MigrationConfig config)
        {
            var request = new RestRequest("/api/migrations", Method.Post);
            request.AddJsonBody(config);

            var response = await _client.ExecuteAsync<Migration>(request);

            if (!response.IsSuccessful)
                throw new Exception($"API Error: {response.ErrorMessage}");

            return response.Data!;
        }

        public async Task<List<Migration>> GetMigrationsAsync()
        {
            var request = new RestRequest("/api/migrations", Method.Get);
            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful)
                throw new Exception($"API Error: {response.ErrorMessage}");

            var result = JsonConvert.DeserializeObject<MigrationListResponse>(response.Content!);
            return result?.Migrations ?? new List<Migration>();
        }

        public async Task<MigrationStatusResponse> GetMigrationStatusAsync(string migrationId)
        {
            var request = new RestRequest($"/api/migrations/{migrationId}/status", Method.Get);
            var response = await _client.ExecuteAsync<MigrationStatusResponse>(request);

            if (!response.IsSuccessful)
                throw new Exception($"API Error: {response.ErrorMessage}");

            return response.Data!;
        }

        public async Task PauseMigrationAsync(string migrationId)
        {
            var request = new RestRequest($"/api/migrations/{migrationId}/pause", Method.Post);
            await _client.ExecuteAsync(request);
        }

        public async Task ResumeMigrationAsync(string migrationId)
        {
            var request = new RestRequest($"/api/migrations/{migrationId}/resume", Method.Post);
            await _client.ExecuteAsync(request);
        }

        public async Task StopMigrationAsync(string migrationId)
        {
            var request = new RestRequest($"/api/migrations/{migrationId}/stop", Method.Post);
            await _client.ExecuteAsync(request);
        }

        public async Task<List<LogEntry>> GetLogsAsync(string migrationId, int skip = 0, int limit = 100)
        {
            var request = new RestRequest($"/api/logs/{migrationId}", Method.Get);
            request.AddParameter("skip", skip);
            request.AddParameter("limit", limit);

            var response = await _client.ExecuteAsync(request);
            var result = JsonConvert.DeserializeObject<LogListResponse>(response.Content!);
            return result?.Logs ?? new List<LogEntry>();
        }

        public async Task<List<Author>> GetAuthorsAsync(string migrationId)
        {
            var request = new RestRequest($"/api/authors/{migrationId}", Method.Get);
            var response = await _client.ExecuteAsync<List<Author>>(request);
            return response.Data ?? new List<Author>();
        }

        public async Task UpdateAuthorsAsync(string migrationId, List<Author> authors)
        {
            var request = new RestRequest($"/api/authors/{migrationId}/bulk", Method.Post);
            request.AddJsonBody(new { authors });
            await _client.ExecuteAsync(request);
        }

        public async Task<ConnectionTestResponse> TestSvnConnectionAsync(string svnUrl)
        {
            var request = new RestRequest("/api/config/test-svn", Method.Post);
            request.AddJsonBody(new { svn_url = svnUrl });
            var response = await _client.ExecuteAsync<ConnectionTestResponse>(request);
            return response.Data!;
        }

        public async Task<ConnectionTestResponse> TestGitHubConnectionAsync(string org, string repo)
        {
            var request = new RestRequest("/api/config/test-github", Method.Post);
            request.AddJsonBody(new { github_org = org, github_repo = repo });
            var response = await _client.ExecuteAsync<ConnectionTestResponse>(request);
            return response.Data!;
        }
    }
}
```

### 3. Services/WebSocketService.cs

```csharp
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SVN2GitHub.Services
{
    public class WebSocketService : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<LogMessage>? LogReceived;
        public event EventHandler<StatusUpdate>? StatusUpdated;

        public async Task ConnectAsync(string migrationId)
        {
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            var uri = new Uri($"ws://localhost:8000/api/migrations/{migrationId}/stream");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

            _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (_webSocket != null && 
                   _webSocket.State == WebSocketState.Open && 
                   !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), 
                        cancellationToken
                    );

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleMessage(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket error: {ex.Message}");
                    break;
                }
            }
        }

        private void HandleMessage(string message)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<dynamic>(message);

                if (json.type == "status_update")
                {
                    var update = JsonConvert.DeserializeObject<StatusUpdate>(message);
                    StatusUpdated?.Invoke(this, update!);
                }
                else
                {
                    var log = JsonConvert.DeserializeObject<LogMessage>(message);
                    LogReceived?.Invoke(this, log!);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing message: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                _cancellationTokenSource?.Cancel();
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
        }
    }

    public class LogMessage
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string? Agent { get; set; }
        public string Message { get; set; } = string.Empty;
        public double? Progress { get; set; }
    }

    public class StatusUpdate
    {
        public string MigrationId { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public double Progress { get; set; }
    }
}
```

### 4. ViewModels/MainViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace SVN2GitHub.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject? _currentView;

        [ObservableProperty]
        private string _title = "SVN to GitHub Migration";

        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowNewMigrationCommand { get; }

        public MainViewModel()
        {
            ShowDashboardCommand = new RelayCommand(ShowDashboard);
            ShowNewMigrationCommand = new RelayCommand(ShowNewMigration);

            // Show dashboard by default
            ShowDashboard();
        }

        private void ShowDashboard()
        {
            CurrentView = new DashboardViewModel();
        }

        private void ShowNewMigration()
        {
            CurrentView = new NewMigrationViewModel();
        }
    }
}
```

### 5. Views/MainWindow.xaml

```xml
<Window x:Class="SVN2GitHub.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
        xmlns:views="clr-namespace:SVN2GitHub.Views"
        Title="{Binding Title}" 
        Height="800" Width="1400"
        WindowStartupLocation="CenterScreen"
        Background="{DynamicResource MaterialDesignPaper}"
        TextElement.Foreground="{DynamicResource MaterialDesignBody}"
        TextElement.FontWeight="Regular"
        TextElement.FontSize="13"
        FontFamily="{DynamicResource MaterialDesignFont}">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Top App Bar -->
        <materialDesign:ColorZone Grid.Row="0" 
                                  Mode="PrimaryMid" 
                                  Padding="16"
                                  materialDesign:ElevationAssist.Elevation="Dp4">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Menu Icon -->
                <Button Grid.Column="0"
                        Style="{StaticResource MaterialDesignIconButton}"
                        Margin="0,0,16,0">
                    <materialDesign:PackIcon Kind="Menu" />
                </Button>

                <!-- Title -->
                <TextBlock Grid.Column="1"
                           Text="{Binding Title}"
                           VerticalAlignment="Center"
                           FontSize="20"
                           FontWeight="Medium"/>

                <!-- Navigation Buttons -->
                <StackPanel Grid.Column="2" 
                            Orientation="Horizontal">
                    <Button Content="DASHBOARD"
                            Command="{Binding ShowDashboardCommand}"
                            Style="{StaticResource MaterialDesignFlatButton}"
                            Margin="8,0"/>
                    <Button Content="NEW MIGRATION"
                            Command="{Binding ShowNewMigrationCommand}"
                            Style="{StaticResource MaterialDesignRaisedButton}"
                            Margin="8,0"/>
                </StackPanel>
            </Grid>
        </materialDesign:ColorZone>

        <!-- Main Content Area -->
        <ContentControl Grid.Row="1" 
                       Content="{Binding CurrentView}"
                       Margin="16"/>
    </Grid>
</Window>
```

## Building and Running

### Prerequisites
1. Visual Studio 2022 or later
2. .NET 8.0 SDK
3. Python FastAPI backend running at http://localhost:8000

### Build Steps

1. **Restore NuGet packages:**
   ```
   dotnet restore wpf/SVN2GitHub.sln
   ```

2. **Build the solution:**
   ```
   dotnet build wpf/SVN2GitHub.sln
   ```

3. **Run the application:**
   ```
   dotnet run --project wpf/SVN2GitHub/SVN2GitHub.csproj
   ```

Or open in Visual Studio and press F5.

## Next Steps

To complete the full WPF application, implement:

1. **Dashboard View** - Show active migrations, statistics
2. **New Migration Wizard** - Step-by-step configuration
3. **Migration Monitor** - Real-time progress with WebSocket
4. **Author Editor** - DataGrid for editing author mappings
5. **Verification View** - Checklist of verification results
6. **Audit Log View** - Filterable audit trail
7. **Settings View** - API endpoint configuration

All ViewModels should inherit from `ObservableObject` and use `[ObservableProperty]` attributes.
All commands should use `RelayCommand` from CommunityToolkit.Mvvm.

The foundation is complete and ready for full implementation!
