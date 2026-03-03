# ADR-001: Plugin Metadata System

**Status**: Accepted  
**Date**: 2026-03-01  
**Deciders**: Pulsar Development Team

---

## Context

Prior to v4.0.0, plugin UI properties (display name, icon, category, badges) were hardcoded in the core application code. This created tight coupling between plugins and the UI layer, requiring core code changes for every new plugin.

Additionally, configuration validation happened at runtime, leading to cryptic errors when users misconfigured plugin slots.

---

## Decision

Plugins self-describe their capabilities via `IPluginMetadataProvider` interface, returning a `PluginMetadata` object containing:

- **Display Info**: Name, icon, category, description
- **UI Hints**: Badge text, color, sort order
- **Capabilities**: Supported actions, dependencies, permissions
- **Config Schema**: JSON schema for validating plugin arguments

```csharp
public interface IPluginMetadataProvider
{
    PluginMetadata GetMetadata();
}
```

---

## Rationale

### Alternatives Considered

1. **External metadata files (JSON/YAML)**: Rejected due to synchronization issues between code and metadata
2. **Attributes on plugin class**: Rejected due to limited expressiveness and inability to compute metadata dynamically
3. **Convention-based discovery**: Rejected due to lack of explicit contract

### Why This Approach

- **Decoupling**: Core code no longer needs to know about plugin-specific UI properties
- **Dynamic UI**: Settings pages can be generated automatically from metadata
- **Early Validation**: Configuration errors detected at startup instead of runtime
- **Extensibility**: New metadata fields can be added without breaking existing plugins

---

## Consequences

### Positive

- New plugins require no core code changes
- UI automatically adapts to plugin capabilities
- Configuration errors detected at startup instead of runtime
- Plugin developers have full control over their UI representation

### Negative

- Plugins must implement additional interface (small overhead)
- Metadata must be kept in sync with actual plugin behavior (developer responsibility)

### Neutral

- Metadata is cached at startup for performance
- Plugins without metadata provider fall back to basic display

---

## Implementation

See `Docs/architecture/PLUGIN_SYSTEM.md` for full implementation details.

---

## Related Decisions

- [ADR-002: Circuit Breaker for Extension Plugins](./002-circuit-breaker-for-extension-plugins.md)

---

**Change History**:
- v1.0.0 (2026-03-03): Initial ADR creation
