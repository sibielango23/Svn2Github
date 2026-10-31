using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using SVN2GitHub.Api.Models;
using SVN2GitHub.Api.Services;

namespace SVN2GitHub.Api.Tests;

public sealed class MigrationStateStoreTests
{
    [Fact]
    public void MigrationPersistence_RetainsExistingDataButNeverStoresToken()
    {
        var dataDirectory = CreateTempDirectory();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataDirectory"] = dataDirectory
                })
                .Build();
            var store = new MigrationStateStore(configuration, new TestEnvironment());
            store.Initialize();
            var response = store.Create(new MigrationRequest
            {
                SvnUrl = "https://svn.example.test/repo",
                GithubOrg = "example",
                GithubRepo = "migration-test",
                GithubToken = "token-must-not-be-persisted",
                Authors =
                [
                    new AuthorInput
                    {
                        SvnUsername = "alice",
                        GitName = "Alice Example",
                        GitEmail = "alice@example.test"
                    }
                ]
            });

            var record = store.FindMigration(response.Id);
            Assert.NotNull(record);
            Assert.Equal("token-must-not-be-persisted", record.GithubToken);
            Assert.Equal(
                "alice",
                Assert.Single(store.GetLegacyAuthors()[response.Id])["svn_username"]);

            using (var connection = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = Path.Combine(dataDirectory, "local_migrations.sqlite3")
                       }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT record_json FROM migration_state WHERE id = $id";
                command.Parameters.AddWithValue("$id", response.Id);
                var json = (string)command.ExecuteScalar()!;
                Assert.DoesNotContain("token-must-not-be-persisted", json, StringComparison.Ordinal);
                Assert.Contains("alice@example.test", json, StringComparison.Ordinal);
            }

            var recoveredStore = new MigrationStateStore(configuration, new TestEnvironment());
            recoveredStore.Initialize();
            var recovered = recoveredStore.GetDetail(response.Id);
            Assert.NotNull(recovered);
            Assert.Equal("failed", recovered.Status);
            Assert.Contains("Backend restarted", recovered.Error, StringComparison.Ordinal);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void TemporaryAuthorSession_RetainsMergedAndEditedMappings()
    {
        var dataDirectory = CreateTempDirectory();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataDirectory"] = dataDirectory
                })
                .Build();
            var store = new MigrationStateStore(configuration, new TestEnvironment());
            store.Initialize();
            store.OpenAuthorSession("temp_mapping", "https://svn.example.test/repo");

            var authors = store.MergeExtractedAuthors("temp_mapping", ["alice.smith"]);

            Assert.Single(authors);
            Assert.Equal("Alice Smith", store.GetAuthors("temp_mapping")[0].GitName);
            authors[0].GitName = "Alice S.";
            store.ReplaceAuthors("temp_mapping", authors);
            Assert.Equal("Alice S.", store.GetAuthors("temp_mapping")[0].GitName);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SVN2GitHub.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SVN2GitHub.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
