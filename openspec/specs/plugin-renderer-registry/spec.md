# plugin-renderer-registry Specification

## Purpose
Defines how third-party plugins contribute custom radial-menu renderers. A mutable, thread-safe renderer registry accepts plugin-registered `IRadialRenderer` implementations gated by the `ui.render` permission, and cleans them up on disable/unload; UI caches invalidate on registry changes so a removed plugin renderer safely falls back to the Default renderer.

## Requirements

### Requirement: Plugins SHALL be able to contribute radial renderers through a registry

The host SHALL expose a mutable, thread-safe renderer registry (`IRadialRendererRegistry`) as a singleton service. An activated plugin MAY register an `IRadialRenderer` implementation under its own owner id and the host SHALL include it in renderer resolution and settings enumeration.

#### Scenario: Plugin renderer is resolvable after registration
- **WHEN** an enabled plugin registers a renderer with a unique, non-reserved id and its owner id
- **THEN** `StyleRendererFactory.Create(id)` SHALL return that renderer for the registered id
- **AND** the settings renderer selector SHALL list it alongside the built-in renderers

#### Scenario: Reserved built-in ids cannot be shadowed
- **WHEN** a plugin attempts to register a renderer whose id equals a built-in renderer id (case-insensitive)
- **THEN** the registration SHALL be rejected and renderer resolution SHALL continue to return the built-in renderer

### Requirement: Renderer contributions SHALL be permission-gated

Registering a renderer on behalf of an owner SHALL require the owner to hold the `ui.render` permission. Unknown owners SHALL be rejected.

#### Scenario: Owner without ui.render is rejected
- **WHEN** a plugin whose `PluginProfile.GrantedPermissions` does not contain `ui.render` attempts to register a renderer
- **THEN** the registration SHALL fail without throwing
- **AND** renderer resolution SHALL be unaffected

#### Scenario: Registered renderer is honored after grant
- **WHEN** the owner's granted permissions contain `ui.render` and the renderer id is unique and non-reserved
- **THEN** the registration SHALL succeed and raise the registry change event

### Requirement: Renderer contributions SHALL be cleaned up on plugin disable or unload

When a plugin is disabled or unloaded, the host SHALL remove every renderer registered under that plugin's owner id, even if the plugin did not unregister them itself.

#### Scenario: Disable removes plugin renderers
- **WHEN** an enabled plugin that registered renderers is disabled
- **THEN** all renderers owned by that plugin SHALL be removed from the registry
- **AND** subsequent resolution of those renderer ids SHALL fall back to the Default renderer

#### Scenario: Unload removes plugin renderers
- **WHEN** all plugins are unloaded
- **THEN** no plugin-owned renderers SHALL remain in the registry

### Requirement: Cached renderer references SHALL survive registry changes

UI surfaces that cache the resolved renderer SHALL invalidate their cache when the registry changes, so removal of a plugin renderer never leaves a dangling reference.

#### Scenario: Hover after plugin removal falls back safely
- **WHEN** a plugin renderer that was selected and cached is removed from the registry
- **THEN** the next renderer resolution SHALL return the Default renderer (or the newly registered renderer for that id)
- **AND** no stale renderer instance SHALL be used
