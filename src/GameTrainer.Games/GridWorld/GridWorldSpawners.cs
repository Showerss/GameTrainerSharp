namespace GameTrainer.Games.GridWorld;

/// <summary>
/// Pure initial-state spawner strategies for <see cref="GridWorldEnvironment"/>.
/// Eliminates external post-reset state mutation.
/// </summary>
public static class GridWorldSpawners
{
    public static readonly (int Row, int Col) DefaultStart = (0, 0);
    public static readonly (int Row, int Col) DefaultCornerGoal = (GridWorldRules.Size - 1, GridWorldRules.Size - 1);
    public static readonly (int Row, int Col) DefaultCenterGoal = (GridWorldRules.Size / 2, GridWorldRules.Size / 2);

    /// <summary>Spawns the agent at (0, 0) and the goal at (4, 4).</summary>
    public static GridState FixedStart(Random _) =>
        new(DefaultStart.Row, DefaultStart.Col, DefaultCornerGoal.Row, DefaultCornerGoal.Col, Steps: 0);

    /// <summary>Spawns the agent at a specific coordinate and goal for deterministic testing.</summary>
    public static Func<Random, GridState> Specific(int row, int col, int goalRow = 4, int goalCol = 4) =>
        _ => new GridState(row, col, goalRow, goalCol, Steps: 0);

    /// <summary>
    /// Spawns the agent on a uniformly random square that is not the goal square.
    /// Defaults the goal to the center (2, 2).
    /// </summary>
    public static Func<Random, GridState> RandomStart((int Row, int Col)? goal = null)
    {
        var target = goal ?? DefaultCenterGoal;
        return rng =>
        {
            int row, col;
            do
            {
                row = rng.Next(GridWorldRules.Size);
                col = rng.Next(GridWorldRules.Size);
            } while ((row, col) == target);

            return new GridState(row, col, target.Row, target.Col, Steps: 0);
        };
    }
}
