namespace GameTrainer.Games.GridWorld;

/// <summary>The four moves. Walking into a wall just stays put.</summary>
public enum Move { Up = 0, Down = 1, Left = 2, Right = 3 }

/// <summary>
/// The complete state of a GridWorld episode as an immutable value.
/// Pure rules take one of these and hand back a new one — nothing mutates.
/// </summary>
public sealed record GridState(
    int Row,
    int Col,
    int GoalRow,
    int GoalCol,
    int Steps)
{
    public bool AtGoal => (Row, Col) == (GoalRow, GoalCol);

    /// <summary>The observation the contract promises: position as two floats.</summary>
    public float[] ToObservation() => [Row, Col];
}
