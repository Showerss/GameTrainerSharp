# model-catalog-store Specification

## Purpose

Persists registered games, versioned model checkpoints, generational lineage, episode metrics series, and training logs in a local store shared between the CLI training runner and the desktop observatory dashboard.

## Requirements

### Requirement: Game registration in the local catalog
The system SHALL persist registered games in the local catalog store with their unique game ID, display name, environment configuration, registration timestamp, and latest model summary.

#### Scenario: Adding a discovered game to the catalog
- **WHEN** a user or runner registers a game descriptor in the catalog
- **THEN** the game entry is saved to the local catalog file and survives application restarts

### Requirement: Versioned model metadata and generational lineage tracking
The system SHALL record each trained model version under its game entry with a version identifier, modality (`Vector` or `Visual`), training mode (`Scratch`, `Coach`, or `CoachAndCopyHead`), optional parent model version, creation/update timestamp, final mean reward, final goal/win rate, and file paths to weights, metrics, and logs.

#### Scenario: Saving a newly trained child model version
- **WHEN** a training run completes for a child model coached by parent version `v1.0`
- **THEN** the catalog store writes the model checkpoint and records a new model version entry linking `ParentVersion = "v1.0"`, the training mode, summary metrics, and the UTC/local update timestamp

### Requirement: Episode metrics series and training log persistence
The system SHALL write structured per-episode/per-batch progression data (`metrics.jsonl`) and human-readable log entries (`training.log`) inside the run directory for each model version.

#### Scenario: Inspecting historical improvement curve and logs for a model version
- **WHEN** the catalog store is queried for a specific game and model version
- **THEN** it returns the ordered series of episode metrics (episode number, mean reward, goal rate, distillation weight $\lambda$) from `metrics.jsonl` and the text contents of `training.log`
