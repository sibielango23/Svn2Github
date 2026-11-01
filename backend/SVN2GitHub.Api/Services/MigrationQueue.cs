using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SVN2GitHub.Api.Models;
using SVN2GitHub.Shared;

namespace SVN2GitHub.Api.Services;

public sealed class MigrationQueue : BackgroundService
{
    private sealed class QueuedMigration(string id)
    {
        public string Id { get; } = id;
        public CancellationTokenSource Cancellation { get; } = new();
        public bool Started { get; set; }
    }

    private readonly object _queueSync = new();
    private readonly Dictionary<string, QueuedMigration> _pending = new();
    private readonly Channel<QueuedMigration> _queue = Channel.CreateUnbounded<QueuedMigration>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly MigrationStateStore _store;
    private readonly RepositoryAnalyzer _analyzer;
    private readonly AuthorMapper _authorMapper;
    private readonly MigrationExecutor _executor;
    private readonly HistoryCleaner _cleaner;
    private readonly GitHubSetup _githubSetup;
    private readonly PushVerifier _pushVerifier;
    private readonly ILogger<MigrationQueue> _logger;
    private readonly string _workspaceRoot;

    public MigrationQueue(
        MigrationStateStore store,
        RepositoryAnalyzer analyzer,
        AuthorMapper authorMapper,
        MigrationExecutor executor,
        HistoryCleaner cleaner,
        GitHubSetup githubSetup,
        PushVerifier pushVerifier,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<MigrationQueue> logger)
    {
        _store = store;
        _analyzer = analyzer;
        _authorMapper = authorMapper;
        _executor = executor;
        _cleaner = cleaner;
        _githubSetup = githubSetup;
        _pushVerifier = pushVerifier;
        _logger = logger;
        _workspaceRoot = ResolveWorkspaceRoot(configuration, environment);
    }

    public static string ResolveWorkspaceRoot(
        IConfiguration configuration,
        IWebHostEnvironment environment) =>
        configuration["WorkspaceDirectory"] ??
        Path.Combine(FindProjectRoot(environment.ContentRootPath), "temp");

    public bool Enqueue(string migrationId)
    {
        lock (_queueSync)
        {
            if (_pending.ContainsKey(migrationId))
            {
                return false;
            }

            var item = new QueuedMigration(migrationId);
            _pending.Add(migrationId, item);
            if (_queue.Writer.TryWrite(item))
            {
                return true;
            }

            _pending.Remove(migrationId);
            item.Cancellation.Dispose();
            return false;
        }
    }

    public bool TryCancel(string migrationId, out string error)
    {
        lock (_queueSync)
        {
            if (!_pending.TryGetValue(migrationId, out var item) ||
                _store.GetDetail(migrationId) is not { CanCancel: true } detail)
            {
                error = "This migration cannot be cancelled now. Destructive history cleanup must finish; terminal jobs cannot be cancelled.";
                return false;
            }

            _store.UpdateMigration(migrationId, item.Started ? "cancelling" : "failed",
                item.Started ? "Stopping active migration" : "Cancelled before processing", detail.Progress,
                "Cancellation requested. Completed checkpoints are retained; remote changes are not rolled back.",
                item.Started ? null : "Cancelled by user before processing.");
            item.Cancellation.Cancel();
            if (!item.Started)
            {
                _pending.Remove(migrationId);
                _store.ClearGithubToken(migrationId);
            }

            error = string.Empty;
            return true;
        }
    }

