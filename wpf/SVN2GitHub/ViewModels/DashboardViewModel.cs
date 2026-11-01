using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using SVN2GitHub.Services;
using SVN2GitHub.Models;
using SVN2GitHub.Views;

namespace SVN2GitHub.ViewModels
{
    public enum DashboardFilter
    {
        All,
        Active,
        Completed,
        Failed
    }

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly NotificationService _notificationService;
        private readonly DispatcherTimer _liveRefreshTimer;
        private bool _isRefreshing;
        private DateTime? _lastSuccessfulRefresh;

        private static readonly TimeSpan ActiveRefreshInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan IdleRefreshInterval = TimeSpan.FromSeconds(10);

        [ObservableProperty]
        private int _totalMigrations = 0;

        [ObservableProperty]
        private int _activeMigrations = 0;

        [ObservableProperty]
        private int _completedMigrations = 0;

        [ObservableProperty]
        private int _failedMigrations = 0;

        [ObservableProperty]
        private ObservableCollection<MigrationDetail> _recentMigrations = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _hasMigrations;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private DashboardFilter _selectedFilter = DashboardFilter.All;

        [ObservableProperty]
        private bool _isLive;

        public ICollectionView FilteredMigrations { get; }
        public int VisibleMigrationCount => FilteredMigrations.Cast<MigrationDetail>().Count();
        public bool NoMatchingMigrations => HasMigrations && VisibleMigrationCount == 0;

        public event Action<MigrationDetail>? AssistantRequested;
        public event Action? AiSettingsUpdated;
        public event Action? NewMigrationRequested;

