using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using SVN2GitHub.Models;

namespace SVN2GitHub.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService(string baseUrl = "http://localhost:5000")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl)
            };
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

        public async Task<MigrationResponse> CreateMigrationAsync(MigrationRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/migrations", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MigrationResponse>()
                ?? throw new Exception("Failed to create migration");
        }

        public async Task<bool> CancelMigrationAsync(string migrationId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"/api/migrations/{migrationId}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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
