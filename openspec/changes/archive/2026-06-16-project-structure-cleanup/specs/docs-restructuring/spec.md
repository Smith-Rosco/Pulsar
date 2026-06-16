## ADDED Requirements

### Requirement: Flatten archive directories
The `Docs/archive/2026-01/` and `Docs/archive/2026-03/` subdirectories SHALL be consolidated into a single `Docs/archive/` directory. Files SHALL retain their date-prefixed filenames.

#### Scenario: 2026-01 subdirectory is removed
- **WHEN** inspecting `Docs/archive/`
- **THEN** `Docs/archive/2026-01/` SHALL NOT exist

#### Scenario: 2026-03 subdirectory is removed
- **WHEN** inspecting `Docs/archive/`
- **THEN** `Docs/archive/2026-03/` SHALL NOT exist

#### Scenario: Archived files are accessible at flat path
- **WHEN** listing `Docs/archive/`
- **THEN** files previously in `2026-01/` and `2026-03/` SHALL be directly in `Docs/archive/` with their original filenames

### Requirement: Archive TUTORIAL_SYSTEM design documents
The TUTORIAL_SYSTEM design documents in `Docs/architecture/` SHALL be moved to `Docs/archive/` as they describe a completed feature.

#### Scenario: TUTORIAL_SYSTEM files are moved
- **WHEN** inspecting `Docs/architecture/`
- **THEN** all files matching `TUTORIAL_SYSTEM*` SHALL NOT exist there

#### Scenario: TUTORIAL_SYSTEM files are in archive
- **WHEN** inspecting `Docs/archive/`
- **THEN** all `TUTORIAL_SYSTEM*` files SHALL exist there with their original names

### Requirement: Archive PHASE2_PROGRESS_REPORT
The file `Docs/PHASE2_PROGRESS_REPORT.md` SHALL be moved to `Docs/archive/`.

#### Scenario: Progress report is archived
- **WHEN** inspecting `Docs/`
- **THEN** `PHASE2_PROGRESS_REPORT.md` SHALL NOT exist in `Docs/` root

### Requirement: Merge PKI architecture documentation
The content from `Docs/architecture/pki/` SHALL be merged into the existing PKI plugin documentation or `Docs/architecture/INPUT_INJECTION.md`. The `Docs/architecture/pki/` subdirectory SHALL be removed.

#### Scenario: PKI architecture subdirectory is removed
- **WHEN** inspecting `Docs/architecture/`
- **THEN** `pki/` subdirectory SHALL NOT exist

#### Scenario: PKI documentation content is preserved
- **WHEN** inspecting `Docs/Plugins/PkiPlugin.md` or `Docs/architecture/INPUT_INJECTION.md`
- **THEN** the key PKI architecture information from the former `pki/` directory SHALL be consolidated in these files

### Requirement: Update Docs/README.md index
The `Docs/README.md` documentation index SHALL be updated to reflect the new archive structure and removed directories.

#### Scenario: Docs/README.md references are accurate
- **WHEN** inspecting `Docs/README.md`
- **THEN** references to `archive/2026-01/` and `archive/2026-03/` SHALL be updated to point to `archive/`; references to `architecture/pki/` SHALL be removed or redirected; references to `architecture/TUTORIAL_SYSTEM*` SHALL be removed

### Requirement: Project builds after docs restructuring
File renames SHALL NOT affect any C# or XAML source files.

#### Scenario: Build passes after docs restructuring
- **WHEN** running `dotnet build Pulsar/Pulsar/Pulsar.csproj`
- **THEN** the build SHALL succeed with zero errors
