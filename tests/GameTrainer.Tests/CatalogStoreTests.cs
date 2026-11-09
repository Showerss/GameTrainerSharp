using GameTrainer.Core.Catalog;
using GameTrainer.Games.GridWorld;

namespace GameTrainer.Tests;

public class CatalogStoreTests : IDisposable
{
    private readonly string _tempDir;

    public CatalogStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GameTrainerCatalogTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void RegisterGame_persists_game_across_store_instances()
    {
        var descriptor = new GridWorldFixedDescriptor();
        var store1 = new CatalogStore(_tempDir);
        var addedAt = new DateTimeOffset(2026, 11, 7, 16, 0, 0, TimeSpan.Zero);

        var entry = store1.RegisterGame(descriptor, addedAt);
        Assert.Equal("gridworld", entry.GameId);
        Assert.Equal(addedAt, entry.AddedAt);

        // Second instance reading the same directory
        var store2 = new CatalogStore(_tempDir);
        var loaded = store2.GetGame("gridworld");
        Assert.NotNull(loaded);
        Assert.Equal(descriptor.DisplayName, loaded!.DisplayName);
        Assert.Empty(loaded.Versions);
    }

    [Fact]
    public void RecordModelVersion_tracks_lineage_and_updates_latest_version()
    {
        var store = new CatalogStore(_tempDir);
        store.RegisterGame(new GridWorldFixedDescriptor());

        var t1 = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var (w1, m1, l1) = CatalogStore.GetRelativeRunPaths("gridworld", "v1.0");
        var v1 = new ModelVersionEntry(
            Version: "v1.0",
            Modality: TrainingModality.Vector,
            Mode: GenerationalMode.Scratch,
            ParentVersion: null,
            UpdatedAt: t1,
            FinalMeanReward: 0.82f,
            FinalGoalRate: 0.94f,
            TotalEpisodes: 500,
            WeightsRelativePath: w1,
            MetricsRelativePath: m1,
            LogRelativePath: l1);

        var t2 = t1.AddHours(2);
        var (w2, m2, l2) = CatalogStore.GetRelativeRunPaths("gridworld", "v2.0");
        var v2 = new ModelVersionEntry(
            Version: "v2.0",
            Modality: TrainingModality.Visual,
            Mode: GenerationalMode.CoachAndCopyHead,
            ParentVersion: "v1.0",
            UpdatedAt: t2,
            FinalMeanReward: 0.91f,
            FinalGoalRate: 0.98f,
            TotalEpisodes: 500,
            WeightsRelativePath: w2,
            MetricsRelativePath: m2,
            LogRelativePath: l2);

        store.RecordModelVersion("gridworld", v1);
        store.RecordModelVersion("gridworld", v2);

        var game = store.GetGame("gridworld")!;
        Assert.Equal(2, game.Versions.Count);
        Assert.Equal("v2.0", game.LatestVersion!.Version);
        Assert.Equal("v1.0", game.LatestVersion.ParentVersion);
        Assert.Equal(TrainingModality.Visual, game.LatestVersion.Modality);
        Assert.Equal(GenerationalMode.CoachAndCopyHead, game.LatestVersion.Mode);
        Assert.Equal(t2, game.LastUpdatedAt);
    }

    [Fact]
    public void Metrics_and_training_logs_stream_and_roundtrip_correctly()
    {
        var store = new CatalogStore(_tempDir);
        store.RegisterGame(new GridWorldFixedDescriptor());

        var now = DateTimeOffset.UtcNow;
        store.AppendMetricPoint("gridworld", "v1.0", new EpisodeMetricPoint(50, 0.25f, 0.60f, 1.0f, now));
        store.AppendMetricPoint("gridworld", "v1.0", new EpisodeMetricPoint(100, 0.85f, 0.96f, 0.5f, now.AddSeconds(5)));

        store.AppendLogLine("gridworld", "v1.0", "Started training run v1.0 (Vector, Scratch)");
        store.AppendLogLine("gridworld", "v1.0", "Episode 100: GoalRate=96.0%");

        var metrics = store.ReadMetrics("gridworld", "v1.0");
        Assert.Equal(2, metrics.Count);
        Assert.Equal(50, metrics[0].Episode);
        Assert.Equal(100, metrics[1].Episode);
        Assert.Equal(0.96f, metrics[1].GoalRate, precision: 4);

        var log = store.ReadTrainingLog("gridworld", "v1.0");
        Assert.Contains("Started training run v1.0", log);
        Assert.Contains("Episode 100: GoalRate=96.0%", log);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp directory
        }
    }
}
