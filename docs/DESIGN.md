# GameTrainerSharp: Locked Design Decisions (from grill session, 2026-11-04)

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
- **Classic OOP** (chosen over functional). Borrow FP habits: immutable records/value objects, injected seeded RNG, one place for Step logic with no hidden side effects.
- OOP patterns to showcase: abstraction/polymorphism (IEnvironment), inheritance, strategy (policies), decorator (epsilon-greedy), factory, dependency injection.
- Rule: only `ExperimentRunner` and the factory know concrete classes; everything else depends on interfaces.

## Responsibilities
- `IEnvironment<TObs,TAct>`: game contract (exists)
- `IPolicy<TObs,TAct>`: acting (exists)
- `ILearner`: `Train(env, budget) -> IPolicy`; `PpoLearner`, `DistillationLearner`
- `IObservationEncoder`: turns raw observations into policy input (e.g., dropping features)
- `Evaluator`: runs a policy on a fixed held-out episode set, returns score
- `ExperimentRunner`: reads config, builds parts via factory, runs seeds, writes run folders
- `Lineage`: records which policy taught which, and what was reduced
- A small service layer sits between the GUI/CLI and `ExperimentRunner`; GUI never touches learners directly.

## Methods
- Main subject: **PPO**. Random is the baseline floor. Q-learning and REINFORCE are optional extras (complexity ladder: Random -> Q-learning -> REINFORCE -> PPO).
- Neural nets: **TorchSharp** (PyTorch bindings; mirrors the Python API).
- Later phase: **distillation chains** (vector v1 teaches vector v2; change ONE thing per generation) and **vision students** (vector teacher -> pixel student, optional RL polish; DAgger-style correction). Always measure each generation against the original task score, not just its parent.

## Success and measurement
- "Works just as well" = target score (goal rate or mean reward) reached on a **fixed held-out evaluation set**, averaged over **>= 5 seeds**.
- Efficiency = **environment steps to reach the target score**.

## Games
- Order: GridWorld (exists) -> CartPole -> a harder discrete game (maze with keys/doors, Snake, Sokoban, etc.). Longer-term: something harder than tic-tac-toe/minesweeper, such as a simple roguelike.
- Observations: **vector and local-grid first**; pixels come later as an ablation.
- Design rule: every game has **configurable randomness** (random starts, chance events such as slip probability) and **multiple win/fail conditions**.
- One game should be able to emit both a vector and a rendered frame from the same state.

## Randomness
- One master seed per run; derived seeds per component (env, learner, evaluator); never a shared RNG; no global `Random`.
- RNG injected through constructors.
- Evaluation uses a fixed held-out seed set, identical across runs. Training seeds vary.
- Seeding makes runs replayable; the games stay random.
- Headline runs are **CPU-only** (GPU kernels can be nondeterministic).

## Experiment tracking
- JSON configs. One folder per run: config, metrics CSV, checkpoint, lineage. No database. CLI plus GUI. Aggregated reports (mean and spread across seeds).

## Interface
- **GUI is the main interface, built with Avalonia** (XAML/MVVM, themeable, cross-platform), with a visible **debugger console** panel. Every action is triggerable from the GUI.
- CLI remains as a second thin front end over the same `ExperimentRunner`.

## User stories (draft)
- A. Learn a new game: add a game class and register it, run `train --game <name> --method ppo --seeds 5`; done when 5 run folders and a steps-to-solve report exist.
- B. Does shorter training still work?: copy baseline config, halve budget, run both over 5 seeds on the same held-out seeds; done when side-by-side report shows verdict vs target.
- C. Vector baseline teaches next version: select vector-v1, click "Train successor", change one thing (e.g., network 256->128); done when vector-v2 appears with lineage and score next to its parent's.
- D. Vector training feeds vision training: select a finished vector model, "Train vision student"; teacher plays headless while frames and actions are recorded; done when "vision-v1 from vector-v1" is saved with a comparison against vision-from-scratch.
- E. Find the minimum information: tick features/data percent to drop, "Run sweep"; done when a table shows each variant vs baseline with pass/fail.
- F. Watch an agent play: pick a model and seed, "Play"; render with action and confidence; pause, step, replay identical seed.
- G. Debug a failing run: console filters by run/component/severity; shows config, seeds, metrics; "Re-run with same seed" button.
- H. Theme the app: pick a theme in Settings; UI, charts and console change without restart.
- I (employer): read the repo, see results, rerun them with one command.

## Working agreement with the AI
- Human owns architecture; AI owns implementation inside it and flags anything that would change the architecture.
- Tests first, in small features; human reviews tests, not just implementation.
- Short design docs only; avoid thick upfront specs.
- Review diffs; ask "why" when surprised.

## Still undecided
- Exact target score per game.
- Specific harder games beyond GridWorld/CartPole.
- Solution/project layout for the Avalonia GUI project and the service layer.
- First milestone and its tests (suggested: `ILearner` + `Evaluator` + seeded RNG plumbing, then PPO on GridWorld).
