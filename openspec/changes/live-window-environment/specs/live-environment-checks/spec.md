# Spec Delta

## Purpose

Proves from the command line that a live game environment really sees and controls its game, using negative controls so a healthy-looking result can't come from input landing nowhere or from stale observations.

## ADDED Requirements

### Requirement: Live check command runs four controls
The Runner SHALL provide a `live-check --game <id>` command that runs four controls against the live game window and prints PASS or FAIL for each, with the evidence that decided it, followed by an overall verdict. The overall verdict SHALL be PASS only if all four controls pass.

#### Scenario: All controls pass
- **WHEN** `live-check --game libremines-easy` runs with LibreMines open on an Easy board
- **THEN** each control prints PASS with its evidence, the overall verdict is PASS, and the command exits with code 0

#### Scenario: Any control fails
- **WHEN** one control fails
- **THEN** that control prints FAIL with its evidence, the overall verdict is FAIL, and the command exits with a non-zero code

### Requirement: Control 1, keys live
The game profile SHALL supply a probe: a short action sequence and the single cell or state it is expected to change. Control 1 SHALL reset, play the probe with real input, and pass only if the observed state changed in exactly the expected way and nowhere else.

#### Scenario: LibreMines probe
- **WHEN** control 1 runs for LibreMines with the probe "down, right, right, flag"
- **THEN** it passes only if cell (1, 2) changed from hidden to flagged and no other cell changed

### Requirement: Control 2, null input
Control 2 SHALL replay the same probe with input disconnected and pass only if the observed state does not change at all.

#### Scenario: Disconnected input
- **WHEN** control 2 replays the probe with null input
- **THEN** it passes only if zero cells changed

### Requirement: Control 3, frozen frame
Control 3 SHALL capture one frame, play the probe with real input, then read observations both from the frozen frame and from a fresh capture. It SHALL pass only if the fresh observation changed and the frozen-frame observation did not.

#### Scenario: Observations follow the screen
- **WHEN** control 3 runs
- **THEN** it passes only if the live observation shows the probe's change and the frozen one shows none

### Requirement: Control 4, unattended reset
Control 4 SHALL perform 20 consecutive resets with no human input and pass only if every reset produces a clean starting state (for LibreMines, all cells hidden).

#### Scenario: Twenty clean resets
- **WHEN** control 4 runs
- **THEN** it reports how many of 20 resets were clean and passes only at 20 of 20

### Requirement: Checks report NOT RUN when they cannot start
When the game window is missing, the platform has no live backend, or the game has no state reader or probe, `live-check` SHALL print NOT RUN with the reason and exit with a non-zero code. It SHALL never report PASS for checks that did not execute.

#### Scenario: Game not open
- **WHEN** `live-check --game libremines-easy` runs with LibreMines closed
- **THEN** it prints NOT RUN naming the missing window and exits non-zero

### Requirement: Random-agent smoke run is recorded in the catalog
The Runner SHALL provide a `live-random --game <id> --episodes <n>` command that plays n episodes with uniformly random actions on the live game. It SHALL record per-episode reward and win rate as metrics and every episode summary in the training log, under a version label (default `random-baseline`, overridable with `--version`). The resulting version SHALL appear in the dashboard with its episode count, mean reward and win rate. No weights file SHALL be written for it.

#### Scenario: Smoke run visible in the dashboard
- **WHEN** `live-random --game libremines-easy --episodes 10` completes
- **THEN** the dashboard lists version `random-baseline` for LibreMines with 10 episodes, its mean reward and win rate, its curve, and its log

#### Scenario: Smoke run cannot be used as a parent
- **WHEN** a training run names `random-baseline` as its parent
- **THEN** it fails before training starts with an error that the parent's weights file does not exist

#### Scenario: Run interrupted
- **WHEN** a smoke run fails partway, for example because the window closed
- **THEN** the episodes completed so far remain in the metrics and log, the error is printed, and the command exits non-zero
