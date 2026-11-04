# GameTrainerSharp

**C# port of [GameTrainer](https://github.com/Showerss/GameTrainer) (now the deprecated reference): teach a computer to play games by trial and error, with game-swapping as a config change.**

The Python original proved the concept. This repo rebuilds it in C# with two deliberate style choices:

- **Functional core** — game rules, rewards, and vision parsing are pure functions over immutable records (`GridWorldRules.Step`: state in, new state out). No mocks needed to test a rule.
- **Imperative shell** — OS touch-points (screen capture, key presses) and the PPO brain live behind interfaces (`IEyes`, `IHands`, `IPolicy`) as plain stateful classes.

See `docs/design-plan.md` for the full OOP-vs-functional comparison this layout was chosen from.

## The contract

Every game keeps one promise (`IEnvironment<TObs, TAct>`):

```csharp
TObs Reset();
StepResult<TObs> Step(TAct action);   // observation, reward, terminated, truncated, steps
```

If a game keeps that promise, any learner can play it. (This is Gymnasium's `reset()`/`step()`, spelled in C#.)

## Layout

```
src/
  GameTrainer.Core/    StepResult, IEnvironment, IPolicy — the seams
  GameTrainer.Games/   GridWorld (pure rules + thin stateful shell + RandomStart decorator)
  GameTrainer.Runner/  composition root; random-policy baseline runner
tests/
  GameTrainer.Tests/   xUnit — pure-rule tests need no mocks
```

## Milestone 0 (this commit)

- [x] Solution scaffold, the `IEnvironment` contract, `StepResult`
- [x] GridWorld ported from `gridworld.py` (5x5, walls clamp, −0.01 step cost, +1.0 goal, 100-step cap)
- [x] `RandomStartEnvironment` decorator (random start, goal off the corner — unsolvable blind)
- [x] Random-policy runner reproducing the Python repo's measured baselines:

| Task | Python baseline | C# M0 measured |
| :--- | :--- | :--- |
| GridWorld (fixed start) | random ≈ −0.09 | **−0.07** |
| GridWorld vision task (25-step cap) | random ≈ 53% goal rate | **54.3%** |

## Run it

```bash
dotnet test                                        # 12 tests
dotnet run --project src/GameTrainer.Runner        # fixed-start baseline
dotnet run --project src/GameTrainer.Runner -- gridworld-vision
```

## Next milestones

1. **PPO brain** via TorchSharp (`IPolicy` implementation) — full port, no Python at runtime
2. CartPole physics env (validates PPO against the known M1 result: 22 → 500)
3. Eyes/hands: `IEyes`/`IHands` with one implementation per OS
4. YAML profiles + factory (swap games with zero code edits)
5. Minesweeper on a real desktop app

## License

MIT — see [LICENSE](LICENSE).
