using GameTrainer.Core;
using GameTrainer.Games.GridWorld;
using GameTrainer.Runner;

// Milestone 0 of the C# port: prove the reset/step loop end-to-end with a
// random policy. Compare against the Python repo's measured baselines:
//   gridworld         random agent ≈ -0.09 mean reward
//   gridworld-vision  random agent ≈ 53% goal rate at the 25-step cap
var (env, label, episodes) = args switch
{
    ["gridworld-vision"] => (
        (IEnvironment<float[], Move>)new RandomStartEnvironment(
            new GridWorldEnvironment(maxSteps: RandomStartEnvironment.VisionTaskStepCap)),
        "GridWorld (random start, 25-step cap)",
        1000),
    _ => (
        (IEnvironment<float[], Move>)new GridWorldEnvironment(),
        "GridWorld (fixed start)",
        1000),
};

var policy = new RandomPolicy();
float totalReward = 0f;
var goals = 0;

for (var ep = 0; ep < episodes; ep++)
{
    var obs = env.Reset();
    while (true)
    {
        var result = env.Step(policy.Act(obs));
        totalReward += result.Reward;
        obs = result.Observation;
        if (result.Terminated) goals++;
        if (result.Done) break;
    }
}

Console.WriteLine($"{label}: {episodes} episodes, random policy");
Console.WriteLine($"  mean reward : {totalReward / episodes:+0.00;-0.00}");
Console.WriteLine($"  goal rate   : {100.0 * goals / episodes:0.#}%");
