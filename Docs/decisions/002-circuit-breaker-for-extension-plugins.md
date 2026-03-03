# ADR-002: Circuit Breaker for Extension Plugins

**Status**: Accepted  
**Date**: 2026-03-01  
**Deciders**: Pulsar Development Team

---

## Context

Pulsar's plugin architecture allows third-party code to run within the main application process. A crashing plugin could bring down the entire application, resulting in poor user experience.

Additionally, some plugins are essential (Core plugins like PKI), while others are optional features (Extension plugins like VbaRunner). The failure handling strategy should differ based on plugin criticality.

---

## Decision

Implement a two-tier plugin architecture with different failure handling:

### Core Plugins
- Essential infrastructure plugins (PKI, Hotkey management)
- Cannot be disabled by users
- Crashes are fatal (application exits)
- No Circuit Breaker protection

### Extension Plugins
- Optional feature plugins (WinSwitcher, VbaRunner, BookmarkletRunner)
- Can be disabled by users
- Protected by Circuit Breaker pattern
- Crashes are isolated and do not affect other plugins

**Circuit Breaker Parameters**:
- **Trigger Condition**: 3 crashes within 1 minute
- **Breaker Duration**: 60 seconds
- **Recovery Strategy**: Half-Open state, allows single retry
- **User Notification**: Windows Toast/Balloon tip via `ITrayService`

---

## Rationale

### Alternatives Considered

1. **All plugins protected by Circuit Breaker**: Rejected because core plugin failures should be immediately visible
2. **No Circuit Breaker (fail-fast for all)**: Rejected because optional plugin crashes shouldn't bring down the app
3. **Process isolation (separate processes per plugin)**: Rejected due to complexity and performance overhead

### Why This Approach

- **Graceful Degradation**: Optional features can fail without affecting core functionality
- **User Awareness**: Toast notifications inform users when plugins are disabled
- **Automatic Recovery**: Plugins can recover after cooldown period
- **Clear Semantics**: Plugin tier (Core vs Extension) clearly communicates failure expectations

---

## Consequences

### Positive

- Extension plugin crashes don't bring down the app
- Users notified via toast when plugin disabled
- Automatic recovery after cooldown period
- Clear distinction between critical and optional plugins

### Negative

- Core plugin crashes still cause application exit (by design)
- Circuit Breaker adds complexity to plugin execution path
- Developers must choose correct plugin tier

### Neutral

- Circuit Breaker state is not persisted across application restarts
- Plugins can implement `IPluginTiered` to explicitly declare their tier

---

## Implementation

See `Docs/architecture/PLUGIN_SYSTEM.md` for full implementation details.

**State Transitions**:
```
Closed (Normal) → Open (Breaker) → Half-Open (Test) → Closed (Recovered)
     ↑                                                    ↓
     └──────────────── Successful Execution ─────────────┘
```

---

## Related Decisions

- [ADR-001: Plugin Metadata System](./001-plugin-metadata-system.md)

---

**Change History**:
- v1.0.0 (2026-03-03): Initial ADR creation
