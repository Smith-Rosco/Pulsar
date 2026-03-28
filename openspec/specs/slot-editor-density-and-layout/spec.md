# slot-editor-density-and-layout

## Purpose
Define density and layout rules that keep inline slot quick edit compact while allowing complex fields to expand when needed.

## Requirements

### Requirement: Quick edit SHALL use compact row-oriented layouts for simple fields
The system SHALL use a denser row-oriented presentation for simple, high-frequency quick-edit fields so expanded slot cards remain materially shorter than full configuration dialogs.

#### Scenario: Quick-edit surface contains simple text and picker fields
- **WHEN** the expanded quick-edit layer renders label, action, appearance, or similarly simple fields
- **THEN** the layout SHALL align labels and controls compactly enough to reduce vertical height compared to stacked field blocks

#### Scenario: User edits several slots in sequence
- **WHEN** the user expands multiple slot cards one after another
- **THEN** the quick-edit layout SHALL preserve sufficient list rhythm for surrounding slots to remain discoverable without excessive scrolling

### Requirement: Complex fields MAY escape compact rows
The system SHALL allow complex controls, long validation, or high-density picker interactions to use full-width presentation when compact rows would harm clarity, but the Create Slot and full configuration dialogs SHALL use compact editor rows or lightweight stacked form groups for simple fields before introducing boxed card treatments.

#### Scenario: Field requires multi-line guidance or a complex picker state
- **WHEN** a parameter's content cannot be clearly represented in a compact row
- **THEN** the UI SHALL be permitted to render that field using a wider or taller layout while preserving the overall quick-edit hierarchy

#### Scenario: Dialog renders advanced configuration
- **WHEN** the full configuration surface contains complex or long-form fields
- **THEN** the dialog SHALL prioritize clarity over strict compactness and SHALL NOT be forced into row-based density for every field

#### Scenario: Dialog renders simple editable fields
- **WHEN** Create Slot or full configuration renders simple text, action, or picker fields
- **THEN** the UI SHALL prefer compact row-oriented or lightly stacked editor layouts instead of wrapping each field in an equivalent bordered card

### Requirement: Visible headings SHALL be minimized in quick edit
The system SHALL avoid redundant section headings inside expanded slot cards and SHALL minimize redundant headings in Create Slot and full configuration by using spacing, alignment, and lightweight grouping before introducing additional visible titles.

#### Scenario: Expanded quick edit contains several related controls
- **WHEN** the UI groups common edits such as identity, key parameters, and appearance
- **THEN** it SHALL prefer lightweight visual grouping over multiple prominent textual headings unless a heading is necessary to prevent ambiguity

#### Scenario: Creation or configuration dialog contains multiple editable regions
- **WHEN** the dialog groups preview, behavior, required fields, and optional polish
- **THEN** it SHALL avoid presenting every region as an equal-weight titled card and SHALL reserve stronger headings for primary editing landmarks only
