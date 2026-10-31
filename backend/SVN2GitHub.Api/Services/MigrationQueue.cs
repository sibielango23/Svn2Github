using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SVN2GitHub.Api.Models;

namespace SVN2GitHub.Api.Services;

public sealed class MigrationQueue : BackgroundService
{
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(
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
        _workspaceRoot = configuration["WorkspaceDirectory"] ??
                         Path.Combine(FindProjectRoot(environment.ContentRootPath), "temp");
    }

    public bool Enqueue(string migrationId) => _queue.Writer.TryWrite(migrationId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var migrationId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunMigrationAsync(migrationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
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
        }
    }

    private async Task RunMigrationAsync(string migrationId, CancellationToken cancellationToken)
    {
        var record = _store.FindMigration(migrationId)
            ?? throw new InvalidOperationException("Migration disappeared from state.");
        var request = new MigrationRequest
        {
            SvnUrl = record.Detail.SvnUrl,
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
            Update(migrationId, "analyzing", "Repository analysis complete", 20,
                $"Found {total} revisions");

            Update(migrationId, "mapping_authors", "Mapping SVN authors", 30,
                "Generating author mappings...");
            var mappedAuthors = _store.GetAuthors(migrationId);
            string authorsFile;
            if (mappedAuthors.Count > 0)
            {
                authorsFile = Path.Combine(workspace, "authors.txt");
                var content = string.Join(Environment.NewLine, mappedAuthors.Select(author =>
                    $"{author.SvnUsername} = {author.GitName} <{author.GitEmail}>"));
                await File.WriteAllTextAsync(authorsFile, content + Environment.NewLine, cancellationToken);
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

            Update(migrationId, "mapping_authors", "Author mapping complete", 40,
                $"Mapped {mappedAuthors.Count} authors");

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
            Update(migrationId, "migrating", "Migration complete", 70,
                $"Migrated {commitCount} commits");

            if (request.CleanHistory)
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

                Update(migrationId, "cleaning", "History cleaned", 80,
                    "Repository history cleaned");
            }

            Update(migrationId, "pushing", "Setting up GitHub repository", 85,
                "Creating GitHub repository...");
            var github = await _githubSetup.SetupAsync(
                request.GithubOrg, request.GithubRepo, request.GithubToken, cancellationToken);
            if (github.GetValueOrDefault("success") is false)
            {
                throw new InvalidOperationException(
                    github.GetValueOrDefault("error")?.ToString() ??
                    "GitHub setup failed.");
            }

            var remoteUrl = github.GetValueOrDefault("clone_url")?.ToString();
            if (string.IsNullOrWhiteSpace(remoteUrl))
            {
                throw new InvalidOperationException("GitHub setup did not return a clone URL");
            }

            Update(migrationId, "pushing", "GitHub repository created", 90,
                $"Repository URL: {github.GetValueOrDefault("web_url")}");
            Update(migrationId, "pushing", "Pushing to GitHub", 90,
                "Pushing all branches and tags...");

            var push = await _pushVerifier.PushAndVerifyAsync(
                workspace, remoteUrl, request.GithubToken, cancellationToken);
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
            Update(migrationId, "completed", "Migration completed successfully", 100,
                "Migration finished! Repository available on GitHub");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _store.UpdateMigration(
                migrationId, "failed", "Interrupted by backend shutdown",
                _store.GetDetail(migrationId)?.Progress ?? 0,
                "Migration interrupted because the backend is shutting down.",
                "Backend stopped before migration completed; start a new migration.");
            throw;
        }
        catch (Exception exception)
        {
            var safeMessage = Redact(exception.Message, request.GithubToken);
            _logger.LogError(
                "Migration {MigrationId} failed: {Error}",
                migrationId,
                safeMessage);
            _store.UpdateMigration(
                migrationId, "failed", "Migration failed",
                _store.GetDetail(migrationId)?.Progress ?? 0,
                "ERROR: " + safeMessage, safeMessage);
        }
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
