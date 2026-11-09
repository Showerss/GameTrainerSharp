# Design

## Context

See proposal.md (Why). Facts that shape the approach:

- `IGameEnvironment` (Core) is synchronous: `Reset()`, `Step(int)`, `GetVectorObservation()`, `GetVisualObservation()`, plus a static `EnvironmentSpec`. `IGameDescriptor` creates environments, and `BuiltInGames` (Games) is the registry the Runner, catalog and GUI read.
- All projects target `net10.0`. CI runs `dotnet test` on **ubuntu-latest**, so anything Windows-only must compile on Linux, and its tests must not need a window.
- The Python reference (`../GameTrainer`, M5, verified PASS on Windows 11 on 2026-09-15) provides:
  - `screen.py`: find the window by title via `EnumWindows`; its bounds via `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`; per-monitor-v2 DPI awareness; `mss` screen grab of that rectangle.
  - `input.py`: `SendInput` with a 40-byte x64 `INPUT` struct; focus via `SetForegroundWindow` + `AttachThreadInput`; W/A/S/D/O/P and the Ctrl+R chord.
  - `minesweeper_vision.py`: board found as the largest square, solid dark connected component on the near-white window background; middle 60% of each cell sampled; three flat backgrounds (hidden 70, cursor 185, revealed 26 grey); measured digit colours (BGR); ink thresholds; raises rather than guesses.
  - `rewards.py`: +1 per new safe cell, -10 mine, +10 win, 54 safe cells.
  - `check_hands.py`: the four controls.
- M5 lessons that become design constraints:
  - The first W/A/S/D press only activates cursor mode at (0,0).
  - Ctrl+R plays a ~0.25 s fade, and mid-fade frames are unreadable. M5 used 0.25 s/step and 0.5 s/reset settle times.
  - Agent subshells on Windows can land in an isolated desktop, so binding to the `Default` desktop was needed.
  - Full-window vision cost ~133 ms/frame. Cropping to the board was the noted fix.

## Goals / Non-Goals

**Goals:**
- One generic live environment that a new game plugs into with a profile plus at most two small readers, and no engine changes.
- A capture/input seam that a later Linux/X11 backend can implement without touching the environment or profiles.
- M5 parity on LibreMines Easy, proven by the same four controls.
- Board-reader correctness locked by fixture tests that run in CI on Linux.

**Non-Goals:**
- Training performance or any learning result on the live window. The existing trainer may technically accept `--game libremines-easy`, but that path is not validated here.
- Running with the window occluded, minimized, or with the desktop locked.
- Mouse input, multiple simultaneous game instances, and theme or difficulty variants of LibreMines.

## Decisions

### D1. New `GameTrainer.Live` project holds the engine and the Windows backend

```
  GameTrainer.Core      IGameEnvironment, EnvironmentSpec, VisualFrame (unchanged)
        ^
  GameTrainer.Live      LiveWindowEnvironment, LiveGameProfile, IScreenCapture,
        ^               IInputSender, NullInputSender, FrozenCapture,
        |               Windows/ (Win32 interop, [SupportedOSPlatform("windows")])
  GameTrainer.Games     LibreMines/ (profile, board reader, rewards), BuiltInGames
        ^
  GameTrainer.Runner    live-check, live-random
```

A separate project keeps OS interop out of Core and Games, and gives the future Linux backend a natural home (`Live/X11/`). The Windows backend is compiled on every platform (P/Invoke compiles anywhere) and chosen at runtime via `OperatingSystem.IsWindows()`, so there's no `-windows` TFM split.
*Alternatives:* a separate `GameTrainer.Live.Windows` project (more projects for no gain yet); putting it in Games (mixes rules with OS glue).

### D2. Seams: `IScreenCapture` and `IInputSender`, plus a window handle abstraction

