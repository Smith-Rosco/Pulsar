## MODIFIED Requirements

### Requirement: Quick edit SHALL use compact row-oriented layouts for simple fields
The system SHALL use a denser row-oriented presentation for simple, high-frequency quick-edit fields so expanded slot cards remain materially shorter than full configuration dialogs. The unified slot editor SHALL render the Behavior and Appearance sections as compact row-oriented groups rather than boxed card containers.

#### Scenario: Quick-edit surface contains simple text and picker fields
- **WHEN** the expanded quick-edit layer renders label, action, appearance, or similarly simple fields
- **THEN** the layout SHALL align labels and controls compactly enough to reduce vertical height compared to stacked field blocks

#### Scenario: User edits several slots in sequence
- **WHEN** the user expands multiple slot cards one after another
- **THEN** the quick-edit layout SHALL preserve sufficient list rhythm for surrounding slots to remain discoverable without excessive scrolling

### Requirement: Complex fields MAY escape compact rows
The system SHALL allow complex controls, long validation, or high-density picker interactions to use full-width presentation when compact rows would harm clarity. Both the Create and Edit Slot dialogs SHALL use the same compact row layout for the unified Behavior and Appearance sections, eliminating layout divergence between the two dialogs.

#### Scenario: Field requires multi-line guidance or a complex picker state
- **WHEN** a parameter's content cannot be clearly represented in a compact row
- **THEN** the UI SHALL be permitted to render that field using a wider or taller layout while preserving the overall editing hierarchy

#### Scenario: Dialog renders advanced configuration
- **WHEN** the full configuration surface contains complex or long-form fields
- **THEN** the dialog SHALL prioritize clarity over strict compactness and SHALL NOT be forced into row-based density for every field

#### Scenario: Dialog renders simple editable fields
- **WHEN** Create Slot or Edit Slot configuration renders simple text, action, or picker fields
- **THEN** the UI SHALL prefer compact row-oriented or lightly stacked editor layouts instead of wrapping each field in an equivalent bordered card

#### Scenario: Unified picker renders slot type choices
- **WHEN** the unified picker renders primary intent cards and browse-all plugin types
- **THEN** the selection surface SHALL use a consistent card grid and SHALL NOT use the former dual-column (left picker + right config) layout

### Requirement: Visible headings SHALL be minimized in the unified editor
The system SHALL avoid redundant section headings inside expanded slot cards and SHALL minimize redundant headings in the unified slot editor by using spacing, alignment, and lightweight grouping before introducing additional visible titles. The unified editor SHALL use exactly three section landmarks: Behavior, Appearance, and Advanced — consistent across Create and Edit modes.

#### Scenario: Expanded quick edit contains several related controls
- **WHEN** the UI groups common edits such as identity, key parameters, and appearance
- **THEN** it SHALL prefer lightweight visual grouping over multiple prominent textual headings unless a heading is necessary to prevent ambiguity

#### Scenario: Creation or edit dialog contains multiple editable regions
- **WHEN** the dialog groups preview, behavior, required fields, and optional polish
- **THEN** it SHALL avoid presenting every region as an equal-weight titled card and SHALL reserve stronger headings for the three primary editing landmarks only: Behavior, Appearance, Advanced

#### Scenario: Create or Edit dialog renders the full authoring flow
- **WHEN** the unified slot editor renders the configuration view
- **THEN** the single-column layout SHALL use a small number of strong landmarks (Behavior, Appearance, Advanced) and SHALL rely on spacing and ordering before introducing explanatory headings for every subsection, consistent between Create and Edit modes
