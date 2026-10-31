using SVN2GitHub.Api.Services;

namespace SVN2GitHub.Api.Tests;

public sealed class MigrationExecutorTests
{
    [Fact]
    public async Task ConvertSvnRefsAsync_CreatesGitBranchesAndTagsFromMappings()
    {
        var repository = Path.Combine(
            Path.GetTempPath(), "SVN2GitHub.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repository);
        try
        {
            var commands = new ExternalCommandRunner();
            await RunGitAsync(commands, repository, "init", "-q");
            await RunGitAsync(commands, repository, "config", "user.name", "Migration Test");
            await RunGitAsync(commands, repository, "config", "user.email", "test@example.test");
            await File.WriteAllTextAsync(Path.Combine(repository, "README.txt"), "test");
            await RunGitAsync(commands, repository, "add", "README.txt");
            await RunGitAsync(commands, repository, "commit", "-m", "initial");
            var commit = (await RunGitAsync(commands, repository, "rev-parse", "HEAD"))
                .StandardOutput.Trim();

            await RunGitAsync(commands, repository, "update-ref",
                "refs/remotes/svn/trunk", commit);
            await RunGitAsync(commands, repository, "update-ref",
                "refs/remotes/svn/branches/release", commit);
            await RunGitAsync(commands, repository, "update-ref",
                "refs/remotes/svn/tags/v1", commit);
            await RunGitAsync(commands, repository, "config", "svn-remote.svn.fetch",
                "trunk:refs/remotes/svn/trunk");
            await RunGitAsync(commands, repository, "config", "svn-remote.svn.branches",
                "branches/*:refs/remotes/svn/branches/*");
            await RunGitAsync(commands, repository, "config", "svn-remote.svn.tags",
                "tags/*:refs/remotes/svn/tags/*");

            await new MigrationExecutor(commands).ConvertSvnRefsAsync(
                repository, CancellationToken.None);

            Assert.Equal(commit, (await RunGitAsync(commands, repository,
                "rev-parse", "refs/heads/main")).StandardOutput.Trim());
            Assert.Equal(commit, (await RunGitAsync(commands, repository,
                "rev-parse", "refs/heads/release")).StandardOutput.Trim());
            Assert.Equal(commit, (await RunGitAsync(commands, repository,
                "rev-parse", "refs/tags/v1")).StandardOutput.Trim());
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(
                         repository, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(repository, recursive: true);
        }
    }

    private static Task<CommandResult> RunGitAsync(
        ExternalCommandRunner commands,
        string repository,
        params string[] arguments) =>
        commands.RunCheckedAsync("git", arguments, repository);
}
