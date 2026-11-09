using GameTrainer.Brain;
using GameTrainer.Core;
using GameTrainer.Core.Catalog;
using GameTrainer.Games.GridWorld;
using GameTrainer.Runner;

if (args is ["baseline"] or ["gridworld-vision"])
{
    RunRandomBaseline(args[0]);
    return;
}

var gameId = GetOption(args, "--game") ?? "gridworld";
var version = GetOption(args, "--version") ?? "v1.0";
var modalityStr = GetOption(args, "--modality") ?? "Vector";
var modeStr = GetOption(args, "--mode") ?? "Scratch";
var parentVersion = GetOption(args, "--parent");
var episodesStr = GetOption(args, "--episodes");
var catalogDir = GetOption(args, "--catalog");

var descriptor = BuiltInGames.FindById(gameId)
    ?? throw new InvalidOperationException(
        $"Unknown game '{gameId}'. Available games: {string.Join(", ", BuiltInGames.All.Select(g => g.Id))}");

var modality = Enum.Parse<TrainingModality>(modalityStr, ignoreCase: true);
var mode = Enum.Parse<GenerationalMode>(modeStr, ignoreCase: true);
var episodes = int.TryParse(episodesStr, out var parsedEp) ? parsedEp : 120;

var store = new CatalogStore(catalogDir);
store.RegisterGame(descriptor);

ModularPolicy? parentPolicy = null;
if (!string.IsNullOrWhiteSpace(parentVersion))
{
    var gameEntry = store.GetGame(descriptor.Id);
    var parentMeta = gameEntry?.Versions.FirstOrDefault(v =>
        string.Equals(v.Version, parentVersion, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Parent model version '{parentVersion}' was not found for game '{descriptor.Id}'.");

    var parentWeightsPath = store.GetWeightsPath(descriptor.Id, parentMeta.Version);
    if (!File.Exists(parentWeightsPath))
    {
        throw new FileNotFoundException(
            $"Parent weights file not found at '{parentWeightsPath}'.", parentWeightsPath);
    }

    parentPolicy = new ModularPolicy(descriptor.Spec, parentMeta.Modality);
    parentPolicy.LoadWeights(parentWeightsPath);
}

using (parentPolicy)
using (var studentPolicy = new ModularPolicy(descriptor.Spec, modality))
{
    var env = descriptor.CreateEnvironment();
    var trainer = new GenerationalPpoTrainer();
    var config = new TrainingConfig(
        TotalEpisodes: episodes,
        BatchEpisodes: Math.Min(10, Math.Max(1, episodes / 6)),
        UpdateEpochs: 4,
        LearningRate: 3e-3f,
        InitialDistillationWeight: 1.0f,
        DistillationWarmupEpisodes: Math.Max(10, (int)(episodes * 0.7)));

    Console.WriteLine($"=== GameTrainer.Runner: {descriptor.DisplayName} ({descriptor.Id}) ===");
    Console.WriteLine($"  Version  : {version}");
    Console.WriteLine($"  Modality : {modality}");
    Console.WriteLine($"  Mode     : {mode}" + (parentVersion is not null ? $" (Taught by {parentVersion})" : ""));
    Console.WriteLine($"  Episodes : {episodes}");
    Console.WriteLine($"  Catalog  : {store.RootDirectory}");

    var summary = trainer.Train(
        env,
        studentPolicy,
        parent: parentPolicy,
        mode: mode,
        config: config,
        onMetric: point => store.AppendMetricPoint(descriptor.Id, version, point),
        onLog: line =>
        {
            store.AppendLogLine(descriptor.Id, version, line);
            Console.WriteLine($"  {line}");
        });

    var weightsPath = store.GetWeightsPath(descriptor.Id, version);
    studentPolicy.SaveWeights(weightsPath);

    var (weightsRel, metricsRel, logRel) = CatalogStore.GetRelativeRunPaths(descriptor.Id, version);
    var versionEntry = new ModelVersionEntry(
        Version: version,
        Modality: modality,
        Mode: mode,
        ParentVersion: parentVersion,
        UpdatedAt: DateTimeOffset.UtcNow,
        FinalMeanReward: summary.FinalMeanReward,
        FinalGoalRate: summary.FinalGoalRate,
        TotalEpisodes: summary.TotalEpisodes,
        WeightsRelativePath: weightsRel,
        MetricsRelativePath: metricsRel,
        LogRelativePath: logRel);

    store.RecordModelVersion(descriptor.Id, versionEntry);
    Console.WriteLine($"Saved model '{version}' -> {weightsPath}");
}

static string? GetOption(string[] args, string flag)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}

static void RunRandomBaseline(string modeArg)
{
    var (env, label, episodes) = modeArg switch
    {
        "gridworld-vision" => (
            (IEnvironment<float[], Move>)new RandomStartEnvironment(
                maxSteps: RandomStartEnvironment.VisionTaskStepCap),
            "GridWorld (random start, 25-step cap)",
            1000),
        _ => (
            (IEnvironment<float[], Move>)new GridWorldEnvironment(),
            "GridWorld (fixed start)",
            1000),
    };

    var policy = new RandomPolicy();
    float totalReward = 0f;
    var goals = 0;

    for (var ep = 0; ep < episodes; ep++)
    {
        var obs = env.Reset();
        while (true)
        {
            var result = env.Step(policy.Act(obs));
            totalReward += result.Reward;
            obs = result.Observation;
            if (result.Terminated) goals++;
            if (result.Done) break;
        }
    }

    Console.WriteLine($"{label}: {episodes} episodes, random policy");
    Console.WriteLine($"  mean reward : {totalReward / episodes:+0.00;-0.00}");
    Console.WriteLine($"  goal rate   : {100.0 * goals / episodes:0.#}%");
}
