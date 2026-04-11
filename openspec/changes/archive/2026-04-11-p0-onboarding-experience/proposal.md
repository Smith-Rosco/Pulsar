## Why

Pulsar already has strong core capabilities, but the first-run experience still asks users to understand modes, plugins, and slot configuration before they can feel the value of the product. This change is needed now to turn the current P0 priorities into a coherent onboarding flow that helps new users reach their first successful action quickly and with less configuration friction.

## What Changes

- Add a guided first-run onboarding flow that teaches the difference between Switch Mode and Command Mode through a short, success-oriented tutorial.
- Add a first-launch setup wizard that lets users choose a usage profile and common apps, then generates a working default slot configuration.
- Add a scenario-based slot creation entry point so users can create slots from intent like "switch app" or "open URL" instead of selecting plugins first.
- Add user-facing execution feedback for common success and failure outcomes so action results are understandable without reading logs.

## Capabilities

### New Capabilities
- `guided-onboarding-tutorial`: A short interactive tutorial that leads users through creating and testing their first useful Pulsar actions.
- `first-launch-setup-wizard`: A first-run wizard that generates a usable default configuration based on user profile and common apps.
- `scenario-based-slot-authoring`: An intent-first slot creation flow that maps common user goals onto the appropriate plugin and action configuration.
- `user-facing-action-feedback`: Clear, non-technical execution feedback for common success and failure outcomes during action execution.

### Modified Capabilities
- (none - this change introduces new user-facing capabilities without changing existing published requirements)

## Impact

**Affected Code:**
- Tutorial services, triggers, and first-run orchestration under `Pulsar/Pulsar/Services/Tutorial/`
- Settings and slot authoring UI under `Pulsar/Pulsar/Views/` and `Pulsar/Pulsar/ViewModels/`
- Configuration initialization and startup flow in `Pulsar/Pulsar/App.xaml.cs` and related services
- Execution feedback surfaces in plugin execution, tray notifications, and radial menu feedback paths

**Affected Systems:**
- Tutorial system
- Settings onboarding flow
- Slot authoring flow
- Default configuration generation
- User feedback and error messaging

**Dependencies / Constraints:**
- Must preserve existing plugin architecture and plugin action semantics
- Must respect current WPF dialog, theme injection, and button style rules
- Must avoid exposing secrets or technical implementation details in user-facing feedback
