# bookmarklet-example-library

## Purpose

Define the built-in bookmarklet example library: registration of curated example scripts with localized metadata, user browsing, and one-click import that places an editable copy into the user's scripts directory so it can be run and edited in-app.

## Requirements

### Requirement: Example library registers curated scripts with metadata
The system SHALL maintain a library of built-in example scripts, each carrying an identifier, localized title/description, and its script content, targeting common legacy web-page tasks (form fill, data extraction, link traversal).

#### Scenario: Library returns all examples
- **WHEN** the example library is enumerated
- **THEN** it SHALL return all registered examples with their localized metadata

#### Scenario: Library looks up an example by ID
- **WHEN** the example library is queried for a known example ID
- **THEN** it SHALL return the matching example
- **AND** return null when no example matches the given ID

### Requirement: User can import an example as their own script
The system SHALL let a user import an example script, which copies its content into the user's scripts directory (`%APPDATA%\Pulsar\Scripts\`) under a distinct file name so the built-in example is never overwritten.

#### Scenario: Importing an example creates an editable copy
- **WHEN** the user imports an example
- **THEN** a new `.js` file with the example's content SHALL be created in the user's scripts directory
- **AND** the built-in example SHALL remain unchanged

#### Scenario: Import opens in the script editor
- **WHEN** the user imports an example and the script editor is available
- **THEN** the imported copy SHALL open in the in-app script editor ready to edit and save

#### Scenario: Import name collision is avoided
- **WHEN** a file with the intended import name already exists
- **THEN** the import SHALL choose a distinct name (e.g. suffixed) instead of overwriting the existing file
