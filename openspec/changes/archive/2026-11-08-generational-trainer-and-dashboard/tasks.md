# Tasks

## 1. Dual-Modal Environment Contract & GridWorld Refactor

- [x] 1.1 Define `EnvironmentSpec`, `VisualFrame`, `StepOutcome`, `IGameEnvironment`, and `IGameDescriptor` in `src/GameTrainer.Core` and verify `dotnet build src/GameTrainer.Core` succeeds.
- [x] 1.2 Update `GridState` in `src/GameTrainer.Games/GridWorld` to produce normalized vector observations (`ToVectorObservation()`) and rendered pixel frames (`ToVisualObservation()`), replace `RandomStartEnvironment.Place` mutation with injected initial-state spawner strategies on `GridWorldEnvironment`, and implement `IGameEnvironment` and `IGameDescriptor` for GridWorld, verifying with `dotnet build src/GameTrainer.Games`.
- [x] 1.3 Add unit tests in `tests/GameTrainer.Tests` covering discrete action indexing (`0..ActionCount-1`), out-of-range action rejection, simultaneous vector/visual observation consistency on the same tick, spawner strategies, and `IGameDescriptor` discovery, verifying all pass via `dotnet test`.
- [x] 1.4 Update `docs/design-plan.md` to reflect the two-layer `IGameEnvironment` dual-observation contract, generational learning architecture, and catalog/dashboard design, removing conflicting Milestone-0-only assumptions, and verify the document contains no stale references to `Place()`.

## 2. Local Model Catalog & Run Artifact Store

- [x] 2.1 Implement catalog domain records (`GameEntry`, `ModelVersionEntry`, `TrainingModality`, `GenerationalMode`, `EpisodeMetricPoint`) and file-based `CatalogStore` (`catalog.json`, `metrics.jsonl`, `training.log`) in `src/GameTrainer.Core/Catalog` with atomic file writes and shared read/write access, verifying `dotnet build src/GameTrainer.Core` succeeds.
- [x] 2.2 Write unit tests in `tests/GameTrainer.Tests/CatalogStoreTests.cs` verifying game registration persistence, model version lineage linking (`ParentVersion`), `metrics.jsonl` streaming reads/writes, and `training.log` retrieval across temporary directories via `dotnet test`.

## 3. Generational Brain (`GameTrainer.Brain`) & CLI Runner

- [x] 3.1 Create `src/GameTrainer.Brain/GameTrainer.Brain.csproj` with TorchSharp dependencies, add it to `GameTrainerSharp.slnx`, and implement `VectorEncoder`, `VisualEncoder`, and shared `DecisionHead` (`LatentDim = 64`) modules, verifying `dotnet build src/GameTrainer.Brain` succeeds.
- [x] 3.2 Implement `GenerationalPpoTrainer` in `src/GameTrainer.Brain` supporting `Scratch` (`Parent = None`), `Coach` (frozen parent action-probability distillation with decaying $\lambda(t)$), and `CoachAndCopyHead` (`DecisionHead` weight transplantation + coaching), verifying `dotnet build` succeeds.
- [x] 3.3 Write unit and integration tests in `tests/GameTrainer.Tests/GenerationalBrainTests.cs` verifying encoder/head output shapes, `DecisionHead` weight copy parity between vector parent and visual child, and a short GridWorld training run in all three modes (`Scratch`, `Coach`, `CoachAndCopyHead`) via `dotnet test`.
- [x] 3.4 Upgrade `src/GameTrainer.Runner` CLI to execute training runs (registering the game, training a version with optional `--parent` and `--mode`, writing `model.pt`, `metrics.jsonl`, and `training.log` to `CatalogStore`) and update `README.md` with the new CLI commands, verifying `dotnet run --project src/GameTrainer.Runner` completes a baseline/training run and writes catalog artifacts.

## 4. Windows 2000 Avalonia Observatory Dashboard (`GameTrainer.Gui`)

- [x] 4.1 Create `src/GameTrainer.Gui/GameTrainer.Gui.csproj` (Avalonia 11 desktop application), add it to `GameTrainerSharp.slnx`, and define the Windows 2000 classic control styles (`#D4D0C8` surfaces, 3D raised/sunken bevel borders, classic tabs and tables), verifying `dotnet build src/GameTrainer.Gui` succeeds.
- [x] 4.2 Implement the Game Sidebar, "Add Game" registration dialog (listing discovered `IGameDescriptor` implementations), and Model Version Lineage table bound to `CatalogStore`, verifying view-model behavior with unit tests in `tests/GameTrainer.Tests/DashboardViewModelTests.cs`.
- [x] 4.3 Implement the Improvement Graph control (plotting episode `MeanReward` and `GoalRate` series from `metrics.jsonl` with optional parent curve overlay) and the Training Log viewer tab (reading `training.log`), verifying `dotnet test` passes and documenting GUI launch instructions in `README.md`.

## 5. End-to-End Verification

- [x] 5.1 Run full solution build and test suite (`dotnet build GameTrainerSharp.slnx` and `dotnet test GameTrainerSharp.slnx`) and verify zero warnings/errors and all unit/integration tests pass.
