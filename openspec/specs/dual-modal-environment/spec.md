# dual-modal-environment Specification

## Purpose

Defines the uniform contract for game environments and game discovery so any learning agent can interact with any registered game using discrete action indices while observing both vector state and visual frames from the same underlying tick.

## Requirements

### Requirement: Uniform discrete action execution and metadata
The system SHALL expose environment metadata including vector observation dimensions, visual frame dimensions (channels, height, width), discrete action count, and human-readable action names, and SHALL execute environment steps using a zero-based integer action index (`0` to `ActionCount - 1`).

#### Scenario: Stepping an environment with a valid discrete action index
- **WHEN** a caller resets a game environment and invokes `Step(actionIndex)` with `0 <= actionIndex < ActionCount`
- **THEN** the environment advances one tick and returns the step reward, termination flag, truncation flag, and step count

#### Scenario: Rejecting an out-of-range action index
- **WHEN** a caller invokes `Step(actionIndex)` with `actionIndex < 0` or `actionIndex >= ActionCount`
- **THEN** the environment throws an `ArgumentOutOfRangeException`

### Requirement: Simultaneous vector and visual observations from the same tick
The system SHALL provide both a vector observation (`float[]`) and a visual frame observation (`VisualFrame`) representing the exact same current environment state after `Reset()` or `Step(actionIndex)` without advancing or mutating the state.

#### Scenario: Querying vector and visual observations for Teacher-Student coaching
- **WHEN** an environment is at any state after `Reset()` or `Step(actionIndex)`
- **THEN** calling `GetVectorObservation()` returns the numeric feature vector for that state and calling `GetVisualObservation()` returns the rendered pixel tensor for that same state

### Requirement: Encapsulated initial-state spawning
The system SHALL initialize episode state on `Reset()` using a configured spawner strategy without requiring callers or decorators to mutate internal state after reset.

#### Scenario: Resetting with random-start configuration
- **WHEN** an environment configured with a random-start spawner is reset across multiple episodes
- **THEN** each `Reset()` produces a valid non-terminal starting state without external post-reset mutation calls

### Requirement: Discoverable game descriptors
The system SHALL allow games to register a descriptor containing a unique game identifier, display name, supported modalities, and a factory method to instantiate the environment.

#### Scenario: Discovering built-in games for catalog registration
- **WHEN** the application queries available game descriptors
- **THEN** each built-in game descriptor is returned with its unique ID, display name, and environment specification
