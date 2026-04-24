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

### Requirement: Shared window selection SHALL support grouped root-slot direct-trigger intent
Pulsar SHALL define a dedicated selection intent for modifier-release execution from a grouped root radial-menu slot so root direct-switch behavior can be specified independently from generic grouped switching, submenu defaults, and plugin-driven activation.

#### Scenario: Root grouped slot resolves through dedicated intent
- **WHEN** a grouped root radial-menu slot is executed by modifier release
- **THEN** Pulsar SHALL resolve the target window through the shared selection contract using a dedicated grouped root-slot direct-trigger intent

### Requirement: Grouped root-slot direct-trigger intent SHALL return to the app MRU window from outside the app
When a grouped root-slot direct-trigger request targets a process whose eligible windows do not include the current foreground window, Pulsar SHALL select the most recently used eligible window for that process.

#### Scenario: Current foreground is outside target process
- **WHEN** Pulsar resolves a grouped root-slot direct-trigger request
- **AND** the current foreground window does not belong to the target process group
- **THEN** Pulsar SHALL choose the highest-ranked eligible window by tracked activation recency

### Requirement: Grouped root-slot direct-trigger intent SHALL rotate away from the current in-process window
When a grouped root-slot direct-trigger request targets a process whose eligible windows include the current foreground window, Pulsar SHALL skip that current in-process window and select the next most recently used eligible window for the same process.

#### Scenario: Current foreground belongs to target process group
- **WHEN** Pulsar resolves a grouped root-slot direct-trigger request
- **AND** the current foreground window is one of the eligible windows in the target process group
- **THEN** Pulsar SHALL skip that current foreground window during selection
- **AND** Pulsar SHALL choose the next highest-ranked eligible window for the same process

#### Scenario: Current in-process window is the only eligible candidate
- **WHEN** Pulsar resolves a grouped root-slot direct-trigger request
- **AND** the current foreground window is one of the eligible windows in the target process group
- **AND** no other eligible windows remain after skipping it
- **THEN** Pulsar SHALL fall back to the best-ranked eligible window rather than failing the request