        public DashboardViewModel()
        {
            _apiService = new ApiService();
            _notificationService = new NotificationService();
            FilteredMigrations = CollectionViewSource.GetDefaultView(RecentMigrations);
            FilteredMigrations.Filter = MatchesFilter;
            RecentMigrations.CollectionChanged += (_, _) =>
            {
                HasMigrations = RecentMigrations.Count > 0;
                OnPropertyChanged(nameof(VisibleMigrationCount));
                OnPropertyChanged(nameof(NoMatchingMigrations));
            };
            _liveRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = IdleRefreshInterval
            };
            _liveRefreshTimer.Tick += LiveRefreshTimer_Tick;
        }

        [RelayCommand]
        private void NewMigration() => NewMigrationRequested?.Invoke();

        [RelayCommand]
        private void CheckReadiness()
        {
            new ReadinessWindow { Owner = Application.Current.MainWindow }.ShowDialog();
        }

        [RelayCommand]
        private void SelectFilter(DashboardFilter filter) => SelectedFilter = filter;

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedFilter = DashboardFilter.All;
        }

        partial void OnSearchTextChanged(string value) => RefreshFilters();
        partial void OnSelectedFilterChanged(DashboardFilter value) => RefreshFilters();

        private void RefreshFilters()
        {
            FilteredMigrations.Refresh();
            OnPropertyChanged(nameof(VisibleMigrationCount));
            OnPropertyChanged(nameof(NoMatchingMigrations));
        }

        private bool MatchesFilter(object item)
        {
            if (item is not MigrationDetail migration)
            {
                return false;
            }

            var statusMatches = SelectedFilter switch
            {
                DashboardFilter.Active => migration.Status is not ("completed" or "failed"),
                DashboardFilter.Completed => migration.Status == "completed",
                DashboardFilter.Failed => migration.Status == "failed",
                _ => true
            };
            var search = SearchText.Trim();
            return statusMatches &&
                (search.Length == 0 ||
                 $"{migration.GithubOrg}/{migration.GithubRepo}".Contains(search, StringComparison.OrdinalIgnoreCase) ||
                 migration.SvnUrl.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        public async Task InitializeAsync()
        {
            _liveRefreshTimer.Start();
            await RefreshDashboardAsync(showErrors: true, showLoading: true);
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            await RefreshDashboardAsync(showErrors: true, showLoading: true);
        }

        private async void LiveRefreshTimer_Tick(object? sender, EventArgs e)
        {
            await RefreshDashboardAsync(showErrors: false, showLoading: false);
        }

        private async Task RefreshDashboardAsync(bool showErrors, bool showLoading)
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            if (showLoading)
            {
                IsLoading = true;
                StatusMessage = "Refreshing migrations...";
            }

            try
            {
                var isHealthy = await _apiService.IsHealthyAsync();
                if (!isHealthy)
                {
                    IsLive = false;
                    StatusMessage = _lastSuccessfulRefresh is null
                        ? "API unavailable. Live updates will retry automatically."
                        : $"Live update unavailable. Showing data from {_lastSuccessfulRefresh:HH:mm:ss}; retrying.";
                    if (showErrors)
                    {
                        _notificationService.ShowWarning(
                            "The API server is not running. Please start it with:\n.\\start-api.ps1",
                            "API Not Available");
                    }

                    return;
                }

                var stats = await _apiService.GetStatsAsync();
                TotalMigrations = stats.Total;
                ActiveMigrations = stats.Active;
                CompletedMigrations = stats.Completed;
                FailedMigrations = stats.Failed;

                var migrations = await _apiService.GetMigrationsAsync();
                UpdateRecentMigrations(migrations
                    .OrderByDescending(migration => migration.UpdatedAt)
                    .ToList());
                HasMigrations = RecentMigrations.Count > 0;

                IsLive = true;
                _lastSuccessfulRefresh = DateTime.Now;
                _liveRefreshTimer.Interval = ActiveMigrations > 0
                    ? ActiveRefreshInterval
                    : IdleRefreshInterval;
                StatusMessage = ActiveMigrations > 0
                    ? $"Live progress updates every 2 seconds. Last updated: {_lastSuccessfulRefresh:HH:mm:ss}"
                    : $"Updates check every 10 seconds. Last updated: {_lastSuccessfulRefresh:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                IsLive = false;
                StatusMessage = _lastSuccessfulRefresh is null
                    ? $"Could not load migrations: {ex.Message}"
                    : $"Live update failed. Showing data from {_lastSuccessfulRefresh:HH:mm:ss}; retrying.";
                if (showErrors)
                {
                    _notificationService.ShowError($"Failed to refresh: {ex.Message}", "Error");
                }
            }
            finally
            {
                _isRefreshing = false;
                if (showLoading)
                {
                    IsLoading = false;
                }
            }
        }

        private void UpdateRecentMigrations(IReadOnlyList<MigrationDetail> migrations)
        {
            for (var targetIndex = 0; targetIndex < migrations.Count; targetIndex++)
            {
                var migration = migrations[targetIndex];
                var currentIndex = -1;
                for (var index = targetIndex; index < RecentMigrations.Count; index++)
                {
                    if (RecentMigrations[index].Id == migration.Id)
                    {
                        currentIndex = index;
                        break;
                    }
                }

                if (currentIndex < 0)
                {
                    RecentMigrations.Insert(targetIndex, migration);
                    continue;
                }

                if (currentIndex != targetIndex)
                {
                    RecentMigrations.Move(currentIndex, targetIndex);
                }

                if (!HaveSameProgress(RecentMigrations[targetIndex], migration))
                {
                    RecentMigrations[targetIndex] = migration;
                }
            }

            while (RecentMigrations.Count > migrations.Count)
            {
                RecentMigrations.RemoveAt(RecentMigrations.Count - 1);
            }
        }

        private static bool HaveSameProgress(
            MigrationDetail current,
            MigrationDetail updated) =>
            current.Status == updated.Status &&
            current.Progress == updated.Progress &&
            current.CurrentStep == updated.CurrentStep &&
            current.UpdatedAt == updated.UpdatedAt &&
            current.ElapsedSeconds == updated.ElapsedSeconds &&
            current.RunningSince == updated.RunningSince &&
            current.Error == updated.Error &&
            current.Logs.SequenceEqual(updated.Logs);

        [RelayCommand]
        private async Task ViewMigration(MigrationDetail migration)
        {
            if (migration == null) return;

            try
            {
                var detail = await _apiService.GetMigrationAsync(migration.Id);
                if (detail != null)
                {
                    new MigrationDiagnosticsWindow(detail)
                    {
                        Owner = Application.Current.MainWindow
                    }.ShowDialog();
                }
                else
                {
                    _notificationService.ShowError(
                        "Could not load the migration diagnostics. Check the API and try again.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to load migration details: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task CancelMigrationAsync(MigrationDetail migration)
        {
            if (migration is null || !migration.CanCancel)
            {
                return;
            }

            if (!_notificationService.ShowConfirmation(
                "Stop this migration? Active child processes will be stopped. Completed checkpoints stay saved; " +
                "Resume repeats the incomplete stage, including a whole incomplete SVN import. " +
                "Already-created repositories and pushed refs are not rolled back.", "Cancel migration"))
            {
                return;
            }

            try
            {
                await _apiService.CancelMigrationAsync(migration.Id);
                StatusMessage = "Cancellation requested. Wait for the job to stop before resuming or editing its workspace.";
                await RefreshDashboardAsync(showErrors: false, showLoading: false);
            }
            catch (Exception exception)
            {
                _notificationService.ShowError($"Cancellation failed: {exception.Message}", "Cancellation failed");
            }
        }

        [RelayCommand]
        private async Task ResumeMigrationAsync(MigrationDetail migration)
        {
            if (migration is null || !migration.CanResume)
            {
                return;
            }

            var dialog = new ResumeMigrationWindow(migration)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var githubToken = dialog.TakeGithubToken();
            try
            {
                await _apiService.ResumeMigrationAsync(migration.Id, githubToken);
                StatusMessage =
                    $"Resume queued for {migration.GithubOrg}/{migration.GithubRepo}. " +
                    "The dashboard will update its progress automatically.";
                await RefreshDashboardAsync(showErrors: false, showLoading: false);
            }
            catch (Exception exception)
            {
                _notificationService.ShowError(
                    $"Could not resume the migration: {exception.Message}",
                    "Resume Failed");
            }
        }

        [RelayCommand]
        private void ConfigureAi()
        {
            var dialog = new AiSettingsWindow
            {
                Owner = Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
            if (dialog.SettingsChanged)
            {
                AiSettingsUpdated?.Invoke();
                StatusMessage = result == true
                    ? "AI assistant settings saved."
                    : "AI assistant settings updated.";
            }
        }

        [RelayCommand]
        private async Task AskAi(MigrationDetail migration)
        {
            if (migration == null)
            {
                return;
            }

            try
            {
                var detail = await _apiService.GetMigrationAsync(migration.Id);
                if (detail == null)
                {
                    _notificationService.ShowError(
                        "Could not load the latest migration details and logs.",
                        "Migration Unavailable");
                    return;
                }

                AssistantRequested?.Invoke(detail);
            }
            catch (Exception exception)
            {
                _notificationService.ShowError(
                    $"Could not open the AI assistant: {exception.Message}",
                    "AI Assistant Error");
            }
        }
    }
}
