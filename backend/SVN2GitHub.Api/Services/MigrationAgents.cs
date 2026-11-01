using System.Globalization;
using System.Text.RegularExpressions;
using SVN2GitHub.Api.Models;

namespace SVN2GitHub.Api.Services;

public sealed class RepositoryAnalyzer(IExternalCommandRunner commands)
{
    public async Task<Dictionary<string, object?>> AnalyzeAsync(
        string svnUrl,
        string trunk,
        string branches,
        string tags,
        string workspace,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(workspace);
        var baseUrl = svnUrl.TrimEnd('/');
        var rootResult = await commands.RunAsync(
            "svn", ["list", baseUrl], timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);
        var rootContents = rootResult.ExitCode == 0 ? rootResult.StandardOutput : string.Empty;
        var structure = rootResult.ExitCode != 0
            ? "unknown"
            : rootContents.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().TrimEnd('/'))
                .Contains(trunk, StringComparer.OrdinalIgnoreCase) &&
              rootContents.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().TrimEnd('/'))
                .Contains(branches, StringComparer.OrdinalIgnoreCase) &&
              rootContents.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().TrimEnd('/'))
                .Contains(tags, StringComparer.OrdinalIgnoreCase)
                ? "standard"
                : "non-standard";

        var branchList = await ListDirectoryAsync($"{baseUrl}/{branches}", cancellationToken);
        var tagList = await ListDirectoryAsync($"{baseUrl}/{tags}", cancellationToken);
        var revisionResult = await commands.RunAsync(
            "svn", ["log", "--quiet", "--limit", "1", baseUrl],
            timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);
        var revisionMatch = Regex.Match(revisionResult.StandardOutput, @"r(\d+)");
        int? latest = revisionResult.ExitCode == 0 && revisionMatch.Success
            ? int.Parse(revisionMatch.Groups[1].Value, CultureInfo.InvariantCulture)
            : null;

        object sizeEstimate = new { trunk_files = (int?)null, estimated_complexity = "unknown" };
        var sizeResult = await commands.RunAsync(
            "svn", ["list", "-R", $"{baseUrl}/{trunk}"],
            timeout: TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
        if (sizeResult.ExitCode == 0)
        {
            var count = sizeResult.StandardOutput.Split(
                ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
            sizeEstimate = new
            {
                trunk_files = count,
                estimated_complexity = count > 10_000 ? "high" : count > 1_000 ? "medium" : "low"
            };
        }

        var authorResult = await commands.RunAsync(
            "svn", ["log", "--quiet", "--limit", "1000", baseUrl],
            timeout: TimeSpan.FromMinutes(10), cancellationToken: cancellationToken);
        var discoveredAuthors = authorResult.ExitCode == 0
            ? ExtractUsernames(authorResult.StandardOutput).Order(StringComparer.Ordinal).ToList()
            : [];
        var revisionRange = new { first = latest is null ? (int?)null : 1, last = latest, total = latest };

        var analysis = new Dictionary<string, object?>
        {
            ["structure"] = structure,
            ["branches"] = branchList,
            ["tags"] = tagList,
            ["revision_range"] = revisionRange,
            ["size_estimate"] = sizeEstimate,
            ["authors"] = discoveredAuthors
        };
        var outputPath = Path.Combine(workspace, "analysis-results.json");
        await File.WriteAllTextAsync(
            outputPath,
            System.Text.Json.JsonSerializer.Serialize(analysis),
            cancellationToken);
        analysis["output_file"] = outputPath;
        return analysis;
    }

    public static IEnumerable<string> ExtractUsernames(string svnLog)
    {
        foreach (var line in svnLog.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"r\d+ \| (\S+) \|");
            if (match.Success)
            {
                yield return match.Groups[1].Value;
            }
        }
    }

    private async Task<List<string>> ListDirectoryAsync(string url, CancellationToken cancellationToken)
    {
        var result = await commands.RunAsync(
            "svn", ["list", url], timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);
        return result.ExitCode == 0
            ? result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim().TrimEnd('/')).Where(item => item.Length > 0).ToList()
            : [];
    }
}

