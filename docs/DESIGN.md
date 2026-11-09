# GameTrainerSharp: Design Decisions

> **Doc roles (read first):**
> - `docs/design-plan.md` = architecture reference (projects, seams, generational learning).
> - This file = project goals, locked decisions, and open questions.
> - `openspec/specs/` = capability requirements; `openspec/changes/` = in-flight changes.
> - `docs/TURNOVER.md` = session handoff only (current state + next action).
>   Never contains decisions that aren't also recorded in the docs above.

## Current state of the repo
- `GameTrainer.Core`: `IGameEnvironment` (discrete `int` actions, vector + visual observations on the same tick), `IGameDescriptor` (game discovery), `EnvironmentSpec`, `StepOutcome`, `VisualFrame`, and `CatalogStore` (persistent model/lineage store). The original `IEnvironment<TObs,TAct>` / `IPolicy` / `StepResult<TObs>` contracts remain for the random baselines.
- `GameTrainer.Games`: GridWorld (pure rules, immutable `GridState`, spawners, vector + 3x20x20 visual renderer, descriptors).
- `GameTrainer.Brain`: TorchSharp `VectorEncoder` / `VisualEncoder` + shared 64-dim `DecisionHead`, and `GenerationalPpoTrainer`.
- `GameTrainer.Runner`: CLI trainer writing to `CatalogStore`, plus the legacy random-policy baselines (gridworld random ≈ -0.09 mean reward; gridworld-vision random ≈ 53% goal rate at the 25-step cap).
- `GameTrainer.Gui`: Windows 2000-styled Avalonia observatory dashboard.
- `GameTrainer.Tests`: GridWorld, catalog, dashboard view-model, and generational brain tests.

## Purpose
- Portfolio piece for employers AND a personal exploration/study of RL methods.
- Research focus: ablations on PPO and generational learning. Can training be shortened, or data/features/network size be reduced, while the model still solves the game? Can a trained parent make a child learn faster?
- "Done" = a model fully solves multiple games with few or no failures.
- Priority: a generic live-window environment that can attach to any game; simulators are optional accelerators.

## Style
- **Functional core, imperative shell.** Game rules, rewards, spawners, and observation projections are pure functions over immutable records (`GridWorldRules.Step`, `GridState`). Mutable state lives only in thin shells (`GridWorldEnvironment`, trainers, the GUI).
- **Two-layer dual-modal seam.** Games keep strongly typed enums (`Move`) internally and expose `IGameEnvironment` at the boundary with `int` actions (`0..ActionCount-1`).
- Interfaces at every seam (`IGameEnvironment`, `IGameDescriptor`, `ICatalogStore`) so trainers, catalog, and dashboard work with any game unchanged.

### Code readability rules
- **Simple data structures and simple, effective logic** — prefer a plain record/array/list over a clever abstraction; no design-pattern-for-its-own-sake.
- **Human-readable code a junior dev could review.** Descriptive names over comments, small methods, no clever one-liners.
- **XML doc comments (`/// <summary>`) on every public type and member.**

## Contracts
- `StepOutcome.Done` / `StepResult.Done` is a **computed property**: `Terminated || Truncated`. Never stored. (Gymnasium convention.)
- `IGameEnvironment` emits `GetVectorObservation()` and `GetVisualObservation()` for the same state tick, so a frozen vector parent can coach a visual child step by step.
- `EnvironmentSpec` describes shapes: vector size, visual CHW size, action count and names.

## Methods
- Main subject: **PPO** (`GenerationalPpoTrainer`). Random is the baseline floor.
- Neural nets: **TorchSharp**.
- Every policy = **modality-specific encoder** (`VectorEncoder` or `VisualEncoder`, both producing a 64-dim latent) + **shared `DecisionHead`** (actor logits + critic value).
- Training modes:
  1. **`Scratch`**: pure PPO from random initialization.
  2. **`Coach`**: a frozen parent observes each tick alongside the child; PPO loss is blended with a decaying KL coaching loss λ(t)·KL(π_parent ‖ π_child), so the child learns fast early and can surpass the parent as λ(t) → 0.
  3. **`CoachAndCopyHead`**: coaching plus a **`DecisionHead` weight transplant** — the parent's head weights are copied into the child before episode 1 (latent size and action count must match), while the child's encoder is coached.
