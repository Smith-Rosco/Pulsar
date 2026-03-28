## MODIFIED Requirements

### Requirement: Full configuration SHALL establish a deeper editing hierarchy
The system SHALL present full configuration and slot creation as deeper editing layers with a typography-led hierarchy, making slot identity, validation state, and grouped fields easy to understand without relying on repeated card-style containers for every section, and SHALL keep slot identity and health indicators readable in every supported theme.

#### Scenario: User opens full configuration from the slots page
- **WHEN** the full configuration dialog opens
- **THEN** the dialog SHALL identify the slot clearly and present grouped editing content in a more explicit hierarchy than the inline quick-edit surface, using text hierarchy and spacing as the primary structure

#### Scenario: Slot has both common and advanced settings
- **WHEN** the dialog renders a slot with mixed-complexity fields
- **THEN** the dialog SHALL differentiate foundational settings from deeper configuration without forcing all content into the same visual priority or surrounding every group with equivalent bordered panels

#### Scenario: User creates a new slot
- **WHEN** the Create Slot dialog opens
- **THEN** the surface SHALL emphasize the primary editing path in a single coherent hierarchy where slot type, required setup, and optional polish read as one workflow rather than as competing stacked summary sections

#### Scenario: Slot preview badges render with theme tones
- **WHEN** full configuration or slot creation renders slot identity and health badges
- **THEN** those badges SHALL preserve readable text contrast and SHALL NOT degrade into border-only pills because of resource scope mismatches
