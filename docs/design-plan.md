# GameTrainerSharp — Design Plan: OOP vs Functional Layouts

> **SUPERSEDED (2026-11-04):** The M0 decision below (Option C, hybrid) was
> overturned in a later grill session. Final decision: **full classic OOP** with
> FP habits only. See `docs/DESIGN.md` — Style. This file is kept for history;
> it explains the options considered, but do not implement from it.

Planning document for porting `Showerss/GameTrainer` (Python) to C#.

**Decision (M0): Option C — hybrid "functional core, imperative shell".** *(superseded — see banner above)*
Options A and B are kept below for reference; they explain why the code
looks the way it does.

---

## 1. What the Python repo actually is

The repo has three responsibilities, cleanly separated by the Eyes/Brain/Hands metaphor:

| Python module | Responsibility | Nature of the code |
|---|---|---|
| `gridworld.py`, `minesweeper.py` | Game rules + state transitions | **Pure logic** (state in, new state + reward out) |
| `perception.py`, `screen.py`, `minesweeper_vision.py` | Eyes — read pixels → structured observation | Impure I/O + pure parsing |
| `input.py` | Hands — press real OS keys | **Pure side effect** |
| `referee.py`, `rewards.py` | Judge outcomes, compute reward | **Pure logic** |
| `factory.py`, `profile.py` | Wiring — build env from YAML config | Composition root |
| `training.py` | The RL loop (reset/step with PPO) | Orchestration |
| `logger.py`, `tui.py` | Observability | Side effects |

Key insight: **the interesting logic is already mostly pure** (game rules, rewards,
vision parsing). The impure parts are thin edges (screen capture, key presses,
neural net). This is exactly the shape the *functional core, imperative shell*
pattern was invented for — which is why the OOP-vs-functional question is worth
asking here at all.

---

## 2. Option A — Classic OOP layout

Interfaces at every seam, one class per implementation, composition via
constructor injection. This is the direct transliteration of the Python design
(and matches your existing ADR-0005 "one class per OS").

```
src/
  GameTrainer.Core/
    Abstractions/
      IEnvironment.cs        // Reset() / Step(action) -> StepResult
      IEyes.cs               // Capture() -> Observation
      IHands.cs              // Press(Key), Click(x, y)
      IReferee.cs            // Judge(board) -> Verdict
      IPolicy.cs             // Act(observation) -> Action  (the brain's face)
    Domain/
      StepResult.cs          // record: Observation, Reward, Done, Info
      Observation.cs
      Action.cs
      Profile.cs             // YAML-deserialized config
  GameTrainer.Environments/
    GameEnvironmentBase.cs   // template: owns IEyes/IHands/IReferee
    GridWorldEnvironment.cs
    CartPoleEnvironment.cs
    MinesweeperEnvironment.cs
  GameTrainer.Perception/
    ScreenCapture.cs
    MinesweeperVision.cs
  GameTrainer.Input/
    IHands.Windows.cs        // SendInput
    IHands.Mac.cs            // CGEvent
  GameTrainer.Brain/
    PpoPolicy.cs             // TorchSharp
    RandomPolicy.cs
  GameTrainer.Runner/        // composition root: reads profile YAML, wires
                             // everything with a small DI container or by hand
tests/
  GameTrainer.Tests/
```

**The seam test:** `GridWorldEnvironment : GameEnvironmentBase` gets `IEyes`/`IHands`
injected; in tests you inject fakes. Training loop depends only on `IEnvironment`.

### Strengths
- Direct 1:1 mapping from the Python repo — easy to cross-reference while porting.
- Interfaces make the "swap a game = swap a config" promise explicit in the type system.
- Familiar to most C# devs; DI tooling (Microsoft.Extensions.DependencyInjection) is standard.
- Mocking seams (IEyes/IHands) is trivial with Moq or hand-rolled fakes.

### Weaknesses
- Game *rules* buried inside stateful classes: `env.Step()` mutates hidden fields.
  Testing a rule means constructing an environment, not calling a function.
- Inheritance (`GameEnvironmentBase`) couples games to a template — the Python
  repo deliberately chose a factory over this (ADR-0001).
- Side effects can hide anywhere; nothing in the types tells you what's pure.

---

## 3. Option B — Functional layout

Environments as **values**, transitions as **pure functions**, side effects pushed
to the edges and composed explicitly. C# supports this well: records, discriminated
unions (via `OneOf` or base-record pattern matching), LINQ, function types.

