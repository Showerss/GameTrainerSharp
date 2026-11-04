# GameTrainerSharp — Turnover / Session Handoff

Last updated: 2026-11-04. Read this first when resuming work in a fresh chat.

## How to resume

Paste this into a new session:

> Repo: Showerss/GameTrainerSharp. Read docs/DESIGN.md (the locked spec) and
> docs/TURNOVER.md (this file — current state and next actions). We work
> docs-first: design doc -> tests -> implementation, in small slices.
> Ask clarifying questions one at a time with a recommendation before
> implementing anything ambiguous. Next action is at the bottom of TURNOVER.md.

## Where things stand

- **Milestone 0 done**: solution scaffold, `IEnvironment`/`IPolicy`/`StepResult`
  contracts, GridWorld (rules, rewards, random-start variant), console runner
  with RandomPolicy, GridWorld tests, CI (`.github/workflows/tests.yml`).
- **Two grill sessions completed** (2026-11-04): ~24 design decisions locked and
  written into `docs/DESIGN.md`. That file is the single source of truth.
- `docs/design-plan.md` is **superseded** (banner at top). Do not implement from it.

## Key locked decisions (summary — details in DESIGN.md)

- Full classic OOP (FP habits only). Junior-reviewable code, simple data
  structures, XML doc comments on all public types/members.
- `StepResult.Done` = computed (`Terminated || Truncated`). Learner budget is in
  environment steps.
- GridWorld: 5x5 open grid, -0.01/step +1.0 goal, 25-step cap, observation
  `[agentX, agentY, goalX, goalY]` normalized 0-1, edge bump costs a step
  without movement, random non-overlapping agent/goal starts.
- Evaluation: checked-in 20-seed held-out set per game; one seed fully
  determines an episode; GridWorld target = >= 95% goal rate over >= 5 seeds.
- Distillation: weights never transfer (teacher teaches behavior via DAgger);
  a generation passes if it hits target AND lands within 2 points of its
  parent; lineage stores the parent/child config diff.
- Milestone 1 locked: `ILearner` + `Evaluator` + seeded RNG plumbing, **no
  neural net code**.
- Fresh runs only (no checkpoint resume); encoder settings saved in checkpoint;
  in-process service layer; GUI watch-play drives a live env.

## Working agreement (from DESIGN.md)

- Human owns architecture; AI implements inside it and flags anything that
  would change the architecture.
- Tests first, in small features; human reviews tests, not just implementation.
- Short design docs; review diffs; ask "why" when surprised.
- Interaction style the human prefers: **questions asked one at a time, short
  and simple, each with a recommendation tied to the project goals.**

## Next actions (in order)

1. **Finish the open-questions round** — the "Still undecided" list at the
   bottom of DESIGN.md, top priority first:
   - Approve/adjust proposed signatures:
     `ILearner { IPolicy<TObs,TAct> Train(IEnvironment<TObs,TAct> env, int stepBudget); }`,
     `Evaluator { EvalResult Evaluate(IPolicy<TObs,TAct> policy, IReadOnlyList<int> evalSeeds); }`,
     `IObservationEncoder<TRaw,TEnc> { TEnc Encode(TRaw raw); }`
   - Eval cadence during training (every N steps vs end only)
   - "Steps to solve" definition (first pass vs K consecutive passes)
   - RNG derivation rule (master seed -> component seeds)
   - Run folder layout (filenames, metrics CSV columns)
   - Config JSON schema (fields, defaults)
   - Aggregation stats (std vs min/max; final vs best eval)
   - Then: PPO config surface, failure policy, GUI v1 scope, "confidence" def.
2. **Write Milestone 1 tests first** (after signatures are locked):
   - Seeded RNG: same master seed -> identical component streams; different
     components get independent streams; no global `Random`.
   - Evaluator: fixed seed list produces identical scores across two runs;
     a known-strength stub policy scores as expected.
   - ILearner contract: a trivial learner (e.g., random or tabular) respects
     the step budget and returns a working policy.
3. **Implement Milestone 1** against those tests.
4. Milestone 2: PPO on GridWorld to the 95% gate.

## Session artifacts

- Working clone used by the last session: `/tmp/GameTrainerSharp` (may not
  exist in a new session — just re-clone).
