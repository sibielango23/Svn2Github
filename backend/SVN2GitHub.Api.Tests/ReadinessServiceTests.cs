using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using SVN2GitHub.Api.Services;

namespace SVN2GitHub.Api.Tests;

public sealed class ReadinessServiceTests
{
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 5)]
    public async Task Readiness_ChecksOnlyLocalVersionCommands_AndCleansProbes(bool cleanup, int expectedCommands)
    {
        var root = Path.Combine(Path.GetTempPath(), "readiness-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataDirectory"] = Path.Combine(root, "data"),
                    ["WorkspaceDirectory"] = Path.Combine(root, "workspaces")
                }).Build();
            var runner = new StubRunner();
            var environment = new TestEnvironment();
            var store = new MigrationStateStore(configuration, environment);
            var service = new ReadinessService(
                runner, store, configuration, environment, NullLogger<ReadinessService>.Instance);
            var report = await service.CheckAsync(cleanup, CancellationToken.None);
            Assert.False(report.HasBlockingIssues);
            Assert.Equal(expectedCommands, runner.Calls.Count);
            Assert.All(runner.Calls, arguments => Assert.Contains("--version", arguments));
            Assert.Contains(report.Checks, check => check.Name == "Repository access and author coverage" && check.Status == "warning");
            Assert.Equal(cleanup, report.Checks.Any(check => check.Name == "History rewrite"));
            Assert.Empty(Directory.GetFiles(root, ".readiness-*", SearchOption.AllDirectories));
            Assert.NotEmpty(report.CheckedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Readiness_MissingTool_IsBlocking_WithoutExposingCommandOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), "readiness-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataDirectory"] = Path.Combine(root, "data"),
                    ["WorkspaceDirectory"] = Path.Combine(root, "workspaces")
                }).Build();
            var environment = new TestEnvironment();
            var store = new MigrationStateStore(configuration, environment);
            var runner = new StubRunner { ExitCode = 1 };
            var report = await new ReadinessService(
                runner, store, configuration, environment, NullLogger<ReadinessService>.Instance)
                .CheckAsync(false, CancellationToken.None);
            Assert.True(report.HasBlockingIssues);
            Assert.Equal(4, report.Checks.Count(check => check.Status == "failed"));
            Assert.DoesNotContain(report.Checks, check => check.Detail.Contains("secret-output"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Readiness_CancellationPropagates()
    {
        var root = Path.Combine(Path.GetTempPath(), "readiness-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["DataDirectory"] = root }).Build();
            var environment = new TestEnvironment();
            var store = new MigrationStateStore(configuration, environment);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => new ReadinessService(
                new StubRunner(), store, configuration, environment, NullLogger<ReadinessService>.Instance)
                .CheckAsync(false, cancellation.Token));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Readiness_WorkspaceBlockedByAFile_IsBlocking()
    {
        var root = Path.Combine(Path.GetTempPath(), "readiness-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var blocked = Path.Combine(root, "blocked");
            await File.WriteAllTextAsync(blocked, "keep-me");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataDirectory"] = Path.Combine(root, "data"),
                    ["WorkspaceDirectory"] = blocked
                }).Build();
            var environment = new TestEnvironment();
            var store = new MigrationStateStore(configuration, environment);
            var report = await new ReadinessService(
                new StubRunner(), store, configuration, environment, NullLogger<ReadinessService>.Instance)
                .CheckAsync(false, CancellationToken.None);
            Assert.True(report.HasBlockingIssues);
            Assert.Contains(report.Checks, check => check.Name == "Workspace directory" && check.Status == "failed");
            Assert.Equal("keep-me", await File.ReadAllTextAsync(blocked));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    private sealed class StubRunner : IExternalCommandRunner
    {
        public List<string[]> Calls { get; } = [];
        public int ExitCode { get; set; }

        public Task<CommandResult> RunAsync(
            string executable,
            IEnumerable<string> arguments,
            string? workingDirectory = null,
            TimeSpan? timeout = null,
            IReadOnlyDictionary<string, string?>? environment = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Null(environment);
            Assert.Equal(TimeSpan.FromSeconds(15), timeout);
            Calls.Add(arguments.ToArray());
            return Task.FromResult(new CommandResult(ExitCode, "version 1.0", "secret-output"));
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ReadinessTests";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
