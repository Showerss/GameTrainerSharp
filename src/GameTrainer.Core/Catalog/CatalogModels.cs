using System.Text.Json.Serialization;

namespace GameTrainer.Core.Catalog;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrainingModality
{
    Vector = 0,
    Visual = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GenerationalMode
{
    Scratch = 0,
    Coach = 1,
    CoachAndCopyHead = 2
}

/// <summary>
/// A single evaluation point recorded in <c>metrics.jsonl</c> during a training run.
/// </summary>
public sealed record EpisodeMetricPoint(
    int Episode,
    float MeanReward,
    float GoalRate,
    float DistillationWeight,
    DateTimeOffset Timestamp);

/// <summary>
/// Metadata for a trained model version and its generational lineage.
/// </summary>
public sealed record ModelVersionEntry(
    string Version,
    TrainingModality Modality,
    GenerationalMode Mode,
    string? ParentVersion,
    DateTimeOffset UpdatedAt,
    float FinalMeanReward,
    float FinalGoalRate,
    int TotalEpisodes,
    string WeightsRelativePath,
    string MetricsRelativePath,
    string LogRelativePath);

/// <summary>
/// A registered game in the local catalog along with its trained model versions.
/// </summary>
public sealed record GameEntry(
    string GameId,
    string DisplayName,
    string Description,
    DateTimeOffset AddedAt,
    DateTimeOffset LastUpdatedAt,
    IReadOnlyList<ModelVersionEntry> Versions)
{
    [JsonIgnore]
    public ModelVersionEntry? LatestVersion =>
        Versions.Count == 0
            ? null
            : Versions.OrderByDescending(v => v.UpdatedAt).First();
}

/// <summary>
/// Root JSON document persisted in <c>catalog.json</c>.
/// </summary>
public sealed record CatalogDocument(IReadOnlyList<GameEntry> Games);

/// <summary>
/// Contract for reading and writing the shared local catalog and per-version run artifacts.
/// </summary>
public interface ICatalogStore
{
    string RootDirectory { get; }
    IReadOnlyList<GameEntry> GetGames();
    GameEntry? GetGame(string gameId);
    GameEntry RegisterGame(IGameDescriptor descriptor, DateTimeOffset? timestamp = null);
    ModelVersionEntry RecordModelVersion(string gameId, ModelVersionEntry version);
    string GetRunDirectory(string gameId, string version);
    string GetWeightsPath(string gameId, string version);
    void AppendMetricPoint(string gameId, string version, EpisodeMetricPoint point);
    IReadOnlyList<EpisodeMetricPoint> ReadMetrics(string gameId, string version);
    void AppendLogLine(string gameId, string version, string message);
    string ReadTrainingLog(string gameId, string version);
}
