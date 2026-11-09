using GameTrainer.Core;
using GameTrainer.Games.GridWorld;

namespace GameTrainer.Tests;

/// <summary>Tests for the stateful shell: lifecycle, truncation, dual-modal observations, and descriptors.</summary>
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
        var env = new GridWorldEnvironment(spawner: GridWorldSpawners.Specific(4, 3, 4, 4));
        env.Reset();
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

    [Fact]
    public void Discrete_action_index_steps_environment_and_rejects_out_of_range()
    {
        IGameEnvironment env = new GridWorldEnvironment();
        env.Reset();

        // Action 1 = Move.Down -> (1, 0)
        var outcome = env.Step(1);
        Assert.Equal(1, outcome.Steps);
        Assert.False(outcome.Done);
        Assert.Equal([0.25f, 0f, 1f, 1f], env.GetVectorObservation());

        Assert.Throws<ArgumentOutOfRangeException>(() => env.Step(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => env.Step(env.Spec.ActionCount));
    }

    [Fact]
    public void Simultaneous_vector_and_visual_observations_reflect_same_state_tick()
    {
        var env = new GridWorldEnvironment(spawner: GridWorldSpawners.Specific(1, 2, 3, 4));
        env.Reset();

        var vec = env.GetVectorObservation();
        var vis = env.GetVisualObservation();

        Assert.Equal([0.25f, 0.5f, 0.75f, 1.0f], vec);
        Assert.Equal(3, vis.Channels);
        Assert.Equal(20, vis.Height);
        Assert.Equal(20, vis.Width);
        Assert.Equal(3 * 20 * 20, vis.Pixels.Length);

        // Agent at (1, 2) -> channel 0 block at y=4..7, x=8..11 is 1.0f
        var planeSize = vis.Height * vis.Width;
        Assert.Equal(1.0f, vis.Pixels[0 * planeSize + 4 * vis.Width + 8]);
        Assert.Equal(0.0f, vis.Pixels[0 * planeSize + 0 * vis.Width + 0]);

        // Goal at (3, 4) -> channel 1 block at y=12..15, x=16..19 is 1.0f
        Assert.Equal(1.0f, vis.Pixels[1 * planeSize + 12 * vis.Width + 16]);
    }

    [Fact]
    public void Built_in_game_descriptors_are_discoverable_and_create_valid_environments()
    {
        Assert.True(BuiltInGames.All.Count >= 2);
        var fixedGame = BuiltInGames.FindById("gridworld");
        var randomGame = BuiltInGames.FindById("gridworld-random");

        Assert.NotNull(fixedGame);
        Assert.NotNull(randomGame);

        var env = fixedGame!.CreateEnvironment(new Random(123));
        env.Reset();
        Assert.Equal(4, env.Spec.ActionCount);
        Assert.Equal(["Up", "Down", "Left", "Right"], env.Spec.ActionNames);
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
