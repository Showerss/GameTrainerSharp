using GameTrainer.Core;

namespace GameTrainer.Games.GridWorld;

/// <summary>
/// GridWorld configured with a random start square each episode and the goal off the corner
/// (default: centre). Uses encapsulated spawner injection on <see cref="GridWorldEnvironment"/>
/// rather than post-reset state mutation.
/// </summary>
public sealed class RandomStartEnvironment : IGameEnvironment, IEnvironment<float[], Move>
{
    public const int VisionTaskStepCap = 25;

    private readonly GridWorldEnvironment _inner;

    public RandomStartEnvironment(
        RewardCalculator? rewards = null,
        int maxSteps = VisionTaskStepCap,
        (int Row, int Col)? goal = null,
        Random? rng = null)
    {
        _inner = new GridWorldEnvironment(
            rewards: rewards,
            maxSteps: maxSteps,
            spawner: GridWorldSpawners.RandomStart(goal),
            rng: rng);
    }

    public EnvironmentSpec Spec => _inner.Spec;

    public GridState State => _inner.State;

    void IGameEnvironment.Reset() => _inner.Reset();

    public float[] Reset() => _inner.Reset();

    public float[] GetVectorObservation() => _inner.GetVectorObservation();

    public VisualFrame GetVisualObservation() => _inner.GetVisualObservation();

    public StepOutcome Step(int actionIndex) => _inner.Step(actionIndex);

    public StepResult<float[]> Step(Move action) => _inner.Step(action);
}
