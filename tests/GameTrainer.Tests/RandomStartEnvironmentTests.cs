using GameTrainer.Games.GridWorld;

namespace GameTrainer.Tests;

public class RandomStartEnvironmentTests
{
    [Fact]
    public void Agent_never_starts_on_the_goal()
    {
        var inner = new GridWorldEnvironment();
        var env = new RandomStartEnvironment(inner, rng: new Random(42));

        for (var i = 0; i < 200; i++)
        {
            env.Reset();
            Assert.False(inner.State.AtGoal);
        }
    }

    [Fact]
    public void Goal_defaults_to_the_centre_not_the_corner()
    {
        var inner = new GridWorldEnvironment();
        var env = new RandomStartEnvironment(inner, rng: new Random(42));

        env.Reset();

        Assert.Equal((2, 2), (inner.State.GoalRow, inner.State.GoalCol));
    }
}
