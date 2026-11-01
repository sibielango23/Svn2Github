using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SVN2GitHub.Models;
using SVN2GitHub.Services;
using SVN2GitHub.Views;
using System.Windows;

namespace SVN2GitHub.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AiProviderSettingsStore _aiSettingsStore = new();
        private readonly AiAssistantService _aiAssistantService = new();
        private readonly AiChatHistoryStore _aiChatHistoryStore = new();
        private readonly NotificationService _notificationService = new();

        [ObservableProperty]
        private string _title = "SVN to GitHub Migration Tool";

        [ObservableProperty]
        private string _pageTitle = "Migration dashboard";

        [ObservableProperty]
        private string _pageSubtitle = "A clear view of your migration activity.";

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private AiAssistantViewModel? _assistantViewModel;

        [ObservableProperty]
        private bool _isAssistantPanelOpen;

        public DashboardViewModel DashboardViewModel { get; }
        public NewMigrationViewModel NewMigrationViewModel { get; }

        public MainViewModel()
        {
            DashboardViewModel = new DashboardViewModel();
            DashboardViewModel.AssistantRequested += OpenMigrationAssistant;
            DashboardViewModel.AiSettingsUpdated += ResetAssistant;
            DashboardViewModel.NewMigrationRequested += ShowNewMigration;
            NewMigrationViewModel = new NewMigrationViewModel();
            CurrentView = new DashboardView { DataContext = DashboardViewModel };
            _ = DashboardViewModel.InitializeAsync();
        }

        [RelayCommand]
        private async Task ShowDashboardAsync()
        {
            PageTitle = "Migration dashboard";
            PageSubtitle = "A clear view of your migration activity.";
            CurrentView = new DashboardView { DataContext = DashboardViewModel };
            await DashboardViewModel.RefreshAsync();
        }

        [RelayCommand]
        private void ShowNewMigration()
        {
            PageTitle = "New migration";
            PageSubtitle = "Move your SVN history to GitHub in a few guided steps.";
            CurrentView = new NewMigrationView { DataContext = NewMigrationViewModel };
        }

        [RelayCommand]
        private void OpenAiAssistant()
        {
            try
            {
                var settings = _aiSettingsStore.Load();
                if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    var settingsWindow = new AiSettingsWindow
                    {
                        Owner = Application.Current.MainWindow
                    };
                    if (settingsWindow.ShowDialog() != true)
                    {
                        return;
                    }

                    settings = _aiSettingsStore.Load();
                }

                if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    _notificationService.ShowWarning(
                        "Save an AI provider, model ID, and API key before starting a chat.",
                        "AI Assistant Setup");
                    return;
                }

                AssistantViewModel ??= new AiAssistantViewModel(
                    settings,
                    _aiAssistantService,
                    _aiChatHistoryStore,
                    () => IsAssistantPanelOpen = false);
                IsAssistantPanelOpen = true;
            }
            catch (Exception exception)
            {
                _notificationService.ShowError(
                    $"Could not open the AI assistant: {exception.Message}",
                    "AI Assistant Error");
            }
        }

        [RelayCommand]
        private void CloseAiAssistant()
        {
            IsAssistantPanelOpen = false;
        }

        private void OpenMigrationAssistant(MigrationDetail migration)
        {
            try
            {
                var settings = _aiSettingsStore.Load();
                if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    var settingsWindow = new AiSettingsWindow
                    {
                        Owner = Application.Current.MainWindow
                    };
                    if (settingsWindow.ShowDialog() != true)
                    {
                        return;
                    }

                    settings = _aiSettingsStore.Load();
                }

                if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    _notificationService.ShowWarning(
                        "Save an AI provider, model ID, and API key before starting a chat.",
                        "AI Assistant Setup");
                    return;
                }

                AssistantViewModel ??= new AiAssistantViewModel(
                    settings,
                    _aiAssistantService,
                    _aiChatHistoryStore,
                    () => IsAssistantPanelOpen = false);
                AssistantViewModel.StartNewConversation(migration);
                IsAssistantPanelOpen = true;
            }
            catch (Exception exception)
            {
                _notificationService.ShowError(
                    $"Could not open the AI assistant: {exception.Message}",
                    "AI Assistant Error");
            }
        }

        private void ResetAssistant()
        {
            IsAssistantPanelOpen = false;
            AssistantViewModel = null;
        }
    }
}
