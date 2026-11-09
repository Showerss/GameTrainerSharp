# Proposal

## Why

Milestone 0 established a basic reset/step loop for `GridWorld` with a random policy, but the current generic contract (`IEnvironment<TObs, TAct>`) couples policies to game-specific action enums (`Move`) and only emits single-modality vector observations. To explore generational machine-teaching-machine learning—where fast vector-trained models coach and transplant weights into visual models across generations—and to track model versions, lineage, and improvement curves across games, we need a unified dual-modal environment seam, a modular generational training engine, a persistent model catalog, and an Avalonia desktop observatory dashboard.

## What Changes

- **BREAKING**: Introduce a uniform `IGameEnvironment` and `IGameDescriptor` contract in `GameTrainer.Core` using discrete `int` action indices (`0..ActionCount-1`), `EnvironmentSpec` metadata, and dual observations (`GetVectorObservation()` and `GetVisualObservation()`) from the same game state tick, while preserving strongly-typed enums (`Move`) and pure rules inside each game's functional core.
- **BREAKING**: Refactor `GridWorldEnvironment` and `RandomStartEnvironment` to use injected initial-state spawner strategies instead of mutating state externally via `Place(...)`, and add pixel-grid rendering (`VisualFrame`) to `GridState`.
- Add `GameTrainer.Brain` (TorchSharp) featuring a modular two-part neural architecture (`Encoder` + shared `DecisionHead`) and a generational PPO trainer supporting three experimental modes:
  1. **From Scratch** (`Parent = None`)
  2. **Side-by-Side Coaching** (`Mode = Coach`, blending PPO loss with decaying KL/cross-entropy guidance $\lambda(t)$ from a frozen parent model)
  3. **Coaching + Brain Transplant** (`Mode = Coach + CopyHead`, initializing the child's `DecisionHead` with the parent's trained weights while coaching its encoder)
- Add a local persistent **Catalog & Run Store** (`catalog.json` + `runs/<game-id>/<version>/` containing model checkpoints, `metrics.jsonl`, and `training.log`) recording model versions, modality (`Vector` vs. `Visual`), parent version lineage, and last-updated timestamps.
- Add `GameTrainer.Gui`, a cross-platform **Avalonia UI Observatory Dashboard** styled with a classic Windows 2000 aesthetic, allowing users to add/register games, inspect available model versions and lineage, view improvement graphs across generations, and browse training logs.
- Update `docs/design-plan.md` and `README.md` so repository documentation matches the new dual-modal, generational learning, and dashboard architecture without conflicting guidance.

## Capabilities

### New Capabilities
- `dual-modal-environment`: Uniform game environment contract (`IGameEnvironment`), game plugin discovery (`IGameDescriptor`), pure spawner strategies, and simultaneous vector (`float[]`) + visual (`VisualFrame`) state projection.
- `generational-learning`: Modular `Encoder` + `DecisionHead` neural policies and PPO trainer supporting from-scratch RL, decaying parent-to-child action probability coaching, and cross-modal `DecisionHead` weight transplantation.
- `model-catalog-store`: Local storage schema and repository managing registered games, versioned model metadata, parent-child lineage, timestamps, `metrics.jsonl` series, and `training.log` files.
- `observatory-dashboard`: Avalonia UI desktop application (Windows 2000 theme) for adding games, browsing model versions and update dates, visualizing generational reward/goal-rate curves, and inspecting logs.

### Modified Capabilities

## Impact

- **`src/GameTrainer.Core`**: Adds `IGameEnvironment`, `IGameDescriptor`, `EnvironmentSpec`, `VisualFrame`, and catalog/lineage domain models.
- **`src/GameTrainer.Games`**: Updates `GridWorld` to implement `IGameEnvironment` and `IGameDescriptor`, adds visual frame projection to `GridState`, and replaces `RandomStartEnvironment.Place` mutation with spawner strategies.
- **`src/GameTrainer.Brain` (New Project)**: TorchSharp dependency for modular vector/visual encoders, shared actor-critic decision heads, and generational PPO + distillation training loop.
- **`src/GameTrainer.Runner`**: Upgraded CLI to run training experiments (scratch, coach, coach+copyhead) and persist checkpoints, logs, and metrics to the catalog store.
- **`src/GameTrainer.Gui` (New Project)**: Avalonia UI desktop project reading from the catalog store.
- **`docs/design-plan.md` & `README.md`**: Updated to reflect the unified architecture and remove stale M0-only assumptions.
