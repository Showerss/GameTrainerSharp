using GameTrainer.Core;
using GameTrainer.Core.Catalog;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace GameTrainer.Brain;

/// <summary>
/// MLP encoder mapping normalized vector observations <c>[B, VectorObservationSize]</c>
/// into a fixed <c>[B, LatentDim]</c> embedding.
/// </summary>
public sealed class VectorEncoder : Module<Tensor, Tensor>
{
    private readonly Linear _fc1;
    private readonly Linear _fc2;

    public VectorEncoder(int inputSize, int latentDim = ModularPolicy.DefaultLatentDim)
        : base(nameof(VectorEncoder))
    {
        InputSize = inputSize;
        LatentDim = latentDim;
        _fc1 = Linear(inputSize, latentDim);
        _fc2 = Linear(latentDim, latentDim);
        RegisterComponents();
    }

    public int InputSize { get; }
    public int LatentDim { get; }

    public override Tensor forward(Tensor x)
    {
        using var h1 = tanh(_fc1.forward(x));
        return tanh(_fc2.forward(h1));
    }
}

/// <summary>
/// CNN encoder mapping visual observations <c>[B, C, H, W]</c> into the same
/// fixed <c>[B, LatentDim]</c> embedding space used by <see cref="DecisionHead"/>.
/// </summary>
public sealed class VisualEncoder : Module<Tensor, Tensor>
{
    private readonly Conv2d _conv1;
    private readonly Conv2d _conv2;
    private readonly Linear _fc;

    public VisualEncoder(int channels, int height, int width, int latentDim = ModularPolicy.DefaultLatentDim)
        : base(nameof(VisualEncoder))
    {
        Channels = channels;
        Height = height;
        Width = width;
        LatentDim = latentDim;

        _conv1 = Conv2d(channels, 16, 3, stride: 2, padding: 1);
        _conv2 = Conv2d(16, 32, 3, stride: 2, padding: 1);

        var hAfterConv1 = (height + 2 - 3) / 2 + 1;
        var wAfterConv1 = (width + 2 - 3) / 2 + 1;
        var hAfterConv2 = (hAfterConv1 + 2 - 3) / 2 + 1;
        var wAfterConv2 = (wAfterConv1 + 2 - 3) / 2 + 1;
        var flatFeatures = 32 * hAfterConv2 * wAfterConv2;

        _fc = Linear(flatFeatures, latentDim);
        RegisterComponents();
    }

    public int Channels { get; }
    public int Height { get; }
    public int Width { get; }
    public int LatentDim { get; }

    public override Tensor forward(Tensor x)
    {
        using var c1 = functional.relu(_conv1.forward(x));
        using var c2 = functional.relu(_conv2.forward(c1));
        using var flat = c2.flatten(start_dim: 1);
        return tanh(_fc.forward(flat));
    }
}

/// <summary>
/// Modality-agnostic Actor-Critic decision head mapping <c>[B, LatentDim]</c> features
/// to <c>([B, ActionCount] Logits, [B, 1] Value)</c>.
/// Supports direct weight transplantation across Vector and Visual generations.
/// </summary>
public sealed class DecisionHead : Module<Tensor, (Tensor Logits, Tensor Value)>
{
    private readonly Linear _trunk;
    private readonly Linear _actor;
    private readonly Linear _critic;

    public DecisionHead(int actionCount, int latentDim = ModularPolicy.DefaultLatentDim)
        : base(nameof(DecisionHead))
    {
        ActionCount = actionCount;
        LatentDim = latentDim;
        _trunk = Linear(latentDim, latentDim);
        _actor = Linear(latentDim, actionCount);
        _critic = Linear(latentDim, 1);
        RegisterComponents();
    }

    public int ActionCount { get; }
    public int LatentDim { get; }

    public override (Tensor Logits, Tensor Value) forward(Tensor latent)
    {
        using var hidden = tanh(_trunk.forward(latent));
        var logits = _actor.forward(hidden);
        var value = _critic.forward(hidden);
        return (logits, value);
    }

    public void CopyWeightsFrom(DecisionHead source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.LatentDim != LatentDim || source.ActionCount != ActionCount)
        {
            throw new InvalidOperationException(
                $"Cannot transplant DecisionHead weights: shape mismatch " +
                $"({source.LatentDim}->{source.ActionCount} vs {LatentDim}->{ActionCount}).");
        }

