## ADDED Requirements

### Requirement: CascadeSubMenuDescriptor SHALL carry a layout style
The cascade submenu payload SHALL declare which sub-layout form (Ring or Fan) renders its children, and SHALL be a first-class routable descriptor like the window payload.

#### Scenario: Descriptor declares Fan layout
- **WHEN** a `CascadeSubMenuDescriptor` is created with `LayoutStyle = Fan`
- **THEN** the session SHALL route it to the cascade strategy
- **AND** the strategy SHALL lay the children out using Fan geometry

#### Scenario: Descriptor declares Ring layout
- **WHEN** a `CascadeSubMenuDescriptor` is created with `LayoutStyle = Ring`
- **THEN** the session SHALL route it to the cascade strategy
- **AND** the strategy SHALL lay the children out using Ring geometry

#### Scenario: Style defaults to Fan
- **WHEN** a `CascadeSubMenuDescriptor` is created without specifying a layout style
- **THEN** the style SHALL default to Fan

### Requirement: Cascade pagination SHALL derive from sub-slot count
When a cascade has more children than the configured slots per page, the system SHALL page the remaining children using the existing submenu pagination behavior.

#### Scenario: Children exceed slots per page
- **WHEN** a cascade has more `SubSlots` than the current `SlotsPerPage`
- **THEN** the cascade SHALL present only the first page's children
- **AND** the total page count SHALL be derived from `SubSlots.Count`
- **AND** paging SHALL reuse the existing submenu page navigation

#### Scenario: Children fit in one page
- **WHEN** a cascade has fewer or equal `SubSlots` than `SlotsPerPage`
- **THEN** all children SHALL be visible on a single page
- **AND** the total page count SHALL be one
