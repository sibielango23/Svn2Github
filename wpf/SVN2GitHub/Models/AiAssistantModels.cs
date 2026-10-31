namespace SVN2GitHub.Models
{
    public enum AiProvider
    {
        OpenAI,
        Anthropic
    }

    public sealed class AiProviderSettings
    {
        public AiProvider Provider { get; set; }
        public string Model { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    public sealed class AiChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
