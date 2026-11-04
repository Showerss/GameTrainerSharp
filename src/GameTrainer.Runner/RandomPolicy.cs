using GameTrainer.Core;
using GameTrainer.Games.GridWorld;

namespace GameTrainer.Runner;

/// <summary>
/// Picks uniformly random moves. Not a learner — the baseline every learner
/// must beat, and the proof that the reset/step loop works end-to-end.
/// </summary>
public sealed class RandomPolicy(Random? rng = null) : IPolicy<float[], Move>
{
    private readonly Random _rng = rng ?? new Random();

    public Move Act(float[] observation) => (Move)_rng.Next(4);
}
