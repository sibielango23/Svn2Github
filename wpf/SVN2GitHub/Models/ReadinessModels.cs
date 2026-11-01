namespace SVN2GitHub.Models;

public sealed class ReadinessCheck
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class ReadinessReport
{
    public string CheckedAt { get; set; } = string.Empty;
    public List<ReadinessCheck> Checks { get; set; } = new();
    public bool HasBlockingIssues { get; set; }
}
