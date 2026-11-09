using GameTrainer.Core.Catalog;
using GameTrainer.Games.GridWorld;
using GameTrainer.Gui;

namespace GameTrainer.Tests;

public class DashboardViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public DashboardViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GameTrainerDashboardTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void AddSelectedGame_registers_discovered_descriptor_and_updates_sidebar()
    {
        var store = new CatalogStore(_tempDir);
        var vm = new DashboardViewModel(store, BuiltInGames.All);

        Assert.Empty(vm.Games);
        Assert.Equal(BuiltInGames.All.Count, vm.UnregisteredDescriptors.Count);
        Assert.True(vm.CanAddGame);

        var added = vm.AddSelectedGame();
        Assert.NotNull(added);
        Assert.Single(vm.Games);
        Assert.Equal(added!.GameId, vm.SelectedGame!.GameId);
        Assert.Equal(BuiltInGames.All.Count - 1, vm.UnregisteredDescriptors.Count);
    }

    [Fact]
    public void Selecting_coached_model_version_loads_lineage_parent_metrics_and_logs()
    {
        var store = new CatalogStore(_tempDir);
        store.RegisterGame(new GridWorldFixedDescriptor());

        var t1 = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var (w1, m1, l1) = CatalogStore.GetRelativeRunPaths("gridworld", "v1.0");
        store.AppendMetricPoint("gridworld", "v1.0", new EpisodeMetricPoint(20, 0.70f, 0.90f, 0.0f, t1));
        store.AppendLogLine("gridworld", "v1.0", "Parent v1.0 completed.");
        store.RecordModelVersion("gridworld", new ModelVersionEntry(
            "v1.0",
            TrainingModality.Vector,
            GenerationalMode.Scratch,
            ParentVersion: null,
            UpdatedAt: t1,
            FinalMeanReward: 0.70f,
            FinalGoalRate: 0.90f,
            TotalEpisodes: 20,
            WeightsRelativePath: w1,
            MetricsRelativePath: m1,
            LogRelativePath: l1));

        var t2 = t1.AddHours(1);
        var (w2, m2, l2) = CatalogStore.GetRelativeRunPaths("gridworld", "v2.0");
        store.AppendMetricPoint("gridworld", "v2.0", new EpisodeMetricPoint(20, 0.91f, 1.00f, 0.5f, t2));
        store.AppendLogLine("gridworld", "v2.0", "Child v2.0 coached by v1.0 completed.");
        store.RecordModelVersion("gridworld", new ModelVersionEntry(
            "v2.0",
            TrainingModality.Visual,
            GenerationalMode.CoachAndCopyHead,
            ParentVersion: "v1.0",
            UpdatedAt: t2,
            FinalMeanReward: 0.91f,
            FinalGoalRate: 1.00f,
            TotalEpisodes: 20,
            WeightsRelativePath: w2,
            MetricsRelativePath: m2,
            LogRelativePath: l2));

        var vm = new DashboardViewModel(store, BuiltInGames.All);

        Assert.Single(vm.Games);
        Assert.Equal(2, vm.ModelVersions.Count);

        // Newest version (v2.0) is selected by default
        Assert.Equal("v2.0", vm.SelectedModelVersion!.Version);
        Assert.Equal("v1.0", vm.SelectedModelVersion.TaughtByDisplay);
        Assert.Single(vm.SelectedVersionMetrics);
        Assert.Single(vm.ParentVersionMetrics);
        Assert.Contains("Child v2.0 coached by v1.0 completed.", vm.SelectedVersionTrainingLog);
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
