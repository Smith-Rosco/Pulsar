# Pulsar

Pulsar is a Windows productivity launcher that summons a hotkey-invoked radial menu, optimized for blind operation through muscle memory. All functionality is delivered by plugins dispatched against an immutable context snapshot.

## Language

### Interaction

**Radial Menu**:
A circular menu invoked by a global hotkey, arranging Slots spatially around a center point so actions can be selected without looking.
_Avoid_: Grid, wheel, pie menu

**Command Mode**:
The radial menu mode (default `Ctrl+Q`) that presents the statically configured Slots of the active window's Profile.
_Avoid_: Action mode, ShowSwitcher (legacy hotkey ID)

**Switch Mode**:
The radial menu mode (default `Ctrl+Shift+Q`) for activating other applications: static Slots on the outer ring, the MRU Window in the center.
_Avoid_: Task mode, ShowGrid (legacy hotkey ID)

**Slot**:
A fixed radial position within a mode, bound to one plugin Action and its arguments. Positions never move, so layout can be learned by feel.
_Avoid_: Item, button, grid item

**Action**:
A named operation of a Plugin that a Slot invokes with a set of arguments.
_Avoid_: Command (overloaded with Command Mode)

**MRU Window**:
The most recently used window, surfaced at the center of Switch Mode for instant return to the previous application.
_Avoid_: Last window, previous tab

**Muscle Memory**:
The guiding principle that spatial layout is static and learned by feel; reordering by usage frequency is deliberately forbidden.

**Menu Session**:
The stateful lifetime of one Radial Menu invocation: visibility, hovered Slot, paging, submenu morph, and input decisions. A pure logic module; the ViewModel projects its state for binding.
_Avoid_: RadialMenuViewModel, menu state holder

**Focus Boomerang**:
The guarantee that focus returns to the window that invoked the Radial Menu before a plugin injects input into it.

### Plugin System

**Plugin**:
A self-describing unit of functionality invoked from a Slot.
_Avoid_: Module, add-on

**Plugin Tier**:
The classification of a Plugin as Core or Extension, which determines whether it can be disabled and how its failures are treated.

**Core Plugin**:
An essential Plugin that cannot be disabled; its failure is fatal to the application.
_Avoid_: System plugin

**Extension Plugin**:
An optional Plugin that can be disabled; its failures are isolated and governed by a circuit breaker.
_Avoid_: Optional plugin

**External Plugin**:
A Plugin loaded from outside the application whose manifest permissions must each be explicitly granted by the user before it may run. Provenance, not a Tier — an External Plugin is still either Core or Extension.

**PulsarContext**:
The immutable snapshot of the environment (foreground window, process) captured once at menu invocation and handed to plugins; plugins never query live window state.
_Avoid_: Context, window state, environment

**PluginExecutionContext**:
A stack-scoped per-execution scope carrying correlation data (plugin ID, Action, execution ID). Distinct from PulsarContext, which never holds per-execution data.
_Avoid_: Context

### Configuration & Secrets

**Profile**:
A per-application configuration, keyed by process name, mapping an application to its Command Mode and Switch Mode Slots; the special `Global` Profile applies regardless of the foreground process.
_Avoid_: App profile, config

**Profiles.json**:
The single source of truth for business configuration: Profiles, Slots, settings, and hotkeys.
_Avoid_: Config file

**Secret**:
A credential entry managed by the PKI module; display metadata (ID, Label, Icon) lives in Profiles.json while the encrypted payload lives in secrets.json.
_Avoid_: Password, credential item

**SecretPayload**:
The encrypted sensitive half of a Secret (account + data), stored separately from display metadata and linked by ID.

**PKI**:
The Core module that stores Secrets and injects credentials into target windows.
_Avoid_: Password manager
