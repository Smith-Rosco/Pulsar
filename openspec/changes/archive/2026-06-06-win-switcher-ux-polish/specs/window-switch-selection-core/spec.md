## MODIFIED Requirements

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

## ADDED Requirements

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
