# GameTrainerSharp — Turnover / Session Handoff

Last updated: 2026-11-08. Read this first when resuming work in a fresh chat.

## How to resume

Paste this into a new session:

> Repo: Showerss/GameTrainerSharp. Read docs/design-plan.md (architecture),
> docs/DESIGN.md (decisions and open questions), and docs/TURNOVER.md (this
> file — current state and next actions). Changes go through OpenSpec, tests
> first, in small slices. Next action is at the bottom of TURNOVER.md.

## Where things stand

- **Milestone 0 done**: solution scaffold, GridWorld, random baselines, CI
  (`.github/workflows/tests.yml`).
- **Generational trainer and dashboard done** (OpenSpec change archived
  2026-11-08):
  - `IGameEnvironment` dual-modal contract (int actions, vector + visual
    observations per tick), `IGameDescriptor`, `EnvironmentSpec`.
  - `CatalogStore`: `catalog.json` + `runs/<game>/<version>/` with `model.pt`,
    `metrics.jsonl`, `training.log`.
  - `GameTrainer.Brain`: vector/visual encoders + shared 64-dim
    `DecisionHead`; `GenerationalPpoTrainer` with Scratch, Coach, and
    CoachAndCopyHead modes.
  - `GameTrainer.Gui`: Windows 2000-styled Avalonia observatory.
- **In flight**: `openspec/changes/live-window-environment` — generic live
  game window environment, Windows backend, LibreMines as the first live game.

## Key decisions (summary — details in design-plan.md and DESIGN.md)

- Functional core, imperative shell; two-layer dual-modal seam.
- `Done` = computed (`Terminated || Truncated`).
- GridWorld: 5x5 open grid, -0.01/step +1.0 goal, 25-step cap, observation
  `[agentX, agentY, goalX, goalY]` normalized 0-1, edge bump costs a step
  without movement.
- Generational learning: vector parent coaches a visual child via decaying KL;
  CoachAndCopyHead also transplants the parent's `DecisionHead` weights.
- GridWorld target = >= 95% goal rate over >= 5 seeds; a generation passes if
  it hits target AND lands within 2 points of its parent.

## Working agreement

- Human owns architecture; AI implements inside it and flags anything that
  would change the architecture.
- Tests first, in small features; human reviews tests, not just implementation.
- Interaction style the human prefers: **questions asked one at a time, short
  and simple, each with a recommendation tied to the project goals.**

## Next actions (in order)

1. Implement `openspec/changes/live-window-environment` (tasks.md), tests first.
2. Run the live verification checks against LibreMines on Windows.
3. Work through the open questions at the bottom of DESIGN.md.
4. Train GridWorld to the 95% gate and record vector → visual generations.
