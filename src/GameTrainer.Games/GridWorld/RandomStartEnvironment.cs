using GameTrainer.Core;

namespace GameTrainer.Games.GridWorld;

/// <summary>
/// Decorator that makes GridWorld impossible to solve blind: random start square
/// each episode, goal moved off the corner (default: centre). A fixed route or a
/// "always down/right" coin flip no longer works, so the eyes have to matter.
/// (Port of gridworld.py RandomStart, M3.)
/// </summary>
public sealed class RandomStartEnvironment : IEnvironment<float[], Move>
{
    public const int VisionTaskStepCap = 25;

    private readonly GridWorldEnvironment _inner;
    private readonly Random _rng;
    private readonly (int Row, int Col) _goal;

    public RandomStartEnvironment(
        GridWorldEnvironment inner,
        (int Row, int Col)? goal = null,
        Random? rng = null)
    {
        _inner = inner;
        _rng = rng ?? new Random();
        _goal = goal ?? (GridWorldRules.Size / 2, GridWorldRules.Size / 2);
    }

    public float[] Reset()
    {
        _inner.Reset();

        // Any square except the goal — starting on it wins the episode before it
        // begins, which teaches nothing and inflates mean reward.
        int row, col;
        do
        {
            row = _rng.Next(GridWorldRules.Size);
            col = _rng.Next(GridWorldRules.Size);
        } while ((row, col) == _goal);

        _inner.Place(row, col, _goal.Row, _goal.Col);
        return _inner.State.ToObservation();
    }

    public StepResult<float[]> Step(Move action) => _inner.Step(action);
}
