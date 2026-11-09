using GameTrainer.Core;

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
    public const int VisualScale = 4;
    public const int VisualSize = GridWorldRules.Size * VisualScale; // 20x20
    public const int VisualChannels = 3;

    public bool AtGoal => (Row, Col) == (GoalRow, GoalCol);

    /// <summary>Legacy 2-float coordinates observation.</summary>
    public float[] ToObservation() => [Row, Col];

    /// <summary>
    /// Normalized 4-float vector observation: [Row, Col, GoalRow, GoalCol] in 0..1.
    /// Includes both agent and goal coordinates so both fixed and random-start tasks are observable.
    /// </summary>
    public float[] ToVectorObservation()
    {
        const float maxIndex = GridWorldRules.Size - 1;
        return
        [
            Row / maxIndex,
            Col / maxIndex,
            GoalRow / maxIndex,
            GoalCol / maxIndex
        ];
    }

    /// <summary>
    /// Renders the 5x5 grid into a 3x20x20 CHW float tensor (values in 0..1):
    /// Channel 0 (Red/Agent): 1.0 in the agent's 4x4 cell block
    /// Channel 1 (Green/Goal): 1.0 in the goal's 4x4 cell block
    /// Channel 2 (Blue/Grid): subtle cell border pattern (0.15 on cell edges)
    /// </summary>
    public VisualFrame ToVisualObservation()
    {
        var pixels = new float[VisualChannels * VisualSize * VisualSize];
        var planeSize = VisualSize * VisualSize;

        // Channel 2: subtle grid lines for spatial structure
        for (var y = 0; y < VisualSize; y++)
        {
            for (var x = 0; x < VisualSize; x++)
            {
                if (y % VisualScale == 0 || x % VisualScale == 0)
                {
                    pixels[2 * planeSize + y * VisualSize + x] = 0.15f;
                }
            }
        }

        FillCell(pixels, channel: 1, GoalRow, GoalCol, planeSize, value: 1.0f);
        FillCell(pixels, channel: 0, Row, Col, planeSize, value: 1.0f);

        return new VisualFrame(VisualChannels, VisualSize, VisualSize, pixels);
    }

    private static void FillCell(float[] pixels, int channel, int cellRow, int cellCol, int planeSize, float value)
    {
        var startY = Math.Clamp(cellRow, 0, GridWorldRules.Size - 1) * VisualScale;
        var startX = Math.Clamp(cellCol, 0, GridWorldRules.Size - 1) * VisualScale;
        var channelOffset = channel * planeSize;

        for (var dy = 0; dy < VisualScale; dy++)
        {
            var rowOffset = channelOffset + (startY + dy) * VisualSize + startX;
            for (var dx = 0; dx < VisualScale; dx++)
            {
                pixels[rowOffset + dx] = value;
            }
        }
    }
}