- `IScreenCapture.Capture(window) -> Frame` returns a BGRA byte buffer with width, height and stride. `Frame` is a plain type in Live, not a bitmap library type.
- `IInputSender` exposes `Focus(window)`, `Tap(key)` and `Chord(modifier, key)`. Keys use a small platform-neutral `Key` enum that each backend maps to its own codes (the same idea as Python's VK to Quartz table).
- `IWindowLocator.Find(titleContains) -> GameWindow` (handle + bounds).
- Test and verification doubles: `NullInputSender` (records but sends nothing), `FrozenCapture` (returns one stored frame), and an in-memory scripted fake capture for unit tests.

*Windows backend:* window discovery via `EnumWindows` on the `Default` desktop; bounds via `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`; capture via GDI `BitBlt` from the screen DC over those bounds (equivalent to M5's `mss`); input via `SendInput` with `AttachThreadInput` + `SetForegroundWindow` focus, then verifying `GetForegroundWindow()` before sending. The process sets per-monitor-v2 DPI awareness once at startup.
*Alternatives:* `PrintWindow(PW_RENDERFULLCONTENT)` can capture occluded windows, but it's slower, unreliable for some GPU-rendered apps, and pointless while `SendInput` needs the window in front anyway. `Windows.Graphics.Capture` is faster but needs WinRT projections and shows a capture border. Both remain options behind the same seam.

### D3. Profiles are C# objects, not data files

`LiveGameProfile` is a C# record:
- `WindowTitleContains`
- an ordered `Actions` list (name + key steps)
- a `Reset` key-step sequence
- `ActionSettle` / `ResetSettle` / `ReadTimeout` durations
- `MaxSteps`
- `VisualShape`, plus an optional `VisualRegion(Frame) -> Rect`
- `IGameReader`: frame to game state, reward, terminated, an optional vector encoding, and an optional verification probe

Game-specific readers (board parsing, reward rules) are code no matter what, so a data file would only hold half the profile. Records keep everything in one place and match how `BuiltInGames` already registers games.
*Alternative:* YAML/JSON profiles. Defer them until the cockpit's "add a game" feature needs them. The record shape is designed so its non-code fields can be serialized later.

### D4. Action space: M5's six keyboard actions, with an engine that supports key sequences

LibreMines ships with the proven six actions (W/A/S/D/O/P). The engine treats every action as a *sequence* of key steps, so a later change can add a "reveal cell (r,c)" action map (64 or 128 actions, delivered as cursor-walk macros from the tracked cursor) without engine changes.
*Why not direct-cell actions now:* this change's bar is M5 parity, proven by the four controls. Changing the action space at the same time would mix two unknowns. Learnability is the next change's concern.
*Why not mouse:* keyboard is proven, coordinate-free, and DPI-independent.

### D5. Cursor is tracked by the reader from actions, not from pixels

After reset's activation key the cursor is at (0,0). Each move updates a tracked position clamped to 0..7. The cursor tint (185 grey) is accepted as "hidden" by the board reader, as in M5, but isn't used to locate the cursor, because a cursor on a revealed cell has no measured appearance. Edge clamping is assumed and verified live (tasks 5.x).

### D6. Settle-then-retry reading

After each action: sleep `ActionSettle`, capture, read. If the reader throws its "unreadable" error type, re-capture every ~50 ms until `ReadTimeout` (default 1.5 s), then rethrow. The settle defaults are M5's (0.25 s / 0.5 s). The retry absorbs the Ctrl+R fade without inflating every step's delay. `BoardNotFound` and focus errors aren't retried: they mean the setup is wrong, not that the frame is in motion.

### D7. Observations

- **Visual (generic):** crop to `VisualRegion` (LibreMines: the located board; default: the whole window), area-average downscale to `VisualShape`, BGRA to RGB CHW floats 0..1. Done in plain C# (no image library) to keep runtime dependencies at zero.
- **Vector (LibreMines):** 64 x 12 one-hot states followed by a 64 one-hot cursor = 832 floats. One-hot avoids implying that "hidden" (9) is numerically near "8". The cursor is included because cursor actions are meaningless without it.
- Both are computed from the single frame captured at the end of the step, and cached until the next step (spec: one frame).

### D8. Board reader is a faithful port, with one replacement

Every threshold, colour and rule from `minesweeper_vision.py` is ported as-is. Note that Python works in BGR and the C# frame is BGRA, so colour constants keep BGR order and are read accordingly. OpenCV's `connectedComponentsWithStats` is replaced by a small two-pass 8-connected labelling over a downsampled dark mask (board-sized blobs survive downsampling, and it's cheaper). Fixture tests are the arbiter: if a fixture reads differently from Python, the port is wrong.

### D9. Fixtures and PNG decoding in tests only

The nine PNGs from `../GameTrainer/tests/fixtures` are copied to `tests/GameTrainer.Tests/Fixtures/LibreMines/`, along with the expected grid for `board_easy_01.png`, taken from the Python test. Tests decode them with `StbImageSharp` (public domain, pure managed) into the same `Frame` type. No image package enters `src/`.

### D10. Runner commands and the baseline catalog entry

- `live-check --game <id>`: four controls implemented generically over `IGameReader` state equality and the profile's probe, following `check_hands.py`'s decision rules. Exit codes: 0 = PASS, 1 = FAIL, 2 = NOT RUN.
- `live-random --game <id> --episodes <n> [--version random-baseline]`: random actions. Each episode appends one `EpisodeMetricPoint` (that episode's reward, win 0/1, distillation weight 0) and a log line. At the end it records a `ModelVersionEntry` with `Mode = Scratch`, `Modality = Vector`, no parent, and the standard relative paths. No weights file is written, so the existing parent check in the Runner rejects it as a parent with "weights file not found". This keeps the catalog schema and its spec unchanged.
- Both commands set DPI awareness first and catch live errors to print NOT RUN / FAIL with the reason.

## Risks / Trade-offs

- [Windows focus-stealing rules block `SetForegroundWindow`] → use M5's `AttachThreadInput` technique, verify the foreground window before every send, and fail loudly (spec) rather than type into the wrong window.
- [The user touches the mouse or keyboard during a run] → accepted for this change, since unattended VM runs are the long-term answer. Focus is re-verified before every action, so a stolen focus becomes an error, not silent wrong input.
- [Locked screen or disconnected RDP session makes capture black and input fail] → documented runtime requirement. Reading a black frame raises `BoardNotFound`, which surfaces as NOT RUN/FAIL. VM setup notes come in a later change.
- [Colour constants from another theme or machine] → fixtures pin the measured palette. A theme change raises unreadable-cell errors instead of misreading, by design.
- [BGR/BGRA ordering bugs in the port] → fixture tests cover every cell state, and any channel swap fails them.
- [Step rate ~2-4 steps/s with settle delays] → fine for verification. Throughput is a training-change concern (board ROI caching, smaller settle times).
- [Cursor edge behaviour assumed to clamp] → verified live in a task. If LibreMines wraps, the tracker changes. The specs state the clamp, so a wrap would mean a spec update.

## Migration Plan

Additive only. No existing API, catalog schema, or environment changes. Rollback means removing the `GameTrainer.Live` project reference and the LibreMines registry entry.

## Open Questions

- Does a cursor on a revealed cell have a distinct tint? This doesn't affect reading (tracking is from actions), but it's worth capturing as a fixture during live verification.
