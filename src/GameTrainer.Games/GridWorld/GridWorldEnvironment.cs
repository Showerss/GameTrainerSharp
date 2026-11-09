using GameTrainer.Core;

namespace GameTrainer.Games.GridWorld;

/// <summary>
/// A 5x5 walk-to-the-goal world behind both <see cref="IGameEnvironment"/> (uniform dual-modal seam)
/// and <see cref="IEnvironment{TObs,TAct}"/>.
/// All rules live in the pure core; this class only holds "where we are now".
/// </summary>
public sealed class GridWorldEnvironment : IGameEnvironment, IEnvironment<float[], Move>
{
    public const int MaxSteps = 100;

    public static readonly EnvironmentSpec DefaultSpec = new(
        VectorObservationSize: 4,
        VisualChannels: GridState.VisualChannels,
        VisualHeight: GridState.VisualSize,
        VisualWidth: GridState.VisualSize,
        ActionCount: 4,
        ActionNames: ["Up", "Down", "Left", "Right"]);

    private readonly RewardCalculator _rewards;
    private readonly int _maxSteps;
    private readonly Func<Random, GridState> _spawner;
    private readonly Random _rng;
    private GridState _state;
    private bool _terminated;

    public GridWorldEnvironment(
        RewardCalculator? rewards = null,
        int maxSteps = MaxSteps,
        Func<Random, GridState>? spawner = null,
        Random? rng = null)
    {
        _rewards = rewards ?? new RewardCalculator(StepCost: -0.01f, GoalReward: 1.0f);
        _maxSteps = maxSteps;
        _spawner = spawner ?? GridWorldSpawners.FixedStart;
        _rng = rng ?? new Random();
        _state = _spawner(_rng);
    }

    public EnvironmentSpec Spec => DefaultSpec;

    /// <summary>Current immutable state value.</summary>
    public GridState State => _state;

    void IGameEnvironment.Reset() => Reset();

    public float[] Reset()
    {
        _state = _spawner(_rng);
        _terminated = false;
        return _state.ToObservation();
    }

    public float[] GetVectorObservation() => _state.ToVectorObservation();

    public VisualFrame GetVisualObservation() => _state.ToVisualObservation();

    public StepOutcome Step(int actionIndex)
    {
        if (actionIndex < 0 || actionIndex >= DefaultSpec.ActionCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actionIndex),
                actionIndex,
                $"Action index must be between 0 and {DefaultSpec.ActionCount - 1}.");
        }

        var result = Step((Move)actionIndex);
        return new StepOutcome(result.Reward, result.Terminated, result.Truncated, result.Steps);
    }

    public StepResult<float[]> Step(Move action)
    {
        if (_terminated)
            throw new InvalidOperationException("Step() called after termination; call Reset().");

        var (next, reward, terminated) = GridWorldRules.Step(_state, action, _rewards);
        _state = next;
        _terminated = terminated;

        var truncated = _state.Steps >= _maxSteps && !terminated;
        return new StepResult<float[]>(
            _state.ToObservation(), reward, terminated, truncated, _state.Steps);
    }
}
