using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using SVN2GitHub.Api.Models;

namespace SVN2GitHub.Api.Services;

public sealed class MigrationAssessmentService(
    IExternalCommandRunner commands,
    ReadinessService readiness,
    ILogger<MigrationAssessmentService> logger)
{
    public async Task<ReadinessReport> AssessAsync(MigrationRequest request, CancellationToken cancellationToken)
    {
        var report = await readiness.CheckAsync(request.CleanHistory, cancellationToken);
        var baseUrl = request.SvnUrl.TrimEnd('/');
        foreach (var (name, path) in new[]
                 { ("Trunk access", request.TrunkPath), ("Branches access", request.BranchesPath), ("Tags access", request.TagsPath) })
        {
            if (!IsRelativeLayoutPath(path))
            {
                report.Checks.Add(new ReadinessCheck(name, "failed",
                    "Use a nonempty relative SVN path without traversal, URL query, or fragment."));
                continue;
            }

            var result = await RunAsync("svn", ["info", "--non-interactive", $"{baseUrl}/{path}"],
                request, cancellationToken);
            report.Checks.Add(new ReadinessCheck(name, result?.ExitCode == 0 ? "passed" : "failed",
                result?.ExitCode == 0 ? $"Readable: {path}" : "SVN path could not be read. Check layout and cached SVN credentials."));
        }

        var log = await RunAsync("svn", ["log", "--xml", "--quiet", "--non-interactive", baseUrl],
            request, cancellationToken);
        if (log?.ExitCode == 0)
        {
            try
            {
                var entries = XDocument.Parse(log.StandardOutput).Descendants("logentry").ToList();
                var names = entries.Elements("author").Select(element => element.Value)
                    .Distinct(StringComparer.Ordinal).ToList();
                var mappings = request.Authors.ToDictionary(author => author.SvnUsername.Trim(), StringComparer.Ordinal);
                var missing = names.Where(name => !mappings.ContainsKey(name)).Order(StringComparer.Ordinal).ToList();
                var unverified = names.Where(name => mappings.TryGetValue(name, out var author) && !author.Verified).ToList();
                report.Checks.Add(new ReadinessCheck("Visible-history author coverage",
                    entries.Count == 0 || missing.Count > 0 ? "failed" : unverified.Count > 0 ? "warning" : "passed",
                    $"{entries.Count} visible log entries; {names.Count} named authors. " +
                    (missing.Count > 0 ? "Missing explicit mappings: " + string.Join(", ", missing) + ". " : string.Empty) +
                    $"{unverified.Count} mapped identities need confirmation. " +
                    "Coverage applies only to history visible to these SVN credentials; it is not an identity certification."));
                if (entries.Any(entry => entry.Element("author") is null))
                {
                    report.Checks.Add(new ReadinessCheck("Anonymous SVN revisions", "warning",
                        "Some visible revisions have no author. Review import behavior before production migration."));
                }
            }
            catch (XmlException exception)
            {
                logger.LogWarning("Assessment SVN log XML invalid: {Error}", exception.Message);
                report.Checks.Add(new ReadinessCheck("Author coverage", "failed", "SVN returned invalid log XML; coverage is unknown."));
            }
        }
        else
        {
            report.Checks.Add(new ReadinessCheck("Author coverage", "failed", "Full visible SVN log could not be read within five minutes. Coverage is unknown, not zero."));
        }

        if (IsRelativeLayoutPath(request.TrunkPath))
        {
            var files = await RunAsync("svn",
                ["list", "--xml", "--recursive", "--non-interactive", $"{baseUrl}/{request.TrunkPath}"],
                request, cancellationToken);
            try
            {
                if (files is not { ExitCode: 0 })
                {
                    report.Checks.Add(new ReadinessCheck("Current trunk large files", "failed", "File inventory unavailable; large-file risk is unknown."));
                }
                else
                {
                    var large = XDocument.Parse(files.StandardOutput).Descendants("entry")
                        .Where(entry => long.TryParse(entry.Element("size")?.Value, out var size) && size > 100L * 1024 * 1024)
                        .Select(entry => entry.Element("name")?.Value ?? "(unnamed)").ToList();
                    report.Checks.Add(new ReadinessCheck("Current trunk large files", large.Count > 0 ? "failed" : "warning",
                        large.Count > 0 ? "Files above 100 MiB require an explicit cleanup/LFS strategy: " + string.Join(", ", large)
                            : "No files above 100 MiB found in current trunk. Historical versions, branches, tags and repository-wide storage limits were not scanned."));
                }
            }
            catch (XmlException exception)
            {
                logger.LogWarning("Assessment SVN inventory XML invalid: {Error}", exception.Message);
                report.Checks.Add(new ReadinessCheck("Current trunk large files", "failed", "Invalid SVN inventory XML; size risk is unknown."));
            }
        }

        var user = await RunAsync("gh", ["api", "--method", "GET", "user"], request, cancellationToken);
        report.Checks.Add(new ReadinessCheck("GitHub authentication", user?.ExitCode == 0 ? "passed" : "failed",
            user?.ExitCode == 0 ? "Token authenticated to github.com." : "Token could not authenticate to github.com."));
        var repository = await RunAsync("gh", ["api", "--method", "GET", $"repos/{request.GithubOrg}/{request.GithubRepo}"],
            request, cancellationToken);
        if (repository?.ExitCode == 0)
        {
            try
            {
                using var json = JsonDocument.Parse(repository.StandardOutput);
                var canPush = json.RootElement.TryGetProperty("permissions", out var permissions) &&
                    permissions.TryGetProperty("push", out var push) && push.ValueKind == JsonValueKind.True;
                report.Checks.Add(new ReadinessCheck("Existing destination permission", canPush ? "passed" : "failed",
                    canPush ? "GitHub reports push permission. Branch protection, rulesets and divergence still need review."
                        : "GitHub did not confirm push permission for this destination."));
            }
            catch (JsonException exception)
            {
                logger.LogWarning("Assessment GitHub response invalid: {Error}", exception.Message);
                report.Checks.Add(new ReadinessCheck("Destination permission", "failed", "Invalid GitHub response; permission is unknown."));
            }
        }
        else
        {
            report.Checks.Add(new ReadinessCheck("Destination creation", "warning",
                "Destination is absent, inaccessible, or its lookup failed. Read-only checks cannot prove creation permission. Verify owner policy in a disposable trial."));
        }

        return report;
    }

    private async Task<CommandResult?> RunAsync(
        string executable, string[] arguments, MigrationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyDictionary<string, string?>? environment = executable == "gh"
                ? new Dictionary<string, string?>
                {
                    ["GH_TOKEN"] = request.GithubToken, ["GH_HOST"] = "github.com",
                    ["GH_PROMPT_DISABLED"] = "1", ["GIT_TERMINAL_PROMPT"] = "0"
                } : null;
            return await commands.RunAsync(executable, arguments, timeout: TimeSpan.FromMinutes(5),
                environment: environment, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            logger.LogWarning("Assessment command {Command} unavailable or timed out ({ErrorType})",
                executable, exception.GetType().Name);
            return null;
        }
    }

    private static bool IsRelativeLayoutPath(string path) =>
        !string.IsNullOrWhiteSpace(path) && !path.StartsWith('/') && !path.StartsWith('-') &&
        !path.Contains('\\') && !path.Contains('?') && !path.Contains('#') &&
        path.Split('/').All(part => part.Length > 0 && part is not "." and not "..");
}
