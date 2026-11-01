using System.Text.RegularExpressions;
using SVN2GitHub.Api.Models;
using SVN2GitHub.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(
    Environment.GetEnvironmentVariable("SVN2GITHUB_API_URL") ?? "http://127.0.0.1:5000");
var dataDirectory = Environment.GetEnvironmentVariable("SVN2GITHUB_DATA_DIRECTORY");
if (!string.IsNullOrWhiteSpace(dataDirectory))
{
    builder.Configuration["DataDirectory"] = dataDirectory;
}

var workspaceDirectory = Environment.GetEnvironmentVariable("SVN2GITHUB_WORKSPACE_DIRECTORY");
if (!string.IsNullOrWhiteSpace(workspaceDirectory))
{
    builder.Configuration["WorkspaceDirectory"] = workspaceDirectory;
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddSingleton<MigrationStateStore>();
builder.Services.AddSingleton<ExternalCommandRunner>();
builder.Services.AddSingleton<IExternalCommandRunner>(serviceProvider =>
    serviceProvider.GetRequiredService<ExternalCommandRunner>());
builder.Services.AddSingleton<ReadinessService>();
builder.Services.AddSingleton<MigrationAssessmentService>();
builder.Services.AddSingleton<RepositoryAnalyzer>();
builder.Services.AddSingleton<AuthorMapper>();
builder.Services.AddSingleton<MigrationExecutor>();
builder.Services.AddSingleton<HistoryCleaner>();
builder.Services.AddSingleton<GitHubSetup>();
builder.Services.AddSingleton<PushVerifier>();
builder.Services.AddSingleton<MigrationQueue>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<MigrationQueue>());

var app = builder.Build();
var stateStore = app.Services.GetRequiredService<MigrationStateStore>();
stateStore.Initialize();

app.MapGet("/", () => Results.Ok(new
{
    message = "SVN to GitHub Migration API",
    version = "1.0.0"
}));

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    runtime = "dotnet",
    timestamp = DateTimeOffset.UtcNow.ToString("O")
}));

app.MapPost("/api/readiness", async (
    ReadinessRequest request,
    ReadinessService readiness,
    CancellationToken cancellationToken) =>
    Results.Ok(await readiness.CheckAsync(request.CleanHistory, cancellationToken)));

app.MapPost("/api/assessment", async (
    MigrationRequest request, MigrationAssessmentService assessment, CancellationToken cancellationToken) =>
{
    var error = ValidateMigration(request);
    return error is not null
        ? Results.UnprocessableEntity(new ApiError { Detail = error })
        : Results.Ok(await assessment.AssessAsync(request, cancellationToken));
});

