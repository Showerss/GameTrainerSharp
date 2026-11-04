namespace GameTrainer.Core;

/// <summary>
/// The outcome of one <see cref="IEnvironment{TObs,TAct}.Step"/> call —
/// the C# spelling of Gymnasium's (observation, reward, terminated, truncated, info).
/// </summary>
/// <param name="Observation">What the agent sees after the move.</param>
/// <param name="Reward">What the move earned.</param>
/// <param name="Terminated">The game ended for real (win or loss).</param>
/// <param name="Truncated">The game was cut off (e.g. out of moves).</param>
/// <param name="Steps">How many moves this episode has used.</param>
public sealed record StepResult<TObs>(
    TObs Observation,
    float Reward,
    bool Terminated,
    bool Truncated,
    int Steps)
{
    public bool Done => Terminated || Truncated;
}
