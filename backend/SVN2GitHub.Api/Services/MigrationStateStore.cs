using System.Text.Json;
using Microsoft.Data.Sqlite;
using SVN2GitHub.Api.Models;

namespace SVN2GitHub.Api.Services;

public sealed class MigrationStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly object _sync = new();
    private readonly Dictionary<string, MigrationRecord> _migrations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _authorSessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Dictionary<string, string>>> _legacyAuthors = new(StringComparer.Ordinal);
    private readonly string _connectionString;

    public MigrationStateStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var dataDirectory = configuration["DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            var root = FindProjectRoot(environment.ContentRootPath);
            dataDirectory = root is null
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SVN2GitHub", "data")
                : Path.Combine(root, "data");
        }

        Directory.CreateDirectory(dataDirectory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "local_migrations.sqlite3"),
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS migration_state " +
                "(id TEXT PRIMARY KEY, record_json TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, record_json FROM migration_state";
        using var reader = select.ExecuteReader();
        var recovered = new List<MigrationRecord>();
        while (reader.Read())
        {
            recovered.Add(ReadPersistedRecord(reader.GetString(0), reader.GetString(1)));
        }

        lock (_sync)
        {
            foreach (var record in recovered)
            {
                if (IsActive(record.Detail.Status))
                {
                    record.Detail.Status = "failed";
                    record.Detail.Error =
                        "Backend restarted before this migration completed; start a new migration.";
                    record.Detail.CurrentStep = "Interrupted by backend restart";
                    record.Detail.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
                }

                _migrations[record.Detail.Id] = record;
                Persist(record);
            }
        }
    }

    public IReadOnlyList<MigrationDetail> GetMigrations()
    {
        lock (_sync)
        {
            return _migrations.Values
                .OrderByDescending(record => record.Detail.UpdatedAt, StringComparer.Ordinal)
                .Select(record => Clone(record.Detail)).ToList();
        }
    }

    public MigrationRecord? FindMigration(string id)
    {
        lock (_sync)
        {
            return _migrations.TryGetValue(id, out var record) ? Clone(record) : null;
        }
    }

    public MigrationDetail? GetDetail(string id)
    {
        lock (_sync)
        {
            return _migrations.TryGetValue(id, out var record) ? Clone(record.Detail) : null;
        }
    }

    public MigrationResponse Create(MigrationRequest request)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = Guid.NewGuid().ToString();
        var record = new MigrationRecord
        {
            Detail = new MigrationDetail
            {
                Id = id,
                SvnUrl = request.SvnUrl.Trim(),
                GithubOrg = request.GithubOrg,
                GithubRepo = request.GithubRepo,
                Status = "pending",
                Progress = 0,
                CurrentStep = "Queued",
                Logs = ["Migration created"],
                CreatedAt = now,
                UpdatedAt = now
            },
            GithubToken = request.GithubToken,
            AuthorsFile = request.AuthorsFile,
            CleanHistory = request.CleanHistory,
            TrunkPath = request.TrunkPath,
            BranchesPath = request.BranchesPath,
            TagsPath = request.TagsPath,
            Authors = request.Authors.Select((author, index) => new AuthorRecord
            {
                Id = index + 1,
                MigrationId = id,
                SvnUsername = author.SvnUsername.Trim(),
                GitName = author.GitName.Trim(),
                GitEmail = author.GitEmail.Trim(),
                Verified = author.Verified
            }).ToList()
        };

        lock (_sync)
        {
            _migrations.Add(id, record);
            Persist(record);
        }

        return new MigrationResponse
        {
            Id = id,
            Status = "pending",
            Message = "Migration job created successfully",
            CreatedAt = now
        };
    }

    public void UpdateMigration(
        string id,
        string status,
        string step,
        int progress,
        string log,
        string? error = null)
    {
        lock (_sync)
        {
            if (!_migrations.TryGetValue(id, out var record))
            {
                throw new KeyNotFoundException("Migration not found");
            }

            var now = DateTimeOffset.UtcNow.ToString("O");
            record.Detail.Status = status;
            record.Detail.CurrentStep = step;
            record.Detail.Progress = progress;
            record.Detail.UpdatedAt = now;
            record.Detail.Logs.Add($"{now}: {log}");
            record.Detail.Error = error;
            Persist(record);
        }
    }

    public MigrationStats GetStats()
    {
        lock (_sync)
        {
            return new MigrationStats
            {
                Total = _migrations.Count,
                Active = _migrations.Values.Count(record => IsActive(record.Detail.Status)),
                Completed = _migrations.Values.Count(record => record.Detail.Status == "completed"),
                Failed = _migrations.Values.Count(record => record.Detail.Status == "failed")
            };
        }
    }

    public Dictionary<string, List<Dictionary<string, object?>>> GetLegacyAuthors()
    {
        lock (_sync)
        {
            var result = _legacyAuthors.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Select(author =>
                    author.ToDictionary(
                        field => field.Key,
                        field => (object?)field.Value,
                        StringComparer.Ordinal)).ToList(),
                StringComparer.Ordinal);

            foreach (var (id, record) in _migrations)
            {
                result[id] = record.Authors.Select(author => new Dictionary<string, object?>
                {
                    ["id"] = author.Id,
                    ["migration_id"] = author.MigrationId,
                    ["svn_username"] = author.SvnUsername,
                    ["git_name"] = author.GitName,
                    ["git_email"] = author.GitEmail,
                    ["commit_count"] = author.CommitCount,
                    ["verified"] = author.Verified
                }).ToList();
            }

            return result;
        }
    }

    public void SetLegacyAuthors(List<Dictionary<string, string>> authors)
    {
        lock (_sync)
        {
            _legacyAuthors["default"] = authors.Select(author =>
                new Dictionary<string, string>(author, StringComparer.Ordinal)).ToList();
        }
    }

    public bool IsAuthorOwner(string id)
    {
        lock (_sync)
        {
            return _migrations.ContainsKey(id) || _authorSessions.ContainsKey(id);
        }
    }

    public bool TryGetSvnUrl(string id, out string svnUrl)
    {
        lock (_sync)
        {
            if (_migrations.TryGetValue(id, out var migration))
            {
                svnUrl = migration.Detail.SvnUrl;
                return true;
            }

            return _authorSessions.TryGetValue(id, out svnUrl!);
        }
    }

    public void OpenAuthorSession(string id, string svnUrl)
    {
        lock (_sync)
        {
            _authorSessions[id] = svnUrl;
            _legacyAuthors[id] = [];
        }
    }

    public void CloseAuthorSession(string id)
    {
        lock (_sync)
        {
            _authorSessions.Remove(id);
            _legacyAuthors.Remove(id);
        }
    }

    public List<AuthorRecord> GetAuthors(string id)
    {
        lock (_sync)
        {
            return GetAuthorsWithoutLock(id).Select(Clone).ToList();
        }
    }

    public void ReplaceAuthors(string id, List<AuthorRecord> authors)
    {
        lock (_sync)
        {
            if (_migrations.TryGetValue(id, out var record))
            {
                record.Authors = authors.Select(Clone).ToList();
                Persist(record);
            }
            else if (_authorSessions.ContainsKey(id))
            {
                _legacyAuthors[id] = authors.Select(author => new Dictionary<string, string>
                {
                    ["svnUsername"] = author.SvnUsername,
                    ["gitName"] = author.GitName,
                    ["gitEmail"] = author.GitEmail,
                    ["verified"] = author.Verified ? "true" : "false"
                }).ToList();
            }
        }
    }

    public List<AuthorRecord> MergeExtractedAuthors(string id, IEnumerable<string> usernames)
    {
        lock (_sync)
        {
            var existing = GetAuthorsWithoutLock(id);
            var known = existing.Select(author => author.SvnUsername)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var username in usernames.Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
            {
                if (!known.Add(username))
                {
                    continue;
                }

                var name = username.Contains('.')
                    ? string.Join(' ', username.Split('.').Select(part =>
                        part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]))
                    : username.Length == 0
                        ? username
                        : char.ToUpperInvariant(username[0]) + username[1..];
                existing.Add(new AuthorRecord
                {
                    Id = existing.Count + 1,
                    MigrationId = id,
                    SvnUsername = username,
                    GitName = name,
                    GitEmail = $"{username}@example.com"
                });
            }

            if (_migrations.TryGetValue(id, out var migration))
            {
                migration.Authors = existing;
                Persist(migration);
            }
            else
            {
                ReplaceAuthors(id, existing);
            }

            return existing.Select(Clone).ToList();
        }
    }

    public bool UpdateAuthor(string id, int authorId, AuthorRecord update)
    {
        lock (_sync)
        {
            var authors = GetAuthorsWithoutLock(id);
            var index = authors.FindIndex(author => author.Id == authorId);
            if (index < 0)
            {
                return false;
            }

            update.Id = authorId;
            update.MigrationId = id;
            authors[index] = update;
            ReplaceAuthors(id, authors);
            return true;
        }
    }

    public bool DeleteAuthor(string id, int authorId)
    {
        lock (_sync)
        {
            var authors = GetAuthorsWithoutLock(id);
            var removed = authors.RemoveAll(author => author.Id == authorId) > 0;
            if (removed)
            {
                for (var index = 0; index < authors.Count; index++)
                {
                    authors[index].Id = index + 1;
                }

                ReplaceAuthors(id, authors);
            }

            return removed;
        }
    }

    private List<AuthorRecord> GetAuthorsWithoutLock(string id)
    {
        if (_migrations.TryGetValue(id, out var record))
        {
            return record.Authors.Select(Clone).ToList();
        }

        if (_legacyAuthors.TryGetValue(id, out var legacy))
        {
            return legacy.Select((author, index) => new AuthorRecord
            {
                Id = index + 1,
                MigrationId = id,
                SvnUsername = author.GetValueOrDefault("svnUsername", string.Empty),
                GitName = author.GetValueOrDefault("gitName", string.Empty),
                GitEmail = author.GetValueOrDefault("gitEmail", string.Empty),
                Verified = author.GetValueOrDefault("verified") == "true"
            }).ToList();
        }

        return [];
    }

    private void Persist(MigrationRecord record)
    {
        var migration = record.Detail;
        var savedRecord = new
        {
            migration = new Dictionary<string, object?>
            {
                ["id"] = migration.Id,
                ["svn_url"] = migration.SvnUrl,
                ["github_org"] = migration.GithubOrg,
                ["github_repo"] = migration.GithubRepo,
                ["status"] = migration.Status,
                ["progress"] = migration.Progress,
                ["current_step"] = migration.CurrentStep,
                ["logs"] = migration.Logs,
                ["created_at"] = migration.CreatedAt,
                ["updated_at"] = migration.UpdatedAt,
                ["error"] = migration.Error,
                ["authors_file"] = record.AuthorsFile,
                ["clean_history"] = record.CleanHistory,
                ["trunk_path"] = record.TrunkPath,
                ["branches_path"] = record.BranchesPath,
                ["tags_path"] = record.TagsPath
            },
            authors = record.Authors.Select(author => new Dictionary<string, object?>
            {
                ["id"] = author.Id,
                ["migration_id"] = author.MigrationId,
                ["svn_username"] = author.SvnUsername,
                ["git_name"] = author.GitName,
                ["git_email"] = author.GitEmail,
                ["commit_count"] = author.CommitCount,
                ["verified"] = author.Verified
            }).ToList()
        };
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO migration_state (id, record_json) VALUES ($id, $json) " +
            "ON CONFLICT(id) DO UPDATE SET record_json = excluded.record_json";
        command.Parameters.AddWithValue("$id", migration.Id);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(savedRecord));
        command.ExecuteNonQuery();
    }

    private static MigrationRecord ReadPersistedRecord(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var storedMigration = root.GetProperty("migration");
        var detail = new MigrationDetail
        {
            Id = String(storedMigration, "id") ?? id,
            SvnUrl = String(storedMigration, "svn_url", "svnUrl") ?? string.Empty,
            GithubOrg = String(storedMigration, "github_org", "githubOrg") ?? string.Empty,
            GithubRepo = String(storedMigration, "github_repo", "githubRepo") ?? string.Empty,
            Status = String(storedMigration, "status") ?? "failed",
            Progress = Int(storedMigration, "progress"),
            CurrentStep = String(storedMigration, "current_step", "currentStep") ?? string.Empty,
            Logs = Strings(storedMigration, "logs"),
            CreatedAt = String(storedMigration, "created_at", "createdAt") ?? string.Empty,
            UpdatedAt = String(storedMigration, "updated_at", "updatedAt") ?? string.Empty,
            Error = String(storedMigration, "error")
        };
        var authors = root.TryGetProperty("authors", out var authorArray) &&
                      authorArray.ValueKind == JsonValueKind.Array
            ? authorArray.EnumerateArray().Select((author, index) => new AuthorRecord
            {
                Id = Int(author, "id", index + 1),
                MigrationId = String(author, "migration_id", "migrationId") ?? id,
                SvnUsername = String(author, "svn_username", "svnUsername") ?? string.Empty,
                GitName = String(author, "git_name", "gitName") ?? string.Empty,
                GitEmail = String(author, "git_email", "gitEmail") ?? string.Empty,
                CommitCount = Int(author, "commit_count", Int(author, "commitCount")),
                Verified = Bool(author, "verified")
            }).ToList()
            : [];
        return new MigrationRecord
        {
            Detail = detail,
            GithubToken = string.Empty,
            AuthorsFile = String(storedMigration, "authors_file", "authorsFile"),
            CleanHistory = Bool(storedMigration, "clean_history", "cleanHistory"),
            TrunkPath = String(storedMigration, "trunk_path", "trunkPath") ?? "trunk",
            BranchesPath = String(storedMigration, "branches_path", "branchesPath") ?? "branches",
            TagsPath = String(storedMigration, "tags_path", "tagsPath") ?? "tags",
            Authors = authors
        };
    }

    private static string? String(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int Int(JsonElement element, string name, int fallback = 0) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : fallback;

    private static bool Bool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    return value.GetBoolean();
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                {
                    return number != 0;
                }
            }
        }

        return false;
    }

    private static List<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).ToList()
            : [];

    private static bool IsActive(string status) =>
        status is "pending" or "analyzing" or "mapping_authors" or
            "migrating" or "cleaning" or "pushing";

    private static MigrationDetail Clone(MigrationDetail detail) =>
        JsonSerializer.Deserialize<MigrationDetail>(
            JsonSerializer.Serialize(detail, JsonOptions), JsonOptions)!;

    private static MigrationRecord Clone(MigrationRecord record) =>
        new()
        {
            Detail = Clone(record.Detail),
            GithubToken = record.GithubToken,
            AuthorsFile = record.AuthorsFile,
            CleanHistory = record.CleanHistory,
            TrunkPath = record.TrunkPath,
            BranchesPath = record.BranchesPath,
            TagsPath = record.TagsPath,
            Authors = record.Authors.Select(Clone).ToList()
        };

    private static AuthorRecord Clone(AuthorRecord author) => new()
    {
        Id = author.Id,
        MigrationId = author.MigrationId,
        SvnUsername = author.SvnUsername,
        GitName = author.GitName,
        GitEmail = author.GitEmail,
        CommitCount = author.CommitCount,
        Verified = author.Verified
    };

    private static string? FindProjectRoot(string initialPath)
    {
        for (var path = new DirectoryInfo(initialPath); path is not null; path = path.Parent)
        {
            if (Directory.Exists(Path.Combine(path.FullName, "wpf", "SVN2GitHub")) &&
                Directory.Exists(Path.Combine(path.FullName, "data")))
            {
                return path.FullName;
            }
        }

        return null;
    }
}
