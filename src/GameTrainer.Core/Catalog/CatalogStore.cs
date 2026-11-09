using System.Text;
using System.Text.Json;

namespace GameTrainer.Core.Catalog;

/// <summary>
/// Local file-based implementation of <see cref="ICatalogStore"/>.
/// Uses atomic temporary-file replacement for <c>catalog.json</c> and <see cref="FileShare.ReadWrite"/>
/// for <c>metrics.jsonl</c> and <c>training.log</c> so the CLI runner and Avalonia GUI can run concurrently.
/// </summary>
public sealed class CatalogStore : ICatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _sync = new();

    public CatalogStore(string? rootDirectory = null)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? ResolveDefaultRootDirectory()
            : Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    public string CatalogFilePath => Path.Combine(RootDirectory, "catalog.json");

    public static string ResolveDefaultRootDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GameTrainerSharp.slnx")))
            {
                return Path.Combine(dir.FullName, "artifacts");
            }
            dir = dir.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
    }

    public IReadOnlyList<GameEntry> GetGames()
    {
        lock (_sync)
        {
            return LoadDocumentUnsafe().Games;
        }
    }

    public GameEntry? GetGame(string gameId)
    {
        return GetGames().FirstOrDefault(g =>
            string.Equals(g.GameId, gameId, StringComparison.OrdinalIgnoreCase));
    }

    public GameEntry RegisterGame(IGameDescriptor descriptor, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var now = timestamp ?? DateTimeOffset.UtcNow;

        lock (_sync)
        {
            var doc = LoadDocumentUnsafe();
            var existing = doc.Games.FirstOrDefault(g =>
                string.Equals(g.GameId, descriptor.Id, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                return existing;
            }

            var added = new GameEntry(
                GameId: descriptor.Id,
                DisplayName: descriptor.DisplayName,
                Description: descriptor.Description,
                AddedAt: now,
                LastUpdatedAt: now,
                Versions: []);

            var updatedGames = doc.Games.Append(added).ToList();
            SaveDocumentUnsafe(new CatalogDocument(updatedGames));
            return added;
        }
    }

    public ModelVersionEntry RecordModelVersion(string gameId, ModelVersionEntry version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentNullException.ThrowIfNull(version);

        lock (_sync)
        {
            var doc = LoadDocumentUnsafe();
            var index = doc.Games.ToList().FindIndex(g =>
                string.Equals(g.GameId, gameId, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                throw new InvalidOperationException($"Game '{gameId}' is not registered in the catalog.");
            }

            var game = doc.Games[index];
            var versions = game.Versions
                .Where(v => !string.Equals(v.Version, version.Version, StringComparison.OrdinalIgnoreCase))
                .Append(version)
                .OrderByDescending(v => v.UpdatedAt)
                .ToList();

            var updatedGame = game with
            {
                LastUpdatedAt = versions[0].UpdatedAt,
                Versions = versions
            };

            var gamesList = doc.Games.ToList();
            gamesList[index] = updatedGame;
            SaveDocumentUnsafe(new CatalogDocument(gamesList));
            return version;
        }
    }

    public string GetRunDirectory(string gameId, string version)
    {
        var dir = Path.Combine(RootDirectory, "runs", SanitizeSegment(gameId), SanitizeSegment(version));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetWeightsPath(string gameId, string version) =>
        Path.Combine(GetRunDirectory(gameId, version), "model.pt");

    public string GetMetricsPath(string gameId, string version) =>
        Path.Combine(GetRunDirectory(gameId, version), "metrics.jsonl");

    public string GetLogPath(string gameId, string version) =>
        Path.Combine(GetRunDirectory(gameId, version), "training.log");

    public static (string WeightsRel, string MetricsRel, string LogRel) GetRelativeRunPaths(string gameId, string version)
    {
        var prefix = $"runs/{SanitizeSegment(gameId)}/{SanitizeSegment(version)}";
        return ($"{prefix}/model.pt", $"{prefix}/metrics.jsonl", $"{prefix}/training.log");
    }

    public void AppendMetricPoint(string gameId, string version, EpisodeMetricPoint point)
    {
        var path = GetMetricsPath(gameId, version);
        var line = JsonSerializer.Serialize(point, JsonLineOptions) + Environment.NewLine;

        lock (_sync)
        {
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(line);
        }
    }

    public IReadOnlyList<EpisodeMetricPoint> ReadMetrics(string gameId, string version)
    {
        var path = GetMetricsPath(gameId, version);
        if (!File.Exists(path))
        {
            return [];
        }

        var points = new List<EpisodeMetricPoint>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var point = JsonSerializer.Deserialize<EpisodeMetricPoint>(line, JsonLineOptions);
            if (point is not null)
            {
                points.Add(point);
            }
        }

        return points.OrderBy(p => p.Episode).ToList();
    }

    public void AppendLogLine(string gameId, string version, string message)
    {
        var path = GetLogPath(gameId, version);
        var line = $"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

        lock (_sync)
        {
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(line);
        }
    }

    public string ReadTrainingLog(string gameId, string version)
    {
        var path = GetLogPath(gameId, version);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private CatalogDocument LoadDocumentUnsafe()
    {
        if (!File.Exists(CatalogFilePath))
        {
            return new CatalogDocument([]);
        }

        using var stream = new FileStream(CatalogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return JsonSerializer.Deserialize<CatalogDocument>(stream, JsonOptions)
               ?? new CatalogDocument([]);
    }

    private void SaveDocumentUnsafe(CatalogDocument document)
    {
        Directory.CreateDirectory(RootDirectory);
        var tempPath = $"{CatalogFilePath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(tempPath, json, Encoding.UTF8);
        File.Move(tempPath, CatalogFilePath, overwrite: true);
    }

    private static string SanitizeSegment(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(segment.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