public sealed class AuthorMapper(IExternalCommandRunner commands)
{
    public async Task<(List<string> Authors, string File)> CreateMappingAsync(
        string svnUrl,
        string workspace,
        string authorDomain,
        CancellationToken cancellationToken)
    {
        var result = await commands.RunCheckedAsync(
            "svn", ["log", "--quiet", svnUrl],
            timeout: TimeSpan.FromMinutes(10), cancellationToken: cancellationToken);
        var authors = RepositoryAnalyzer.ExtractUsernames(result.StandardOutput)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        Directory.CreateDirectory(workspace);
        var file = Path.Combine(workspace, "authors.txt");
        var lines = authors.Select(author =>
        {
            var name = author.Contains('.')
                ? string.Join(' ', author.Split('.').Select(Capitalize))
                : Capitalize(author);
            return $"{author} = {name} <{author}@{authorDomain}>";
        });
        await File.WriteAllLinesAsync(file, lines, cancellationToken);
        return (authors, file);
    }

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
}

public sealed class MigrationExecutor(IExternalCommandRunner commands)
{
    public async Task<Dictionary<string, object?>> ExecuteAsync(
        MigrationRequest migration,
        string workspace,
        string authorsFile,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var repoPath = Path.Combine(workspace, "migrated-repo");
        var arguments = new List<string> { "svn", "clone" };
        var standard = migration.TrunkPath == "trunk" &&
                       migration.BranchesPath == "branches" &&
                       migration.TagsPath == "tags";
        if (standard)
        {
            arguments.Add("--stdlayout");
        }
        else
        {
            arguments.AddRange(["--trunk", migration.TrunkPath, "--branches", migration.BranchesPath,
                "--tags", migration.TagsPath]);
        }

        if (!string.IsNullOrWhiteSpace(authorsFile))
        {
            arguments.AddRange(["--authors-file", authorsFile]);
        }

        arguments.Add("--no-metadata");
        arguments.Add(migration.SvnUrl);
        arguments.Add(repoPath);

        var clone = await commands.RunAsync(
            "git", arguments, workspace, timeout: TimeSpan.FromHours(24),
            cancellationToken: cancellationToken);
        if (clone.ExitCode != 0)
        {
            return new Dictionary<string, object?>
            {
                ["success"] = false,
                ["error"] = "git-svn clone failed: " + clone.StandardError,
                ["duration"] = (DateTime.UtcNow - started).TotalSeconds
            };
        }

        await ConvertSvnRefsAsync(repoPath, cancellationToken);
        var statistics = await CollectStatisticsAsync(repoPath, cancellationToken);
        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["repo_path"] = repoPath,
            ["duration"] = (DateTime.UtcNow - started).TotalSeconds,
            ["statistics"] = statistics
        };
    }

    internal async Task ConvertSvnRefsAsync(string repoPath, CancellationToken cancellationToken)
    {
        var refs = (await commands.RunCheckedAsync(
                "git", ["for-each-ref", "--format=%(refname)", "refs/remotes/"],
                repoPath, cancellationToken: cancellationToken))
            .StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);

        async Task<List<string>> GetMappingsAsync(string key)
        {
            var result = await commands.RunAsync(
                "git", ["config", "--get-all", $"svn-remote.svn.{key}"],
                repoPath, cancellationToken: cancellationToken);
            return result.ExitCode is 0 or 1
                ? result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToList()
                : throw new InvalidOperationException(
                    $"Unable to read git-svn {key} mapping: {result.StandardError}");
        }

        async Task<string> RevisionAsync(string refName) =>
            (await commands.RunCheckedAsync(
                "git", ["rev-parse", refName], repoPath,
                cancellationToken: cancellationToken)).StandardOutput.Trim();

        async Task UpdateRefAsync(string name, string oid)
        {
            var valid = await commands.RunAsync(
                "git", ["check-ref-format", name], repoPath,
                cancellationToken: cancellationToken);
            if (valid.ExitCode != 0)
            {
                throw new InvalidOperationException($"Invalid Git ref generated from SVN path: {name}");
            }

            await commands.RunCheckedAsync(
                "git", ["update-ref", name, oid], repoPath,
                cancellationToken: cancellationToken);
        }

        var fetchMappings = await GetMappingsAsync("fetch");
        var trunkTargets = fetchMappings.Select(mapping => mapping.Split(':', 2))
            .Where(parts => parts.Length == 2).Select(parts => parts[1]).ToList();
        var trunkRef = trunkTargets.FirstOrDefault(refs.Contains);
        var mainExists = (await commands.RunAsync(
            "git", ["show-ref", "--verify", "--quiet", "refs/heads/main"],
            repoPath, cancellationToken: cancellationToken)).ExitCode == 0;
        if (trunkRef is null && !mainExists)
        {
            throw new InvalidOperationException("git-svn did not create an SVN trunk ref");
        }

        if (trunkRef is not null)
        {
            var trunkObject = await RevisionAsync(trunkRef);
            var existingMain = await commands.RunAsync(
                "git", ["rev-parse", "--verify", "refs/heads/main"],
                repoPath, cancellationToken: cancellationToken);
            if (existingMain.ExitCode == 0 &&
                !string.Equals(existingMain.StandardOutput.Trim(), trunkObject, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Existing main branch does not match the imported SVN trunk");
            }

            await UpdateRefAsync("refs/heads/main", trunkObject);
            var existingMaster = await commands.RunAsync(
                "git", ["rev-parse", "--verify", "refs/heads/master"],
                repoPath, cancellationToken: cancellationToken);
            if (existingMaster.ExitCode == 0 &&
                string.Equals(existingMaster.StandardOutput.Trim(), trunkObject, StringComparison.Ordinal))
            {
                await commands.RunCheckedAsync(
                    "git", ["update-ref", "-d", "refs/heads/master", trunkObject],
                    repoPath, cancellationToken: cancellationToken);
            }
        }

        static (string Prefix, string Suffix)? PatternFor(string mapping)
        {
            var parts = mapping.Split(':', 2);
            if (parts.Length != 2)
            {
                return null;
            }

            var wildcard = parts[1].IndexOf('*');
            return wildcard < 0
                ? null
                : (parts[1][..wildcard], parts[1][(wildcard + 1)..]);
        }

        static string? Match(string reference, (string Prefix, string Suffix) pattern)
        {
            if (!reference.StartsWith(pattern.Prefix, StringComparison.Ordinal) ||
                !reference.EndsWith(pattern.Suffix, StringComparison.Ordinal))
            {
                return null;
            }

            var length = reference.Length - pattern.Prefix.Length - pattern.Suffix.Length;
            return length > 0
                ? reference.Substring(pattern.Prefix.Length, length)
                : null;
        }

        var tagRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pattern in (await GetMappingsAsync("tags")).Select(PatternFor)
                     .Where(pattern => pattern.HasValue).Select(pattern => pattern!.Value))
        {
            foreach (var reference in refs)
            {
                var name = Match(reference, pattern);
                if (name is null)
                {
                    continue;
                }

                await UpdateRefAsync($"refs/tags/{name}", await RevisionAsync(reference));
                tagRefs.Add(reference);
            }
        }

        foreach (var pattern in (await GetMappingsAsync("branches")).Select(PatternFor)
                     .Where(pattern => pattern.HasValue).Select(pattern => pattern!.Value))
        {
            foreach (var reference in refs)
            {
                if (reference == trunkRef || tagRefs.Contains(reference))
                {
                    continue;
                }

                var name = Match(reference, pattern);
                if (name is not null)
                {
                    await UpdateRefAsync($"refs/heads/{name}", await RevisionAsync(reference));
                }
            }
        }
    }

    private async Task<Dictionary<string, int>> CollectStatisticsAsync(
        string repoPath,
        CancellationToken cancellationToken)
    {
        var commits = await commands.RunCheckedAsync(
            "git", ["rev-list", "--branches", "--tags", "--count"],
            repoPath, cancellationToken: cancellationToken);
        var branches = await commands.RunCheckedAsync(
            "git", ["for-each-ref", "--format=%(refname)", "refs/heads/"],
            repoPath, cancellationToken: cancellationToken);
        var tags = await commands.RunCheckedAsync(
            "git", ["for-each-ref", "--format=%(refname)", "refs/tags/"],
            repoPath, cancellationToken: cancellationToken);
        return new Dictionary<string, int>
        {
            ["commits"] = int.Parse(commits.StandardOutput.Trim(), CultureInfo.InvariantCulture),
            ["branches"] = CountLines(branches.StandardOutput),
            ["tags"] = CountLines(tags.StandardOutput)
        };
    }

    private static int CountLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
}

