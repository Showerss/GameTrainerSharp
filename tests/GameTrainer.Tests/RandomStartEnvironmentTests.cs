using GameTrainer.Games.GridWorld;

namespace GameTrainer.Tests;

public class RandomStartEnvironmentTests
{
    [Fact]
    public void Agent_never_starts_on_the_goal()
    {
        var env = new RandomStartEnvironment(rng: new Random(42));

        for (var i = 0; i < 200; i++)
        {
            env.Reset();
            Assert.False(env.State.AtGoal);
        }
    }

    [Fact]
    public void Goal_defaults_to_the_centre_not_the_corner()
    {
        var env = new RandomStartEnvironment(rng: new Random(42));

        env.Reset();

        Assert.Equal((2, 2), (env.State.GoalRow, env.State.GoalCol));
    }
}
