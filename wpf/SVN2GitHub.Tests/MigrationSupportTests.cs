using SVN2GitHub.Models;
using SVN2GitHub.Services;
using SVN2GitHub.ViewModels;

namespace SVN2GitHub.Tests;

public sealed class MigrationSupportTests
{
    [Theory]
    [InlineData("github_setup", "does not force-push")]
    [InlineData("authors", "repeats the import")]
    [InlineData("none", "SVN analysis")]
    public void RecoveryAdvice_IsCheckpointAware(string stage, string expected)
    {
        var migration = new MigrationDetail { Status = "failed", LastCompletedStage = stage };
        Assert.Contains(expected, MigrationSupportService.GetRecoveryAdvice(migration));
    }

    [Fact]
    public void SupportReport_RedactsKnownSecrets_AndRetainsAllLogs()
    {
        var providerKey = "sk-ant-" + new string('a', 40);
        var githubToken = "ghp_" + new string('b', 40);
        var migration = new MigrationDetail
        {
            Id = "test",
            Status = "failed",
            SvnUrl = "https://user:password@example.test/repo",
            Error = "Authorization: Bearer sensitive-value",
            Logs = Enumerable.Range(0, 50).Select(index => $"entry-{index}").ToList()
        };
        migration.Logs.Add($"API key={providerKey} token={githubToken}");
        var report = MigrationSupportService.BuildReport(migration);
        Assert.DoesNotContain(providerKey, report);
        Assert.DoesNotContain(githubToken, report);
        Assert.DoesNotContain("user:password", report);
        Assert.DoesNotContain("sensitive-value", report);
        Assert.Contains("entry-0", report);
        Assert.Contains("entry-49", report);
        Assert.Contains("[REDACTED]", report);
        Assert.Contains("not a tamper-proof audit", report);
    }

    [Fact]
    public void Diagnostics_FilterDoesNotMutateRecordedLogs()
    {
        var migration = new MigrationDetail { Logs = new List<string> { "ERROR: read failed", "Push complete" } };
        var viewModel = new MigrationDiagnosticsViewModel(migration) { SearchText = "error" };
        Assert.Equal("ERROR: read failed", viewModel.FilteredLogs);
        Assert.Equal(2, migration.Logs.Count);
    }

    [Theory]
    [InlineData(0, "00h 00m 00s")]
    [InlineData(3661, "01h 01m 01s")]
    [InlineData(90061, "1d 01h 01m 01s")]
    public void ConversionTime_FormatsHoursAndDays(double seconds, string expected)
    {
        Assert.Equal(expected, MigrationDetail.FormatDuration(seconds));
        var detail = new MigrationDetail { ElapsedSeconds = seconds };
        Assert.Contains(expected, MigrationSupportService.BuildSnapshot(detail));
    }

    [Fact]
    public void ConversionTime_UnknownHistoryIsNotReportedAsZero()
    {
        Assert.Contains("not recorded", new MigrationDetail { Status = "completed" }.ConversionTimeDisplay);
        Assert.Contains("waiting in queue", new MigrationDetail { Status = "pending" }.ConversionTimeDisplay);
        Assert.Contains("(running)", new MigrationDetail
        {
            ElapsedSeconds = 5, RunningSince = DateTimeOffset.UtcNow
        }.ConversionTimeDisplay);
    }
}