public sealed class HistoryCleaner(IExternalCommandRunner commands)
{
    public async Task<Dictionary<string, object?>> CleanAsync(
        string workspace,
        CancellationToken cancellationToken)
    {
        var repoPath = Path.Combine(workspace, "migrated-repo");
        var version = await commands.RunAsync(
            "git", ["filter-repo", "--version"], repoPath,
            cancellationToken: cancellationToken);
        if (version.ExitCode != 0)
        {
            return new Dictionary<string, object?>
            {
                ["success"] = false,
                ["error"] = "git-filter-repo not found",
                ["message"] = "Install git-filter-repo and ensure it is available to Git."
            };
        }

        var before = await GetStatsAsync(repoPath, cancellationToken);
        var filters = new List<string>();
        foreach (var pattern in new[] { "*.exe", "*.dll", "*.zip", "*.tar.gz", "node_modules", "bin", "obj" })
        {
            filters.AddRange(["--path-glob", pattern, "--invert-paths"]);
        }

        filters.AddRange(["--strip-blobs-bigger-than", "104857600", "--force"]);
        var result = await commands.RunAsync(
            "git", ["filter-repo", .. filters], repoPath,
            timeout: TimeSpan.FromHours(2), cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            return new Dictionary<string, object?>
            {
                ["success"] = false,
                ["error"] = result.StandardError
            };
        }

        var after = await GetStatsAsync(repoPath, cancellationToken);
        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["stats_before"] = before,
            ["stats_after"] = after,
            ["size_reduction_mb"] = before.GetValueOrDefault("size_mb") - after.GetValueOrDefault("size_mb")
        };
    }

    private async Task<Dictionary<string, double>> GetStatsAsync(
        string repoPath,
        CancellationToken cancellationToken)
    {
        var result = await commands.RunCheckedAsync(
            "git", ["count-objects", "-vH"], repoPath,
            cancellationToken: cancellationToken);
        var stats = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
        var size = stats.GetValueOrDefault("size-pack", "0 MiB").Split(' ', 2)[0];
        _ = double.TryParse(size, CultureInfo.InvariantCulture, out var sizeMb);
        return new Dictionary<string, double> { ["size_mb"] = sizeMb };
    }
}

