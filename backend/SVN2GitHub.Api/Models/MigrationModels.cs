namespace SVN2GitHub.Api.Models;

public sealed class MigrationRequest
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
    public List<AuthorInput> Authors { get; set; } = [];
}

public sealed class AuthorInput
{
    public string SvnUsername { get; set; } = string.Empty;
    public string GitName { get; set; } = string.Empty;
    public string GitEmail { get; set; } = string.Empty;
    public bool Verified { get; set; }
}

public sealed class AuthorRecord
{
    public int Id { get; set; }
    public string MigrationId { get; set; } = string.Empty;
    public string SvnUsername { get; set; } = string.Empty;
    public string GitName { get; set; } = string.Empty;
    public string GitEmail { get; set; } = string.Empty;
    public int CommitCount { get; set; }
    public bool Verified { get; set; }
}

public sealed class MigrationResponse
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class MigrationDetail
{
    public string Id { get; set; } = string.Empty;
    public string SvnUrl { get; set; } = string.Empty;
    public string GithubOrg { get; set; } = string.Empty;
    public string GithubRepo { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int Progress { get; set; }
    public string CurrentStep { get; set; } = "Queued";
    public List<string> Logs { get; set; } = [];
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public double? ElapsedSeconds { get; set; }
    public DateTimeOffset? RunningSince { get; set; }
    public string? Error { get; set; }
    public bool CleanHistory { get; set; }
    public string LastCompletedStage { get; set; } = MigrationStages.None;
    public bool CanResume => Status == "failed";
    public bool CanCancel => Status is "pending" or "analyzing" or "mapping_authors" or
        "migrating" or "pushing";
    public ReadinessReport? VerificationReport { get; set; }
}

public static class MigrationStages
{
    public const string None = "none";
    public const string Analysis = "analysis";
    public const string Authors = "authors";
    public const string Import = "import";
    public const string HistoryCleanup = "history_cleanup";
    public const string GitHubSetup = "github_setup";

    public static int GetRank(string stage) => stage switch
    {
        Analysis => 1,
        Authors => 2,
        Import => 3,
        HistoryCleanup => 4,
        GitHubSetup => 5,
        _ => 0
    };

    public static string GetDisplayName(string stage) => stage switch
    {
        Analysis => "SVN analysis",
        Authors => "author mapping",
        Import => "SVN import",
        HistoryCleanup => "history cleanup",
        GitHubSetup => "GitHub repository setup",
        _ => "migration start"
    };
}

public sealed class ResumeMigrationRequest
{
    public string GithubToken { get; set; } = string.Empty;
}

public sealed class MigrationRecord
{
    public MigrationDetail Detail { get; set; } = new();
    public string GithubToken { get; set; } = string.Empty;
    public string? AuthorsFile { get; set; }
    public bool CleanHistory { get; set; }
    public string TrunkPath { get; set; } = "trunk";
    public string BranchesPath { get; set; } = "branches";
    public string TagsPath { get; set; } = "tags";
    public List<AuthorRecord> Authors { get; set; } = [];
}

public sealed class MigrationStats
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
}

public sealed class AuthorSessionRequest
{
    public string SvnUrl { get; set; } = string.Empty;
}

public sealed class AuthorBulkRequest
{
    public List<AuthorInput>? Authors { get; set; }
}

public sealed class ApiError
{
    public string Detail { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}
