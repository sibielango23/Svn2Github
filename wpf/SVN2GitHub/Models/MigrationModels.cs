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
        public string? Error { get; set; }
    }

    public class Stats
    {
        public int Total { get; set; }
        public int Active { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
    }
}