app.MapPost("/api/migrations", (MigrationRequest request, MigrationQueue queue) =>
{
    var validationError = ValidateMigration(request);
    if (validationError is not null)
    {
        return Results.UnprocessableEntity(new ApiError { Detail = validationError });
    }

    var response = stateStore.Create(request);
    if (!queue.Enqueue(response.Id))
    {
        stateStore.UpdateMigration(response.Id, "failed", "Could not queue migration", 0,
            "Migration could not be queued.", "Migration could not be queued.");
        stateStore.ClearGithubToken(response.Id);
        return Results.Problem(
            detail: "Migration could not be queued.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(response);
});

app.MapGet("/api/migrations", () => Results.Ok(stateStore.GetMigrations()));

app.MapGet("/api/migrations/{migrationId}", (string migrationId) =>
    stateStore.GetDetail(migrationId) is { } detail
        ? Results.Ok(detail)
        : Results.NotFound(new ApiError { Detail = "Migration not found" }));

app.MapPost("/api/migrations/{migrationId}/resume", (
    string migrationId,
    ResumeMigrationRequest request,
    MigrationQueue queue) =>
{
    if (string.IsNullOrWhiteSpace(request.GithubToken))
    {
        return Results.UnprocessableEntity(new ApiError
        {
            Detail = "A GitHub token is required to resume the migration."
        });
    }

    if (!queue.TryPrepareResume(migrationId, request.GithubToken, out var error))
    {
        return error == "Migration not found."
            ? Results.NotFound(new ApiError { Detail = error })
            : Results.Conflict(new ApiError { Detail = error });
    }

    if (!queue.Enqueue(migrationId))
    {
        stateStore.FailResumeQueueing(migrationId);
        return Results.Problem(
            detail: "The migration resume could not be queued.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Accepted(
        $"/api/migrations/{migrationId}",
        stateStore.GetDetail(migrationId));
});

app.MapDelete("/api/migrations/{migrationId}", (string migrationId, MigrationQueue queue) =>
{
    var migration = stateStore.GetDetail(migrationId);
    if (migration is null)
    {
        return Results.NotFound(new ApiError { Detail = "Migration not found" });
    }

    if (migration.Status is "completed" or "failed")
    {
        return Results.BadRequest(new ApiError
        {
            Detail = "Cannot cancel completed or failed migration"
        });
    }

    return queue.TryCancel(migrationId, out var error)
        ? Results.Accepted($"/api/migrations/{migrationId}", stateStore.GetDetail(migrationId))
        : Results.Conflict(new ApiError { Detail = error });
});

app.MapGet("/api/stats", () => Results.Ok(stateStore.GetStats()));

app.MapGet("/api/authors", () => Results.Ok(stateStore.GetLegacyAuthors()));
app.MapPost("/api/authors", (List<Dictionary<string, string>>? authors) =>
{
    if (authors is null)
    {
        return Results.UnprocessableEntity(new ApiError { Detail = "The authors list is required" });
    }

    stateStore.SetLegacyAuthors(authors);
    return Results.Ok(authors);
});

app.MapGet("/api/authors/{migrationId}", (string migrationId) =>
    !stateStore.IsAuthorOwner(migrationId)
        ? Results.NotFound(new ApiError { Detail = "Migration not found" })
        : Results.Ok(stateStore.GetAuthors(migrationId)));

app.MapPost("/api/authors/{migrationId}/session", (string migrationId, AuthorSessionRequest request) =>
{
    if (!migrationId.StartsWith("temp_", StringComparison.Ordinal))
    {
        return Results.BadRequest(new ApiError
        {
            Detail = "Temporary author session IDs must start with 'temp_'"
        });
    }

    if (!IsValidSvnUrl(request.SvnUrl))
    {
        return Results.UnprocessableEntity(new ApiError
        {
            Detail = "SVN URL must use https://, http://, or svn://"
        });
    }

    stateStore.OpenAuthorSession(migrationId, request.SvnUrl);
    return Results.Ok(new { id = migrationId });
});

app.MapDelete("/api/authors/{migrationId}/session", (string migrationId) =>
{
    stateStore.CloseAuthorSession(migrationId);
    return Results.Ok(new { message = "Temporary author session closed" });
});

app.MapPost("/api/authors/{migrationId}/extract", async (
    string migrationId,
    CancellationToken cancellationToken,
    MigrationStateStore store,
    ExternalCommandRunner commands) =>
{
    if (!store.TryGetSvnUrl(migrationId, out var svnUrl))
    {
        return Results.NotFound(new ApiError { Detail = "Migration not found" });
    }

    try
    {
        var result = await commands.RunCheckedAsync(
            "svn", ["log", "--quiet", "--limit", "5000", svnUrl],
            timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
        var usernames = RepositoryAnalyzer.ExtractUsernames(result.StandardOutput);
        return Results.Ok(store.MergeExtractedAuthors(migrationId, usernames));
    }
    catch (InvalidOperationException exception) when (
        exception.Message.Contains("not installed", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(new ApiError { Detail = "SVN client is not installed" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException)
    {
        return Results.Json(new ApiError { Detail = "SVN author extraction timed out" },
            statusCode: StatusCodes.Status408RequestTimeout);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Json(new ApiError
        {
            Detail = "SVN author extraction failed: " + exception.Message
        }, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/authors/{migrationId}/bulk", (string migrationId, AuthorBulkRequest request) =>
{
    if (!stateStore.IsAuthorOwner(migrationId))
    {
        return Results.NotFound(new ApiError { Detail = "Migration not found" });
    }

    if (request.Authors is null)
    {
        return Results.UnprocessableEntity(new ApiError { Detail = "The authors list is required" });
    }

    var seen = new HashSet<string>(StringComparer.Ordinal);
    var normalized = new List<AuthorRecord>();
    for (var index = 0; index < request.Authors.Count; index++)
    {
        var author = request.Authors[index];
        var username = author.SvnUsername.Trim();
        var name = author.GitName.Trim();
        var email = author.GitEmail.Trim();
        if (username.Length == 0 || name.Length == 0 || email.Length == 0)
        {
            return Results.UnprocessableEntity(new ApiError
            {
                Detail = "Each author requires a username, Git name, and email"
            });
        }

        if (!seen.Add(username))
        {
            return Results.UnprocessableEntity(new ApiError
            {
                Detail = $"Duplicate SVN username: {username}"
            });
        }

        normalized.Add(new AuthorRecord
        {
            Id = index + 1,
            MigrationId = migrationId,
            SvnUsername = username,
            GitName = name,
            GitEmail = email,
            Verified = author.Verified
        });
    }

    stateStore.ReplaceAuthors(migrationId, normalized);
    return Results.Ok(stateStore.GetAuthors(migrationId));
});

app.MapPost("/api/authors/{migrationId}", (string migrationId, AuthorInput request) =>
{
    if (!stateStore.IsAuthorOwner(migrationId))
    {
        return Results.NotFound(new ApiError { Detail = "Migration not found" });
    }

    var authors = stateStore.GetAuthors(migrationId);
    if (!ValidateSingleAuthor(request, authors, out var error))
    {
        return Results.UnprocessableEntity(new ApiError { Detail = error });
    }

    authors.Add(ToAuthorRecord(migrationId, authors.Count + 1, request));
    stateStore.ReplaceAuthors(migrationId, authors);
    return Results.Ok(authors[^1]);
});

app.MapPut("/api/authors/{migrationId}/{authorId:int}", (
    string migrationId, int authorId, AuthorInput request) =>
{
    if (!stateStore.IsAuthorOwner(migrationId))
    {
        return Results.NotFound(new ApiError { Detail = "Migration not found" });
    }

    var authors = stateStore.GetAuthors(migrationId);
    if (!ValidateSingleAuthor(request, authors.Where(author => author.Id != authorId).ToList(),
            out var error))
    {
        return Results.UnprocessableEntity(new ApiError { Detail = error });
    }

    var update = ToAuthorRecord(migrationId, authorId, request);
    return stateStore.UpdateAuthor(migrationId, authorId, update)
        ? Results.Ok(update)
        : Results.NotFound(new ApiError { Detail = "Author not found" });
});

app.MapDelete("/api/authors/{migrationId}/{authorId:int}", (string migrationId, int authorId) =>
{
    if (!stateStore.IsAuthorOwner(migrationId))
    {
        return Results.NotFound(new ApiError { Detail = "Migration not found" });
    }

    return stateStore.DeleteAuthor(migrationId, authorId)
        ? Results.Ok(new { message = "Author deleted" })
        : Results.NotFound(new ApiError { Detail = "Author not found" });
});

app.MapGet("/api/authors/{migrationId}/export", (string migrationId) =>
{
    if (!stateStore.IsAuthorOwner(migrationId))
    {
        return Results.NotFound(new ApiError { Detail = "Migration not found" });
    }

    var content = string.Join(Environment.NewLine, stateStore.GetAuthors(migrationId)
        .Select(author => $"{author.SvnUsername} = {author.GitName} <{author.GitEmail}>"));
    return Results.Ok(new { content });
});

app.Run();

static string? ValidateMigration(MigrationRequest request)
{
    if (request.Authors is null)
    {
        return "The authors list is required";
    }

    if (!IsValidSvnUrl(request.SvnUrl))
    {
        return "SVN URL must use https://, http://, or svn://";
    }

    if (string.IsNullOrWhiteSpace(request.GithubOrg) ||
        !Regex.IsMatch(request.GithubOrg,
            @"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?\z"))
    {
        return "Invalid GitHub organization name";
    }

    if (string.IsNullOrWhiteSpace(request.GithubRepo) ||
        !Regex.IsMatch(request.GithubRepo, @"\A[A-Za-z0-9._-]{1,100}\z") ||
        request.GithubRepo is "." or "..")
    {
        return "Invalid GitHub repository name";
    }

    if (string.IsNullOrWhiteSpace(request.GithubToken))
    {
        return "GitHub token cannot be empty";
    }

    var authors = new HashSet<string>(StringComparer.Ordinal);
    foreach (var author in request.Authors)
    {
        if (author is null ||
            string.IsNullOrWhiteSpace(author.SvnUsername) ||
            string.IsNullOrWhiteSpace(author.GitName) ||
            string.IsNullOrWhiteSpace(author.GitEmail))
        {
            return "Each author requires a username, Git name, and email";
        }

        if (!authors.Add(author.SvnUsername.Trim()))
        {
            return $"Duplicate SVN username: {author.SvnUsername.Trim()}";
        }
    }

    return null;
}

static bool IsValidSvnUrl(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
    uri.Host.Length > 0 &&
    uri.UserInfo.Length == 0 &&
    uri.Scheme is "http" or "https" or "svn";

static bool ValidateSingleAuthor(
    AuthorInput request,
    IReadOnlyList<AuthorRecord> authors,
    out string error)
{
    var username = request.SvnUsername.Trim();
    if (username.Length == 0 || request.GitName.Trim().Length == 0 ||
        request.GitEmail.Trim().Length == 0)
    {
        error = "Each author requires a username, Git name, and email";
        return false;
    }

    if (authors.Any(author => author.SvnUsername == username))
    {
        error = $"Duplicate SVN username: {username}";
        return false;
    }

    error = string.Empty;
    return true;
}

static AuthorRecord ToAuthorRecord(string migrationId, int id, AuthorInput input) => new()
{
    Id = id,
    MigrationId = migrationId,
    SvnUsername = input.SvnUsername.Trim(),
    GitName = input.GitName.Trim(),
    GitEmail = input.GitEmail.Trim(),
    Verified = input.Verified
};
