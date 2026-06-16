# PKI (Pulsar Key Injector) Implementation Report

**Status**: ✅ Archived (Completed)  
**Version**: v3.2.0-beta  
**Implementation Date**: 2026-01-18  
**Archive Date**: 2026-03-01

---

## Overview

This document archives the implementation details of the PKI (Pulsar Key Injector) module, which enables context-aware credential injection with focus management.

## Architecture Evolution

| Feature | Pulsar v2.0 | **Pulsar PKI v3.0** |
|---------|-------------|---------------------|
| **Interaction Logic** | Dual-mode isolation (Launcher vs Command) | **Context-Aware Injection** |
| **Architecture** | Layered Architecture | **Modular Monolith** (Core + Features) |
| **Focus Management** | Conditional Focus Return | **Focus Boomerang** (Capture → Return → Wait → Inject) |
| **Execution Logic** | Idempotent: Switch → Fail → Launch | **Dynamic Injection Sequence**: (Account? → Tab → Pwd → Enter?) |
| **Configuration** | WYSIWYG Process Picker | **Credential Vault** (Vault Editing & Partial Updates) |

---

## Module Structure

```plaintext
Features/Pki/                    # PKI Module
├── PkiHandler.cs                # Supports dynamic sequence (Password Only Mode)
├── Services/
│   ├── CredentialsManager.cs    # DPAPI encryption service
│   └── SecretRepository.cs      # Independent secrets.json repository (with retry logic)
└── Models/
    ├── SecretItem.cs            # UI binding model (with TargetProcessName)
    └── SecretPayload.cs         # Storage model (Account + EncryptedData)
```

---

## Technical Contracts

### Storage Strategy (Split Storage)

- **appsettings.json**: Stores only UI layout structure (`SecretItem` ID, Label, Icon)
- **secrets.json**: Stores sensitive data (`SecretPayload` Account, EncryptedData), linked by ID

**Data Hydration**: `RadialMenuViewModel` loads both files in parallel and fills Payload back into Item in memory.

**Concurrency**: Must save `secrets.json` first and release file lock, **then** save `appsettings.json` (triggers reload event).

### Injection Logic

- **UI State Control**: Use ViewModel properties (e.g., `CanAddSecrets`) to control View element `Visibility`. Never expose PKI entry in Launcher mode.
- **Partial Update Strategy**: When editing credentials, if password field is empty, treat as "keep original password" and only update metadata; if input exists, re-encrypt and overwrite.
- **Dynamic Injection Sequence**: `PkiHandler` should not hardcode `Account → Tab → Password`. If `Account` is empty, automatically downgrade to "Password Only Mode" (`Password → Enter`).

### Focus Management (Focus Boomerang)

**Context Awareness**: When `RadialMenuViewModel.Show()` is triggered (before display), **must** call `WindowService.SetPreviousWindow` to capture current foreground window handle.

**Focus Boomerang Flow**:
1. `PkiHandler` starts execution
2. `HideMainWindow()` (Dispatcher Invoke)
3. `GetPreviousWindow()` retrieves previously captured handle
4. `SetForegroundWindow` forcefully returns focus
5. `await Task.Delay(100)` (buffer time)
6. `SendKeys`

---

## Implemented Features

| Module | Feature | Implementation Summary |
|--------|---------|------------------------|
| **PKI** | **Context Restriction** | `SettingsViewModel` introduces `CanAddSecrets` property, combined with `BooleanToVisibilityConverter`, only shows add button in Global/Profile mode |
| **PKI** | **Credential Editing** | Added `EditSecretCommand`, added edit button in `DataTemplate`; dialog supports `LoadForEdit`, implements empty password retention logic |
| **PKI** | **Password Only Mode** | `PkiHandler` refactored injection flow: detects `Account` field, if empty skips Account input and Tab key, directly injects password |
| **PKI** | **Split Storage** | `SettingsViewModel` intercepts save logic, strips sensitive fields to `SecretRepository` writing to `secrets.json` |
| **PKI** | **Context Awareness** | `RadialMenuViewModel` compares `ForegroundProcess` with `SecretItem.TargetProcessName` on `Show`, triggers `IsRecommended` highlight on match |
| **PKI** | **Focus Capture** | `WindowService` upgraded to state machine, "snapshots" current window handle when radial menu is invoked, solves input focus loss |
| **UI** | **Golden Halo** | `JellyOrb` adds DataTrigger, plays golden breathing animation when `IsRecommended=True` |

---

## Troubleshooting

### Q: Configuration save error `System.IO.IOException: The process cannot access the file ... secrets.json`?

**Cause**: `SettingsViewModel` executes saves in parallel (`Task.WhenAll`), main config save immediately triggers reload event, causing read operation to conflict with unfinished write operation.

**Fix**: Change to sequential execution: `await SaveSecrets(); await SaveConfig();`, and add retry mechanism in `SecretRepository`.

### Q: PKI triggered but no response, focus didn't return to editor?

**Cause**: Context not captured when radial menu invoked, causing `PkiHandler` to get `PreviousWindowHandle` as `IntPtr.Zero`.

**Fix**: Call `WindowService.SetPreviousWindow(GetForegroundWindow())` at first line of `RadialMenuViewModel.Show()` method.

---

## Implementation Phases

### Phase 7.1: Modular Refactoring ✅
- Established `Features/Pki`
- Implemented `ActionRegistry` and `IActionHandler`

### Phase 7.2: Credential Core ✅
- Implemented `SecretItem` model
- Implemented `CredentialsManager` (DPAPI) encryption logic

### Phase 7.3: Injection Core ✅
- Implemented **Focus Boomerang** logic
- Solved focus loss issue

### Phase 7.4: Credential Entry UI ✅
- Developed `QuickSecretsDialog`
- Implemented split storage (`secrets.json`)

### Phase 7.5: Context Integration ✅
- Implemented process name matching highlight (Context Awareness)
- Implemented data hydration

### Phase 7.6: Interaction Refinement ✅
- Implemented context button restriction
- Implemented credential modification feature
- Implemented password-only mode support

---

**Archive Reason**: PKI implementation completed, content superseded by ARCHITECTURE.md v4.0.0  
**Related Documents**: 
- [ARCHITECTURE.md](../../../ARCHITECTURE.md) - Current system architecture
- [PLUGIN_DEVELOPMENT.md](../../../PLUGIN_DEVELOPMENT.md) - Plugin development guide
