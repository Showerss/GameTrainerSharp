namespace GameTrainer.Games.GridWorld;

/// <summary>
/// Reward for one GridWorld step: <see cref="GoalReward"/> on the goal,
/// <see cref="StepCost"/> otherwise. Numbers come from the caller (profile),
/// never hardcoded in the env. (Port of rewards.py RewardCalculator, M4.)
/// </summary>
public sealed record RewardCalculator(float StepCost, float GoalReward)
{
    public float Reward(bool reachedGoal) => reachedGoal ? GoalReward : StepCost;
}
