using CommunityToolkit.Mvvm.ComponentModel;

namespace SVN2GitHub.Models
{
    public partial class AiChatConversation : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [ObservableProperty]
        private string _title = "New chat";

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
        public bool IsMigrationContext { get; set; }
        public string? MigrationId { get; set; }
        public string LocalGuidance { get; set; } = string.Empty;
        public string ContextPrompt { get; set; } = string.Empty;
        public int ContextVersion { get; set; }
        public string MigrationLogs { get; set; } = string.Empty;
        public List<AiChatMessage> Messages { get; set; } = new();
    }
}
