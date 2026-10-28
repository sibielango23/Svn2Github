using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace SVN2GitHub.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "SVN to GitHub Migration Tool";

        [ObservableProperty]
        private object? _currentView;

        public DashboardViewModel DashboardViewModel { get; }
        public NewMigrationViewModel NewMigrationViewModel { get; }

        public MainViewModel()
        {
            DashboardViewModel = new DashboardViewModel();
            NewMigrationViewModel = new NewMigrationViewModel();

            // Start with Dashboard
            CurrentView = new Views.DashboardView { DataContext = DashboardViewModel };

            // Initialize dashboard
            _ = DashboardViewModel.InitializeAsync();
        }

        [RelayCommand]
        private async Task ShowDashboardAsync()
        {
            CurrentView = new Views.DashboardView { DataContext = DashboardViewModel };
            await DashboardViewModel.RefreshAsync();
        }

        [RelayCommand]
        private void ShowNewMigration()
        {
            CurrentView = new Views.NewMigrationView { DataContext = NewMigrationViewModel };
        }
    }
}