```csharp
// An environment is not an object. It's a pair of functions over immutable state.
public record Env<TState, TObs, TAct>(
    Func<Random, TState>            Reset,
    Func<TState, TAct, (TState Next, float Reward, bool Done)> Step,
    Func<TState, TObs>              Observe);

// GridWorld rules: pure, testable in one line
public static class GridWorld
{
    public static (GridState Next, float Reward, bool Done) Step(GridState s, Move m) =>
        // no mutation, no I/O — pattern matching over a small state record
        ...;
}

// The training loop is a fold over an infinite action stream
public static TrainingResult Run<TState, TObs, TAct>(
    Env<TState, TObs, TAct> env, IPolicy<TObs, TAct> policy, int episodes) { ... }
```

Side effects live in explicitly-typed shells:

```csharp
// Hands/eyes are just functions returning "descriptions" of effects,
// interpreted at the program edge — or, pragmatically, plain IO functions
// passed in as delegates:
public record RealWorld(Func<Screen> GrabScreen, Action<KeyPress> SendKey);
```

```
src/
  GameTrainer.Core/
    Env.cs                   // Env<TState,TObs,TAct> + StepResult as DU
    Training.fs-style loop   // pure orchestration, effects injected
  GameTrainer.Games/
    GridWorld.cs             // static class, pure functions, record state
    CartPole.cs              // pure physics step
  GameTrainer.Vision/        // pure: Pixels -> Board parse
  GameTrainer.Effects/       // Screen, Keyboard — the only impure file
  GameTrainer.Brain/         // PPO (inherently stateful — honest about it)
  GameTrainer.Runner/        // composition: build Env values from YAML
```

### Strengths
- Game rules become **pure functions**: testing `GridWorld.Step` needs no mocks,
  no setup — input state in, assert output. Your M2/M3 result tables become
  property-based tests almost for free.
- Immutability kills a whole class of bugs (training loop mutating game state
  mid-step, reward computed from stale board).
- The training loop is written **once**, generically — "swap a game" = pass a
  different `Env` value. Stronger than the Python version's promise.
- Vision parsing (pixels → board) is a pure pipeline — matches ADR-0003's
  "judge by cells, not pixels" philosophy in the type system.

### Weaknesses
- Non-idiomatic for a lot of C# codebases; fewer examples to copy from.
- PPO/TorchSharp is inherently mutable — the functional purity stops at the
  brain's door, so you must be disciplined about the boundary.
- Real-time interaction (watch it play Minesweeper live) is more awkward to
  express as folds/streams than as a mutable loop.
- Steeper learning curve — though that's arguably a *feature* for you right now.

---

## 4. Option C — Hybrid: "functional core, imperative shell" (recommended)

The industry-standard answer for exactly this shape of program, and honestly
what your Python repo already *is* spiritually:

- **Pure core** (Option B style): game rules, rewards, vision parsing, referee.
  Records + static pure functions. No I/O, no mutation. 100% unit-testable.
- **OOP shell** (Option A style): `IEyes`/`IHands` interfaces for OS effects,
  one class per OS (keeps ADR-0005), a small mutable `EnvironmentRunner` that
  owns the live loop, and PPO as a plain stateful class (it *is* mutable state).
- The contract between them is the `Env<TState,TObs,TAct>` value from Option B —
  so you still get "training loop written once, games are data."

```
Pure core (no dependencies):   GridWorld.Step, Rewards.Compute, Vision.ParseBoard
Interfaces (the seams):        IEyes, IHands, IPolicy
Shell (all the I/O):           ScreenCapture, WindowsHands, PpoPolicy, Runner
```

### Why this fits your repo specifically
| Milestone from the Python repo | What the hybrid gives it |
|---|---|
| M2/M3 (learns grid game, from pixels) | Pure `Step` + pure `ParseBoard` → trivially testable |
| M4 (swap games, zero code edits) | `Env` values + YAML profile → composition root only |
| M5 (real desktop app, per-OS hands) | `IHands` interface, one impl per OS (ADR-0005 survives) |
| PPO brain | Honest mutable class; purity boundary is explicit |

---

## 5. Decision points for review

1. **Core style:** pure functions (B/C) vs stateful classes (A)?
2. **Seam style:** interfaces + DI (A/C) vs injected delegates (B)?
3. **Learning goal:** if the point is *learning OOP*, A is the richer exercise.
   If the point is *learning functional*, B. C teaches both and is what most
   production C# actually looks like.
4. **Brain:** all three options treat TorchSharp PPO as a stateful class. No
   controversy there.

## 6. Proposed next step

Whichever you pick, milestone 0 of the new repo is identical:
solution scaffold + `StepResult` + GridWorld (the smallest game) + training
loop with a random policy + tests + CI. That commit is small enough to review
in one sitting and proves the chosen architecture end-to-end.
