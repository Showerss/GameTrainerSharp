# GameTrainerSharp: Locked Design Decisions (from grill sessions, 2026-11-04)

> **Doc roles (read first):**
> - **This file = THE SPEC.** Every locked decision lives here; it is the single
>   source of truth for all implementation. Spec-first rule: **no code gets
>   written until "Still undecided" (bottom of this file) is empty.**
> - `docs/TURNOVER.md` = session handoff only (current state + next action).
>   Never contains decisions that aren't also here.
> - `docs/design-plan.md` = superseded history. Do not implement from it.

## Current state of the repo (read from code)
- C# port of an existing Python project; at "Milestone 0".
- `GameTrainer.Core`: `IEnvironment<TObs,TAct>` (Reset/Step), `IPolicy<in TObs,out TAct>` (Act), `StepResult<TObs>` record (Observation, Reward, Terminated, Truncated, Steps, Done).
- `GameTrainer.Games`: GridWorld (rules, rewards, state, random-start variant).
- `GameTrainer.Runner`: console app with `RandomPolicy`; baselines from the Python repo: gridworld random ≈ -0.09 mean reward; gridworld-vision random ≈ 53% goal rate at 25-step cap.
- `GameTrainer.Tests`: GridWorld tests.

## Purpose
- Portfolio piece for employers AND a personal exploration/study of RL methods (grad-school OOP).
- Research focus: ablations on PPO. Can training be shortened, or data/features/network size be reduced, while the model still solves the game?
- "Done" = a model fully solves multiple games with few or no failures.

## Style
- **Full classic OOP** (locked, 2026-11-04 — supersedes the Option C hybrid in design-plan.md). Borrow FP habits only: immutable records/value objects, injected seeded RNG, one place for Step logic with no hidden side effects.
- OOP patterns to showcase: abstraction/polymorphism (IEnvironment), inheritance, strategy (policies), decorator (epsilon-greedy), factory, dependency injection.
- Rule: only `ExperimentRunner` and the factory know concrete classes; everything else depends on interfaces.

