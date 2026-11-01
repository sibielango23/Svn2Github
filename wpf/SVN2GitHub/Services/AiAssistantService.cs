using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using SVN2GitHub.Models;

namespace SVN2GitHub.Services
{
    public sealed class AiAssistantService
    {
        private static readonly HttpClient DefaultHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
        private readonly HttpClient _httpClient;

        public AiAssistantService(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? DefaultHttpClient;
        }

        private static readonly Regex SecretPattern = new(
            @"(?<Token>\b(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})\b)|" +
            @"(?<ProviderKey>\bsk-(?:proj-|ant-)?[A-Za-z0-9_-]{20,}\b)|" +
            @"(?<Url>(?<Scheme>https?|svn|svn\+ssh)://[^/\s:@]+:[^/@\s]+@)|" +
            @"(?<Bearer>(?<BearerPrefix>authorization\s*[:=]\s*bearer\s+)[^\s,;]+)|" +
            @"(?<Assignment>(?<SecretName>\b(?:token|password|passwd|secret|api[_-]?key)\b)" +
            @"(?<Separator>\s*[:=]\s*)[""']?[^""'\s,;]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static string RedactSensitiveData(string value)
        {
            var redacted = SecretPattern.Replace(value, match =>
            {
                if (match.Groups["Url"].Success)
                {
                    return match.Groups["Scheme"].Value + "://[REDACTED]@";
                }

                if (match.Groups["Bearer"].Success)
                {
                    return match.Groups["BearerPrefix"].Value + "[REDACTED]";
                }

                if (match.Groups["Assignment"].Success)
                {
                    return match.Groups["SecretName"].Value +
                           match.Groups["Separator"].Value + "[REDACTED]";
                }

                return "[REDACTED]";
            });

            return new string(redacted.Where(character =>
                character is '\r' or '\n' or '\t' || !char.IsControl(character)).ToArray());
        }

        public async Task<string> GetResponseAsync(
            AiProviderSettings settings,
            string systemPrompt,
            IReadOnlyList<AiChatMessage> conversation,
            CancellationToken cancellationToken = default)
        {
            using var request = settings.Provider switch
            {
                AiProvider.OpenAI => CreateOpenAiRequest(settings, systemPrompt, conversation),
                AiProvider.Anthropic => CreateAnthropicRequest(settings, systemPrompt, conversation),
                _ => throw new InvalidOperationException("The selected AI provider is not supported.")
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var safeDetails = RedactSensitiveData(responseBody);
                if (safeDetails.Length > 500)
                {
                    safeDetails = safeDetails[..500];
                }

                throw new HttpRequestException(
                    $"The AI provider returned HTTP {(int)response.StatusCode}: {safeDetails}",
                    inner: null,
                    response.StatusCode);
            }

            using var document = JsonDocument.Parse(responseBody);
            return settings.Provider switch
            {
                AiProvider.OpenAI => ReadOpenAiResponse(document.RootElement),
                AiProvider.Anthropic => ReadAnthropicResponse(document.RootElement),
                _ => throw new InvalidOperationException("The selected AI provider is not supported.")
            };
        }

        private static HttpRequestMessage CreateOpenAiRequest(
            AiProviderSettings settings,
            string systemPrompt,
            IReadOnlyList<AiChatMessage> conversation)
        {
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };
            messages.AddRange(conversation.Select(message => new
            {
                role = message.Role,
                content = message.Content
            }));

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions")
            {
                Content = JsonContent.Create(new
                {
                    model = settings.Model,
                    messages
                })
            };
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            return request;
        }

        private static HttpRequestMessage CreateAnthropicRequest(
            AiProviderSettings settings,
            string systemPrompt,
            IReadOnlyList<AiChatMessage> conversation)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(new
                {
                    model = settings.Model,
                    max_tokens = 1200,
                    system = systemPrompt,
                    messages = conversation.Select(message => new
                    {
                        role = message.Role,
                        content = message.Content
                    })
                })
            };
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            return request;
        }

        private static string ReadOpenAiResponse(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.GetArrayLength() == 0 ||
                !choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content))
            {
                throw new InvalidDataException("The OpenAI response did not contain a message.");
            }

            var text = content.GetString();
            return !string.IsNullOrWhiteSpace(text)
                ? text
                : throw new InvalidDataException("The OpenAI response message was empty.");
        }

        private static string ReadAnthropicResponse(JsonElement root)
        {
            if (!root.TryGetProperty("content", out var content))
            {
                throw new InvalidDataException("The Anthropic response did not contain content.");
            }

            var text = content.EnumerateArray()
                .Where(block => block.TryGetProperty("type", out var type) &&
                                type.GetString() == "text")
                .Select(block => block.GetProperty("text").GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value));
            var response = string.Join(Environment.NewLine, text);

            return string.IsNullOrWhiteSpace(response)
                ? throw new InvalidDataException("The Anthropic response did not contain text.")
                : response;
        }
    }
}
