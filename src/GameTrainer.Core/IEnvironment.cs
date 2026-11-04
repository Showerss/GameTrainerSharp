namespace GameTrainer.Core;

/// <summary>
/// The one promise every game keeps: <see cref="Reset"/> hands back a starting
/// observation, <see cref="Step"/> moves the world one tick. Anything that keeps
/// this promise can be played by any learner. (Gymnasium's contract, in C#.)
/// </summary>
public interface IEnvironment<TObs, TAct>
{
    TObs Reset();
    StepResult<TObs> Step(TAct action);
}
