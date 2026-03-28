# slot-editor-shared-icon-rendering

## Purpose
Define how Create Slot and related picker surfaces interpret icon keys through the shared icon rendering pipeline.

## Requirements

### Requirement: Create Slot picker SHALL render icon keys through the shared icon pipeline
The system SHALL render plugin and slot-type icons in the Create Slot dialog through the shared icon interpretation pipeline used by slot surfaces elsewhere in Pulsar, so values representing Fluent/MDL2 hex glyphs, emoji, or file-backed icon paths are interpreted as semantic icon keys instead of drawn as raw text.

#### Scenario: Plugin type uses a Fluent or MDL2 hex icon key
- **WHEN** the Create Slot picker renders a plugin type whose icon value is a hex key such as `E8A7`
- **THEN** the dialog SHALL convert that icon key into its glyph representation before rendering and SHALL NOT display the literal hex characters or replacement boxes

#### Scenario: Plugin type uses an emoji icon
- **WHEN** the Create Slot picker renders a plugin type whose icon value is an emoji or other non-PUA character
- **THEN** the dialog SHALL render it using the appropriate text/emoji fallback behavior from the shared icon pipeline

#### Scenario: Plugin type uses a file-backed icon path
- **WHEN** the Create Slot picker renders a plugin type whose icon value refers to an image path
- **THEN** the dialog SHALL resolve and show the image through the same icon-key interpretation rules used by slot preview surfaces

### Requirement: Create Slot icon rendering SHALL remain semantically aligned with slot surfaces
The system SHALL avoid maintaining a Create Slot-specific raw glyph rendering path that can diverge from slot preview, slot card, or icon picker behavior.

#### Scenario: Shared icon interpretation changes in the future
- **WHEN** Pulsar updates its shared icon parsing or font-selection behavior
- **THEN** Create Slot plugin type rendering SHALL inherit the same interpretation rules without requiring a separate manual update for raw picker text rendering