public sealed class GitHubSetup(IExternalCommandRunner commands)
{
    public async Task<Dictionary<string, object?>> SetupAsync(
        string organization,
        string repository,
        string token,
        CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string?> { ["GH_TOKEN"] = token };
        var auth = await commands.RunAsync(
            "gh", ["auth", "status"], environment: environment,
            cancellationToken: cancellationToken);
        if (auth.ExitCode != 0)
        {
            return Failure("GitHub CLI authentication failed: " + auth.StandardError);
        }

        var setupGit = await commands.RunAsync(
            "gh", ["auth", "setup-git"], environment: environment,
            cancellationToken: cancellationToken);
        if (setupGit.ExitCode != 0)
        {
            return Failure("Unable to configure Git authentication: " + setupGit.StandardError);
        }

        var existing = await commands.RunAsync(
            "gh", ["repo", "view", $"{organization}/{repository}", "--json", "nameWithOwner"],
            environment: environment, cancellationToken: cancellationToken);
        if (existing.ExitCode == 0)
        {
            return Destination(organization, repository);
        }

        if (!existing.StandardError.Contains("Could not resolve to a Repository", StringComparison.OrdinalIgnoreCase) &&
            !existing.StandardError.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Could not inspect the destination repository: " + existing.StandardError);
        }

        var create = await commands.RunAsync(
            "gh",
            ["repo", "create", $"{organization}/{repository}", "--description", "Migrated from SVN", "--private"],
            environment: environment,
            cancellationToken: cancellationToken);
        if (create.ExitCode != 0)
        {
            return Failure("Could not create the destination. If it already exists, verify the owner, repository name and token access: " +
                create.StandardError);
        }

        return Destination(organization, repository);
    }

