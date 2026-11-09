# Proposal

## Why

Every environment GameTrainerSharp can run today is a simulator written in C# (`GridWorld`). Most games worth training on can't be simulated by hand, so the project needs a **game-agnostic way to attach to a real game window**: see it through screen capture, act on it through OS input, and score it by reading the screen. The Python sibling project proved this works on LibreMines in its M5 milestone, but none of it exists in C#. This change ports that capability natively and generalizes it, so later changes can train on live windows (including long unattended runs inside a Windows VM).

## What Changes

- Add a generic **live game environment** that implements the existing `IGameEnvironment` contract against a real game window. It finds the window by title, captures frames, sends keyboard input, and runs reset/step through a per-game profile. Trainers, catalog and dashboard use it unchanged.
- Put screen capture and input behind platform-neutral seams. Ship a **Windows backend only**. Other platforms are deferred, but the seam is designed for them.
- Add a **per-game profile** model: window title, action map (each action is a key, a chord, or a short key sequence), reset procedure, settle timings, step cap, a reward/termination reader, and an optional state reader.
  - **Visual** observations come from raw window pixels, so every live game gets them with no game-specific code.
  - **Vector** observations exist only when a profile supplies a state reader.
- Add **LibreMines (v2.3.0, Easy 8x8, 10 mines)** as the first live game and register it as a built-in game. It ports M5's board reader (measured palette, fail-loudly rules), keyboard controls (W/A/S/D cursor, O reveal, P flag, Ctrl+R reset) and reward rules (+1 per safe cell, -10 mine, +10 win).
- Add a **live verification run** to the Runner CLI. It runs the four M5 controls against the live window (keys live, null input, frozen frame, 20x unattended reset), then a random-agent smoke run whose metrics and log go to the catalog, so the result is visible in the existing dashboard.
- Copy the M5 board fixtures into this repo so the board reader is tested on every CI run (Linux) without a live window.

## Capabilities

### New Capabilities
- `live-game-environment`: Attaching to a real game window and exposing it as a standard game environment. Covers window discovery, frame capture, input delivery, focus handling, profile-driven reset/step, settle timing, visual/vector observation production, and loud failure on unreadable or missing windows.
- `libremines-game`: The LibreMines Easy game profile. Covers board reading from pixels, cell states, cursor-based action map, reset procedure, reward and termination rules, and registration as a built-in game.
- `live-environment-checks`: Command-line verification that a live environment really works: the four negative-controlled checks and a random-agent smoke run recorded to the catalog.

### Modified Capabilities

(none; the LibreMines game is added through the existing `dual-modal-environment` descriptor contract, and the baseline run is recorded within the existing `model-catalog-store` requirements, so no existing requirement changes)

## Impact

- **New project `src/GameTrainer.Live`**: the generic live environment, profile model, capture/input seams, and the Windows backend (Win32 interop, no new runtime packages).
- **`src/GameTrainer.Games`**: new `LibreMines/` folder (profile, board reader, rewards); `BuiltInGames` gains the LibreMines descriptor; it gains a reference to `GameTrainer.Live`.
- **`src/GameTrainer.Runner`**: new live-check / live-random commands.
- **`tests/GameTrainer.Tests`**: board-reader tests on copied PNG fixtures, and live-environment tests using fake capture/input. Adds a small PNG-decoding package for tests only.
- **Runtime requirements (Windows):** an unlocked, interactive desktop session with LibreMines v2.3.0 open on an Easy board. CI stays on Linux, so live checks run manually.
- **Docs:** `README.md` and `docs/design-plan.md` gain a short live-environment section.
- **Out of scope:** training on the live window, a Minesweeper simulator, the GUI cockpit, demo recording, VM setup, Medium/Expert, mouse input, and non-Windows backends.
