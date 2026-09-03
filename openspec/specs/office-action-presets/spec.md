# office-action-presets Specification

## Purpose

Define the office-action preset pack capability: the preset-pack model (metadata + slot actions + optional prerequisite checks), the built-in catalog of first-party packs, the one-click install/uninstall lifecycle (permission gating and revision-guarded `Profiles.json` writes), and the first-launch linkage that can seed initial configuration from a selected pack.

## Requirements

### Requirement: Preset pack model defines pack metadata and action set
The system SHALL define a preset-pack model that bundles all configuration for a pack: identifier, localized display strings, a list of command slot templates, and an optional prerequisite provider reference.

#### Scenario: Pack exposes all required metadata
- **WHEN** a preset pack instance is constructed
- **THEN** it SHALL expose `Id`, `TitleKey`, `DescriptionKey`, `SlotDescriptionKey`, `CommandSlotTemplates` (list of command slot templates), and `PrerequisiteProvider` (optional)

#### Scenario: Pack can define multiple command slots
- **WHEN** a preset pack is defined with multiple command slot templates
- **THEN** all templates SHALL be installable as command slots
- **AND** the templates SHALL target existing plugin actions (e.g. VBA runner `run`, PKI fill, bookmarklet `run`) without requiring new plugin contracts

### Requirement: Built-in preset pack catalog
The system SHALL maintain a catalog of first-party preset packs, register the initial set (Excel/WPS macro templates, common form fills, sign-in flows), and support lookup by ID and enumeration.

#### Scenario: Catalog returns all registered packs
- **WHEN** the preset pack catalog is enumerated
- **THEN** it SHALL return all registered first-party packs

#### Scenario: Catalog looks up a pack by ID
- **WHEN** the catalog is queried for a known pack ID
- **THEN** it SHALL return the matching pack
- **AND** return null when no pack matches the given ID

### Requirement: Pack install writes slots through the revision-guarded config path
The system SHALL install a preset pack by writing its command slots into the Global profile's command-mode slot list through the revision-guarded configuration edit path, and SHALL record the installed pack state; installing the same pack version twice SHALL be rejected.

#### Scenario: Installing a pack adds its slots
- **WHEN** a user installs a pack containing 2 slot templates
- **THEN** the Global profile's command-mode list SHALL contain exactly 2 new slots with the pack's plugin IDs and actions
- **AND** the installed pack SHALL be recorded so its slots can be traced back to it

#### Scenario: Re-installing the same pack version is rejected
- **WHEN** a user attempts to install a pack whose version is already installed
- **THEN** the install SHALL be rejected with a clear message and no configuration change SHALL occur

#### Scenario: Concurrent config edits remain safe
- **WHEN** a pack install commits while another config edit is in flight
- **THEN** the write SHALL follow the existing revision-conflict rebase path instead of silently overwriting the other change

### Requirement: Pack install gates permissions before writing slots
The system SHALL evaluate a pack's permission requirements before installing; when the pack references capabilities such as PKI or web-script execution, the install SHALL not proceed until the user grants the required permissions.

#### Scenario: Ungranted permissions block pack install
- **WHEN** a pack requires PKI permissions that the user has not granted
- **THEN** the install SHALL be blocked with a permission prompt
- **AND** no slots SHALL be written to the configuration until granted

#### Scenario: Granted permissions allow install
- **WHEN** the user grants all required permissions for a pack
- **THEN** the install SHALL complete and write the pack's slots

### Requirement: Pack uninstall removes only its own slots
The system SHALL uninstall a preset pack by removing the slots it created and clearing its installed state, leaving other user configuration untouched.

#### Scenario: Uninstalling a pack removes its slots
- **WHEN** a user uninstalls an installed pack
- **THEN** the slots created by that pack SHALL be removed from the Global profile
- **AND** the pack's installed state SHALL be cleared

#### Scenario: Uninstalling a non-installed pack is reported
- **WHEN** a user attempts to uninstall a pack that is not installed
- **THEN** the system SHALL report that the pack is not installed and make no configuration change

### Requirement: Pack prerequisite validation before install
The system SHALL let a pack declare prerequisite checks (e.g. Excel/VBA or browser availability) and SHALL validate them before installing; an unmet prerequisite SHALL block the install with a readable reason.

#### Scenario: Met prerequisites allow install
- **WHEN** all declared prerequisites of a pack are met
- **THEN** the install SHALL proceed

#### Scenario: Unmet prerequisite blocks install with reason
- **WHEN** a pack declares an Excel prerequisite and Excel is not available
- **THEN** the install SHALL be blocked
- **AND** the user SHALL receive a readable message stating the missing prerequisite

### Requirement: Pack selection can seed first-launch initial configuration
The first-launch flow SHALL be able to use a selected preset pack as the source of initial configuration, generating command slots for that pack, and SHALL fall back to the default onboarding scenario when no pack is selected.

#### Scenario: Selecting a pack seeds initial config
- **WHEN** the first-launch flow seeds initial configuration from a selected Excel-macro pack
- **THEN** the generated initial configuration SHALL contain the slots defined by that pack

#### Scenario: No pack selection falls back to default
- **WHEN** the first-launch flow runs without a pack selection
- **THEN** it SHALL generate the default onboarding scenario configuration
