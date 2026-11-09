namespace GameTrainer.Core;

/// <summary>
/// Outcome of one discrete <see cref="IGameEnvironment.Step"/> tick.
/// Observations (vector and/or visual) are queried via <see cref="IGameEnvironment.GetVectorObservation"/>
/// and <see cref="IGameEnvironment.GetVisualObservation"/> on the same state tick.
/// </summary>
public sealed record StepOutcome(
    float Reward,
    bool Terminated,
    bool Truncated,
    int Steps)
{
    public bool Done => Terminated || Truncated;
}
