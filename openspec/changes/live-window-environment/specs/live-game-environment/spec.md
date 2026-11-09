# Spec Delta

## Purpose

Lets any game that runs in a desktop window be exposed as a standard game environment, so the existing trainers, catalog and dashboard can work with real games, not only hand-written simulators.

## ADDED Requirements

### Requirement: Live environments use the standard environment contract
A live environment SHALL implement the same environment contract as simulated games: discrete action indices `0..ActionCount-1`, reset, step returning reward/terminated/truncated/step count, and vector and visual observations taken from the same captured frame.

#### Scenario: Trainer runs against a live environment unchanged
- **WHEN** a component that accepts any game environment is given a live environment
- **THEN** it can reset, step and read observations without any live-specific code

#### Scenario: Observations come from one frame
- **WHEN** a step completes and both vector and visual observations are read
- **THEN** both are derived from the same captured frame

### Requirement: Game window is found by title
A live environment SHALL attach to a game window whose title contains the profile's configured title text. When no such window exists, it SHALL fail with an error that names the title it searched for.

#### Scenario: Window present
- **WHEN** the environment is created and a window titled "LibreMines" is open
- **THEN** the environment attaches to that window

#### Scenario: Window missing
- **WHEN** the environment is reset and no window title contains the configured text
- **THEN** reset fails with an error naming the missing title, and no input is sent

### Requirement: Describing a live game needs no window
Registering a live game, listing it, or reading its metadata (id, name, description, observation/action spec) SHALL NOT require the game window to be open. Only creating and using an environment needs the window.

#### Scenario: Catalog lists a live game with the game closed
- **WHEN** the catalog or dashboard registers or lists a live game while the game is not running
- **THEN** the operation succeeds and shows the game's metadata

### Requirement: Actions are delivered as keyboard input to the game window
Each action index SHALL map, through the game profile, to a key tap, a modifier chord, or a short sequence of either. Before sending input, the environment SHALL bring the game window to the foreground. If it cannot do so, it SHALL fail with an error instead of sending input elsewhere.

#### Scenario: Action sends the mapped key
- **WHEN** a step is taken with an action mapped to the W key
- **THEN** a W key press and release is delivered to the game window

#### Scenario: Focus cannot be obtained
- **WHEN** a step is taken and the game window cannot be brought to the foreground
- **THEN** the step fails with a focus error and no key is sent

#### Scenario: Action index out of range
- **WHEN** a step is taken with an action index outside `0..ActionCount-1`
- **THEN** the step fails with an error naming the valid range and no key is sent

### Requirement: Frames are read only after the game settles
After every action and after reset, the environment SHALL wait the profile's settle time before capturing. If the profile's reader reports the frame unreadable, it SHALL capture again until a profile-defined timeout. After the timeout it SHALL fail with the reader's error and SHALL NOT guess a state.

#### Scenario: Transient animation frame
- **WHEN** the first capture after an action lands mid-animation and is unreadable, and a later capture within the timeout is readable
- **THEN** the step completes using the readable frame

#### Scenario: Persistently unreadable frame
- **WHEN** every capture until the timeout is unreadable
- **THEN** the step fails with the reader's error describing what could not be read

### Requirement: Reset is driven by the profile's reset procedure
Reset SHALL perform the profile's reset input sequence, wait to settle, reset the step counter to zero, and capture a fresh frame for the initial observations.

#### Scenario: Reset starts a fresh episode
- **WHEN** reset is called after an episode has ended
- **THEN** the profile's reset sequence is sent, the step count is 0, and observations reflect the new game

### Requirement: Episodes end on termination or step cap
A step SHALL report terminated when the profile's reader detects a game-ending state, and truncated when the profile's step cap is reached without termination. Reward for each step SHALL come from the profile's reward reader, comparing the previous and current state.

#### Scenario: Step cap reached
- **WHEN** the step count reaches the profile's step cap and the game has not ended
- **THEN** the step reports truncated and not terminated

### Requirement: Visual observations work for any game
Every live environment SHALL provide a visual observation built only from captured pixels: the window area (or a profile-defined sub-region), scaled to the profile's fixed channel/height/width and normalized to 0..1, matching the game's advertised visual spec.

#### Scenario: Visual frame matches the spec
- **WHEN** a visual observation is read from a live environment whose spec is 3x84x84
- **THEN** the frame has 3 channels, height 84, width 84, and every value is between 0 and 1

#### Scenario: Window size changes between launches
- **WHEN** the game window opens at a different size than in an earlier session
- **THEN** the visual observation still has the advertised shape

### Requirement: Vector observations require a state reader
Vector observations SHALL be produced only by a profile's optional state reader, which turns a frame into a fixed-length numeric vector. When a profile supplies one, vector observations SHALL come from that reader. When it does not, the game SHALL advertise a vector observation size of 0 and only visual observations SHALL be offered.

#### Scenario: Profile without a state reader
- **WHEN** a live game has no state reader
- **THEN** its spec advertises a vector size of 0 and requesting vector training for it fails with a clear error

### Requirement: Input and capture can be disconnected for verification
A live environment SHALL accept a null input sender, which sends nothing, and a frozen capture source, which keeps returning one stored frame. These make negative controls possible without changing the environment.

#### Scenario: Null input changes nothing
- **WHEN** a live environment with null input steps through any action sequence
- **THEN** no input reaches the game window

### Requirement: Unsupported platforms fail clearly
Creating a live environment on a platform without a capture/input backend SHALL fail with an error naming the platform. Building and running the rest of the project, including its tests, on that platform SHALL still work.

#### Scenario: Linux CI
- **WHEN** the test suite runs on Linux
- **THEN** all non-live tests run and pass, and no test needs a game window
