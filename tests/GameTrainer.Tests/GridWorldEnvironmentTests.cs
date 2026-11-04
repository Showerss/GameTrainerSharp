using GameTrainer.Games.GridWorld;

namespace GameTrainer.Tests;

/// <summary>Tests for the stateful shell: lifecycle, truncation, wiring.</summary>
public class GridWorldEnvironmentTests
{
    [Fact]
    public void Reset_returns_the_start_position()
    {
        var env = new GridWorldEnvironment();
        Assert.Equal([0f, 0f], env.Reset());
    }

    [Fact]
    public void Straight_line_to_the_goal_wins_with_expected_reward()
    {
        var env = new GridWorldEnvironment();
        env.Reset();

        StepResult total = Play(env, [Move.Down, Move.Down, Move.Down, Move.Down,
                                      Move.Right, Move.Right, Move.Right, Move.Right]);
        Assert.True(total.Terminated);
        Assert.False(total.Truncated);
        // 7 step costs + 1 goal reward.
        Assert.Equal(7 * -0.01f + 1.0f, total.Reward, precision: 5);
    }

    [Fact]
    public void Running_out_of_moves_truncates()
    {
        var env = new GridWorldEnvironment(maxSteps: 10);
        env.Reset();

        Core.StepResult<float[]>? last = null;
        for (var i = 0; i < 10; i++)
            last = env.Step(Move.Up); // pinned to the top wall, never reaches the goal

        Assert.NotNull(last);
        Assert.False(last!.Terminated);
        Assert.True(last.Truncated);
        Assert.Equal(10, last.Steps);
    }

    [Fact]
    public void Stepping_after_termination_throws()
    {
        var env = new GridWorldEnvironment();
        env.Reset();
        env.Place(4, 3, 4, 4);
        env.Step(Move.Right);

        Assert.Throws<InvalidOperationException>(() => env.Step(Move.Left));
    }

    [Fact]
    public void Reward_numbers_come_from_the_caller()
    {
        var env = new GridWorldEnvironment(new RewardCalculator(StepCost: -0.5f, GoalReward: 2.0f));
        env.Reset();

        var step = env.Step(Move.Down);
        Assert.Equal(-0.5f, step.Reward);
    }

    private sealed record StepResult(float Reward, bool Terminated, bool Truncated, int Steps);

    private static StepResult Play(GridWorldEnvironment env, Move[] moves)
    {
        float total = 0f;
        Core.StepResult<float[]>? last = null;
        foreach (var move in moves)
        {
            last = env.Step(move);
            total += last.Reward;
            if (last.Done) break;
        }
        return new StepResult(total, last!.Terminated, last.Truncated, last.Steps);
    }
}
