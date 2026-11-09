# observatory-dashboard Specification

## Purpose

Provides a cross-platform Avalonia UI desktop observatory styled after classic Windows 2000 for managing registered games, browsing model versions and lineage, plotting generational improvement graphs, and reading training logs.

## Requirements

### Requirement: Game list and Add Game registration workflow
The system SHALL display all catalog-registered games in the dashboard sidebar with their current latest model version, modality badge, and last-updated date, and SHALL allow the user to add new games from discovered `IGameDescriptor` implementations.

#### Scenario: Adding a game from the GUI
- **WHEN** the user opens the "Add Game" dialog and selects an unregistered built-in game descriptor
- **THEN** the game is persisted to the catalog store and immediately appears in the dashboard game list

### Requirement: Model version history and generational lineage table
The system SHALL display a table of all trained model versions for the selected game showing version tag, modality (`Vector` or `Visual`), parent version (`Taught By`), training mode, goal/win rate, mean reward, and last-updated timestamp.

#### Scenario: Selecting a game in the dashboard
- **WHEN** the user selects a game in the sidebar
- **THEN** the details pane displays the game's summary header and populates the version history table ordered from newest to oldest

### Requirement: Generational improvement curve visualization
The system SHALL render a graphical chart plotting training progression (`Mean Reward` and `Goal Rate %` over episodes) from `metrics.jsonl` for the selected model version, with support for overlaying its parent model's curve for comparison.

#### Scenario: Comparing child and parent learning curves
- **WHEN** the user views the "Improvement Graph" tab for a coached model version
- **THEN** the chart plots the selected model's progression curve alongside its parent version's curve

### Requirement: Training log viewer
The system SHALL provide a log viewer tab that displays the contents of `training.log` for the selected model version and supports refreshing when new log lines are written.

#### Scenario: Opening training logs for a model version
- **WHEN** the user selects a model version and switches to the "Training Logs" tab
- **THEN** the dashboard loads and displays the formatted text from that version's `training.log` file

### Requirement: Classic Windows 2000 visual theme
The system SHALL style the observatory dashboard using a classic Windows 2000 aesthetic (warm gray `#D4D0C8` surfaces, 3D raised/sunken bevel borders, crisp sans-serif typography, and classic tabular/tab controls).

#### Scenario: Launching the observatory window
- **WHEN** the user launches `GameTrainer.Gui`
- **THEN** the application renders with the Windows 2000 bevel/gray palette across panels, buttons, tabs, and tables
