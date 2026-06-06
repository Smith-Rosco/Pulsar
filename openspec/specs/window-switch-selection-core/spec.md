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
When candidate windows have tracked activation history, Pulsar SHALL use that real activation recency as the canonical ranking signal for user-facing switching decisions, and it SHALL treat visual stacking order only as a fallback signal when real activation data is unavailable. When multiple candidates have equivalent activation recency, Pulsar SHALL prefer candidates on the same monitor as the calling context's preferred monitor when one is specified.

#### Scenario: Candidates have tracked activation history
- **WHEN** Pulsar selects a target window from candidates that include activation-monitor-backed recency data
- **THEN** it SHALL rank those candidates by real activation recency for switching decisions

#### Scenario: Candidate lacks tracked activation history
- **WHEN** a candidate window has no tracked real activation record yet
- **THEN** Pulsar SHALL treat that candidate as a deterministic fallback rather than as a higher-priority window than candidates with known activation history

#### Scenario: All candidates lack tracked activation history
- **WHEN** Pulsar selects a target from candidates that do not have tracked activation history
- **THEN** it SHALL use explicit fallback ordering signals such as visual stacking order or stable deterministic ordering rather than interpreting synthetic recency as canonical activation history

#### Scenario: Multiple candidates share equivalent activation recency on different monitors
- **WHEN** Pulsar selects a target window and multiple candidates have equivalent RealActivationTime
- **AND** the selection request specifies a preferred monitor rectangle
- **THEN** Pulsar SHALL rank candidates whose window rectangle intersects the preferred monitor rectangle above candidates that do not

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

### Requirement: Window selection requests SHALL support an optional preferred monitor
The `WindowSelectionRequest` SHALL accept an optional monitor rectangle that callers can set to indicate the preferred display for selection tiebreaking.

#### Scenario: Caller provides preferred monitor
- **WHEN** a switching entry point creates a `WindowSelectionRequest` with a preferred monitor rectangle
- **THEN** the selection engine SHALL apply same-monitor preference as a secondary ordering criterion after activation recency

#### Scenario: Caller omits preferred monitor
- **WHEN** a switching entry point creates a `WindowSelectionRequest` without a preferred monitor rectangle
- **THEN** the selection engine SHALL NOT apply monitor-based preference and SHALL behave identically to the current selection logic

### Requirement: Same-monitor preference SHALL use window rectangle intersection
Pulsar SHALL determine same-monitor eligibility by checking whether a candidate window's bounding rectangle intersects the preferred monitor rectangle, not by checking the window's center point.

#### Scenario: Window spans two monitors including the preferred monitor
- **WHEN** a candidate window's rectangle partially overlaps the preferred monitor rectangle
- **THEN** Pulsar SHALL consider that window as matching the preferred monitor

#### Scenario: Window is entirely on a different monitor
- **WHEN** a candidate window's rectangle does not intersect the preferred monitor rectangle
- **THEN** Pulsar SHALL treat that window as not matching the preferred monitor for tiebreaking purposes

### Requirement: Grouped root-slot SHALL pass cursor monitor as preferred monitor in selection requests
When a grouped radial-menu slot resolves its target window, the selection request SHALL include the monitor rectangle containing the current cursor position as the preferred monitor.

#### Scenario: Grouped root slot resolves target window
- **WHEN** a grouped radial-menu slot requests target window selection
- **THEN** the selection request SHALL set `PreferredMonitorRect` to the monitor rectangle containing the current cursor position
