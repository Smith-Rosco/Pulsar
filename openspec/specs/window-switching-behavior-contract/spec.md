## ADDED Requirements

### Requirement: Equivalent app-switching entry points SHALL remain behaviorally aligned
Pulsar SHALL keep WinSwitcher plugin switching, grouped radial switching, submenu default selection, and quick-switch resolution aligned through one explicit behavior contract so equivalent user intents do not drift between entry points.

#### Scenario: Plugin switch and grouped slot target the same process
- **WHEN** a WinSwitcher plugin action and a grouped radial slot both target the same set of eligible process windows under the same switching conditions
- **THEN** Pulsar SHALL resolve targets according to the same behavior contract for ranking and skipping candidates

### Requirement: Submenu display order SHALL remain stable independently of default target selection
Pulsar SHALL allow submenu presentation to use stable ordering for muscle memory while default target selection uses the explicit selection contract for switching behavior.

#### Scenario: Submenu is shown for a multi-window process
- **WHEN** Pulsar opens a submenu that lists windows for one process
- **THEN** it SHALL render the submenu in stable display order and SHALL determine the default switch target independently through the shared selection contract

### Requirement: Blacklist behavior SHALL distinguish discovery filtering from activation denial
Pulsar SHALL define whether a blacklist entry affects candidate discovery, explicit activation, or both, and SHALL apply that behavior consistently across runtime code and documentation.

#### Scenario: Process is excluded from discovery only
- **WHEN** a process appears on a discovery blacklist but not on an activation denylist
- **THEN** Pulsar SHALL omit that process from auto-discovered switching lists while preserving explicit switching if the behavior contract defines that path as allowed

#### Scenario: Process is denied for explicit activation
- **WHEN** a process appears on an activation denylist
- **THEN** Pulsar SHALL reject explicit switch actions targeting that process and SHALL report the behavior consistently with product documentation

### Requirement: Selection results SHALL explain why a window was chosen
Pulsar SHALL produce decision metadata for user-facing switching flows so logs and tests can explain which candidate won and which rules affected the outcome.

#### Scenario: Multi-window process resolves a target
- **WHEN** Pulsar selects a target from multiple eligible windows
- **THEN** the selection result SHALL identify the winning candidate and the ranking or skip rules that led to that choice
