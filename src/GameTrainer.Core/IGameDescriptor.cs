namespace GameTrainer.Core;

/// <summary>
/// Describes a discoverable game that can be registered in the catalog and instantiated
/// for vector or visual training runs.
/// </summary>
public interface IGameDescriptor
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }
    EnvironmentSpec Spec { get; }
    IGameEnvironment CreateEnvironment(Random? rng = null);
}
