## ADDED Requirements

### Requirement: Foreground configuration reads SHALL avoid hidden side-effecting background work
The system SHALL keep foreground configuration and settings discovery reads free of ad hoc background mutation work unless that work is explicitly requested by the calling flow.

#### Scenario: Settings dialog queries discovery data
- **WHEN** a settings-facing dialog requests discovery data for configuration purposes
- **THEN** the system SHALL not implicitly trigger process registration, cache persistence, or unrelated background mutation as a side effect of serving that read
