using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SVN2GitHub.Api.Models;
using SVN2GitHub.Api.Services;

namespace SVN2GitHub.Api.Tests;

public sealed class MigrationSafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Verification_UsesFullRefNames_AndPersistsMismatchEvidence(bool mismatch)
    {
        using var fixture = new Fixture();
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var runner = new Runner((executable, args, environment, _) =>
        {
            Assert.Equal("git", executable);
            if (args[0] == "for-each-ref")
            {
                Assert.Contains("--format=%(objectname)\t%(refname)", args);
                return Task.FromResult(Ok($"{hash}\t{args[2]}{(args[2] == "refs/heads/" ? "main" : "v1")}\n"));
            }
            if (args[0] == "ls-remote")
            {
                Assert.Equal("test-token", environment!["GH_TOKEN"]);
                var prefix = args.Contains("--heads") ? "refs/heads/main" : "refs/tags/v1";
                return Task.FromResult(Ok($"{(mismatch ? new string('b', 40) : hash)}\t{prefix}\n"));
            }
            if (args[0] == "log") return Task.FromResult(Ok("Alice <alice@example.test>\nAlice <alice@example.test>\n"));
            return Task.FromResult(Ok(""));
        });
        var result = await new PushVerifier(runner).PushAndVerifyAsync(
            fixture.Root, "https://github.com/test/repo.git", "test-token", CancellationToken.None);
        var report = Assert.IsType<ReadinessReport>(result["report"]);
        Assert.Equal(mismatch, report.HasBlockingIssues);
        Assert.Contains(report.Checks, check => check.Name == "Branch: main" && check.Status == (mismatch ? "failed" : "passed"));
        Assert.Contains(report.Checks, check => check.Name == "Tag: v1");
        Assert.Contains(report.Checks, check => check.Name == "Git object integrity" && check.Status == "passed");
        var id = fixture.Store.Create(new MigrationRequest()).Id;
        fixture.Store.SaveVerificationReport(id, report);
        fixture.Store.UpdateMigration(id, "failed", "test", 95, "test");
        var recovered = new MigrationStateStore(fixture.Configuration, fixture.Environment);
        recovered.Initialize();
        Assert.Equal(report.Checks.Count, recovered.GetDetail(id)!.VerificationReport!.Checks.Count);
        Assert.True(recovered.TryPrepareResume(id, "new-token", out _));
        Assert.Null(recovered.GetDetail(id)!.VerificationReport);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("integrity")]
    public async Task Verification_RejectsEmptyImportsAndIntegrityFailures(string failure)
    {
        using var fixture = new Fixture();
        var runner = new Runner((_, args, _, _) => Task.FromResult(
            args[0] == "fsck" && failure == "integrity"
                ? new CommandResult(1, "", "corrupt object")
                : Ok(args[0] is "for-each-ref" or "ls-remote" && failure != "empty"
                    ? "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\trefs/heads/main\n" : "")));
        var result = await new PushVerifier(runner).PushAndVerifyAsync(
            fixture.Root, "https://github.com/test/repo.git", "test-token", CancellationToken.None);
        var report = Assert.IsType<ReadinessReport>(result["report"]);
        Assert.True(report.HasBlockingIssues);
        Assert.Contains(report.Checks, check => check.Status == "failed" &&
            check.Name == (failure == "empty" ? "Imported branches" : "Git object integrity"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assessment_ReadOnlyChecks_FindMissingMappingsAndLargeFiles(bool blockers)
    {
        using var fixture = new Fixture();
        var runner = new Runner((executable, args, environment, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.DoesNotContain("test-token", args);
            Assert.DoesNotContain(args, arg => arg is "push" or "clone" or "create" or "import" or "checkout");
            if (args.Contains("--version")) return Task.FromResult(Ok("version"));
            if (executable == "svn")
            {
                Assert.Null(environment);
                Assert.Contains("--non-interactive", args);
                return Task.FromResult(Ok(args[0] switch
                {
                    "log" => """<log><logentry revision="1"><author>alice</author></logentry></log>""",
                    "list" => $"<lists><list><entry kind=\"file\"><name>binary.zip</name><size>{(blockers ? 104857601 : 104857600)}</size></entry></list></lists>",
                    _ => "readable"
                }));
            }
            Assert.Equal("test-token", environment!["GH_TOKEN"]);
            Assert.Equal("GET", args[2]);
            return Task.FromResult(Ok(args[^1] == "user" ? """{"login":"test"}""" : """{"permissions":{"push":true}}"""));
        });
        var readiness = fixture.Readiness(runner);
        var request = new MigrationRequest
        {
            SvnUrl = "https://svn.example.test/repo", GithubOrg = "test", GithubRepo = "repo",
            GithubToken = "test-token",
            Authors = blockers ? [] : [new AuthorInput { SvnUsername = "alice", GitName = "Alice", GitEmail = "alice@example.test", Verified = true }]
        };
        var report = await new MigrationAssessmentService(runner, readiness,
            NullLogger<MigrationAssessmentService>.Instance).AssessAsync(request, CancellationToken.None);
        Assert.Equal(blockers, report.HasBlockingIssues);
        Assert.Contains(report.Checks, check => check.Name == "Visible-history author coverage" && check.Status == (blockers ? "failed" : "passed"));
        Assert.Contains(report.Checks, check => check.Name == "Current trunk large files" && check.Status == (blockers ? "failed" : "warning"));
        Assert.Equal("test-token", request.GithubToken);
        Assert.Empty(fixture.Store.GetMigrations());
    }

    [Fact]
    public async Task QueuedCancellation_StopsBeforeCommands_AndOldQueueItemCannotRunResumedJob()
    {
        using var fixture = new Fixture();
        var calls = 0;
        var runner = new Runner((_, _, _, _) =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("test command should fail explicitly");
        });
        using var queue = fixture.Queue(runner);
        var id = fixture.Store.Create(new MigrationRequest
            { SvnUrl = "https://svn.example.test/repo", GithubToken = "test-token" }).Id;
        Assert.True(queue.Enqueue(id));
        Assert.True(queue.TryCancel(id, out _));
        Assert.Equal("failed", fixture.Store.GetDetail(id)!.Status);
        Assert.Null(fixture.Store.GetDetail(id)!.ElapsedSeconds);
        Assert.Empty(fixture.Store.FindMigration(id)!.GithubToken);
        Assert.True(fixture.Store.TryPrepareResume(id, "new-token", out _));
        Assert.True(queue.Enqueue(id));
        await queue.StartAsync(CancellationToken.None);
        await WaitForAsync(() => fixture.Store.GetDetail(id)!.Status == "failed" &&
            fixture.Store.FindMigration(id)!.GithubToken == "");
        await queue.StopAsync(CancellationToken.None);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ActiveCancellation_PropagatesToken_RetainsCheckpoint_AndWorkerProcessesNextJob()
    {
        using var fixture = new Fixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new Runner(async (_, _, _, cancellationToken) =>
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Ok("");
            }
            finally
            {
                stopped.TrySetResult();
                await release.Task;
            }
        });
        using var queue = fixture.Queue(runner);
        var id = fixture.Store.Create(new MigrationRequest
            { SvnUrl = "https://svn.example.test/repo", GithubToken = "test-token" }).Id;
        fixture.Store.CompleteStage(id, MigrationStages.Analysis, "analyzing", "Analysis saved", 20, "test");
        Assert.True(queue.Enqueue(id));
        await queue.StartAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(queue.TryCancel(id, out _));
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("cancelling", fixture.Store.GetDetail(id)!.Status);
        Assert.False(queue.TryPrepareResume(id, "new-token", out _));
        release.SetResult();
        await WaitForAsync(() => fixture.Store.GetDetail(id)!.Status == "failed" &&
            fixture.Store.FindMigration(id)!.GithubToken == "");
        Assert.Equal(MigrationStages.Analysis, fixture.Store.GetDetail(id)!.LastCompletedStage);
        Assert.NotNull(fixture.Store.GetDetail(id)!.ElapsedSeconds);
        Assert.Null(fixture.Store.GetDetail(id)!.RunningSince);
        Assert.False(queue.TryCancel(id, out _));
        var second = fixture.Store.Create(new MigrationRequest
            { SvnUrl = "https://svn.example.test/repo", GithubToken = "second-token" }).Id;
        Assert.True(queue.Enqueue(second));
        await WaitForAsync(() => fixture.Store.GetDetail(second)!.RunningSince is not null);
        Assert.True(queue.TryCancel(second, out _));
        await WaitForAsync(() => fixture.Store.GetDetail(second)!.Status == "failed");
        await queue.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExternalCommandCancellation_KillsOnlyDisposableProcess()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExternalCommandRunner().RunAsync(
            "powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"],
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CleanupCancellation_IsRejected_AndShutdownClearsQueuedTokens()
    {
        using var fixture = new Fixture();
        var runner = new Runner((_, _, _, _) => throw new InvalidOperationException("No commands should run."));
        using var queue = fixture.Queue(runner);
        var id = fixture.Store.Create(new MigrationRequest { GithubToken = "test-token" }).Id;
        Assert.True(queue.Enqueue(id));
        fixture.Store.UpdateMigration(id, "cleaning", "Destructive cleanup", 75, "test");
        Assert.False(fixture.Store.GetDetail(id)!.CanCancel);
        Assert.False(queue.TryCancel(id, out var error));
        Assert.Contains("cleanup", error);
        await queue.StopAsync(CancellationToken.None);
        Assert.Empty(fixture.Store.FindMigration(id)!.GithubToken);
        Assert.Equal("failed", fixture.Store.GetDetail(id)!.Status);
        Assert.False(queue.Enqueue(id));
    }

    [Fact]
    public async Task AlreadyCancelledCommand_DoesNotStartAnExecutable()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ExternalCommandRunner().RunAsync(
            "this-executable-does-not-exist", [], cancellationToken: cancellation.Token));
    }

    private static CommandResult Ok(string output) => new(0, output, "");

    [Theory]
    [InlineData("existing")]
    [InlineData("missing")]
    [InlineData("error")]
    public async Task GitHubSetup_ReusesExistingDestinationWithoutCreationOrSettingsChanges(string scenario)
    {
        var calls = new List<string[]>();
        var runner = new Runner((_, args, environment, _) =>
        {
            calls.Add(args);
            Assert.Equal("test-token", environment!["GH_TOKEN"]);
            Assert.DoesNotContain("test-token", args);
            if (args[0] == "repo" && args[1] == "view")
            {
                return Task.FromResult(scenario switch
                {
                    "existing" => Ok("""{"nameWithOwner":"test/repo"}"""),
                    "missing" => new CommandResult(1, "", "GraphQL: Could not resolve to a Repository with the name 'test/repo'."),
                    _ => new CommandResult(1, "", "HTTP 403: rate limit exceeded")
                });
            }
            return Task.FromResult(Ok(""));
        });
        var result = await new GitHubSetup(runner).SetupAsync("test", "repo", "test-token", CancellationToken.None);
        Assert.Equal(scenario != "error", result["success"]);
        Assert.DoesNotContain(calls, args => args.Length > 1 && args[0] == "repo" && args[1] == "edit");
        Assert.Equal(scenario == "missing",
            calls.Any(args => args.Length > 1 && args[0] == "repo" && args[1] == "create"));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, deadline.Token);
    }

    private sealed class Runner(
        Func<string, string[], IReadOnlyDictionary<string, string?>?, CancellationToken, Task<CommandResult>> run) : IExternalCommandRunner
    {
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            string? workingDirectory = null, TimeSpan? timeout = null,
            IReadOnlyDictionary<string, string?>? environment = null, CancellationToken cancellationToken = default) =>
            run(executable, arguments.ToArray(), environment, cancellationToken);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "migration-safety-" + Guid.NewGuid().ToString("N"));
        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; } = new TestEnvironment();
        public MigrationStateStore Store { get; }
        public Fixture()
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataDirectory"] = Path.Combine(Root, "data"), ["WorkspaceDirectory"] = Path.Combine(Root, "workspaces")
            }).Build();
            Store = new MigrationStateStore(Configuration, Environment);
            Store.Initialize();
        }
        public ReadinessService Readiness(IExternalCommandRunner runner) =>
            new(runner, Store, Configuration, Environment, NullLogger<ReadinessService>.Instance);
        public MigrationQueue Queue(IExternalCommandRunner runner) => new(Store,
            new RepositoryAnalyzer(runner), new AuthorMapper(runner), new MigrationExecutor(runner),
            new HistoryCleaner(runner), new GitHubSetup(runner), new PushVerifier(runner),
            Configuration, Environment, NullLogger<MigrationQueue>.Instance);
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(Root, true);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SafetyTests";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
