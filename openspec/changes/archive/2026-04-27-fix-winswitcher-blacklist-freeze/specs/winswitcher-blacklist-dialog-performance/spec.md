## ADDED Requirements

### Requirement: WinSwitcher blacklist dialog SHALL remain responsive during initial load
The system SHALL render the WinSwitcher process blacklist dialog without waiting for full window inventory, per-process icon extraction, or registry mutation work to complete.

#### Scenario: Large process registry opens blacklist dialog
- **WHEN** the user opens the WinSwitcher process blacklist dialog and the process registry contains many known processes
- **THEN** the dialog SHALL present a usable process list from lightweight metadata without blocking on full icon resolution or runtime inventory side effects

### Requirement: Blacklist dialog SHALL use lightweight running-state lookup
The system SHALL determine blacklist dialog running-state indicators through a lightweight lookup path that does not require full window candidate construction.

#### Scenario: Running indicators are shown in blacklist dialog
- **WHEN** the blacklist dialog needs to mark which known processes are currently running
- **THEN** the system SHALL obtain running-state membership without enumerating full switchable window candidates for every visible row

### Requirement: Blacklist dialog SHALL tolerate slow icon sources
The system SHALL degrade icon presentation gracefully when icon cache access or icon extraction is slow, unavailable, or fails.

#### Scenario: Icon source is slow or unavailable
- **WHEN** a process icon cannot be loaded quickly for the blacklist dialog
- **THEN** the dialog SHALL keep the row usable with a fallback visual and SHALL continue loading remaining content without freezing the settings surface
