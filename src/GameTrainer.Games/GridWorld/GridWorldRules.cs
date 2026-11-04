namespace GameTrainer.Games.GridWorld;

/// <summary>
/// GridWorld game rules as pure functions — the functional core.
/// State in, (new state, reward, terminated) out. No I/O, no mutation,
/// so every rule is testable with a single assertion.
/// </summary>
public static class GridWorldRules
{
    public const int Size = 5;

    // Up is row - 1 because row 0 is the top.
    private static (int DRow, int DCol) Delta(Move move) => move switch
    {
        Move.Up => (-1, 0),
        Move.Down => (1, 0),
        Move.Left => (0, -1),
        Move.Right => (0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(move), move, "invalid action"),
    };

    /// <summary>One move. Walls clamp; reaching the goal terminates the episode.</summary>
    public static (GridState Next, float Reward, bool Terminated) Step(
        GridState state, Move move, RewardCalculator rewards)
    {
        var (dRow, dCol) = Delta(move);

        var next = state with
        {
            Row = Math.Clamp(state.Row + dRow, 0, Size - 1),
            Col = Math.Clamp(state.Col + dCol, 0, Size - 1),
            Steps = state.Steps + 1,
        };

        return (next, rewards.Reward(next.AtGoal), next.AtGoal);
    }
}
