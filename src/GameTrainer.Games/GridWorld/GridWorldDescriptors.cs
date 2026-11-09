using GameTrainer.Core;

namespace GameTrainer.Games.GridWorld;

/// <summary>
/// Descriptor for standard 5x5 fixed-start GridWorld.
/// </summary>
public sealed class GridWorldFixedDescriptor : IGameDescriptor
{
    public string Id => "gridworld";
    public string DisplayName => "GridWorld (5x5 Fixed Start)";
    public string Description => "5x5 grid navigation from top-left (0,0) to bottom-right (4,4) with 25-step cap.";
    public EnvironmentSpec Spec => GridWorldEnvironment.DefaultSpec;

    public IGameEnvironment CreateEnvironment(Random? rng = null) =>
        new GridWorldEnvironment(
            maxSteps: RandomStartEnvironment.VisionTaskStepCap,
            spawner: GridWorldSpawners.FixedStart,
            rng: rng);
}

/// <summary>
/// Descriptor for 5x5 random-start GridWorld where agent must observe start and goal positions.
/// </summary>
public sealed class GridWorldRandomDescriptor : IGameDescriptor
{
    public string Id => "gridworld-random";
    public string DisplayName => "GridWorld (5x5 Random Start)";
    public string Description => "5x5 grid with randomized start positions and center goal (2,2), 25-step cap.";
    public EnvironmentSpec Spec => GridWorldEnvironment.DefaultSpec;

    public IGameEnvironment CreateEnvironment(Random? rng = null) =>
        new RandomStartEnvironment(rng: rng);
}

/// <summary>
/// Discovery registry for all built-in games in <c>GameTrainer.Games</c>.
/// </summary>
public static class BuiltInGames
{
    public static IReadOnlyList<IGameDescriptor> All { get; } =
    [
        new GridWorldFixedDescriptor(),
        new GridWorldRandomDescriptor()
    ];

    public static IGameDescriptor? FindById(string id) =>
        All.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
}
