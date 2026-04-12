## ADDED Requirements

### Requirement: Hover preview SHALL resolve through layered fallback
The system SHALL resolve radial-menu window hover preview through a layered fallback strategy instead of relying on a single fresh screenshot attempt.

#### Scenario: Live preview is available
- **WHEN** the user hovers a window-backed slot and the target window supports the preferred live preview path
- **THEN** the center slot SHALL display that live preview

#### Scenario: Fresh live preview is unavailable but cached snapshot exists
- **WHEN** the user hovers a window-backed slot, the preferred live preview path is unavailable, and the system has a last-known-good snapshot for that target window
- **THEN** the center slot SHALL display the cached snapshot instead of falling back directly to the icon

#### Scenario: No preview representation is available
- **WHEN** the user hovers a window-backed slot and neither a live preview nor a cached snapshot is available for the target window
- **THEN** the center slot SHALL fall back to the window or process icon

### Requirement: Window switchability SHALL remain independent from previewability
The system SHALL continue to treat a valid window-switch target as selectable even when preview generation fails.

#### Scenario: Valid target cannot provide preview
- **WHEN** a window remains a valid switch target for the submenu or hover surface but current preview generation fails
- **THEN** the slot SHALL remain interactive and executable
- **AND** the preview surface SHALL degrade through the defined fallback order without removing the slot from the switcher

### Requirement: Preview cache SHALL preserve last-known-good snapshots across menu sessions
The system SHALL preserve successful static preview snapshots beyond a single radial-menu session until the target window is explicitly invalidated or replaced.

#### Scenario: New radial menu session opens after prior successful snapshot
- **WHEN** the user opens the radial menu in a later session for a window that previously produced a successful snapshot
- **THEN** the system SHALL keep that snapshot available as a fallback even if a fresh preview attempt is not yet available

#### Scenario: Fresh preview failure does not erase previous snapshot
- **WHEN** the system attempts to refresh a target window preview and the refresh attempt fails
- **THEN** any existing last-known-good snapshot for that target window SHALL remain available as the fallback representation

### Requirement: Preview cache SHALL invalidate on target window loss
The system SHALL invalidate stored preview state when the target window can no longer be treated as the same valid window.

#### Scenario: Window handle is no longer valid
- **WHEN** the system detects that a cached preview's target window handle is no longer valid
- **THEN** the cached preview for that window SHALL be discarded

#### Scenario: Window becomes non-previewable and no cached snapshot remains
- **WHEN** the user hovers a target window that is invalid, cloaked, or otherwise cannot provide either a live preview or a retained snapshot
- **THEN** the center slot SHALL show icon fallback only

### Requirement: Preview state SHALL distinguish among live, snapshot, and icon outcomes
The preview subsystem SHALL expose enough state for the radial-menu presentation layer to distinguish live preview, static snapshot fallback, and icon fallback outcomes.

#### Scenario: Presentation layer evaluates preview result
- **WHEN** the preview subsystem resolves a target window representation
- **THEN** the result SHALL indicate whether the displayed representation is a live preview, a snapshot fallback, or an icon fallback

#### Scenario: Provider failure occurs after fallback already exists
- **WHEN** a higher-priority preview provider fails after a lower-priority representation has already been resolved
- **THEN** the system SHALL preserve the lower-priority representation instead of replacing it with an empty preview state
