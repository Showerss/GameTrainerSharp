# Design

## Context

See [proposal.md](proposal.md) for motivation. Currently, `GameTrainerSharp` has completed Milestone 0 with three projects (`GameTrainer.Core`, `GameTrainer.Games`, `GameTrainer.Runner`) and an initial `IEnvironment<TObs, TAct>` contract. While the functional core (`GridWorldRules` + `GridState`) is clean, three structural limitations block multi-game scaling, vector-to-visual generational teaching, and the desktop dashboard:

1. **Action Enum Coupling**: `IEnvironment<float[], Move>` forces policies and runners to depend on each game's specific action enum (`Move`).
2. **Single Observation Modality**: `Step()` only returns a single `TObs`, whereas cross-modal teacher-student training requires the frozen parent to read `float[]` vector state and the visual child to read pixel frames (`VisualFrame`) on the exact same tick.
3. **External State Mutation**: `RandomStartEnvironment` mutates `GridWorldEnvironment` after `Reset()` via a public `Place(...)` method.

## Goals / Non-Goals

**Goals:**
- Provide a unified, type-safe `IGameEnvironment` seam in `GameTrainer.Core` that supports discrete `int` actions (`0..ActionCount-1`) and simultaneous Vector (`float[]`) + Visual (`VisualFrame`) observations while keeping game-internal rules (`GridWorldRules`, `Move`) strongly typed and pure.
- Implement a modular TorchSharp neural architecture (`Encoder` + `DecisionHead`) in `GameTrainer.Brain` that supports from-scratch PPO, side-by-side parent->child action-probability coaching (`Coach`), and direct `DecisionHead` weight transplantation (`CopyHead`).
- Decouple the CLI training runner (`GameTrainer.Runner`) from the desktop observatory (`GameTrainer.Gui`) via a local file-based Catalog & Run Store (`catalog.json`, `metrics.jsonl`, `training.log`, `model.pt`).
- Build `GameTrainer.Gui` with Avalonia UI styled with a classic Windows 2000 bevel/gray aesthetic.

**Non-Goals:**
- Process memory hooking or DLL injection into external games (external games added later will use safe OS screen capture `IEyes` and input `IHands` only).
- Embedding a real-time interactive game viewport inside the Avalonia dashboard (the dashboard is a catalog, lineage, chart, and log observatory).
- Distributed multi-machine training or cloud database hosting.

---

## System Architecture Visual Blueprint

```text
+===========================================================================+
|                        GAMETRAINERSHARP SOLUTION                          |
+===========================================================================+

  +-----------------------------------------------------------------------+
  |                       GameTrainer.Core (Seams)                        |
  |                                                                       |
  |  +---------------------+  +--------------------+  +----------------+  |
  |  | IGameEnvironment    |  | IGameDescriptor    |  | Catalog Models |  |
  |  | - Spec              |  | - Id, DisplayName  |  | - GameEntry    |  |
  |  | - Reset()           |  | - CreateEnv(...)   |  | - ModelVersion |  |
  |  | - Step(int action)  |  +--------------------+  | - MetricPoint  |  |
  |  | - GetVectorObs()    |                          +----------------+  |
  |  | - GetVisualObs()    |  +--------------------+                      |
  |  +---------------------+  | ICatalogStore      |                      |
  |                           +--------------------+                      |
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
  | - VisualRenderer  |     | - ModularPolicy   |     | - Lineage Table   |
  |                   |     | - GenerationalPpo |     | - Progress Chart  |
  | Shell:            |     |   Trainer         |     | - Log Viewer      |
  | - GridWorldEnv    |     +---------+---------+     +---------+---------+
  +---------+---------+               ^                         ^
            ^                         |                         |
            +------------+------------+                         |
                         |                                      |
               +---------+---------+                            |
               | GameTrainer.Runner|                            |
               |    (CLI Host)     |                            |
               +---------+---------+                            |
                         | writes                               | reads
                         v                                      |
               +------------------------------------------------+
               |         LOCAL CATALOG & ARTIFACT STORE         |
               |  artifacts/                                    |
               |    +-- catalog.json                            |
               |    +-- runs/<game-id>/<version>/               |
               |          +-- model.pt                          |
               |          +-- metrics.jsonl                     |
               |          +-- training.log                      |
               +------------------------------------------------+
```

---

## Decisions

### 1. Two-Layer Environment Contract (`IGameEnvironment` + Pure Game Core)

**Decision:** Keep strongly-typed state records (`GridState`) and action enums (`Move`) inside each game's pure functional core, while exposing a uniform `IGameEnvironment` interface at the `GameTrainer.Core` boundary that takes discrete `int actionIndex` and provides both `GetVectorObservation()` and `GetVisualObservation()`.

```text
+-------------------------------------------------------------------------+
|                 DUAL-OBSERVATION ENVIRONMENT FLOW                       |
+-------------------------------------------------------------------------+

  Caller (Trainer / Runner)
     |
     | env.Step(actionIndex: 3)
     v
  +--------------------------------------------------------------------+
  | GridWorldEnvironment : IGameEnvironment                            |
  |   1. Maps int 3 -> Move.Right                                      |
  |   2. Calls pure GridWorldRules.Step(_state, Move.Right, _rewards)  |
  |   3. Holds new immutable _state                                    |
  +--------------------------------------------------------------------+
     |                                        |
     | env.GetVectorObservation()             | env.GetVisualObservation()
     v                                        v
  float[4] (Normalized Row, Col,           VisualFrame (Channels: 3,
            GoalRow, GoalCol)              Height: 20, Width: 20, float[])
     |                                        |
     +---> Feeds Vector Teacher               +---> Feeds Visual Student
```

