namespace SVN2GitHub.Api.Models;

public sealed class ReadinessRequest
{
    public bool CleanHistory { get; set; }
}

public sealed record ReadinessCheck(string Name, string Status, string Detail);

public sealed class ReadinessReport
{
    public string CheckedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public List<ReadinessCheck> Checks { get; set; } = [];
    public bool HasBlockingIssues => Checks.Any(check => check.Status == "failed");
}