        using var _ = no_grad();
        var sourceParams = source.named_parameters().ToDictionary(p => p.name, p => p.parameter);
        foreach (var (name, targetParam) in named_parameters())
        {
            if (sourceParams.TryGetValue(name, out var srcParam))
            {
                targetParam.copy_(srcParam);
            }
        }
    }
}

/// <summary>
/// Complete modular policy pairing a modality-specific encoder (<see cref="VectorEncoder"/>
/// or <see cref="VisualEncoder"/>) with a shared <see cref="DecisionHead"/>.
/// </summary>
public sealed class ModularPolicy : Module
{
    public const int DefaultLatentDim = 64;

    private readonly VectorEncoder? _vectorEncoder;
    private readonly VisualEncoder? _visualEncoder;

    public ModularPolicy(
        EnvironmentSpec spec,
        TrainingModality modality,
        int latentDim = DefaultLatentDim)
        : base(nameof(ModularPolicy))
    {
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        Modality = modality;
        LatentDim = latentDim;

        if (modality == TrainingModality.Vector)
        {
            _vectorEncoder = new VectorEncoder(spec.VectorObservationSize, latentDim);
        }
        else
        {
            _visualEncoder = new VisualEncoder(
                spec.VisualChannels,
                spec.VisualHeight,
                spec.VisualWidth,
                latentDim);
        }

        Head = new DecisionHead(spec.ActionCount, latentDim);
        RegisterComponents();
    }

    public EnvironmentSpec Spec { get; }
    public TrainingModality Modality { get; }
    public int LatentDim { get; }
    public DecisionHead Head { get; }

    public Tensor Encode(Tensor observationBatch)
    {
        return Modality == TrainingModality.Vector
            ? _vectorEncoder!.forward(observationBatch)
            : _visualEncoder!.forward(observationBatch);
    }

    public (Tensor Logits, Tensor Value) ForwardTensor(Tensor observationBatch)
    {
        using var latent = Encode(observationBatch);
        return Head.forward(latent);
    }

    public void CopyDecisionHeadFrom(ModularPolicy parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        Head.CopyWeightsFrom(parent.Head);
    }

    public Tensor CreateObservationTensor(IGameEnvironment env)
    {
        if (Modality == TrainingModality.Vector)
        {
            var vec = env.GetVectorObservation();
            return tensor(vec, [1, Spec.VectorObservationSize], dtype: ScalarType.Float32);
        }

        var vis = env.GetVisualObservation();
        return tensor(
            vis.Pixels,
            [1, Spec.VisualChannels, Spec.VisualHeight, Spec.VisualWidth],
            dtype: ScalarType.Float32);
    }

    public float[] GetActionProbabilities(IGameEnvironment env)
    {
        using var scope = NewDisposeScope();
        using var _ = no_grad();
        using var obs = CreateObservationTensor(env);
        var (logits, _) = ForwardTensor(obs);
        using var probs = functional.softmax(logits, dim: -1);
        return probs.data<float>().ToArray();
    }

    public (int ActionIndex, float LogProb, float Value, float[] Probabilities) SampleAction(
        IGameEnvironment env,
        Random rng)
    {
        using var scope = NewDisposeScope();
        using var _ = no_grad();
        using var obs = CreateObservationTensor(env);
        var (logits, valueTensor) = ForwardTensor(obs);
        using var probsTensor = functional.softmax(logits, dim: -1);
        using var logProbsTensor = functional.log_softmax(logits, dim: -1);

        var probs = probsTensor.data<float>().ToArray();
        var logProbs = logProbsTensor.data<float>().ToArray();
        var value = valueTensor.ToSingle();

        var actionIndex = SampleCategorical(probs, rng);
        return (actionIndex, logProbs[actionIndex], value, probs);
    }

    public void SaveWeights(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        save(path);
    }

    public void LoadWeights(string path)
    {
        load(path);
    }

    private static int SampleCategorical(float[] probs, Random rng)
    {
        var sample = (float)rng.NextDouble();
        var cumulative = 0f;
        for (var i = 0; i < probs.Length - 1; i++)
        {
            cumulative += probs[i];
            if (sample <= cumulative)
            {
                return i;
            }
        }
        return probs.Length - 1;
    }
}