**Why over keeping `IEnvironment<TObs, TAct>` everywhere:**
- A single `GenerationalPpoTrainer` and `CatalogStore` can work across `GridWorld`, `CartPole`, and `Minesweeper` without generic type explosion or game-specific enum references.
- Including normalized goal coordinates `(Row, Col, GoalRow, GoalCol)` in the vector observation and rendering a multi-channel pixel representation in `GetVisualObservation()` makes both fixed-goal and random-start tasks solvable by both vector and visual brains.
- Replaces `RandomStartEnvironment.Place()` with a pure `Func<Random, GridState>` spawner injected into `GridWorldEnvironment`.

---

### 2. Modular Two-Part Brain (`Encoder` + `DecisionHead`) & Three Training Modes

**Decision:** Split every neural policy in `GameTrainer.Brain` into an `IObservationEncoder` (producing a fixed `LatentDim = 64` tensor) and a `DecisionHead` (mapping `64 -> ActionCount` actor logits + `64 -> 1` critic value).

```text
+-------------------------------------------------------------------------+
|            GENERATIONAL BRAIN: COACHING + WEIGHT TRANSPLANT             |
+-------------------------------------------------------------------------+

  PARENT MODEL (Frozen during Child training):
  [Vector Obs (4)] ---> [VectorEncoder (MLP)] ---> [Latent (64)]
                                                        |
                                                        v
                                                 [DecisionHead]
                                                   |        |
                             (CopyHead weights) ---+        +--> Parent Probs
                                      |                              |
                                      v                              |
  CHILD MODEL (Trainable):            |                              |
  [Visual Obs (3x20x20)] -> [VisualEncoder (CNN)] -> [Latent (64)]   |
                                                        |            |
                                                        v            v
                                                 [DecisionHead]      |
                                                        |            |
                                                        +--> Child Probs
                                                                 |
                                                                 v
  Total Loss = L_PPO(Child, Reward) + Lambda(t) * KL(ParentProbs || ChildProbs)
```

**Rationale:**
- **Side-by-Side Coaching (`Mode = Coach`)**: By adding $\lambda(t) \cdot D_{\text{KL}}(\pi_{\text{Parent}} \,\|\, \pi_{\text{Child}})$ to the PPO objective, the child receives dense per-step supervision (including the parent's full probability distribution across all actions) instead of waiting for sparse end-of-episode rewards. Decaying $\lambda(t) = \lambda_0 \cdot \max(0, 1 - \frac{\text{episode}}{\text{warmupEpisodes}})$ lets the child outgrow the parent and reach higher precision.
- **Weight Transplant (`Mode = CoachAndCopyHead`)**: Because both encoders output a `64`-element latent vector, we can directly copy the parent's `DecisionHead` state dict into the child before training starts, allowing direct experimental comparison against `Scratch` and `Coach`-only runs.

---

### 3. Local File-Based Catalog & Run Store

**Decision:** Store catalog metadata in `<workspace>/artifacts/catalog.json` (configurable path) and store per-version artifacts in `<workspace>/artifacts/runs/<game-id>/<version>/` (`model.pt`, `metrics.jsonl`, `training.log`).

**Why over SQLite or a background daemon:**
- Zero external server setup; human-readable JSON/JSONL files are easy to inspect, back up, or test in xUnit using temporary directories.
- `metrics.jsonl` appends one JSON object per evaluation window, making it safe for `GameTrainer.Gui` to read while `GameTrainer.Runner` is actively training.

---

### 4. Avalonia UI with Classic Windows 2000 Theme

**Decision:** Build `GameTrainer.Gui` on Avalonia 11 using custom styles that replicate the Windows 2000 control appearance (`#D4D0C8` 3D face, `#FFFFFF` top/left highlight, `#808080` / `#404040` bottom/right shadow bevels, classic sunken list views and chart canvas).

**Why Avalonia:**
- Cross-platform (.NET 10 compatible on Windows, macOS, Linux).
- Custom `Border` and `ControlTheme` styling in Avalonia makes crisp pixel-art / Win2000 3D beveled borders straightforward without fighting modern OS Mica/Acrylic chrome.

---

## Risks / Trade-offs

- **[TorchSharp native library size and tensor memory management]** → Wrap tensor steps in `using var scope = torch.NewDisposeScope();` inside every rollout and PPO update epoch so native memory never leaks during multi-thousand-episode runs.
- **[Concurrent access to `catalog.json` / `metrics.jsonl`]** → Write `catalog.json` atomically via temp-file swap (`catalog.json.tmp` -> `catalog.json`) and read `metrics.jsonl` / `training.log` with `FileShare.ReadWrite` so the GUI never locks files needed by the CLI runner.
- **[Stale documentation in `docs/design-plan.md` and `README.md`]** → Explicitly update both files as part of the change tasks so future contributors and AI agents see a single, accurate architectural reference.
