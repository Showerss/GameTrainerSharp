namespace GameTrainer.Core;

/// <summary>
/// Static shape and action metadata for an <see cref="IGameEnvironment"/> so any policy
/// or trainer can build its encoders and decision head without knowing the game's internal types.
/// </summary>
public sealed record EnvironmentSpec(
    int VectorObservationSize,
    int VisualChannels,
    int VisualHeight,
    int VisualWidth,
    int ActionCount,
    IReadOnlyList<string> ActionNames);