- Typical chain: vector teacher (Scratch) → visual student (Coach or CoachAndCopyHead). Always measure each generation against the original task score, not just its parent.

## Success and measurement
- "Works just as well" = target score (goal rate or mean reward) reached on a **fixed held-out evaluation set**, averaged over **>= 5 seeds**.
- **Target: GridWorld = >= 95% goal rate** (random baseline ≈ 53%).
- Efficiency = **environment steps to reach the target score**.
- A generation **passes** if it reaches the game's target score AND lands within **2 points of its parent**. Worse is a recorded regression, not a chain failure — lineage + checkpoints make rollback free.

## Games
- Built-in: GridWorld. Next: live-window games, starting with LibreMines (see `openspec/changes/live-window-environment`). Simulators such as CartPole are optional accelerators.
- Every game must emit both a vector and a visual observation from the same state where possible (live games always have visual; vector only when a profile supplies a state reader).
- Design rule: every game has **configurable randomness** and **clear win/fail conditions**.

### GridWorld rules
- **5x5 open grid**, no walls.
- Rewards: **-0.01 per step, +1.0 for the goal**; hitting the step cap truncates the episode with no goal reward.
- `gridworld`: fixed start (0,0), goal (4,4), **25-step cap**.
- `gridworld-random-start`: agent placed randomly each episode, never on the goal; goal fixed at the center (2,2); **25-step cap**.
- Observation vector: **[agentX, agentY, goalX, goalY]**, each normalized to 0-1. Visual: 3x20x20 CHW frame (4 px per cell).
- Actions: 4 moves (up/down/left/right). Bumping the grid edge costs the step and the -0.01 penalty but does not move the agent.

## Randomness
- RNG is injected (spawners take `Random`; descriptors create environments from a supplied `Random`). No global `Random`.
- Seeding makes runs replayable; the games stay random.

## Experiment tracking
- `CatalogStore` under `artifacts/`: `catalog.json` (games, model versions, parent lineage) and one directory per version, `runs/<game-id>/<version>/`, holding `model.pt`, `metrics.jsonl`, and `training.log`. No database.
- The CLI runner writes; the GUI reads. Both can run at the same time.

## Interface
- **GUI = Avalonia observatory dashboard** with a classic Windows 2000 theme: game sidebar, add-game dialog, model lineage table, improvement chart, and training log viewer.
- **CLI = `GameTrainer.Runner`**: `--game`, `--version`, `--modality Vector|Visual`, `--mode Scratch|Coach|CoachAndCopyHead`, `--parent`, `--episodes`.
- Future: launching training from the GUI (a full cockpit).

## User stories
- A. Learn a new game: register a game descriptor, train a Scratch vector model; done when the version appears in the catalog with metrics.
- B. Does shorter training still work?: run the same setup with a smaller budget; compare curves against the target.
- C. Vector parent teaches a successor: train v2 with `--parent v1`; done when v2 appears with lineage and its curve next to its parent's.
- D. Vector training feeds visual training: train a Visual model in Coach or CoachAndCopyHead mode from a vector parent; compare against a visual-from-scratch run.
- E. Find the minimum information: drop features/data/network size and compare each variant vs baseline.
- F. Watch an agent play a live game window.
- G. Debug a failing run: inspect metrics and `training.log` in the dashboard.
- H. Theme the app.
- I (employer): read the repo, see results, rerun them with one command.

## Working agreement with the AI
- Human owns architecture; AI owns implementation inside it and flags anything that would change the architecture.
- Tests first, in small features; human reviews tests, not just implementation.
- Changes go through OpenSpec (propose → apply → archive).
- Review diffs; ask "why" when surprised.

## Open questions
- "Steps to solve" definition: first eval pass vs must hold for K consecutive evals.
- Eval cadence during training (every N steps vs end only).
- PPO ablation surface: which knobs configs may vary (lr, clip, gamma, network size, budget, features).
- Aggregation across seeds: std vs min/max; final eval vs best eval.
- Failure policy: when training diverges/NaNs, what does the user see?
- Target scores for live games.
- Does the 2-point tolerance band apply to budget/feature ablations too, or only to generations?
- Are headline results committed to the repo, or generated on demand?