    public bool TryPrepareResume(string migrationId, string token, out string error)
    {
        lock (_queueSync)
        {
            if (_pending.ContainsKey(migrationId))
            {
                error = "The migration is still stopping. Wait for processing to finish before resuming.";
                return false;
            }

            return _store.TryPrepareResume(migrationId, token, out error);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
        if (ExecuteTask is { IsCompleted: false })
        {
            return;
        }

        lock (_queueSync)
        {
            while (_queue.Reader.TryRead(out var item))
            {
                if (_pending.Remove(item.Id))
                {
                    var detail = _store.GetDetail(item.Id)!;
                    _store.UpdateMigration(item.Id, "failed", "Interrupted by backend shutdown",
                        detail.Progress, "Queued migration stopped before processing.", "Backend stopped.");
                    _store.ClearGithubToken(item.Id);
                }

                item.Cancellation.Dispose();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var migrationId = item.Id;
            lock (_queueSync)
            {
                if (!_pending.TryGetValue(migrationId, out var current) || current != item)
                {
                    item.Cancellation.Dispose();
                    continue;
                }

                item.Started = true;
            }

            using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken, item.Cancellation.Token);
            using var timingCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task? timingTask = null;
            try
            {
                _store.StartProcessing(migrationId);
                timingTask = SaveProcessingTimeAsync(migrationId, timingCancellation.Token);
                executionCancellation.Token.ThrowIfCancellationRequested();
                await RunMigrationAsync(migrationId, executionCancellation.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                var detail = _store.GetDetail(migrationId)!;
                if (detail.Status is not ("completed" or "failed"))
                {
                    _store.UpdateMigration(migrationId, "failed", "Interrupted by backend shutdown",
                        detail.Progress, "Migration stopped before processing completed.", "Backend stopped.");
                }
                return;
            }
            catch (OperationCanceledException) when (item.Cancellation.IsCancellationRequested)
            {
                var detail = _store.GetDetail(migrationId)!;
                _store.UpdateMigration(migrationId, "failed", "Cancelled by user", detail.Progress,
                    "Migration stopped. Resume repeats the incomplete stage; no remote rollback was performed.",
                    "Cancelled by user.");
            }
            catch (Exception exception)
            {
                var record = _store.FindMigration(migrationId);
                if (record is not null)
                {
                    var message = Redact(exception.ToString(), record.GithubToken);
                    _logger.LogError(
                        "Unhandled migration error for {MigrationId}: {Error}",
                        migrationId,
                        message);
                    _store.UpdateMigration(
                        migrationId, "failed", "Migration failed", record.Detail.Progress,
                        "ERROR: " + message, message);
                }
            }
            finally
            {
                try
                {
                    timingCancellation.Cancel();
                    if (timingTask is not null)
                    {
                        await timingTask;
                    }
                }
                finally
                {
                    lock (_queueSync)
                    {
                        _store.ClearGithubToken(migrationId);
                        _pending.Remove(migrationId);
                        item.Cancellation.Dispose();
                    }
                }
            }
        }
    }

    private async Task SaveProcessingTimeAsync(string migrationId, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    _store.SaveProcessingTime(migrationId);
                }
                catch (SqliteException exception)
                {
                    _logger.LogError(exception,
                        "Could not save processing time for {MigrationId}; retrying at the next sample", migrationId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunMigrationAsync(string migrationId, CancellationToken cancellationToken)
    {
        var record = _store.FindMigration(migrationId)
            ?? throw new InvalidOperationException("Migration disappeared from state.");
        var checkpoint = record.Detail.LastCompletedStage;
        var request = new MigrationRequest
        {
            SvnUrl = SvnUrlNormalizer.Normalize(record.Detail.SvnUrl),
            GithubOrg = record.Detail.GithubOrg,
            GithubRepo = record.Detail.GithubRepo,
            GithubToken = record.GithubToken,
            AuthorsFile = record.AuthorsFile,
            CleanHistory = record.CleanHistory,
            TrunkPath = record.TrunkPath,
            BranchesPath = record.BranchesPath,
            TagsPath = record.TagsPath
        };
        var workspace = Path.Combine(_workspaceRoot, $"workspace_{migrationId}");
        Directory.CreateDirectory(workspace);

        try
        {
            var mappedAuthors = _store.GetAuthors(migrationId);
            if (!HasCompletedStage(checkpoint, MigrationStages.Analysis))
            {
                Update(migrationId, "analyzing", "Analyzing SVN repository", 10,
                    "Starting repository analysis...");
                var analysis = await _analyzer.AnalyzeAsync(
                    request.SvnUrl, request.TrunkPath, request.BranchesPath,
                    request.TagsPath, workspace, cancellationToken);
                var revisionRange = analysis.GetValueOrDefault("revision_range");
                var total = revisionRange is not null
                    ? JsonSerializer.SerializeToElement(revisionRange).TryGetProperty("total", out var totalValue) &&
                      totalValue.ValueKind == JsonValueKind.Number
                        ? totalValue.GetInt32().ToString()
                        : "0"
                    : "0";
                _store.CompleteStage(
                    migrationId, MigrationStages.Analysis, "analyzing",
                    "Repository analysis complete", 20, $"Found {total} revisions");
            }

            var authorsFile = Path.Combine(workspace, "authors.txt");
            if (HasCompletedStage(checkpoint, MigrationStages.Authors))
            {
                if (mappedAuthors.Count > 0)
                {
                    var content = string.Join(Environment.NewLine, mappedAuthors.Select(author =>
                        $"{author.SvnUsername} = {author.GitName} <{author.GitEmail}>"));
                    await File.WriteAllTextAsync(
                        authorsFile, content + Environment.NewLine, cancellationToken);
                }

                if (!File.Exists(authorsFile))
                {
                    throw new InvalidOperationException(
                        "The author-mapping checkpoint is missing its authors file. " +
                        "The migration cannot safely resume; start a new migration.");
                }
            }
            else
            {
                Update(migrationId, "mapping_authors", "Mapping SVN authors", 30,
                    "Generating author mappings...");
                if (mappedAuthors.Count > 0)
                {
                    var content = string.Join(Environment.NewLine, mappedAuthors.Select(author =>
                        $"{author.SvnUsername} = {author.GitName} <{author.GitEmail}>"));
                    await File.WriteAllTextAsync(
                        authorsFile, content + Environment.NewLine, cancellationToken);
                }
                else
                {
                    var mapping = await _authorMapper.CreateMappingAsync(
                        request.SvnUrl, workspace, "example.com", cancellationToken);
                    authorsFile = mapping.File;
                    mappedAuthors = mapping.Authors.Select((username, index) => new AuthorRecord
                    {
                        Id = index + 1,
                        MigrationId = migrationId,
                        SvnUsername = username,
                        GitName = username.Contains('.')
                            ? string.Join(' ', username.Split('.').Select(part =>
                                part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]))
                            : username.Length == 0
                                ? username
                                : char.ToUpperInvariant(username[0]) + username[1..],
                        GitEmail = $"{username}@example.com"
                    }).ToList();
                    _store.ReplaceAuthors(migrationId, mappedAuthors);
                }

                _store.CompleteStage(
                    migrationId, MigrationStages.Authors, "mapping_authors",
                    "Author mapping complete", 40, $"Mapped {mappedAuthors.Count} authors");
            }

            var repoPath = Path.Combine(workspace, "migrated-repo");
            if (!HasCompletedStage(checkpoint, MigrationStages.Import))
            {
                RemoveIncompleteImport(repoPath);
                Update(migrationId, "migrating", "Migrating SVN to Git", 50,
                    "Starting git-svn clone (this may take hours)...");
                var execution = await _executor.ExecuteAsync(
                    request, workspace, authorsFile, cancellationToken);
                if (!execution.TryGetValue("repo_path", out var repoValue) ||
                    repoValue is not string || execution.GetValueOrDefault("success") is false)
                {
                    throw new InvalidOperationException(
                        execution.GetValueOrDefault("error")?.ToString() ??
                        "Migration executor did not return the migrated repository path");
                }

                var statistics = execution.GetValueOrDefault("statistics");
                var commitCount = statistics is null
                    ? "0"
                    : JsonSerializer.SerializeToElement(statistics).TryGetProperty("commits", out var commits)
                        ? commits.ToString()
                        : "0";
                _store.CompleteStage(
                    migrationId, MigrationStages.Import, "migrating",
                    "Migration complete", 70, $"Migrated {commitCount} commits");
            }
            else if (!Directory.Exists(Path.Combine(repoPath, ".git")))
            {
                throw new InvalidOperationException(
                    "The saved Git repository for this checkpoint is missing. " +
                    "The migration cannot safely resume; start a new migration.");
            }

            if (request.CleanHistory &&
                !HasCompletedStage(checkpoint, MigrationStages.HistoryCleanup))
            {
                Update(migrationId, "cleaning", "Cleaning repository history", 75,
                    "Removing large files and build artifacts...");
                var cleaned = await _cleaner.CleanAsync(workspace, cancellationToken);
                if (cleaned.GetValueOrDefault("success") is false)
                {
                    throw new InvalidOperationException(
                        cleaned.GetValueOrDefault("error")?.ToString() ??
                        "History cleaning failed.");
                }

                _store.CompleteStage(
                    migrationId, MigrationStages.HistoryCleanup, "cleaning",
                    "History cleaned", 80, "Repository history cleaned");
            }

            var remoteUrl = $"https://github.com/{request.GithubOrg}/{request.GithubRepo}.git";
            if (!HasCompletedStage(checkpoint, MigrationStages.GitHubSetup))
            {
                Update(migrationId, "pushing", "Setting up GitHub repository", 85,
                    "Checking existing GitHub destination or creating a private repository...");
                var github = await _githubSetup.SetupAsync(
                    request.GithubOrg, request.GithubRepo, request.GithubToken, cancellationToken);
                if (github.GetValueOrDefault("success") is false)
                {
                    throw new InvalidOperationException(
                        github.GetValueOrDefault("error")?.ToString() ??
                        "GitHub setup failed.");
                }

                remoteUrl = github.GetValueOrDefault("clone_url")?.ToString()
                    ?? throw new InvalidOperationException(
                        "GitHub setup did not return a clone URL");

                _store.CompleteStage(
                    migrationId, MigrationStages.GitHubSetup, "pushing",
                    "GitHub destination ready", 90,
                    $"Repository URL: {github.GetValueOrDefault("web_url")}");
            }
            else
            {
                Update(migrationId, "pushing", "Reusing GitHub repository", 90,
                    $"Resuming push to {remoteUrl}");
            }

            Update(migrationId, "pushing", "Pushing to GitHub", 90,
                "Pushing all branches and tags...");

            var push = await _pushVerifier.PushAndVerifyAsync(
                workspace, remoteUrl, request.GithubToken, cancellationToken);
            if (push.GetValueOrDefault("report") is ReadinessReport report)
            {
                _store.SaveVerificationReport(migrationId, report);
            }
            if (push.GetValueOrDefault("success") is false)
            {
                throw new InvalidOperationException(
                    push.GetValueOrDefault("error")?.ToString() ?? "GitHub push failed.");
            }

            var verification = push.GetValueOrDefault("verification");
            var verificationStatus = verification is null
                ? null
                : JsonSerializer.SerializeToElement(verification).TryGetProperty("status", out var status)
                    ? status.GetString()
                    : null;
            if (verificationStatus != "success")
            {
                throw new InvalidOperationException(
                    "Push verification did not pass: " +
                    JsonSerializer.Serialize(verification));
            }

            var pushed = push.GetValueOrDefault("pushed");
            var branchCount = ReadArrayCount(pushed, "branches");
            var tagCount = ReadArrayCount(pushed, "tags");
            Update(migrationId, "pushing", "Push complete", 95,
                $"Pushed {branchCount} branches, {tagCount} tags");
            lock (_queueSync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Update(migrationId, "completed", "Migration completed successfully", 100,
                    "Migration finished! Repository available on GitHub");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var lastCompletedStage =
                _store.GetDetail(migrationId)?.LastCompletedStage ?? checkpoint;
            _store.UpdateMigration(
                migrationId, "failed", "Migration interrupted",
                _store.GetDetail(migrationId)?.Progress ?? 0,
                "Migration interrupted by cancellation or backend shutdown. " +
                $"Resume is available from {MigrationStages.GetDisplayName(lastCompletedStage)}.",
                "Processing stopped before migration completed. Remote changes are not rolled back.");
            throw;
        }
        catch (Exception exception)
        {
            var lastCompletedStage =
                _store.GetDetail(migrationId)?.LastCompletedStage ?? checkpoint;
            var safeMessage = Redact(exception.Message, request.GithubToken);
            _logger.LogError(
                "Migration {MigrationId} failed: {Error}",
                migrationId,
                safeMessage);
            _store.UpdateMigration(
                migrationId, "failed", "Migration failed",
                _store.GetDetail(migrationId)?.Progress ?? 0,
                "ERROR: " + safeMessage +
                $". Resume is available from {MigrationStages.GetDisplayName(lastCompletedStage)}.",
                safeMessage);
        }
    }

    private static bool HasCompletedStage(string checkpoint, string stage) =>
        MigrationStages.GetRank(checkpoint) >= MigrationStages.GetRank(stage);

    private void RemoveIncompleteImport(string repoPath)
    {
        if (!Directory.Exists(repoPath))
        {
            return;
        }

        var root = Path.GetFullPath(_workspaceRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullRepoPath = Path.GetFullPath(repoPath);
        if (!fullRepoPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetFileName(fullRepoPath),
                "migrated-repo",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The incomplete SVN import is outside the managed migration workspace.");
        }

        Directory.Delete(fullRepoPath, recursive: true);
    }

    private void Update(string id, string status, string step, int progress, string log) =>
        _store.UpdateMigration(id, status, step, progress, log);

    private static string Redact(string value, string token) =>
        string.IsNullOrEmpty(token) ? value : value.Replace(token, "[REDACTED]", StringComparison.Ordinal);

    private static int ReadArrayCount(object? value, string property)
    {
        if (value is null)
        {
            return 0;
        }

        var element = JsonSerializer.SerializeToElement(value);
        return element.TryGetProperty(property, out var array) &&
               array.ValueKind == JsonValueKind.Array
            ? array.GetArrayLength()
            : 0;
    }

    private static string FindProjectRoot(string initialPath)
    {
        for (var directory = new DirectoryInfo(initialPath);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "wpf", "SVN2GitHub")))
            {
                return directory.FullName;
            }
        }

        return Environment.CurrentDirectory;
    }
}
