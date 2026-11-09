using GameTrainer.Brain;
using GameTrainer.Core.Catalog;
using GameTrainer.Games.GridWorld;
using static TorchSharp.torch;

namespace GameTrainer.Tests;

public class GenerationalBrainTests
{
    [Fact]
    public void Vector_and_visual_policies_produce_matching_latent_and_head_shapes()
    {
        using var scope = NewDisposeScope();
        var spec = GridWorldEnvironment.DefaultSpec;

        using var vecPolicy = new ModularPolicy(spec, TrainingModality.Vector, latentDim: 64);
        using var visPolicy = new ModularPolicy(spec, TrainingModality.Visual, latentDim: 64);

        var env = new GridWorldEnvironment();
        env.Reset();

        using var vecObs = vecPolicy.CreateObservationTensor(env);
        using var visObs = visPolicy.CreateObservationTensor(env);

        using var vecLatent = vecPolicy.Encode(vecObs);
        using var visLatent = visPolicy.Encode(visObs);

        Assert.Equal([1L, 64L], vecLatent.shape);
        Assert.Equal([1L, 64L], visLatent.shape);

        var (vecLogits, vecValue) = vecPolicy.Head.forward(vecLatent);
        var (visLogits, visValue) = visPolicy.Head.forward(visLatent);

        Assert.Equal([1L, 4L], vecLogits.shape);
        Assert.Equal([1L, 1L], vecValue.shape);
        Assert.Equal([1L, 4L], visLogits.shape);
        Assert.Equal([1L, 1L], visValue.shape);
    }

    [Fact]
    public void CopyDecisionHeadFrom_transplants_exact_head_weights_from_vector_parent_to_visual_child()
    {
        using var scope = NewDisposeScope();
        var spec = GridWorldEnvironment.DefaultSpec;

        using var vecParent = new ModularPolicy(spec, TrainingModality.Vector, latentDim: 64);
        using var visChild = new ModularPolicy(spec, TrainingModality.Visual, latentDim: 64);

        using var testLatent = ones([2, 64], dtype: ScalarType.Float32);

        // Before transplant, random initializations differ
        visChild.CopyDecisionHeadFrom(vecParent);

        var (parentLogits, parentVal) = vecParent.Head.forward(testLatent);
        var (childLogits, childVal) = visChild.Head.forward(testLatent);

        Assert.True(allclose(parentLogits, childLogits));
        Assert.True(allclose(parentVal, childVal));
    }

    [Fact]
    public void Trainer_runs_all_three_generational_modes_and_decays_lambda()
    {
        var env = new GridWorldEnvironment(maxSteps: 25);
        var spec = env.Spec;
        var trainer = new GenerationalPpoTrainer();
        var rng = new Random(42);

        // 1. Train Vector Parent from Scratch
        using var vectorParent = new ModularPolicy(spec, TrainingModality.Vector);
        var scratchConfig = new TrainingConfig(
            TotalEpisodes: 20,
            BatchEpisodes: 10,
            UpdateEpochs: 2,
            LearningRate: 5e-3f);

        var scratchSummary = trainer.Train(
            env,
            vectorParent,
            parent: null,
            mode: GenerationalMode.Scratch,
            config: scratchConfig,
            rng: rng);

        Assert.Equal(20, scratchSummary.TotalEpisodes);
        Assert.Equal(2, scratchSummary.Metrics.Count);
        Assert.All(scratchSummary.Metrics, m => Assert.Equal(0f, m.DistillationWeight));

        // 2. Train Visual Child in Coach mode
        using var visualCoachChild = new ModularPolicy(spec, TrainingModality.Visual);
        var coachConfig = new TrainingConfig(
            TotalEpisodes: 20,
            BatchEpisodes: 10,
            UpdateEpochs: 2,
            InitialDistillationWeight: 1.0f,
            DistillationWarmupEpisodes: 20);

        var coachSummary = trainer.Train(
            env,
            visualCoachChild,
            parent: vectorParent,
            mode: GenerationalMode.Coach,
            config: coachConfig,
            rng: rng);

        Assert.Equal(2, coachSummary.Metrics.Count);
        Assert.True(coachSummary.Metrics[0].DistillationWeight > coachSummary.Metrics[1].DistillationWeight);

        // 3. Train Visual Child in CoachAndCopyHead mode
        using var visualTransplantChild = new ModularPolicy(spec, TrainingModality.Visual);
        var logs = new List<string>();
        var transplantSummary = trainer.Train(
            env,
            visualTransplantChild,
            parent: vectorParent,
            mode: GenerationalMode.CoachAndCopyHead,
            config: coachConfig,
            onLog: logs.Add,
            rng: rng);

        Assert.Equal(20, transplantSummary.TotalEpisodes);
        Assert.Contains(logs, l => l.Contains("Transplanted DecisionHead weights"));
    }
}
