using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using SVN2GitHub.Services;
using SVN2GitHub.Models;

namespace SVN2GitHub.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ApiService _apiService;
        private readonly NotificationService _notificationService;

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

        public DashboardViewModel()
        {
            _apiService = new ApiService();
            _notificationService = new NotificationService();
        }

        public async Task InitializeAsync()
        {
            await RefreshAsync();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsLoading = true;
            StatusMessage = "Refreshing...";

            try
            {
                // Check if API is healthy
                var isHealthy = await _apiService.IsHealthyAsync();
                if (!isHealthy)
                {
                    StatusMessage = "⚠️ API server not running. Please start the API.";
                    _notificationService.ShowWarning(
                        "The API server is not running. Please start it with:\npython api/main.py",
                        "API Not Available");
                    return;
                }

                // Get stats
                var stats = await _apiService.GetStatsAsync();
                TotalMigrations = stats.Total;
                ActiveMigrations = stats.Active;
                CompletedMigrations = stats.Completed;
                FailedMigrations = stats.Failed;

                // Get recent migrations
                var migrations = await _apiService.GetMigrationsAsync();
                RecentMigrations.Clear();
                foreach (var migration in migrations.OrderByDescending(m => m.UpdatedAt).Take(10))
                {
                    RecentMigrations.Add(migration);
                }

                StatusMessage = $"Last updated: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading data";
                _notificationService.ShowError($"Failed to refresh: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ViewMigration(MigrationDetail migration)
        {
            if (migration == null) return;

            try
            {
                var detail = await _apiService.GetMigrationAsync(migration.Id);
                if (detail != null)
                {
                    var logsText = string.Join("\n", detail.Logs.TakeLast(20));
                    MessageBox.Show(
                        $"Status: {detail.Status}\n" +
                        $"Progress: {detail.Progress}%\n" +
                        $"Current Step: {detail.CurrentStep}\n\n" +
                        $"Recent Logs:\n{logsText}",
                        $"Migration: {detail.GithubOrg}/{detail.GithubRepo}",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to load migration details: {ex.Message}");
            }
        }
    }
}
