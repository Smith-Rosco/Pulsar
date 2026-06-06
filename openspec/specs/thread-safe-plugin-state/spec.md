## Purpose

Ensure thread safety across the plugin runtime by using concurrent collections for all shared-mutable state, exposing read-only interfaces to prevent external mutation, and using atomic compare-and-exchange operations for critical UI guards.

## Requirements

### Requirement: Plugin state dictionaries SHALL be thread-safe
All plugin runtime state stores and circuit breaker counters SHALL use thread-safe collections that support concurrent read and write operations without data corruption or lost updates.

#### Scenario: Concurrent state transitions do not corrupt snapshots
- **WHEN** the runtime transitions two different plugins through lifecycle states concurrently
- **THEN** each plugin's snapshot SHALL reflect its own correct state without cross-contamination or lost updates

#### Scenario: Concurrent breaker counting is atomic
- **WHEN** the circuit breaker records multiple failures for the same plugin from concurrent execution paths
- **THEN** the failure count SHALL be incremented atomically and SHALL never show a lower count than the actual number of failures

#### Scenario: Plugin enumeration during concurrent execution
- **WHEN** a caller enumerates all loaded plugins while another execution path activates or removes a plugin
- **THEN** the enumeration SHALL NOT throw an exception and SHALL return a consistent snapshot

### Requirement: Plugin runtime state SHALL NOT be exposed as mutable dictionaries
The plugin runtime SHALL expose state collections as read-only interfaces (`IReadOnlyDictionary<K,V>` or immutable snapshots) rather than allowing external code to mutate runtime state directly.

#### Scenario: External code cannot mutate plugin catalog
- **WHEN** application code accesses the plugin catalog or runtime state store
- **THEN** the returned collection SHALL be read-only and SHALL NOT permit addition, removal, or replacement of entries

### Requirement: Show() double-invocation guard SHALL be atomic
The radial menu presentation guard (`_isLoading` equivalent) SHALL use an atomic compare-and-exchange operation to prevent double-invocation under rapid hotkey presses without relying on thread affinity.

#### Scenario: Rapid double-press of hotkey is guarded
- **WHEN** the hotkey action is invoked twice in rapid succession from different hook thread dispatches
- **THEN** only one invocation of `Show()` SHALL proceed and the second SHALL be rejected without side effects

#### Scenario: Guard resets on completion
- **WHEN** `Show()` completes (including exception paths)
- **THEN** the atomic guard SHALL be reset so the next hotkey press can invoke `Show()` normally
