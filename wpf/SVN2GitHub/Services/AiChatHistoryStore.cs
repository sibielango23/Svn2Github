using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SVN2GitHub.Models;

namespace SVN2GitHub.Services
{
    public sealed class AiChatHistoryStore
    {
        private readonly string _historyPath;

        public AiChatHistoryStore(string? historyPath = null)
        {
            _historyPath = historyPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SVN2GitHub",
                "ai-chat-history.dat");
        }

        public List<AiChatConversation> Load()
        {
            if (!File.Exists(_historyPath))
            {
                return new List<AiChatConversation>();
            }

            var protectedData = File.ReadAllBytes(_historyPath);
            var json = ProtectedData.Unprotect(
                protectedData,
                optionalEntropy: Encoding.UTF8.GetBytes("SVN2GitHub chat history"),
                scope: DataProtectionScope.CurrentUser);

            return JsonSerializer.Deserialize<List<AiChatConversation>>(json)
                ?? throw new InvalidDataException("The saved AI chat history is invalid.");
        }

        public void Save(IReadOnlyCollection<AiChatConversation> conversations)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(conversations);
            var protectedData = ProtectedData.Protect(
                json,
                optionalEntropy: Encoding.UTF8.GetBytes("SVN2GitHub chat history"),
                scope: DataProtectionScope.CurrentUser);

            var directory = Path.GetDirectoryName(_historyPath)
                ?? throw new InvalidOperationException("The AI chat history path has no parent directory.");
            Directory.CreateDirectory(directory);

            var temporaryPath = _historyPath + ".tmp";
            File.WriteAllBytes(temporaryPath, protectedData);
            File.Move(temporaryPath, _historyPath, overwrite: true);
        }
    }
}
