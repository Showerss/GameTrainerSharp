# Tasks

## 1. Project scaffolding

- [ ] 1.1 Create `src/GameTrainer.Live` (`net10.0`, references Core, `AllowUnsafeBlocks` if needed for interop), add it to `GameTrainerSharp.slnx`, and reference it from Games, Runner and Tests; verify `dotnet build` succeeds
- [ ] 1.2 Add `StbImageSharp` to `tests/GameTrainer.Tests` only and verify `dotnet build` succeeds and no `src/` project references an image package

## 2. Generic live environment (engine + seams)

- [ ] 2.1 Add `Frame` (BGRA buffer, width, height, stride), the platform-neutral `Key` enum, `GameWindow`, and the `IWindowLocator`, `IScreenCapture` and `IInputSender` seams per design D2; verify they compile with no OS references
- [ ] 2.2 Add `LiveGameProfile`, `KeyStep`/action definitions and the `IGameReader` contract (state, reward, terminated, optional vector encoding, optional probe, "unreadable frame" error type) per design D3; verify they compile
- [ ] 2.3 Implement `LiveWindowEnvironment`: find window, focus-then-send actions, settle, capture with retry-until-timeout (D6), reward/termination/truncation via the reader, and one-frame-per-step observation caching. Verify with unit tests using a scripted fake capture and recording input sender, covering: standard contract usage, transient unreadable frame recovered, persistent unreadable frame failing with the reader's error, reset sequence + step count 0, step-cap truncation, missing window error naming the title, focus failure sending no keys, and out-of-range action sending no keys
- [ ] 2.4 Implement generic visual observation (optional region crop, area-average downscale, BGRA to RGB CHW 0..1) per D7; verify unit tests for shape, value range, and identical shape from two different input frame sizes
- [ ] 2.5 Implement vector-size-0 behaviour for profiles without a state reader; verify a unit test that the spec advertises 0 and that vector observation requests fail clearly
- [ ] 2.6 Add `NullInputSender` and `FrozenCapture`; verify unit tests that null input sends nothing through any action sequence and frozen capture always returns the stored frame
- [ ] 2.7 Add the runtime backend selector that returns the Windows backend on Windows and throws an error naming the platform elsewhere; verify a unit test that runs on Linux CI and asserts the error

## 3. Windows backend

- [ ] 3.1 Implement the Windows window locator (`EnumWindows` on the `Default` desktop, title-contains match, `DwmGetWindowAttribute` extended frame bounds) and per-monitor-v2 DPI awareness setup; verify it compiles on Linux and is marked `[SupportedOSPlatform("windows")]`
- [ ] 3.2 Implement Windows screen capture (GDI `BitBlt` of the window bounds into a `Frame`); verify it compiles on Linux, and verify on Windows that a capture of an open window produces a non-empty frame of the window's size (manual, recorded in task 7.1)
- [ ] 3.3 Implement the Windows input sender (`SendInput`, `Key` to virtual-key mapping, chord, `AttachThreadInput` + `SetForegroundWindow` focus with foreground verification); verify a cross-platform unit test that the interop `INPUT` struct is 40 bytes on x64

## 4. LibreMines game

- [ ] 4.1 Copy the nine PNG fixtures from `../GameTrainer/tests/fixtures/` to `tests/GameTrainer.Tests/Fixtures/LibreMines/` with copy-to-output, plus the expected 8x8 grid for `board_easy_01.png` from `../GameTrainer/tests/test_minesweeper_vision.py`; verify the tests can load each fixture into a `Frame`
- [ ] 4.2 Port the cell classifier from `minesweeper_vision.py` (twelve states, the three backgrounds including cursor tint, measured BGR palette, ink thresholds, unknown-cell error naming row/col) per D8; verify fixture tests pass for every single-cell fixture and an unknown-appearance test fails with the cell's position
- [ ] 4.3 Port board location (largest square solid dark component, own connected-component labelling per D8) and board reading; verify `board_easy_01.png` reads exactly as the expected grid, a scaled copy of it reads the same, and a frame with no board fails with board-not-found suggesting an Easy game
- [ ] 4.4 Implement LibreMines rewards and termination (+1 per new safe cell, -10 mine, +10 win at 54 safe, values held in the profile); verify unit tests for 7-cell reveal = +7, mine = -10 + terminated, last safe cell = +10 + terminated, and flag/move/no-op = 0
- [ ] 4.5 Implement the cursor tracker (reset to (0,0), clamp at edges) and the 832-value vector encoding; verify unit tests for edge clamping, cursor after a move sequence, and vector length 832 with exactly 65 ones
- [ ] 4.6 Define the LibreMines profile (title "LibreMines", six actions W/S/A/D/O/P in spec order, reset = Ctrl+R then settle then one activation key, settle defaults 0.25 s / 0.5 s, max 200 steps, visual 3x96x96 from the located board, probe "down, right, right, flag" expecting (1,2) hidden to flagged), the `libremines-easy` descriptor, and its `BuiltInGames` entry; verify tests that it is listed, its spec reports 832 vector / 3x96x96 / 6 actions, and registering it in a temp catalog works with no window present
- [ ] 4.7 Run the full LibreMines environment end to end against a scripted fake capture that replays fixture frames; verify a unit test that a reset + probe sequence produces the expected states, rewards and observations

## 5. Runner commands

- [ ] 5.1 Implement the four control decision rules as pure functions over measurements, following `check_hands.py`; verify unit tests for pass and fail cases of each control and the overall verdict
- [ ] 5.2 Implement `live-check --game <id>` (DPI setup, measurement collection with real and null input and frozen capture, evidence printing, NOT RUN on missing window/backend/reader/probe, exit codes 0/1/2); verify a test that runs it on a non-live setup and asserts NOT RUN plus exit code 2
- [ ] 5.3 Implement `live-random --game <id> --episodes <n> [--version]` (per-episode metric point + log line, final version entry with no weights file, partial results kept on failure); verify a test using a fake-backed environment that metrics, log and the catalog entry are written and no weights file exists
- [ ] 5.4 Make the existing training command fail clearly when `--modality Vector` is requested for a game whose vector size is 0, and verify a test for that error; also verify a test that naming `random-baseline` as `--parent` fails with "weights file not found" before training

## 6. Documentation

- [ ] 6.1 Add a live-environment section to `README.md` (requirements: Windows, unlocked desktop, LibreMines v2.3.0 open on Easy; the `live-check` and `live-random` commands) and the architecture to `docs/design-plan.md`; verify the documented commands match the Runner's actual arguments

## 7. Live verification (Windows, manual)

- [ ] 7.1 With LibreMines v2.3.0 open on an Easy board, run `live-check --game libremines-easy` and verify all four controls PASS. Record date, platform, evidence and wall-clock in `docs/walkthrough.md` (or a results section), including the window size the capture produced
- [ ] 7.2 Verify live that moving up/left from (0,0) and down/right from (7,7) leaves the LibreMines cursor at the edge (clamp); if it wraps instead, stop and update the spec and tracker before continuing
- [ ] 7.3 Run `live-random --game libremines-easy --episodes 10` unattended, then open the dashboard and verify `random-baseline` shows 10 episodes, mean reward, win rate, curve and log

## Workflow follow-up

- Consider archiving `generational-trainer-and-dashboard` first, so its specs exist in `openspec/specs/` before this change is archived.
- Archive this change after live verification passes.
