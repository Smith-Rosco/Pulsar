## Why

Pulsar's built-in plugins currently mix different naming styles, action verbs, and usage patterns, which makes it harder for users to predict which plugin to choose and how to configure it. This is now blocking both usability work and runtime fixes because plugin identity, documentation, and slot authoring guidance no longer describe a single coherent mental model.

## What Changes

- Standardize built-in plugin display names so plugin pickers, metadata, and documentation all use the same user-facing naming model.
- Define a consistent action taxonomy that distinguishes primary user-facing actions from compatibility aliases and internal-only commands.
- Establish recommended usage guidance so common intents such as opening an app, switching windows, sending keys, running scripts, and filling credentials map to a clear plugin/action choice.
- Require internal system-control plugins to expose explicit user-visible actions instead of relying on generic wrapper verbs and nested command arguments.
- Update built-in plugin metadata and plugin documentation so the slot editor, docs, and runtime behavior present the same contracts.

## Capabilities

### New Capabilities
- `plugin-display-identity`: Define how built-in plugins present stable display names, descriptions, and documentation identity across the product.
- `plugin-action-semantics`: Define the contract for primary actions, compatibility aliases, and recommended plugin/action selection for common user intents.
- `internal-plugin-command-contract`: Define how internal Pulsar control plugins expose explicit actions without colliding with generic command-runner semantics.

### Modified Capabilities
- None.

## Impact

- Affected runtime code: built-in plugins under `Pulsar/Pulsar/Plugins/`, especially `BasicCommand`, `WinSwitcher`, `Pki`, and `SystemCommand`.
- Affected UI and metadata flows: plugin metadata registration, plugin picker presentation, action lists, and slot-authoring guidance derived from metadata.
- Affected docs: `PLUGIN_DEVELOPMENT.md` and built-in plugin docs under `Docs/Plugins/`.
- Affected compatibility behavior: existing slot configurations may continue to use legacy action aliases internally, but the product must present one consistent user-facing contract.
