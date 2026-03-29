## Context

The current plugin configuration system in Pulsar has two parallel definition paths:

1. **IPluginConfigurable.GetSettingsDefinition()** - Returns `PluginSettingDefinition` objects, used by Settings UI
2. **PluginMetadata.Schema** - Returns `PropertySchema` objects, used for metadata and documentation

The Settings UI only reads from the first path. This creates problems:
- PkiPlugin defines settings in Schema but users cannot see them in UI
- WinSwitcher requires hardcoded special handling in PluginViewModel.cs to work around this
- Developers must maintain duplicate definitions in two places
- Extension plugins (SimpleCommand, VbaRunner, Bookmarklet) have no configuration UI

## Goals / Non-Goals

**Goals:**
- Unify plugin settings to use Schema as the single source of truth
- Enable configuration UI for all plugins that have Schema-defined settings
- Remove hardcoded special-case handling in PluginViewModel
- Add basic configuration options to extension plugins
- Support multi-value settings (arrays, comma-separated lists)

**Non-Goals:**
- Complete removal of IPluginConfigurable interface (backward compatible during transition)
- Runtime plugin marketplace configuration (future work)
- Plugin configuration import/export (future work)

## Decisions

### Decision 1: Adapter Pattern for Schema-to-UI Conversion

**Choice:** Create an adapter that converts `PropertySchema` (from Metadata) to `PluginSettingDefinition` (for UI).

**Rationale:** 
- Minimal code changes to existing plugins
- Backward compatible - existing IPluginConfigurable implementations continue working
- Schema already has rich property definitions including validators, types, descriptions

**Alternative Considered:**
- *Generate Schema from PluginSettingDefinition* - Rejected because Schema is more comprehensive and already used for metadata

```csharp
public class SchemaToSettingAdapter
{
    public static IEnumerable<PluginSettingDefinition> Convert(ConfigSchema schema) { ... }
}
```

### Decision 2: MultiSelect Setting Type

**Choice:** Add `MultiSelect` to `PluginSettingType` enum and implement UI for it.

**Rationale:** 
- WinSwitcher blacklist is a list of process names, not a single string
- Current String type with comma-separation is error-prone for users
- MultiSelect with available options is more user-friendly

**Alternative Considered:**
- *Keep comma-separated String* - Rejected due to poor UX and validation complexity

### Decision 3: Enable Extension Plugin Configuration

**Choice:** Add basic configurable settings to SimpleCommandPlugin, VbaRunnerPlugin, and BookmarkletRunnerPlugin.

**Rationale:**
- Users may want to customize default behavior (e.g., default delay, preferred input method)
- Low effort to add, high value for power users
- Demonstrates the unified configuration system

### Decision 4: Preserve Hardcoded Logic as Fallback

**Choice:** During transition, if a plugin has both Schema AND hardcoded ViewModel handling (like WinSwitcher), prefer Schema-driven UI.

**Rationale:**
- Gradual migration path
- Prevents breaking existing functionality
- Allows testing Schema-driven approach incrementally

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|------------|
| Schema definition changes require code changes | Medium | Use source generators to auto-generate Schema from code attributes |
| Performance of dynamic UI generation | Low | Cache converted definitions, only regenerate on Schema version change |
| Validation logic divergence | Medium | Ensure PropertySchema validators map 1:1 to PluginSettingViewModel validators |

## Migration Plan

1. **Phase 1: Adapter Implementation**
   - Create `SchemaToSettingAdapter` class
   - Modify `PluginSettingsDialogViewModel` to use adapter as primary source
   - Test with PkiPlugin

2. **Phase 2: PkiPlugin Fix**
   - Verify PkiPlugin settings now render in UI
   - Remove any workarounds in documentation

3. **Phase 3: WinSwitcher Cleanup**
   - Add MultiSelect type support
   - Replace hardcoded ProcessBlacklistViewModel with schema-driven UI
   - Remove special case in PluginViewModel.cs

4. **Phase 4: Extension Plugins**
   - Add basic settings to SimpleCommandPlugin
   - Add basic settings to VbaRunnerPlugin
   - Add basic settings to BookmarkletRunnerPlugin

## Open Questions

1. Should we deprecate IPluginConfigurable entirely in v2.0? 
   - Current: Keep both paths during v1.x transition
   
2. How to handle plugins that need runtime-dependent options (e.g., process list for WinSwitcher)?
   - Option A: Lazy-load options at UI render time
   - Option B: Provide a callback interface for dynamic options
   - Decision needed before Phase 3
