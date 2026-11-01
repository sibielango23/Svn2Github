namespace SVN2GitHub.Models
{
    public enum MigrationStatus
    {
        Pending,
        Analyzing,
        MappingAuthors,
        Migrating,
        Cleaning,
        Pushing,
        Completed,
        Failed
    }

    public class MigrationRequest
    {
        public string SvnUrl { get; set; } = string.Empty;
        public string GithubOrg { get; set; } = string.Empty;
        public string GithubRepo { get; set; } = string.Empty;
        public string GithubToken { get; set; } = string.Empty;
        public string? AuthorsFile { get; set; }
        public bool CleanHistory { get; set; }
        public string TrunkPath { get; set; } = "trunk";
        public string BranchesPath { get; set; } = "branches";
        public string TagsPath { get; set; } = "tags";
        public List<AuthorCreateRequest> Authors { get; set; } = new();
    }

    public class MigrationResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class MigrationDetail
    {
        public string Id { get; set; } = string.Empty;
        public string SvnUrl { get; set; } = string.Empty;
        public string GithubOrg { get; set; } = string.Empty;
        public string GithubRepo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public List<string> Logs { get; set; } = new();
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
        public double? ElapsedSeconds { get; set; }
        public DateTimeOffset? RunningSince { get; set; }
        public string ConversionTimeDisplay => ElapsedSeconds is not { } seconds
            ? Status == "pending" ? "Conversion time: waiting in queue" : "Conversion time: not recorded"
            : $"Conversion time: {FormatDuration(seconds)}{(RunningSince is not null ? " (running)" : string.Empty)}";

        public static string FormatDuration(double seconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return duration.TotalDays >= 1
                ? $"{(int)duration.TotalDays}d {duration.Hours:00}h {duration.Minutes:00}m {duration.Seconds:00}s"
                : $"{(int)duration.TotalHours:00}h {duration.Minutes:00}m {duration.Seconds:00}s";
        }
        public string? Error { get; set; }
        public bool CleanHistory { get; set; }
        public string LastCompletedStage { get; set; } = "none";
        public bool CanResume { get; set; }
        public bool CanCancel { get; set; }
        public ReadinessReport? VerificationReport { get; set; }
    }

    public class ResumeMigrationRequest
    {
        public string GithubToken { get; set; } = string.Empty;
    }

    public class Stats
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
    }
}
