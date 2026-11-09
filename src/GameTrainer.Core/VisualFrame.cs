namespace GameTrainer.Core;

/// <summary>
/// A rendered multi-channel visual observation frame in CHW layout (normalized 0..1 floats).
/// </summary>
public sealed record VisualFrame(
    int Channels,
    int Height,
    int Width,
    float[] Pixels);
