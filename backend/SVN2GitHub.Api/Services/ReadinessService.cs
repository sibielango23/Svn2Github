using System.Security;
using SVN2GitHub.Api.Models;

namespace SVN2GitHub.Api.Services;

public sealed class ReadinessService(
    IExternalCommandRunner commands,
    MigrationStateStore store,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<ReadinessService> logger)
{
    public async Task<ReadinessReport> CheckAsync(
        bool cleanHistory,
        CancellationToken cancellationToken)
    {
        var report = new ReadinessReport();
        foreach (var (name, executable, arguments) in new[]
                 {
                     ("Git", "git", new[] { "--version" }),
                     ("Git-SVN", "git", new[] { "svn", "--version" }),
                     ("Subversion", "svn", new[] { "--version", "--quiet" }),
                     ("GitHub CLI", "gh", new[] { "--version" })
                 })
        {
            report.Checks.Add(await CheckToolAsync(name, executable, arguments, cancellationToken));
        }

        if (cleanHistory)
        {
            report.Checks.Add(await CheckToolAsync(
                "History cleaner", "git", ["filter-repo", "--version"], cancellationToken));
            report.Checks.Add(new ReadinessCheck("History rewrite", "warning",
                "Cleanup rewrites commit IDs and removes files across history. Review the cleanup scope before starting."));
        }

        var workspaceRoot = Path.GetFullPath(MigrationQueue.ResolveWorkspaceRoot(configuration, environment));
        foreach (var (name, path) in new[]
                 {
                     ("Database directory", store.DataDirectory),
                     ("Workspace directory", workspaceRoot)
                 })
        {
            try
            {
                await ProbeDirectoryAsync(path, cancellationToken);
                report.Checks.Add(new ReadinessCheck(name, "passed", $"Writable: {path}"));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or SecurityException)
            {
                logger.LogWarning("Readiness check {Check} failed: {Error}", name, exception.Message);
                report.Checks.Add(new ReadinessCheck(name, "failed",
                    $"Cannot write to {path}: {exception.Message}"));
            }
        }

        try
        {
            var root = Path.GetPathRoot(workspaceRoot)
                ?? throw new IOException("Workspace volume could not be determined.");
            var availableBytes = new DriveInfo(root).AvailableFreeSpace;
            var gibibytes = availableBytes / (1024d * 1024 * 1024);
            report.Checks.Add(new ReadinessCheck("Workspace free space",
                availableBytes < 5L * 1024 * 1024 * 1024 ? "warning" : "passed",
                $"{gibibytes:F1} GiB available. Below 5 GiB is a caution threshold, not a repository-size estimate; large imports may require much more."));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            logger.LogWarning("Readiness disk check failed: {Error}", exception.Message);
            report.Checks.Add(new ReadinessCheck("Workspace free space", "warning",
                $"Free space could not be measured: {exception.Message}. Check it manually."));
        }

        report.Checks.Add(new ReadinessCheck("Repository access and author coverage", "warning",
            "Local checks do not contact SVN/GitHub, validate a token, prove repository-creation permissions, or verify full author coverage. Review these separately using disposable repositories."));
        return report;
    }

    private async Task<ReadinessCheck> CheckToolAsync(
        string name,
        string executable,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await commands.RunAsync(
                executable, arguments, timeout: TimeSpan.FromSeconds(15),
                cancellationToken: cancellationToken);
            if (result.ExitCode != 0)
            {
                logger.LogWarning("Readiness tool {Tool} exited with {ExitCode}", name, result.ExitCode);
                return new ReadinessCheck(name, "failed",
                    $"Version check exited with code {result.ExitCode}. Install/repair this tool and ensure it is available to the API.");
            }

            var version = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "Version command completed.";
            return new ReadinessCheck(name, "passed", version.Length > 200 ? version[..200] : version);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            logger.LogWarning("Readiness tool {Tool} unavailable: {Error}", name, exception.Message);
            return new ReadinessCheck(name, "failed", exception.Message);
        }
    }

    private static async Task ProbeDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(path);
        var probe = Path.Combine(path, $".readiness-{Guid.NewGuid():N}.tmp");
        await using var stream = new FileStream(
            probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await stream.WriteAsync(new byte[] { 0 }, cancellationToken);
    }
}
