namespace GameTrainer.Core;

/// <summary>
/// Uniform dual-modal environment seam. Keeps pure strongly-typed rules inside each game
/// while exposing discrete action indices (<c>0..Spec.ActionCount - 1</c>) and simultaneous
/// vector + visual observations for generational teacher-student learning.
/// </summary>
public interface IGameEnvironment
{
    EnvironmentSpec Spec { get; }
    void Reset();
    StepOutcome Step(int actionIndex);
    float[] GetVectorObservation();
    VisualFrame GetVisualObservation();
}
