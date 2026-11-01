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
            Assert.True(recovered.CanResume);
            Assert.Equal(MigrationStages.None, recovered.LastCompletedStage);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void Resume_PreservesCheckpointAndNeverPersistsFreshToken()
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
                GithubRepo = "resume-test",
                GithubToken = "initial-token"
            });
            store.CompleteStage(
                response.Id,
                MigrationStages.GitHubSetup,
                "pushing",
                "GitHub repository created",
                90,
                "GitHub repository is ready");
            store.UpdateMigration(
                response.Id,
                "failed",
                "Migration failed",
                90,
                "Push failed",
                "Push failed");

            var recoveredStore = new MigrationStateStore(configuration, new TestEnvironment());
            recoveredStore.Initialize();
            Assert.True(recoveredStore.TryPrepareResume(
                response.Id,
                "fresh-token-must-not-be-persisted",
                out var error));
            Assert.Empty(error);

            var resumed = recoveredStore.FindMigration(response.Id);
            Assert.NotNull(resumed);
            Assert.Equal("pending", resumed.Detail.Status);
            Assert.Equal(MigrationStages.GitHubSetup, resumed.Detail.LastCompletedStage);
            Assert.Equal("fresh-token-must-not-be-persisted", resumed.GithubToken);
            recoveredStore.ClearGithubToken(response.Id);
            Assert.Empty(recoveredStore.FindMigration(response.Id)!.GithubToken);

            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = Path.Combine(dataDirectory, "local_migrations.sqlite3")
                }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT record_json FROM migration_state WHERE id = $id";
            command.Parameters.AddWithValue("$id", response.Id);
            var json = (string)command.ExecuteScalar()!;
            Assert.DoesNotContain("fresh-token-must-not-be-persisted", json, StringComparison.Ordinal);
            Assert.Contains(MigrationStages.GitHubSetup, json, StringComparison.Ordinal);

            var afterResumeRestart = new MigrationStateStore(configuration, new TestEnvironment());
            afterResumeRestart.Initialize();
            var interruptedAgain = afterResumeRestart.FindMigration(response.Id);
            Assert.NotNull(interruptedAgain);
            Assert.Equal("failed", interruptedAgain.Detail.Status);
            Assert.Equal(MigrationStages.GitHubSetup, interruptedAgain.Detail.LastCompletedStage);
            Assert.True(interruptedAgain.Detail.CanResume);
            Assert.Empty(interruptedAgain.GithubToken);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void Resume_RejectsActiveMigrationsAndMissingTokens()
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
                GithubRepo = "resume-active"
            });

            Assert.False(store.TryPrepareResume(response.Id, "token", out var activeError));
            Assert.Contains("failed or interrupted", activeError, StringComparison.OrdinalIgnoreCase);

            store.UpdateMigration(response.Id, "failed", "Migration failed", 0, "Failed", "Failed");
            Assert.False(store.TryPrepareResume(response.Id, " ", out var tokenError));
            Assert.Contains("token is required", tokenError, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void ProcessingTime_ExcludesQueueAndRetryWaits_AndAccumulatesAttempts()
    {
        var dataDirectory = CreateTempDirectory();
        try
        {
            var clock = new TestClock();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["DataDirectory"] = dataDirectory }).Build();
            var store = new MigrationStateStore(configuration, new TestEnvironment(), clock);
            store.Initialize();
            var id = store.Create(new MigrationRequest { SvnUrl = "https://svn.example.test/repo" }).Id;
            clock.Advance(TimeSpan.FromHours(2));
            Assert.Null(store.GetDetail(id)!.ElapsedSeconds);
            store.StartProcessing(id);
            clock.Advance(TimeSpan.FromSeconds(65));
            Assert.Equal(65d, store.GetDetail(id)!.ElapsedSeconds);
            store.UpdateMigration(id, "failed", "Failed", 50, "Failed");
            Assert.Null(store.GetDetail(id)!.RunningSince);
            clock.Advance(TimeSpan.FromHours(3));
            Assert.Equal(65d, store.GetDetail(id)!.ElapsedSeconds);
            Assert.True(store.TryPrepareResume(id, "fresh-token", out _));
            clock.Advance(TimeSpan.FromHours(1));
            store.StartProcessing(id);
            clock.Advance(TimeSpan.FromSeconds(35));
            store.UpdateMigration(id, "completed", "Done", 100, "Done");
            Assert.Equal(100d, store.GetDetail(id)!.ElapsedSeconds);
            clock.Advance(TimeSpan.FromDays(1));
            var recovered = new MigrationStateStore(configuration, new TestEnvironment(), clock);
            recovered.Initialize();
            Assert.Equal(100d, recovered.GetDetail(id)!.ElapsedSeconds);
            Assert.Null(recovered.GetDetail(id)!.RunningSince);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void ProcessingTime_UncleanRestartRetainsSavedSample_NotOfflineTime()
    {
        var dataDirectory = CreateTempDirectory();
        try
        {
            var clock = new TestClock();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["DataDirectory"] = dataDirectory }).Build();
            var store = new MigrationStateStore(configuration, new TestEnvironment(), clock);
            store.Initialize();
            var id = store.Create(new MigrationRequest { SvnUrl = "https://svn.example.test/repo" }).Id;
            store.StartProcessing(id);
            clock.Advance(TimeSpan.FromHours(2));
            store.SaveProcessingTime(id);
            Assert.Equal(7200d, store.GetDetail(id)!.ElapsedSeconds);
            clock.Advance(TimeSpan.FromDays(2));
            var recovered = new MigrationStateStore(configuration, new TestEnvironment(), clock);
            recovered.Initialize();
            var detail = recovered.GetDetail(id)!;
            Assert.Equal("failed", detail.Status);
            Assert.Equal(7200d, detail.ElapsedSeconds);
            Assert.Null(detail.RunningSince);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTempDirectory(dataDirectory);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
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
