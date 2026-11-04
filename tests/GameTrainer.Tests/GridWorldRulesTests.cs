using GameTrainer.Games.GridWorld;

namespace GameTrainer.Tests;

/// <summary>Tests for the pure core: state in, result out, no setup needed.</summary>
public class GridWorldRulesTests
{
    private static readonly RewardCalculator Rewards = new(StepCost: -0.01f, GoalReward: 1.0f);

    private static GridState At(int row, int col, int goalRow = 4, int goalCol = 4, int steps = 0) =>
        new(row, col, goalRow, goalCol, steps);

    [Fact]
    public void Walking_into_a_wall_stays_put()
    {
        var (next, reward, terminated) = GridWorldRules.Step(At(0, 0), Move.Up, Rewards);

        Assert.Equal((0, 0), (next.Row, next.Col));
        Assert.Equal(-0.01f, reward);
        Assert.False(terminated);
    }

    [Fact]
    public void Stepping_onto_the_goal_pays_and_terminates()
    {
        var (next, reward, terminated) = GridWorldRules.Step(At(4, 3), Move.Right, Rewards);

        Assert.True(next.AtGoal);
        Assert.Equal(1.0f, reward);
        Assert.True(terminated);
    }

    [Fact]
    public void Ordinary_step_costs_a_little()
    {
        var (_, reward, terminated) = GridWorldRules.Step(At(2, 2), Move.Down, Rewards);

        Assert.Equal(-0.01f, reward);
        Assert.False(terminated);
    }

    [Fact]
    public void Step_count_increments()
    {
        var (next, _, _) = GridWorldRules.Step(At(2, 2, steps: 7), Move.Left, Rewards);
        Assert.Equal(8, next.Steps);
    }

    [Fact]
    public void Invalid_move_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GridWorldRules.Step(At(0, 0), (Move)99, Rewards));
    }
}
