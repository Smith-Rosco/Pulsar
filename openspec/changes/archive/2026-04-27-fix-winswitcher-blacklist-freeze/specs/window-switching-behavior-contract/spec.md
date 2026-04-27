## MODIFIED Requirements

### Requirement: Blacklist behavior SHALL distinguish discovery filtering from activation denial
Pulsar SHALL define whether a blacklist entry affects candidate discovery, explicit activation, or both, and SHALL apply that behavior consistently across runtime code and documentation. The WinSwitcher `ExcludeProcesses` setting SHALL be treated as a discovery blacklist only unless a separate activation denylist is explicitly defined.

#### Scenario: Process is excluded from discovery only
- **WHEN** a process appears on a discovery blacklist but not on an activation denylist
- **THEN** Pulsar SHALL omit that process from auto-discovered switching lists while preserving explicit switching for process-targeted activation flows

#### Scenario: Process is denied for explicit activation
- **WHEN** a process appears on an activation denylist
- **THEN** Pulsar SHALL reject explicit switch actions targeting that process and SHALL report the behavior consistently with product documentation
