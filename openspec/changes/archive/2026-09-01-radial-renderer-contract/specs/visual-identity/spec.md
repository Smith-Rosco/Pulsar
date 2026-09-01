## MODIFIED Requirements

### Requirement: Mode-Based Visual Differentiation
The system SHALL provide distinct visual themes for Task mode (Window Switcher) and Action mode (Command Toolbox) to clearly indicate the current operational context. Mode-based differentiation SHALL continue to hold when the radial menu renders through the typed theme-token and renderer seam: the active mode SHALL select the corresponding tone regardless of the configured renderer or theme preset.

#### Scenario: User invokes Task mode
- **WHEN** the user invokes the radial menu using the Switcher hotkey (Task mode)
- **THEN** the radial menu SHALL display its visual elements (e.g., center orb glow, badge colors) in a cool color tone (e.g., Blue/Cyan)
- **AND** the mode tone SHALL be applied even when the active renderer or theme preset changes

#### Scenario: User invokes Action mode
- **WHEN** the user invokes the radial menu using the Command hotkey (Action mode)
- **THEN** the radial menu SHALL display its visual elements in a warm color tone (e.g., Orange/Red)
- **AND** the mode tone SHALL be applied even when the active renderer or theme preset changes
