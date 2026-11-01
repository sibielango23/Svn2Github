using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SVN2GitHub.Models;
using SVN2GitHub.Services;

namespace SVN2GitHub.Views
{
    public partial class AiSettingsWindow : Window
    {
        private readonly AiProviderSettingsStore _settingsStore = new();
        private AiProviderSettings? _existingSettings;
        private int _lastSelectedProviderIndex;

        public bool SettingsChanged { get; private set; }

        public AiSettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                _existingSettings = _settingsStore.Load();
                if (_existingSettings == null)
                {
                    return;
                }

                ProviderBox.SelectedIndex = _existingSettings.Provider == AiProvider.OpenAI ? 0 : 1;
                ModelBox.Text = _existingSettings.Model;
                var hasSavedKey = !string.IsNullOrWhiteSpace(_existingSettings.ApiKey);
                KeyStatusText.Text = hasSavedKey
                    ? "An API key is saved and protected for this Windows user."
                    : "No API key is saved. Provider and model settings are retained.";
                ClearKeyButton.IsEnabled = hasSavedKey;
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                CryptographicException or
                JsonException or
                FormatException)
            {
                _existingSettings = null;
                KeyStatusText.Text = "Saved settings could not be read. Enter a new API key to replace them.";
                StatusText.Text = exception.Message;
            }
        }

        private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelBox == null)
            {
                return;
            }

            if (ProviderBox.SelectedIndex != _lastSelectedProviderIndex)
            {
                var provider = ProviderBox.SelectedIndex == 1
                    ? AiProvider.Anthropic
                    : AiProvider.OpenAI;
                if (_existingSettings?.Provider == provider)
                {
                    ModelBox.Text = _existingSettings.Model;
                    KeyStatusText.Text = string.IsNullOrWhiteSpace(_existingSettings.ApiKey)
                        ? "No API key is saved. Provider and model settings are retained."
                        : "A protected API key for this provider will be kept unless replaced.";
                }
                else
                {
                    ModelBox.Text = provider == AiProvider.OpenAI ? "gpt-4o-mini" : string.Empty;
                    KeyStatusText.Text = "Enter an API key for the selected provider.";
                }

                _lastSelectedProviderIndex = ProviderBox.SelectedIndex;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProvider = ProviderBox.SelectedIndex == 1
                ? AiProvider.Anthropic
                : AiProvider.OpenAI;
            var apiKey = string.IsNullOrWhiteSpace(ApiKeyBox.Password)
                ? _existingSettings?.Provider == selectedProvider
                    ? _existingSettings.ApiKey
                    : null
                : ApiKeyBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(ModelBox.Text))
            {
                StatusText.Text = "Enter a model ID.";
                return;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                StatusText.Text = "Enter an API key before saving.";
                return;
            }

            var settings = new AiProviderSettings
            {
                Provider = selectedProvider,
                Model = ModelBox.Text.Trim(),
                ApiKey = apiKey
            };

            try
            {
                _settingsStore.Save(settings);
                SettingsChanged = true;
                DialogResult = true;
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                CryptographicException or
                ArgumentException or
                InvalidOperationException)
            {
                StatusText.Text = $"Could not save AI settings: {exception.Message}";
            }
        }

        private void ClearKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    this,
                    "Remove the saved AI API key from this Windows user account? Provider and model settings will be kept.",
                    "Clear saved AI key",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _settingsStore.ClearApiKey();
                if (_existingSettings is not null)
                {
                    _existingSettings.ApiKey = string.Empty;
                }

                ApiKeyBox.Clear();
                ClearKeyButton.IsEnabled = false;
                KeyStatusText.Text = "No API key is saved. Provider and model settings are retained.";
                StatusText.Text = "Saved AI API key cleared.";
                SettingsChanged = true;
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                CryptographicException or
                JsonException or
                FormatException)
            {
                StatusText.Text = $"Could not clear the saved AI key: {exception.Message}";
            }
        }
    }
}
