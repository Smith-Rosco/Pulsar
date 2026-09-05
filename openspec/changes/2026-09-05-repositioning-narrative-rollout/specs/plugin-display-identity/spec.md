# plugin-display-identity Delta

## ADDED Requirements

### Requirement: Built-in plugin display identities SHALL align with the office-automation product narrative

Built-in plugins' canonical display names and descriptions SHALL use the product narrative language of the office-automation workbench positioning (macro execution, legacy-system scripting, secure sign-in, window switching), rather than developer-facing implementation terminology. In particular, the BookmarkletRunner plugin SHALL present itself to users as a web-script / legacy-system entry capability, while its plugin Id and persisted configuration keys remain unchanged.

#### Scenario: BookmarkletRunner presents in product-narrative language

- **WHEN** the plugin picker, plugin settings page, or slot editor displays the BookmarkletRunner plugin's name or description
- **THEN** the displayed text SHALL describe the capability in office-automation narrative terms (web script / legacy-system assistant direction, localized per `ILocalizationService` conventions)
- **AND** the plugin Id and any persisted configuration SHALL be unaffected

#### Scenario: Localization tracks the canonical identity

- **WHEN** a built-in plugin's display name or description is rendered in either supported language (EN / zh-CN)
- **THEN** both language resources SHALL present the same narrative-aligned identity, with no hardcoded user-facing strings in code

#### Scenario: Documentation matches product presentation

- **WHEN** any user-facing documentation references a built-in plugin
- **THEN** it SHALL use the same narrative-aligned canonical name presented in the product UI
