## ADDED Requirements

### Requirement: App-switching entry points SHALL use a shared target-window selection contract
Pulsar SHALL choose the target window for app-switching flows through a shared selection contract so equivalent switching entry points do not diverge in how they rank and skip candidate windows, and that contract SHALL accept an explicit switching intent rather than relying only on a skip flag.

#### Scenario: WinSwitcher plugin activates a multi-window process
- **WHEN** the WinSwitcher plugin executes an app-switching action for a process that has multiple eligible windows
- **THEN** Pulsar SHALL select the target window using the shared selection contract rather than a plugin-specific ranking rule

#### Scenario: Grouped radial slot switches a multi-window process
- **WHEN** a grouped radial slot executes for a process that has multiple eligible windows
- **THEN** Pulsar SHALL select the target window using the same shared selection contract used by WinSwitcher plugin switching

#### Scenario: Submenu requests its default target
- **WHEN** the radial menu needs a default target for a submenu of windows
- **THEN** Pulsar SHALL resolve that target through the same shared selection contract using a submenu-specific switching intent

### Requirement: Shared window selection SHALL prefer real activation recency over synthetic ordering
When candidate windows have tracked activation history, Pulsar SHALL use that real activation recency as the canonical ranking signal for user-facing switching decisions, and it SHALL treat visual stacking order only as a fallback signal when real activation data is unavailable.

#### Scenario: Candidates have tracked activation history
- **WHEN** Pulsar selects a target window from candidates that include activation-monitor-backed recency data
- **THEN** it SHALL rank those candidates by real activation recency for switching decisions

#### Scenario: Candidate lacks tracked activation history
- **WHEN** a candidate window has no tracked real activation record yet
- **THEN** Pulsar SHALL treat that candidate as a deterministic fallback rather than as a higher-priority window than candidates with known activation history

#### Scenario: All candidates lack tracked activation history
- **WHEN** Pulsar selects a target from candidates that do not have tracked activation history
- **THEN** it SHALL use explicit fallback ordering signals such as visual stacking order or stable deterministic ordering rather than interpreting synthetic recency as canonical activation history

### Requirement: Shared window selection SHALL apply explicit skip rules based on switching context
Pulsar SHALL allow switching entry points to declare whether the current foreground window, the Pulsar pre-invocation window, or neither should be skipped during target-window selection, and those skip rules SHALL be attached to explicit switching intent.

#### Scenario: Menu-driven switching skips the pre-invocation window
- **WHEN** Pulsar performs grouped radial-menu switching while Pulsar itself is foregrounded
- **THEN** the selection contract SHALL support skipping the window that was foreground before Pulsar was invoked

#### Scenario: Plugin-driven switching skips the current foreground window
- **WHEN** a WinSwitcher plugin action targets a process while another window is currently foregrounded
- **THEN** the selection contract SHALL support skipping the current foreground window when selecting the target for that process

#### Scenario: Quick-switch request uses dedicated skip behavior
- **WHEN** Pulsar resolves a target for quick switch
- **THEN** the shared selection contract SHALL allow quick-switch-specific skip behavior to be expressed without overloading process-switch semantics