### Code readability rules (locked, 2026-11-04)
- **Simple data structures and simple, effective logic** — prefer a plain record/array/list over a clever abstraction; no design-pattern-for-its-own-sake.
- **Human-readable code a junior dev could review.** Descriptive names over comments, small methods, no clever one-liners. If a junior can't follow it, rewrite it simpler.
- **XML doc comments (`/// <summary>`, the C# equivalent of Javadoc) on every public type and member** — interfaces, classes, public methods. Generated API docs should let a newcomer navigate the codebase without reading implementations.

## Contracts
- `StepResult.Done` is a **computed property**: `Terminated || Truncated`. Never stored. (Gymnasium convention.)
- `ILearner.Train(env, budget)`: **budget is in environment steps** — the same unit as the efficiency metric, so configs, metrics, and ablations share one language.

## Responsibilities
- `IEnvironment<TObs,TAct>`: game contract (exists). Needs a **render/frame API and external step control** so the GUI can drive live play (story F).
- `IPolicy<TObs,TAct>`: acting (exists)
- `ILearner`: `Train(env, stepBudget) -> IPolicy`; `PpoLearner`, `DistillationLearner`
- `IObservationEncoder`: turns raw observations into policy input (e.g., dropping features). **Encoder settings are saved inside the model checkpoint**, so every saved model is self-describing (a vision student can tell exactly what its teacher saw).
- `Evaluator`: runs a policy on a fixed held-out episode set, returns score
- `ExperimentRunner`: reads config, builds parts via factory, runs seeds, writes run folders
- `Lineage`: records which policy taught which, and what was reduced. Stores the **config diff between parent and child**; the "one change per generation" rule is auditable via the diff, **not enforced in code**.
- A small **in-process** service layer sits between the GUI/CLI and `ExperimentRunner` (plain C# interface, not a separate process); GUI never touches learners directly.

## Methods
- Main subject: **PPO**. Random is the baseline floor. Q-learning and REINFORCE are optional extras (complexity ladder: Random -> Q-learning -> REINFORCE -> PPO).
- Neural nets: **TorchSharp** (PyTorch bindings; mirrors the Python API).
- Training runs are **fresh only — no checkpoint resume**. (Research questions are about steps-to-solve from scratch; resume can be added later if needed.)
- Later phase: **distillation chains** (vector v1 teaches vector v2; change ONE thing per generation) and **vision students** (vector teacher -> pixel student, optional RL polish; DAgger-style correction). Always measure each generation against the original task score, not just its parent.
- **How vision distillation works** (locked): weights do NOT transfer between vector and vision models (incompatible input shapes — MLP vs CNN). The teacher teaches *behavior*: it acts as an expert labeler over states while the game renders frames of the same states, and a fresh CNN student is trained to imitate. DAgger correction: the student plays, the teacher labels the states the student visits, fixing drift into unseen states. "Aided by vision" is enforced through evaluation (the gate below), not through shared parameters.

## Success and measurement
- "Works just as well" = target score (goal rate or mean reward) reached on a **fixed held-out evaluation set**, averaged over **>= 5 seeds**.
- The held-out set is a **checked-in list of 20 episode seeds per game**. Identical on every run and machine.
- **One eval seed fully determines an entire episode** — start state AND all mid-episode chance events (e.g., slips). Same seed = identical episode, replayable for debugging.
- **Locked target: GridWorld = >= 95% goal rate** on the eval set (random baseline ≈ 53%).
- **Generation verdict** (locked): a distilled generation **passes** if it (a) reaches the game's target score AND (b) lands within **2 points of its parent**, mean over >= 5 seeds. Worse than that = a recorded regression (a result, not a chain failure — lineage + checkpoints make rollback free).
- Efficiency = **environment steps to reach the target score**.

## Games
- Order: GridWorld (exists) -> CartPole -> a harder discrete game (maze with keys/doors, Snake, Sokoban, etc.). Longer-term: something harder than tic-tac-toe/minesweeper, such as a simple roguelike.
- **CartPole: port the OpenAI Gym equations** (validated, ~30 lines); do not hand-roll physics.
- Observations: **vector and local-grid first**; pixels come later as an ablation.
- Design rule: every game has **configurable randomness** (random starts, chance events such as slip probability) and **multiple win/fail conditions**.
- One game should be able to emit both a vector and a rendered frame from the same state (this is what makes teacher -> vision student possible).

### GridWorld rules (locked, 2026-11-04)
- **5x5 open grid** — no walls. Obstacles come later as a config-driven variant.
- Rewards: **-0.01 per step, +1.0 for the goal**; hitting the **25-step cap** truncates the episode with no goal reward.
- Observation vector: **[agentX, agentY, goalX, goalY]**, each normalized to 0-1.
- Actions: 4 moves (up/down/left/right). **Bumping the grid edge costs the step and the -0.01 penalty but does not move the agent** — wall avoidance is learned through reward, not action masking.
- Random-start variant: **agent and goal both placed randomly each episode, never on the same cell** — forces navigation, not route memorization.

## Randomness
- One master seed per run; derived seeds per component (env, learner, evaluator); never a shared RNG; no global `Random`.
- RNG injected through constructors.
- Evaluation uses the fixed held-out seed set (20 per game, checked in), identical across runs. Training seeds vary.
- Seeding makes runs replayable; the games stay random.
- Headline runs are **CPU-only** (GPU kernels can be nondeterministic).

## Experiment tracking
- JSON configs. One folder per run: config, metrics CSV, checkpoint (includes encoder settings), lineage (includes parent/child config diff). No database. CLI plus GUI. Aggregated reports (mean and spread across seeds).

## Interface
- **GUI is the main interface, built with Avalonia** (XAML/MVVM, themeable, cross-platform), with a visible **debugger console** panel. Every action is triggerable from the GUI.
- "Watch an agent play" (story F) is a **live environment driven by the GUI** with pause/step — not a recording replay. Requires the render + external step control on `IEnvironment`.
- CLI remains as a second thin front end over the same `ExperimentRunner`.

## Milestones
- **Milestone 1 (locked)**: `ILearner` + `Evaluator` + seeded RNG plumbing — **no neural net code**. Proves the testing/seeding/eval architecture end-to-end (e.g., with a trivial learner) so later PPO bugs can't be confused with harness bugs.
- Milestone 2 (suggested): PPO on GridWorld to the 95% gate.

## User stories (draft)
- A. Learn a new game: add a game class and register it, run `train --game <name> --method ppo --seeds 5`; done when 5 run folders and a steps-to-solve report exist.
- B. Does shorter training still work?: copy baseline config, halve budget, run both over 5 seeds on the same held-out seeds; done when side-by-side report shows verdict vs target.
- C. Vector baseline teaches next version: select vector-v1, click "Train successor", change one thing (e.g., network 256->128); done when vector-v2 appears with lineage and score next to its parent's.
- D. Vector training feeds vision training: select a finished vector model, "Train vision student"; teacher plays headless while frames and actions are recorded; done when "vision-v1 from vector-v1" is saved with a comparison against vision-from-scratch.
- E. Find the minimum information: tick features/data percent to drop, "Run sweep"; done when a table shows each variant vs baseline with pass/fail.
- F. Watch an agent play: pick a model and seed, "Play"; live render with action and confidence; pause, step, replay identical seed.
- G. Debug a failing run: console filters by run/component/severity; shows config, seeds, metrics; "Re-run with same seed" button.
- H. Theme the app: pick a theme in Settings; UI, charts and console change without restart.
- I (employer): read the repo, see results, rerun them with one command.

## Working agreement with the AI
- Human owns architecture; AI owns implementation inside it and flags anything that would change the architecture.
- Tests first, in small features; human reviews tests, not just implementation.
- Short design docs only; avoid thick upfront specs.
- Review diffs; ask "why" when surprised.

## Spec completeness rule (locked, 2026-11-04)
- Every decision is either **LOCKED** (written in this doc) or **DELEGATED**
  (explicitly listed below as the implementer's free choice). Nothing is
  implicitly undecided — implicit ambiguity is what this process exists to kill.
- **Done grilling = both tests pass:**
  1. **Walkthrough test**: every user story A-I can be narrated end-to-end with
     no "and then somehow..." gaps.
  2. **Three-AIs test**: three independent implementers reading this doc would
     make the same *observable* choices (game rules, metrics, verdicts,
     milestones, user-facing behavior). Code details may differ freely.

## Still to grill (high-level only)
- "Steps to solve" definition: first eval pass vs must hold for K consecutive
  evals. **This is the headline research metric — it must be exact.**
- Eval cadence during training (every N steps vs end only) — feeds the metric above.
- PPO ablation surface: which knobs configs may vary (lr, clip, gamma, network
  size, budget, features). **Defines the entire experiment space.**
- Aggregation across seeds: std vs min/max; final eval vs best eval.
- Failure policy: when training diverges/NaNs, what does the user see?
  (Story G depends on this.)
- GUI v1 scope: which user stories make the first cut.
- Target scores for CartPole and later games (GridWorld is locked at 95%).
- Specific harder games beyond GridWorld/CartPole.
- "Confidence" in story F: define (PPO action probability?) or drop.
- Milestone definitions beyond M1/M2: what triggers starting game #2, and what
  triggers the vision phase.
- Ablation verdict scope (stories B, E): does the 2-point tolerance band apply
  to budget/feature ablations too, or only to distillation generations?
- Story I (employer): are headline results committed to the repo (e.g., a
  results/ folder or README table), or generated on demand by rerunning?

## Delegated to the implementer (free choice — must document what was chosen)
- Exact C# interface signatures and file/class layout (within the
  responsibilities above).
- Solution/project layout for the Avalonia GUI and service layer.
- Run folder layout: exact filenames, metrics CSV columns.
- Config JSON schema: exact field names and defaults.
- RNG derivation rule: how component seeds are derived from the master seed
  (requirement: deterministic and documented, nothing more).
