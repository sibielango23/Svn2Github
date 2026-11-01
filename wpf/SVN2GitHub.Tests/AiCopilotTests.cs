using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using SVN2GitHub.Models;
using SVN2GitHub.Services;
using SVN2GitHub.ViewModels;

namespace SVN2GitHub.Tests;

public sealed class AiCopilotTests
{
    [Fact]
    public void ClearingKey_PreservesPreferences_AndDoesNotPersistPlaintext()
    {
        WithTemporaryDirectory(root =>
        {
            var path = Path.Combine(root, "settings.json");
            var store = new AiProviderSettingsStore(path);
            var key = "sk-test-" + new string('a', 40);
            store.Save(new AiProviderSettings { Provider = AiProvider.Anthropic, Model = "test-model", ApiKey = key });
            Assert.DoesNotContain(key, File.ReadAllText(path));
            Assert.Equal(key, store.Load()!.ApiKey);
            store.ClearApiKey();
            var cleared = store.Load();
            Assert.NotNull(cleared);
            Assert.Equal(AiProvider.Anthropic, cleared.Provider);
            Assert.Equal("test-model", cleared.Model);
            Assert.Equal(string.Empty, cleared.ApiKey);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        });
    }

    [Fact]
    public void GuidedDraft_DoesNotSendOrAddMessages_AndContextIncludesCheckpoint()
    {
        WithTemporaryDirectory(root =>
        {
            var viewModel = CreateViewModel(root);
            viewModel.StartNewConversation(new MigrationDetail
            {
                Id = "migration-id", Status = "failed", LastCompletedStage = "github_setup"
            });
            viewModel.DraftGuidedTaskCommand.Execute("recover");
            Assert.Contains("checkpoint-aware", viewModel.Question);
            Assert.Empty(viewModel.Messages);
            Assert.False(viewModel.IsBusy);
            Assert.Contains("Last completed checkpoint: github_setup", viewModel.SharedContextPreview);
            Assert.Contains("fresh GitHub token", viewModel.LocalGuidance);
            Assert.True(viewModel.RefreshMigrationContextCommand.CanExecute(null));
            Assert.False(viewModel.IncludeLogs);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void SavedLegacyChat_UsesCurrentCapabilities_NotObsoleteResumeClaims(int contextVersion)
    {
        WithTemporaryDirectory(root =>
        {
            var store = new AiChatHistoryStore(Path.Combine(root, "chats.dat"));
            store.Save(new[]
            {
                new AiChatConversation
                {
                    IsMigrationContext = true,
                    ContextVersion = contextVersion,
                    ContextPrompt = "Jobs cannot resume after restart.\nMigration context:\nStatus: failed",
                    Messages = new List<AiChatMessage> { new() { Role = "user", Content = "old question" } }
                }
            });
            var viewModel = CreateViewModel(root);
            Assert.DoesNotContain("Jobs cannot resume after restart", viewModel.SharedContextPreview);
            Assert.Contains("Manual stage-based Resume", viewModel.SharedContextPreview);
            Assert.Contains("Confirmed Cancel", viewModel.SharedContextPreview);
            Assert.Contains("Status: failed", viewModel.SharedContextPreview);
            Assert.Single(viewModel.Messages);
            Assert.False(viewModel.RefreshMigrationContextCommand.CanExecute(null));
            Assert.Contains("older chat", viewModel.LocalGuidance);
        });
    }

    private static AiAssistantViewModel CreateViewModel(string root) =>
        new(new AiProviderSettings { Provider = AiProvider.OpenAI, Model = "test", ApiKey = "not-sent" },
            new AiAssistantService(), new AiChatHistoryStore(Path.Combine(root, "chats.dat")), () => { });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstSend_ShowsQuestionWhileWaiting_AndSavesReplyToInitiatingChat(bool switchChats)
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-send-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var client = new HttpClient(new ChatHandler((_, _) => reply.Task));
            var store = new AiChatHistoryStore(Path.Combine(root, "chats.dat"));
            var viewModel = new AiAssistantViewModel(
                new AiProviderSettings(), new AiAssistantService(client), store, () => { });
            viewModel.Question = "How do I start?";
            var send = viewModel.SendCommand.ExecuteAsync(null);
            var initiatingChat = viewModel.SelectedConversation!;
            Assert.Single(viewModel.Messages);
            Assert.Equal("How do I start?", viewModel.Messages[0].Content);
            Assert.True(viewModel.IsBusy);
            Assert.Contains("Contacting", viewModel.StatusMessage);
            Assert.False(viewModel.SendCommand.CanExecute(null));
            if (switchChats)
            {
                viewModel.StartNewConversation();
                Assert.Empty(viewModel.Messages);
            }

            reply.SetResult(ChatResponse("Use Check readiness first."));
            await send;
            Assert.Equal(2, initiatingChat.Messages.Count);
            Assert.False(viewModel.IsBusy);
            if (switchChats) Assert.Empty(viewModel.Messages);
            viewModel.SelectedConversation = initiatingChat;
            Assert.Equal(2, viewModel.Messages.Count);
            Assert.Equal("Use Check readiness first.", viewModel.Messages[1].Content);
            Assert.Equal(2, store.Load().Single(chat => chat.Id == initiatingChat.Id).Messages.Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("http")]
    [InlineData("empty")]
    [InlineData("timeout")]
    public async Task FailedFirstSend_RemainsVisibleAndPersisted_AndRetryExcludesFailedTurn(string failure)
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-send-" + Guid.NewGuid().ToString("N"));
        try
        {
            var calls = 0;
            using var client = new HttpClient(new ChatHandler(async (request, cancellationToken) =>
            {
                if (++calls == 1)
                {
                    return failure switch
                    {
                        "http" => new HttpResponseMessage(HttpStatusCode.Unauthorized)
                        {
                            Content = new StringContent("""{"error":"Invalid API key"}""")
                        },
                        "empty" => ChatResponse(" "),
                        _ => throw new TaskCanceledException("test timeout")
                    };
                }

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using var json = System.Text.Json.JsonDocument.Parse(body);
                Assert.Equal(2, json.RootElement.GetProperty("messages").GetArrayLength());
                return ChatResponse("Retry worked.");
            }));
            var store = new AiChatHistoryStore(Path.Combine(root, "chats.dat"));
            var viewModel = new AiAssistantViewModel(
                new AiProviderSettings(), new AiAssistantService(client), store, () => { });
            viewModel.Question = "How do I start?";
            await viewModel.SendCommand.ExecuteAsync(null);
            var conversation = viewModel.SelectedConversation;
            Assert.Single(viewModel.Messages);
            Assert.True(viewModel.Messages[0].IsFailed);
            Assert.True(viewModel.IsError);
            Assert.False(viewModel.IsBusy);
            Assert.Contains("Couldn't get a reply", viewModel.StatusMessage);
            Assert.Equal("How do I start?", viewModel.Question);
            Assert.True(store.Load().Single().Messages.Single().IsFailed);
            await viewModel.SendCommand.ExecuteAsync(null);
            Assert.Same(conversation, viewModel.SelectedConversation);
            Assert.Equal(3, viewModel.Messages.Count);
            Assert.Equal("Retry worked.", viewModel.Messages[2].Content);
            Assert.False(viewModel.IsError);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static HttpResponseMessage ChatResponse(string text) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { choices = new[] { new { message = new { content = text } } } })
    };

    private sealed class ChatHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }

    [Fact]
    public async Task SnapshotRefresh_UpdatesCheckpoint_DisablesLogs_WithoutCallingProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-refresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new SnapshotHandler(HttpStatusCode.OK));
            var viewModel = new AiAssistantViewModel(
                new AiProviderSettings(), new AiAssistantService(),
                new AiChatHistoryStore(Path.Combine(root, "chats.dat")), () => { },
                new ApiService("http://127.0.0.1:12345", client));
            viewModel.StartNewConversation(new MigrationDetail
            {
                Id = "test-id", Status = "failed", LastCompletedStage = "authors"
            });
            viewModel.IncludeLogs = true;
            await viewModel.RefreshMigrationContextCommand.ExecuteAsync(null);
            Assert.Contains("Last completed checkpoint: github_setup", viewModel.SharedContextPreview);
            Assert.Contains("fresh GitHub token", viewModel.LocalGuidance);
            Assert.False(viewModel.IncludeLogs);
            Assert.Empty(viewModel.Messages);
            Assert.False(viewModel.IsError);
            Assert.False(viewModel.IsBusy);
            Assert.Single(viewModel.SelectedConversation!.MigrationLogs.Split('\n'));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task FailedSnapshotRefresh_RetainsOldContext_AndReportsStaleness()
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-refresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new SnapshotHandler(HttpStatusCode.ServiceUnavailable));
            var viewModel = new AiAssistantViewModel(
                new AiProviderSettings(), new AiAssistantService(),
                new AiChatHistoryStore(Path.Combine(root, "chats.dat")), () => { },
                new ApiService("http://127.0.0.1:12345", client));
            viewModel.StartNewConversation(new MigrationDetail
            {
                Id = "test-id", Status = "failed", LastCompletedStage = "authors"
            });
            var oldContext = viewModel.SharedContextPreview;
            await viewModel.RefreshMigrationContextCommand.ExecuteAsync(null);
            Assert.Equal(oldContext, viewModel.SharedContextPreview);
            Assert.True(viewModel.IsError);
            Assert.Contains("do not treat it as current", viewModel.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class SnapshotHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("/api/migrations/test-id", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = JsonContent.Create(new MigrationDetail
                {
                    Id = "test-id", Status = "failed", LastCompletedStage = "github_setup",
                    Logs = new List<string> { "push failure" }
                })
            });
        }
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, true); }
    }
}
