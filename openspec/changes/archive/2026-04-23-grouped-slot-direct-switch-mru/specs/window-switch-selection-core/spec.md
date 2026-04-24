## ADDED Requirements

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
