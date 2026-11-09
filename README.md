# GameTrainerSharp

**Generational reinforcement learning game trainer in C# — teach a fast vector brain first, then coach and transplant its decision weights into visual brains, tracked in a classic Windows 2000 Avalonia observatory.**

## Architecture

- **Functional core** — game rules, rewards, spawner strategies, and dual vector/visual state projections are pure functions over immutable records (`GridWorldRules.Step`, `GridState`).
- **Two-layer dual-modal contract** — games keep strongly-typed enums (`Move`) internally while exposing `IGameEnvironment` at the boundary with discrete `int` actions (`0..ActionCount-1`), `GetVectorObservation()`, and `GetVisualObservation()` on the same tick.
- **Generational Brain (`GameTrainer.Brain`)** — TorchSharp policies split into a modality-specific encoder (`VectorEncoder` or `VisualEncoder`) and a shared 64-dim `DecisionHead`, supporting:
  1. `Scratch`: Pure PPO trial-and-error (`Parent = None`)
  2. `Coach`: Side-by-side action probability distillation from a frozen parent model with decaying guidance $\lambda(t)$
  3. `CoachAndCopyHead`: Direct `DecisionHead` weight transplantation from parent to child + decaying coaching
- **Observatory Dashboard (`GameTrainer.Gui`)** — Cross-platform Avalonia UI desktop dashboard (Windows 2000 classic aesthetic) reading from the local `artifacts/` catalog store (`catalog.json`, `metrics.jsonl`, `training.log`, `model.pt`).

See [docs/design-plan.md](docs/design-plan.md) for the full architectural blueprint.

## Layout

```text
src/
  GameTrainer.Core/    IGameEnvironment, IGameDescriptor, EnvironmentSpec, CatalogStore
  GameTrainer.Games/   GridWorld (pure rules, spawners, vector + 3x20x20 visual renderer)
  GameTrainer.Brain/   TorchSharp VectorEncoder, VisualEncoder, DecisionHead, GenerationalPpoTrainer
  GameTrainer.Runner/  CLI trainer & baseline runner writing to CatalogStore
  GameTrainer.Gui/     Windows 2000-styled Avalonia UI Observatory Dashboard
tests/
  GameTrainer.Tests/   xUnit test suite
```

## Run It

```bash
# Run all unit and integration tests
dotnet test

# 1. Train Generation 1 (Vector Teacher) from scratch
dotnet run --project src/GameTrainer.Runner -- --game gridworld --version v1.0 --modality Vector --mode Scratch --episodes 120

# 2. Train Generation 2 (Visual Student) coached by v1.0 + DecisionHead weight transplant
dotnet run --project src/GameTrainer.Runner -- --game gridworld --version v2.0 --modality Visual --mode CoachAndCopyHead --parent v1.0 --episodes 120

# 3. Launch the Windows 2000 Avalonia Observatory Dashboard
dotnet run --project src/GameTrainer.Gui

# Legacy random-policy baselines
dotnet run --project src/GameTrainer.Runner -- baseline
dotnet run --project src/GameTrainer.Runner -- gridworld-vision
```

## License

MIT — see [LICENSE](LICENSE).
