using SVN2GitHub.Api.Services;

namespace SVN2GitHub.Api.Tests;

public sealed class RepositoryAnalyzerTests
{
    [Fact]
    public void ExtractUsernames_ParsesSubversionLogAuthors()
    {
        const string log = """
            ------------------------------------------------------------------------
            r12 | alice.smith | 2024-01-02 03:04:05 +0000 (Tue, 02 Jan 2024) | 1 line
            ------------------------------------------------------------------------
            r11 | bob | 2024-01-01 03:04:05 +0000 (Mon, 01 Jan 2024) | 1 line
            """;

        Assert.Equal(["alice.smith", "bob"], RepositoryAnalyzer.ExtractUsernames(log));
    }
}
