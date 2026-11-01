using System.Net.Http;
using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using SVN2GitHub.Models;

namespace SVN2GitHub.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService(string? baseUrl = null, HttpClient? httpClient = null)
        {
            _baseUrl = baseUrl
                ?? Environment.GetEnvironmentVariable("SVN2GITHUB_API_URL")
                ?? "http://127.0.0.1:5000";
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Stats> GetStatsAsync()
        {
            var response = await _httpClient.GetAsync("/api/stats");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Stats>()
                ?? new Stats();
        }

        public async Task<List<MigrationDetail>> GetMigrationsAsync()
        {
            var response = await _httpClient.GetAsync("/api/migrations");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<MigrationDetail>>()
                ?? new List<MigrationDetail>();
        }

        public async Task<MigrationDetail?> GetMigrationAsync(string migrationId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/migrations/{migrationId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<MigrationDetail>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<MigrationDetail> GetMigrationSnapshotAsync(
            string migrationId,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(
                $"/api/migrations/{migrationId}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MigrationDetail>(cancellationToken)
                ?? throw new InvalidDataException("The API returned empty migration details.");
        }

        public async Task<MigrationResponse> CreateMigrationAsync(MigrationRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/migrations", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MigrationResponse>()
                ?? throw new Exception("Failed to create migration");
        }

        public async Task ResumeMigrationAsync(string migrationId, string githubToken)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/migrations/{migrationId}/resume",
                new ResumeMigrationRequest { GithubToken = githubToken });
            response.EnsureSuccessStatusCode();
        }

        public async Task<ReadinessReport> CheckReadinessAsync(
            bool cleanHistory,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/readiness", new { CleanHistory = cleanHistory }, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ReadinessReport>(cancellationToken)
                ?? throw new InvalidDataException("The API returned an empty readiness report.");
        }

        public async Task<ReadinessReport> AssessMigrationAsync(
            MigrationRequest request, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/assessment", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ReadinessReport>(cancellationToken)
                ?? throw new InvalidDataException("The API returned an empty assessment report.");
        }

        public async Task CancelMigrationAsync(string migrationId)
        {
            using var response = await _httpClient.DeleteAsync($"/api/migrations/{migrationId}");
            response.EnsureSuccessStatusCode();
        }

        // Author Mapping APIs
        public async Task<List<Author>> GetAuthorsAsync(string migrationId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/authors/{migrationId}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<List<Author>>()
                    ?? new List<Author>();
            }
            catch
            {
                return new List<Author>();
            }
        }

        public async Task PrepareAuthorSessionAsync(string sessionId, string svnUrl)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/authors/{sessionId}/session",
                new { SvnUrl = svnUrl });
            response.EnsureSuccessStatusCode();
        }

        public async Task CloseAuthorSessionAsync(string sessionId)
        {
            var response = await _httpClient.DeleteAsync($"/api/authors/{sessionId}/session");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<Author>> ExtractAuthorsAsync(string migrationId)
        {
            var response = await _httpClient.PostAsync($"/api/authors/{migrationId}/extract", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Author>>()
                ?? new List<Author>();
        }

        public async Task<Author> CreateAuthorAsync(string migrationId, AuthorCreateRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/authors/{migrationId}", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Author>()
                ?? throw new Exception("Failed to create author");
        }

        public async Task<Author> UpdateAuthorAsync(string migrationId, int authorId, AuthorCreateRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/authors/{migrationId}/{authorId}", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Author>()
                ?? throw new Exception("Failed to update author");
        }

        public async Task<List<Author>> BulkUpdateAuthorsAsync(string migrationId, AuthorBulkUpdateRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/authors/{migrationId}/bulk", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<Author>>()
                ?? new List<Author>();
        }

        public async Task<bool> DeleteAuthorAsync(string migrationId, int authorId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/authors/{migrationId}/{authorId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> ExportAuthorsAsync(string migrationId)
        {
            var response = await _httpClient.GetAsync($"/api/authors/{migrationId}/export");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return result?["content"] ?? string.Empty;
        }
    }
}
