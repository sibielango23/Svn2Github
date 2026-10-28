namespace SVN2GitHub.Models
{
    public class Author
    {
        public int Id { get; set; }
        public string SvnUsername { get; set; } = string.Empty;
        public string GitName { get; set; } = string.Empty;
        public string GitEmail { get; set; } = string.Empty;
        public bool Verified { get; set; }
    }

    public class AuthorCreateRequest
    {
        public string SvnUsername { get; set; } = string.Empty;
        public string GitName { get; set; } = string.Empty;
        public string GitEmail { get; set; } = string.Empty;
        public bool Verified { get; set; }
    }

    public class AuthorBulkUpdateRequest
    {
        public List<AuthorCreateRequest> Authors { get; set; } = new();
    }
}
