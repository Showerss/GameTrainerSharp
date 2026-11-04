using GameTrainer.Core;

namespace GameTrainer.Games.GridWorld;

/// <summary>
/// A 5x5 walk-to-the-goal world behind the <see cref="IEnvironment{TObs,TAct}"/>
/// contract — the imperative shell around <see cref="GridWorldRules"/>.
/// All rules live in the pure core; this class only holds "where we are now".
/// (Port of gridworld.py GridWorldEnv, M2.)
/// </summary>
public sealed class GridWorldEnvironment : IEnvironment<float[], Move>
{
    public const int MaxSteps = 100;

    private static readonly (int Row, int Col) Start = (0, 0);
    private static readonly (int Row, int Col) DefaultGoal = (4, 4);

    private readonly RewardCalculator _rewards;
    private readonly int _maxSteps;
    private GridState _state = new(Start.Row, Start.Col, DefaultGoal.Row, DefaultGoal.Col, 0);
    private bool _terminated;

    public GridWorldEnvironment(RewardCalculator? rewards = null, int maxSteps = MaxSteps)
    {
        _rewards = rewards ?? new RewardCalculator(StepCost: -0.01f, GoalReward: 1.0f);
        _maxSteps = maxSteps;
    }

    /// <summary>Current state, exposed for wrappers (RandomStart) and tests.</summary>
    public GridState State => _state;

    public float[] Reset()
    {
        _state = new GridState(Start.Row, Start.Col, DefaultGoal.Row, DefaultGoal.Col, Steps: 0);
        _terminated = false;
        return _state.ToObservation();
    }

    /// <summary>Put the agent/goal somewhere specific (used by RandomStartEnvironment).</summary>
    public void Place(int row, int col, int goalRow, int goalCol)
    {
        _state = new GridState(row, col, goalRow, goalCol, Steps: 0);
        _terminated = false;
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
