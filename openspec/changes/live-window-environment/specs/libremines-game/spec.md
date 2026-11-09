# Spec Delta

## Purpose

Makes LibreMines (v2.3.0, Easy 8x8 with 10 mines) playable as a live game: reading its board from the screen, driving it with its keyboard controls, and scoring each move.

## ADDED Requirements

### Requirement: LibreMines is a registered built-in game
The system SHALL offer LibreMines Easy as a built-in game with id `libremines-easy`, a display name and description, so it can be registered in the catalog and selected from the command line like any other built-in game.

#### Scenario: Listed among built-in games
- **WHEN** the built-in games are listed
- **THEN** `libremines-easy` is among them with its display name and description

### Requirement: Board is located in the window, never assumed
The board reader SHALL find the 8x8 minefield within the captured window each time it reads, so different window sizes and positions work. If no minefield is visible, for example while the difficulty chooser is showing, it SHALL fail with an error saying no board was found and suggesting an Easy game be started.

#### Scenario: Window opened at a different size
- **WHEN** the same board is captured with the window at two different sizes
- **THEN** both captures read as the same 8x8 grid of cell states

#### Scenario: Difficulty chooser showing
- **WHEN** the captured window shows the difficulty chooser instead of a board
- **THEN** reading fails with a board-not-found error that suggests starting an Easy game

### Requirement: Every cell is read into one of twelve states
Each of the 64 cells SHALL be read as exactly one of: revealed with 0-8 neighbouring mines, hidden, flagged, or mine. A hidden cell under the keyboard cursor highlight SHALL read as hidden. A cell matching none of the known appearances SHALL make the read fail with an error naming the cell's row and column. The reader SHALL NOT guess.

#### Scenario: Reference board reads exactly
- **WHEN** the reference full-board screenshot from the M5 milestone is read
- **THEN** every cell matches the recorded expected grid

#### Scenario: Each single-cell state reads correctly
- **WHEN** each reference single-cell screenshot (digits 4-8, mine, cursor on hidden, cursor on flagged) is read
- **THEN** each reads as its recorded state

#### Scenario: Unknown cell appearance
- **WHEN** a cell's appearance matches no known state
- **THEN** reading fails with an error naming that cell's row and column

### Requirement: Six keyboard actions drive the cursor
The game SHALL expose six actions, in this order: up (W), down (S), left (A), right (D), reveal (O) and flag (P). Each is delivered as a single key tap at the keyboard cursor's position.

#### Scenario: Flag a chosen cell
- **WHEN** after a reset the actions down, right, right, flag are taken
- **THEN** exactly cell (1, 2) changes, from hidden to flagged

### Requirement: Cursor position is part of the game state
The system SHALL track the keyboard cursor's row and column from the actions taken since the last reset, staying within the 8x8 board at the edges, and SHALL include it in the vector observation.

#### Scenario: Cursor stops at the edge
- **WHEN** after a reset the up action is taken from row 0
- **THEN** the tracked cursor stays at row 0

### Requirement: Reset starts a new Easy game with the cursor at the top-left
Reset SHALL send Ctrl+R, wait for the board to settle, then send one cursor-activation key press so that keyboard cursor mode is active with the cursor at (0, 0). After reset, every cell SHALL read as hidden.

#### Scenario: Clean board after reset
- **WHEN** reset is called after a lost game
- **THEN** all 64 cells read as hidden and the tracked cursor is at (0, 0)

### Requirement: Moves are scored by board changes
Each step's reward SHALL be computed from the board before and after the step: the mine penalty (-10) if a mine is visible, the win reward (+10) if all 54 safe cells are revealed with no mine, and otherwise +1 for each newly revealed safe cell. Moves, flags and no-op reveals SHALL score 0. These three numbers SHALL be part of the game's profile, not fixed in the environment.

#### Scenario: Revealing opens several cells
- **WHEN** a reveal opens 7 previously hidden safe cells and no mine
- **THEN** the step reward is +7

#### Scenario: Revealing a mine
- **WHEN** a reveal shows a mine
- **THEN** the step reward is -10 and the step reports terminated

### Requirement: Games end on win, loss or step cap
A step SHALL report terminated when a mine is visible or all 54 safe cells are revealed. It SHALL report truncated after 200 steps without termination.

#### Scenario: Winning the game
- **WHEN** a step reveals the last safe cell
- **THEN** the step reward is +10 and the step reports terminated

### Requirement: LibreMines offers vector and visual observations
The vector observation SHALL encode each of the 64 cells as a one-hot over the 12 cell states, followed by a one-hot of the cursor's cell, for 832 values. The visual observation SHALL be the located minefield region, scaled to 3x96x96 and normalized to 0..1.

#### Scenario: Observation sizes match the spec
- **WHEN** the LibreMines spec is inspected and observations are read
- **THEN** the vector has 832 values with exactly 65 ones, and the visual frame is 3x96x96
