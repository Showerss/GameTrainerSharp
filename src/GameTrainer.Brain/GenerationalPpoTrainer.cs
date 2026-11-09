using GameTrainer.Core;
using GameTrainer.Core.Catalog;
using TorchSharp;
using static TorchSharp.torch;

namespace GameTrainer.Brain;

/// <summary>
/// Hyperparameters for <see cref="GenerationalPpoTrainer"/>.
/// </summary>
public sealed record TrainingConfig(
    int TotalEpisodes = 200,
    int BatchEpisodes = 10,
    int UpdateEpochs = 4,
    float LearningRate = 3e-3f,
    float Gamma = 0.99f,
    float ClipEpsilon = 0.2f,
    float ValueLossCoeff = 0.5f,
    float EntropyCoeff = 0.01f,
    float InitialDistillationWeight = 1.0f,
    int DistillationWarmupEpisodes = 120);

/// <summary>
/// Summary result returned when a generational training run completes.
/// </summary>
public sealed record TrainingSummary(
    float FinalMeanReward,
    float FinalGoalRate,
    int TotalEpisodes,
    IReadOnlyList<EpisodeMetricPoint> Metrics);

/// <summary>
/// Generational PPO trainer supporting:
/// - <see cref="GenerationalMode.Scratch"/>: pure trial-and-error RL
/// - <see cref="GenerationalMode.Coach"/>: side-by-side action probability distillation with decaying Lambda(t)
/// - <see cref="GenerationalMode.CoachAndCopyHead"/>: direct <see cref="DecisionHead"/> weight transplant + coaching
/// </summary>
public sealed class GenerationalPpoTrainer
{
    private sealed record StepTransition(
        float[] ObservationFlat,
        int ActionIndex,
        float OldLogProb,
        float Value,
        float Reward,
        bool Done,
        float[]? ParentProbs);

    public TrainingSummary Train(
        IGameEnvironment env,
        ModularPolicy student,
        ModularPolicy? parent = null,
        GenerationalMode mode = GenerationalMode.Scratch,
        TrainingConfig? config = null,
        Action<EpisodeMetricPoint>? onMetric = null,
        Action<string>? onLog = null,
        Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(student);

        if (mode is GenerationalMode.Coach or GenerationalMode.CoachAndCopyHead && parent is null)
        {
            throw new ArgumentNullException(
                nameof(parent),
                $"A frozen parent policy is required when training in {mode} mode.");
        }

        cfgCheck(config ??= new TrainingConfig());
        rng ??= new Random();

        if (mode == GenerationalMode.CoachAndCopyHead && parent is not null)
        {
            student.CopyDecisionHeadFrom(parent);
            onLog?.Invoke(
                $"Transplanted DecisionHead weights (LatentDim={parent.LatentDim}, Actions={parent.Spec.ActionCount}) from parent ({parent.Modality}) to student ({student.Modality}).");
        }

        parent?.eval();
        student.train();

        using var optimizer = optim.Adam(student.parameters(), lr: config.LearningRate);
        var metrics = new List<EpisodeMetricPoint>();
        var batchBuffer = new List<List<StepTransition>>();
        var batchRewards = new List<float>();
        var batchGoals = new List<int>();

        onLog?.Invoke(
            $"Starting training: Modality={student.Modality}, Mode={mode}, Episodes={config.TotalEpisodes}");

        for (var episode = 1; episode <= config.TotalEpisodes; episode++)
        {
            env.Reset();
            var trajectory = new List<StepTransition>();
            var episodeReward = 0f;
            var reachedGoal = false;

            while (true)
            {
                var obsFlat = student.Modality == TrainingModality.Vector
                    ? env.GetVectorObservation()
                    : env.GetVisualObservation().Pixels;

                float[]? parentProbs = null;
                if (parent is not null && mode != GenerationalMode.Scratch)
                {
                    parentProbs = parent.GetActionProbabilities(env);
                }

                var (actionIndex, logProb, value, _) = student.SampleAction(env, rng);
                var outcome = env.Step(actionIndex);

                trajectory.Add(new StepTransition(
                    obsFlat,
                    actionIndex,
                    logProb,
                    value,
                    outcome.Reward,
                    outcome.Done,
                    parentProbs));

                episodeReward += outcome.Reward;
                if (outcome.Terminated)
                {
                    reachedGoal = true;
                }

                if (outcome.Done)
                {
                    break;
                }
            }

            batchBuffer.Add(trajectory);
            batchRewards.Add(episodeReward);
            batchGoals.Add(reachedGoal ? 1 : 0);

            var isBatchBoundary = batchBuffer.Count >= config.BatchEpisodes || episode == config.TotalEpisodes;
            if (!isBatchBoundary)
            {
                continue;
            }

            var batchStartEpisode = episode - batchBuffer.Count + 1;
            var lambda = ComputeDistillationWeight(mode, batchStartEpisode, config);

            UpdatePolicy(student, optimizer, batchBuffer, config, lambda);

            var meanReward = batchRewards.Average();
            var goalRate = (float)batchGoals.Average();
            var metric = new EpisodeMetricPoint(
                Episode: episode,
                MeanReward: meanReward,
                GoalRate: goalRate,
                DistillationWeight: lambda,
                Timestamp: DateTimeOffset.UtcNow);

            metrics.Add(metric);
            onMetric?.Invoke(metric);
            onLog?.Invoke(
                $"Episode {episode,4}/{config.TotalEpisodes}: MeanReward={meanReward:+0.00;-0.00}, GoalRate={goalRate * 100f:0.0}%, Lambda={lambda:0.00}");

            batchBuffer.Clear();
            batchRewards.Clear();
            batchGoals.Clear();
        }

        // Compute final evaluation over 50 episodes to get a clean summary score
        var (finalMeanReward, finalGoalRate) = Evaluate(env, student, episodes: 50, rng);
        onLog?.Invoke(
            $"Finished training: FinalEval MeanReward={finalMeanReward:+0.00;-0.00}, GoalRate={finalGoalRate * 100f:0.0}%");

        return new TrainingSummary(
            FinalMeanReward: finalMeanReward,
            FinalGoalRate: finalGoalRate,
            TotalEpisodes: config.TotalEpisodes,
            Metrics: metrics);
    }

