## MODIFIED Requirements

### Requirement: Plugin state dictionaries SHALL be thread-safe
All plugin runtime state stores and circuit breaker counters SHALL use thread-safe collections that support concurrent read and write operations without data corruption or lost updates. Configuration service cached state SHALL be protected by explicit synchronization to prevent races between concurrent load and save operations.

#### Scenario: Concurrent state transitions do not corrupt snapshots
- **WHEN** the runtime transitions two different plugins through lifecycle states concurrently
- **THEN** each plugin's snapshot SHALL reflect its own correct state without cross-contamination or lost updates

#### Scenario: Concurrent breaker counting is atomic
- **WHEN** the circuit breaker records multiple failures for the same plugin from concurrent execution paths
- **THEN** the failure count SHALL be incremented atomically and SHALL never show a lower count than the actual number of failures

#### Scenario: Plugin enumeration during concurrent execution
- **WHEN** a caller enumerates all loaded plugins while another execution path activates or removes a plugin
- **THEN** the enumeration SHALL NOT throw an exception and SHALL return a consistent snapshot

#### Scenario: Concurrent config load and save do not race on cache
- **WHEN** one execution path calls `ConfigService.SaveAsync()` while another calls `ConfigService.LoadAsync()` or `LoadInternalAsync()`
- **THEN** the cached configuration field SHALL be accessed under synchronization
- **AND** the save path SHALL write to disk before updating the in-memory cache
- **AND** the load path SHALL return a consistent snapshot without observing a partially-updated or stale cache entry

#### Scenario: Config cache is not updated before disk write succeeds
- **WHEN** `ConfigService.SaveAsync()` is called but the file write fails
- **THEN** the in-memory cache SHALL NOT be updated with the unsaved configuration
