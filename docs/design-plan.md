# GameTrainerSharp — Architecture & Design Plan

Architecture reference for `GameTrainerSharp`: a C# generational reinforcement learning game trainer and Windows 2000-styled Avalonia observatory.

**Core Pattern: Functional Core, Imperative Shell + Two-Layer Dual-Modal Seam.**

---

## 1. Architectural Overview

GameTrainerSharp separates pure game mechanics from neural policies, CLI training orchestration, and the desktop observatory GUI:

```text
+===========================================================================+
|                        GAMETRAINERSHARP SOLUTION                          |
+===========================================================================+

  +-----------------------------------------------------------------------+
  |                       GameTrainer.Core (Seams)                        |
  |                                                                       |
  |  - IGameEnvironment : Uniform discrete action + Vector/Visual obs     |
  |  - IGameDescriptor  : Plugin discovery for built-in/external games    |
  |  - EnvironmentSpec  : Shape metadata (Vector size, CHW visual, actions)|
  |  - CatalogStore     : Persistent JSON/JSONL model & lineage store     |
  +-----------------------------------+-----------------------------------+
                                      ^
            +-------------------------+-------------------------+
            |                         |                         |
  +---------+---------+     +---------+---------+     +---------+---------+
  | GameTrainer.Games |     | GameTrainer.Brain |     |  GameTrainer.Gui  |
  |                   |     |   (TorchSharp)    |     |   (Avalonia UI)   |
  | Pure Functional:  |     |                   |     |                   |
  | - GridState       |     | - VectorEncoder   |     | Win2000 Theme:    |
  | - Move (enum)     |     | - VisualEncoder   |     | - Game Sidebar    |
  | - GridWorldRules  |     | - DecisionHead    |     | - Add Game Dialog |
  | - Spawners        |     | - GenerationalPpo |     | - Lineage Table   |
  |                   |     |   Trainer         |     | - Progress Chart  |
  | Shell:            |     +---------+---------+     | - Log Viewer      |
  | - GridWorldEnv    |               ^               +---------+---------+
  +---------+---------+               |                         ^
            ^                         |                         |
            +------------+------------+                         |
                         |                                      |
               +---------+---------+                            |
               | GameTrainer.Runner|-----(artifacts/ disk)------+
               |    (CLI Host)     |
               +-------------------+
```

---

## 2. Two-Layer Environment Contract

Every game keeps two layers:

1. **Pure Functional Core (per game in `GameTrainer.Games`)**
   - Immutable state records (e.g., `GridState`), strongly-typed action enums (`Move`), and pure transition functions (`GridWorldRules.Step`).
   - Initial-state creation is injected via pure spawner delegates (`Func<Random, GridState>` in `GridWorldSpawners`) so callers never mutate environment state externally after `Reset()`.
   - Pure state projects into both a normalized `float[]` vector (`ToVectorObservation()`) and a CHW `VisualFrame` (`ToVisualObservation()`).

2. **Uniform Dual-Modal Shell (`IGameEnvironment` in `GameTrainer.Core`)**
   - Accepts discrete `int actionIndex` (`0..Spec.ActionCount - 1`) and maps to the internal enum at the boundary.
   - Emits both `GetVectorObservation()` and `GetVisualObservation()` for the exact same state tick so a frozen vector parent policy can coach a visual child policy step-by-step.

---

## 3. Generational Learning ("Machines Teaching Machines")

Every neural policy in `GameTrainer.Brain` is split into two snap-together modules:

- **Modality-Specific Encoder (`VectorEncoder` or `VisualEncoder`)**: Maps raw input (`float[]` vector or `VisualFrame` pixels) into a fixed 64-dimensional latent feature vector.
- **Shared `DecisionHead`**: Maps the 64-dimensional latent vector to action logits (Actor) and state value (Critic).

This structure enables three experimental training modes in `GenerationalPpoTrainer`:

1. **`Scratch` (`Parent = None`)**: Standard PPO trial-and-error from random initialization.
2. **`Coach` (Side-by-Side Action Probability Distillation)**: A frozen parent model observes each tick alongside the child, blending PPO loss with a decaying KL-divergence coaching loss $\lambda(t) \cdot D_{\text{KL}}(\pi_{\text{Parent}} \,\|\, \pi_{\text{Child}})$ so the child learns rapidly early on and surpasses the parent as $\lambda(t) \to 0$.
3. **`CoachAndCopyHead` (Coaching + Brain Transplant)**: Pre-loads the parent's trained `DecisionHead` weights directly into the child before episode 1 while coaching the child's encoder.

---

## 4. Decoupled Catalog Store & Observatory GUI

- **`CatalogStore` (`GameTrainer.Core/Catalog`)**: Persists `catalog.json` and per-version run directories (`runs/<game-id>/<version>/` containing `model.pt`, `metrics.jsonl`, and `training.log`).
- **`GameTrainer.Runner` (CLI)**: Executes fast training runs and writes checkpoints, metrics, and logs to `CatalogStore`.
- **`GameTrainer.Gui` (Avalonia UI)**: Classic Windows 2000-styled desktop observatory that reads `CatalogStore` to register games, track model versions and parent lineage, compare improvement curves, and inspect training logs.