    public static (float MeanReward, float GoalRate) Evaluate(
        IGameEnvironment env,
        ModularPolicy policy,
        int episodes = 50,
        Random? rng = null)
    {
        rng ??= new Random();
        policy.eval();

        var totalReward = 0f;
        var goals = 0;

        for (var ep = 0; ep < episodes; ep++)
        {
            env.Reset();
            while (true)
            {
                var probs = policy.GetActionProbabilities(env);
                // Greedy/stochastic evaluation with slight exploration if tied
                var action = SelectEvaluationAction(probs, rng);
                var outcome = env.Step(action);
                totalReward += outcome.Reward;
                if (outcome.Terminated) goals++;
                if (outcome.Done) break;
            }
        }

        return (totalReward / episodes, (float)goals / episodes);
    }

    public static float ComputeDistillationWeight(
        GenerationalMode mode,
        int currentEpisode,
        TrainingConfig config)
    {
        if (mode == GenerationalMode.Scratch)
        {
            return 0f;
        }

        var warmup = Math.Max(1, config.DistillationWarmupEpisodes);
        var progress = (float)(currentEpisode - 1) / warmup;
        return config.InitialDistillationWeight * Math.Max(0f, 1f - progress);
    }

    private static void UpdatePolicy(
        ModularPolicy student,
        optim.Optimizer optimizer,
        List<List<StepTransition>> trajectories,
        TrainingConfig config,
        float distillationWeight)
    {
        var allSteps = new List<StepTransition>();
        var allReturns = new List<float>();
        var allAdvantages = new List<float>();

        foreach (var traj in trajectories)
        {
            var runningReturn = 0f;
            var returns = new float[traj.Count];
            for (var t = traj.Count - 1; t >= 0; t--)
            {
                runningReturn = traj[t].Reward + config.Gamma * runningReturn;
                returns[t] = runningReturn;
            }

            for (var t = 0; t < traj.Count; t++)
            {
                allSteps.Add(traj[t]);
                allReturns.Add(returns[t]);
                allAdvantages.Add(returns[t] - traj[t].Value);
            }
        }

        if (allSteps.Count == 0)
        {
            return;
        }

        // Normalize advantages for stable PPO updates
        var advMean = allAdvantages.Average();
        var advVar = allAdvantages.Select(a => (a - advMean) * (a - advMean)).Average();
        var advStd = MathF.Sqrt(advVar + 1e-8f);
        var normalizedAdv = allAdvantages.Select(a => (a - advMean) / advStd).ToArray();

        var count = allSteps.Count;
        var obsStride = allSteps[0].ObservationFlat.Length;
        var flatObs = new float[count * obsStride];
        var actions = new long[count];
        var oldLogProbs = new float[count];
        var hasParent = distillationWeight > 0f && allSteps[0].ParentProbs is not null;
        var actionCount = student.Spec.ActionCount;
        var flatParentProbs = hasParent ? new float[count * actionCount] : null;

        for (var i = 0; i < count; i++)
        {
            Array.Copy(allSteps[i].ObservationFlat, 0, flatObs, i * obsStride, obsStride);
            actions[i] = allSteps[i].ActionIndex;
            oldLogProbs[i] = allSteps[i].OldLogProb;

            if (hasParent && allSteps[i].ParentProbs is { } pProbs)
            {
                Array.Copy(pProbs, 0, flatParentProbs!, i * actionCount, actionCount);
            }
        }

        long[] obsShape = student.Modality == TrainingModality.Vector
            ? [count, student.Spec.VectorObservationSize]
            : [count, student.Spec.VisualChannels, student.Spec.VisualHeight, student.Spec.VisualWidth];

        for (var epoch = 0; epoch < config.UpdateEpochs; epoch++)
        {
            using var scope = NewDisposeScope();

            using var obsTensor = tensor(flatObs, obsShape, dtype: ScalarType.Float32);
            using var actionsTensor = tensor(actions, [count, 1], dtype: ScalarType.Int64);
            using var oldLogProbsTensor = tensor(oldLogProbs, [count], dtype: ScalarType.Float32);
            using var returnsTensor = tensor(allReturns.ToArray(), [count], dtype: ScalarType.Float32);
            using var advTensor = tensor(normalizedAdv, [count], dtype: ScalarType.Float32);

            var (logits, values) = student.ForwardTensor(obsTensor);
            using var logProbs = nn.functional.log_softmax(logits, dim: -1);
            using var probs = nn.functional.softmax(logits, dim: -1);

            using var selectedLogProbs = logProbs.gather(dim: 1, index: actionsTensor).squeeze(1);
            using var ratio = exp(selectedLogProbs - oldLogProbsTensor);
            using var surr1 = ratio * advTensor;
            using var surr2 = clamp(ratio, 1.0f - config.ClipEpsilon, 1.0f + config.ClipEpsilon) * advTensor;
            using var policyLoss = -minimum(surr1, surr2).mean();

            using var valuePred = values.squeeze(1);
            using var valueLoss = nn.functional.mse_loss(valuePred, returnsTensor);
            using var entropy = -(probs * logProbs).sum(dim: -1).mean();

            using var baseLoss = policyLoss
                + config.ValueLossCoeff * valueLoss
                - config.EntropyCoeff * entropy;

            Tensor totalLoss;
            if (hasParent && flatParentProbs is not null)
            {
                using var parentProbsTensor = tensor(flatParentProbs, [count, actionCount], dtype: ScalarType.Float32);
                using var distillLoss = -(parentProbsTensor * logProbs).sum(dim: -1).mean();
                totalLoss = baseLoss + distillationWeight * distillLoss;
            }
            else
            {
                totalLoss = baseLoss.alias();
            }

            using (totalLoss)
            {
                optimizer.zero_grad();
                totalLoss.backward();
                optimizer.step();
            }
        }
    }

    private static int SelectEvaluationAction(float[] probs, Random rng)
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

    private static void cfgCheck(TrainingConfig config)
    {
        if (config.TotalEpisodes <= 0)
            throw new ArgumentOutOfRangeException(nameof(config), "TotalEpisodes must be positive.");
        if (config.BatchEpisodes <= 0)
            throw new ArgumentOutOfRangeException(nameof(config), "BatchEpisodes must be positive.");
    }
}
