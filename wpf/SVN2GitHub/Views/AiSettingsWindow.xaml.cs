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
                KeyStatusText.Text = "An API key is saved and protected for this Windows user.";
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
                    KeyStatusText.Text = "A protected API key for this provider will be kept unless replaced.";
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
    }
}
