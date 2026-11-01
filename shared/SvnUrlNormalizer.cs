namespace SVN2GitHub.Shared;

public static class SvnUrlNormalizer
{
    public const string ValidationMessage =
        "Enter an SVN URL starting with https://, http://, or svn:// and use forward slashes (/). " +
        "Spaces in the path are supported and encoded as %20. Windows/UNC paths, embedded credentials, queries and fragments are not supported.";

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        var input = value?.Trim();
        if (string.IsNullOrEmpty(input) || input.Contains('\\') || input.Any(char.IsControl) ||
            !Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https" or "svn") ||
            !input.StartsWith(uri.Scheme + "://", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Length == 0 || uri.UserInfo.Length > 0 ||
            uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }

    public static string Normalize(string? value) =>
        TryNormalize(value, out var normalized) ? normalized : throw new ArgumentException(ValidationMessage);

    public static string AppendPath(string baseUrl, string relativePath) =>
        Normalize(Normalize(baseUrl).TrimEnd('/') + "/" + relativePath);
}
