using SVN2GitHub.Shared;
using SVN2GitHub.Api.Services;
using SVN2GitHub.Api.Models;

namespace SVN2GitHub.Api.Tests;

public sealed class SvnUrlTests
{
    [Theory]
    [InlineData("https://svn.example.test/Source Code/", "https://svn.example.test/Source%20Code/")]
    [InlineData("svn://127.0.0.1:3690/Source Code", "svn://127.0.0.1:3690/Source%20Code")]
    [InlineData(" https://svn.example.test/Source%20Code/ ", "https://svn.example.test/Source%20Code/")]
    [InlineData("https://svn.example.test/a%2Fb", "https://svn.example.test/a%2Fb")]
    public void Normalization_EncodesPathWithoutDoubleEncoding(string input, string expected)
    {
        Assert.True(SvnUrlNormalizer.TryNormalize(input, out var result));
        Assert.Equal(expected, result);
        Assert.Equal(result, SvnUrlNormalizer.Normalize(result));
    }

    [Theory]
    [InlineData(@"svn.com\Source Code\")]
    [InlineData(@"https://svn.com\Source Code\")]
    [InlineData(@"\\svn.com\Source Code\")]
    [InlineData("https://user:password@svn.example.test/Source Code")]
    [InlineData("https://svn.example.test/repo?token=value")]
    [InlineData("https://svn.example.test/repo#fragment")]
    [InlineData("file:///C:/Source%20Code")]
    [InlineData("https://svn.example.test/a\nb")]
    public void InvalidInputs_AreExplicitlyRejected(string input)
    {
        Assert.False(SvnUrlNormalizer.TryNormalize(input, out _));
        Assert.Throws<ArgumentException>(() => SvnUrlNormalizer.Normalize(input));
    }

    [Fact]
    public async Task AuthorMapping_PassesEncodedUrlAsOneArgument()
    {
        var root = Path.Combine(Path.GetTempPath(), "svn-url-" + Guid.NewGuid().ToString("N"));
        try
        {
            await new AuthorMapper(new Runner()).CreateMappingAsync(
                "https://svn.example.test/Source Code", root, "example.test", CancellationToken.None);
            Assert.Equal("https://svn.example.test/Source%20Code/Main%20Trunk",
                SvnUrlNormalizer.AppendPath("https://svn.example.test/Source Code", "Main Trunk"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("https://svn.example.test/Source Code", "trunk")]
    [InlineData("https://svn.example.test/Source%20Code", "Main Trunk")]
    public async Task Import_DisablesUrlMinimizationAndPreservesEncodedUrl(string url, string trunk)
    {
        var result = await new MigrationExecutor(new CloneRunner(trunk)).ExecuteAsync(
            new MigrationRequest { SvnUrl = url, TrunkPath = trunk },
            Path.GetTempPath(), "authors.txt", CancellationToken.None);
        Assert.Equal(false, result["success"]);
    }

    private sealed class CloneRunner(string trunk) : IExternalCommandRunner
    {
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            string? workingDirectory = null, TimeSpan? timeout = null,
            IReadOnlyDictionary<string, string?>? environment = null, CancellationToken cancellationToken = default)
        {
            var args = arguments.ToArray();
            Assert.Equal("git", executable);
            Assert.Equal(new[] { "svn", "clone", "--no-minimize-url" }, args.Take(3));
            Assert.Contains("https://svn.example.test/Source%20Code", args);
            Assert.DoesNotContain(args, value => value.Contains("%2520", StringComparison.Ordinal));
            if (trunk == "trunk") Assert.Contains("--stdlayout", args);
            else
            {
                Assert.Equal(trunk, args[Array.IndexOf(args, "--trunk") + 1]);
                Assert.DoesNotContain("--stdlayout", args);
            }
            return Task.FromResult(new CommandResult(1, "", "Intentional test stop before import."));
        }
    }

    private sealed class Runner : IExternalCommandRunner
    {
        public Task<CommandResult> RunAsync(string executable, IEnumerable<string> arguments,
            string? workingDirectory = null, TimeSpan? timeout = null,
            IReadOnlyDictionary<string, string?>? environment = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal("svn", executable);
            Assert.Equal(new[] { "log", "--quiet", "https://svn.example.test/Source%20Code" }, arguments);
            return Task.FromResult(new CommandResult(0, "r1 | alice | date", ""));
        }
    }
}