    private static Dictionary<string, object?> Destination(string organization, string repository)
    {
        var cloneUrl = $"https://github.com/{organization}/{repository}.git";
        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["org"] = organization,
            ["repo"] = repository,
            ["clone_url"] = cloneUrl,
            ["ssh_url"] = $"git@github.com:{organization}/{repository}.git",
            ["web_url"] = $"https://github.com/{organization}/{repository}"
        };
    }

    private static Dictionary<string, object?> Failure(string error) =>
        new() { ["success"] = false, ["error"] = error };
}

public sealed class PushVerifier(IExternalCommandRunner commands)
{
    public async Task<Dictionary<string, object?>> PushAndVerifyAsync(
        string workspace,
        string remoteUrl,
        string token,
        CancellationToken cancellationToken)
    {
        var repoPath = Path.Combine(workspace, "migrated-repo");
        var environment = new Dictionary<string, string?> { ["GH_TOKEN"] = token };
        var existing = await commands.RunAsync(
            "git", ["remote", "get-url", "origin"], repoPath,
            cancellationToken: cancellationToken);
        var remoteArgs = existing.ExitCode == 0
            ? new[] { "remote", "set-url", "origin", remoteUrl }
            : new[] { "remote", "add", "origin", remoteUrl };
        var setRemote = await commands.RunAsync(
            "git", remoteArgs, repoPath, cancellationToken: cancellationToken);
        if (setRemote.ExitCode != 0)
        {
            return Failure("Failed to add remote: " + setRemote.StandardError);
        }

        var branchesPush = await commands.RunAsync(
            "git", ["push", "origin", "--all"], repoPath,
            environment: environment, cancellationToken: cancellationToken);
        if (branchesPush.ExitCode != 0)
        {
            return Failure("Push failed: " + branchesPush.StandardError);
        }

        var tagsPush = await commands.RunAsync(
            "git", ["push", "origin", "--tags"], repoPath,
            environment: environment, cancellationToken: cancellationToken);
        if (tagsPush.ExitCode != 0)
        {
            return Failure("Push failed: " + tagsPush.StandardError);
        }

        var localBranches = await ReadLocalRefsAsync(repoPath, "refs/heads/", cancellationToken);
        var localTags = await ReadLocalRefsAsync(repoPath, "refs/tags/", cancellationToken);
        var remoteBranches = await ReadRemoteRefsAsync(repoPath, "--heads", "refs/heads/", environment, cancellationToken);
        var remoteTags = await ReadRemoteRefsAsync(repoPath, "--tags", "refs/tags/", environment, cancellationToken);
        var branchVerification = CompareRefs(localBranches, remoteBranches);
        var tagVerification = CompareRefs(localTags, remoteTags);
        var report = new ReadinessReport();
        foreach (var (kind, local, remote) in new[]
                 { ("Branch", localBranches, remoteBranches), ("Tag", localTags, remoteTags) })
        {
            foreach (var name in local.Keys.Union(remote.Keys).Order(StringComparer.Ordinal))
            {
                var localHash = local.GetValueOrDefault(name);
                var remoteHash = remote.GetValueOrDefault(name);
                report.Checks.Add(new ReadinessCheck($"{kind}: {name}",
                    localHash is not null && localHash == remoteHash ? "passed" : "failed",
                    $"Imported Git object: {localHash ?? "absent"}; destination object: {remoteHash ?? "absent"}."));
            }
        }

        if (localBranches.Count == 0)
        {
            report.Checks.Add(new ReadinessCheck("Imported branches", "failed", "No local branches were found."));
        }

        var integrity = await commands.RunAsync("git", ["fsck", "--full"], repoPath,
            cancellationToken: cancellationToken);
        report.Checks.Add(new ReadinessCheck("Git object integrity",
            integrity.ExitCode == 0 ? "passed" : "failed",
            integrity.ExitCode == 0 ? "git fsck --full passed." : "git fsck failed. Inspect the managed repository before retrying."));
        var authors = await commands.RunCheckedAsync("git", ["log", "--all", "--format=%aN <%aE>"],
            repoPath, cancellationToken: cancellationToken);
        var identities = authors.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        report.Checks.Add(new ReadinessCheck("Imported author inventory", "warning",
            $"Review these {identities.Count} identities against the source mapping: " + string.Join("; ", identities)));
        report.Checks.Add(new ReadinessCheck("Verification scope", "warning",
            "This compares imported Git refs/object IDs with GitHub and checks local Git integrity. It does not prove SVN history/author completeness, source file parity, build success, or cutover readiness. Cleanup intentionally rewrites history."));
        var verification = new Dictionary<string, object?>
        {
            ["branches_match"] = branchVerification.Match,
            ["tags_match"] = tagVerification.Match,
            ["missing_branches"] = branchVerification.Missing,
            ["different_branches"] = branchVerification.Different,
            ["extra_branches"] = branchVerification.Extra,
            ["missing_tags"] = tagVerification.Missing,
            ["different_tags"] = tagVerification.Different,
            ["extra_tags"] = tagVerification.Extra,
            ["status"] = !report.HasBlockingIssues
                ? "success"
                : "warning"
        };
        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["pushed"] = new { branches = remoteBranches.Keys.Order().ToList(), tags = remoteTags.Keys.Order().ToList() },
            ["verification"] = verification,
            ["report"] = report
        };
    }

    private async Task<Dictionary<string, string>> ReadLocalRefsAsync(
        string repoPath,
        string prefix,
        CancellationToken cancellationToken)
    {
        var result = await commands.RunCheckedAsync(
            "git", ["for-each-ref", "--format=%(objectname)\t%(refname)", prefix],
            repoPath, cancellationToken: cancellationToken);
        return ParseRefs(result.StandardOutput, prefix);
    }

    private async Task<Dictionary<string, string>> ReadRemoteRefsAsync(
        string repoPath,
        string selector,
        string prefix,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        var result = await commands.RunCheckedAsync(
            "git", ["ls-remote", "--refs", selector, "origin"],
            repoPath, timeout: TimeSpan.FromSeconds(120), environment: environment,
            cancellationToken: cancellationToken);
        return ParseRefs(result.StandardOutput, prefix);
    }

    private static Dictionary<string, string> ParseRefs(string output, string prefix)
    {
        var refs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2 && parts[1].StartsWith(prefix, StringComparison.Ordinal))
            {
                refs[parts[1][prefix.Length..]] = parts[0];
            }
        }

        return refs;
    }

    private static (bool Match, List<string> Missing, List<string> Different, List<string> Extra)
        CompareRefs(Dictionary<string, string> local, Dictionary<string, string> remote)
    {
        var missing = local.Keys.Except(remote.Keys).Order().ToList();
        var different = local.Keys.Intersect(remote.Keys)
            .Where(name => local[name] != remote[name]).Order().ToList();
        var extra = remote.Keys.Except(local.Keys).Order().ToList();
        return (!missing.Any() && !different.Any() && !extra.Any(), missing, different, extra);
    }

    private static Dictionary<string, object?> Failure(string error) =>
        new() { ["success"] = false, ["error"] = error };
}
