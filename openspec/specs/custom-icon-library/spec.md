# custom-icon-library Specification

## Purpose
Enables users to import and use their own icons (SVG vector and raster formats) as slot/profile icons: SVG path data is parsed into a WPF image, imported files persist to a user-level custom icon store, and the icon picker gains an import entry so imported icons are selectable as `IconKey` values.

## Requirements

### Requirement: SVG icons SHALL be loadable as image sources

The icon loading surface SHALL accept `.svg` files: an SVG file SHALL be parsed (via its path data) into a WPF-compatible image source, so an SVG path can be used anywhere a raster icon (`PNG`/`ICO`/`JPG`/`BMP`) or extracted EXE/LNK icon is used today. The resulting image SHALL be frozen for cross-thread use.

#### Scenario: SVG path loads as an image
- **WHEN** the icon loader receives a path to an SVG file with parseable path data
- **THEN** the loader SHALL return an image source rendered from the SVG geometry
- **AND** the image source SHALL be usable as a slot/profile icon

#### Scenario: Malformed SVG falls back safely
- **WHEN** the icon loader receives an SVG file whose path data cannot be parsed
- **THEN** the loader SHALL return null / no image
- **AND** the caller SHALL fall back to the previous icon without error

### Requirement: Imported icons SHALL persist to a user-level store

The system SHALL provide a custom icon store that persists user-imported icons under `%AppData%\Pulsar\CustomIcons\`. An imported icon SHALL be assigned a stable store key, survive application restarts, and be enumerable for selection in the picker. The store SHALL support import and removal.

#### Scenario: Imported icon persists across restarts
- **WHEN** a user imports an icon into the store
- **THEN** the icon SHALL be written to `%AppData%\Pulsar\CustomIcons\`
- **AND** the store SHALL return it by its key on a subsequent session

#### Scenario: Imported icons are enumerable
- **WHEN** the icon picker requests available custom icons
- **THEN** the store SHALL return all persisted imported icons with their keys and previews

#### Scenario: Removed icon no longer resolves
- **WHEN** a user removes an imported icon from the store
- **THEN** the icon file SHALL be removed from the store directory
- **AND** the store SHALL no longer return it by its key

### Requirement: The icon picker SHALL expose an import entry

The icon picker dialog SHALL offer an "import custom icon" entry that lets the user choose a local image file (SVG/PNG/ICO/JPG/BMP), persists it via the custom icon store, and makes it immediately selectable as the slot/profile `IconKey`.

#### Scenario: Import via picker makes icon selectable
- **WHEN** the user imports a local icon file through the picker
- **THEN** the imported icon SHALL appear in the picker's selectable list immediately
- **AND** selecting it SHALL set the slot/profile `IconKey` to the store key

#### Scenario: Cancel import changes nothing
- **WHEN** the user opens the import file dialog and cancels
- **THEN** no file SHALL be added to the store
- **AND** the current icon selection SHALL remain unchanged
