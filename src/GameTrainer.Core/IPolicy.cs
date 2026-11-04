namespace GameTrainer.Core;

/// <summary>
/// The brain's public face: given an observation, pick an action.
/// RandomPolicy and (soon) PpoPolicy both live behind this.
/// </summary>
public interface IPolicy<in TObs, out TAct>
{
    TAct Act(TObs observation);
}
