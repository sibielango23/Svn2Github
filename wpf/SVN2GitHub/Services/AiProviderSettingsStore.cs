using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SVN2GitHub.Models;

namespace SVN2GitHub.Services
{
    public sealed class AiProviderSettingsStore
    {
        private readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SVN2GitHub",
            "ai-settings.json");

        public AiProviderSettings? Load()
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            var stored = JsonSerializer.Deserialize<StoredSettings>(
                File.ReadAllText(_settingsPath))
                ?? throw new InvalidDataException("The saved AI settings file is invalid.");

            if (!Enum.IsDefined(stored.Provider) ||
                string.IsNullOrWhiteSpace(stored.Model) ||
                string.IsNullOrWhiteSpace(stored.EncryptedApiKey))
            {
                throw new InvalidDataException("The saved AI settings are incomplete or invalid.");
            }

            var encryptedKey = Convert.FromBase64String(stored.EncryptedApiKey);
            var decryptedKey = ProtectedData.Unprotect(
                encryptedKey,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            var apiKey = Encoding.UTF8.GetString(decryptedKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidDataException("The saved AI API key is empty.");
            }

            return new AiProviderSettings
            {
                Provider = stored.Provider,
                Model = stored.Model,
                ApiKey = apiKey
            };
        }

        public void Save(AiProviderSettings settings)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.Model);
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ApiKey);

            var encryptedKey = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(settings.ApiKey),
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);
            var stored = new StoredSettings
            {
                Provider = settings.Provider,
                Model = settings.Model.Trim(),
                EncryptedApiKey = Convert.ToBase64String(encryptedKey)
            };

            var directory = Path.GetDirectoryName(_settingsPath)
                ?? throw new InvalidOperationException("The AI settings path has no parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(stored));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }

        private sealed class StoredSettings
        {
            public AiProvider Provider { get; set; }
            public string Model { get; set; } = string.Empty;
            public string EncryptedApiKey { get; set; } = string.Empty;
        }
    }
}
