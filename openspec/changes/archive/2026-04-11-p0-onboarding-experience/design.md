## Context

Pulsar's current product surface already supports radial switching, command execution, plugin-backed automation, and an in-progress interactive tutorial system. The gap is not raw capability but first-run usability: new users must understand hotkeys, mode differences, plugin choices, and slot authoring before reaching their first meaningful success.

This change spans multiple parts of the application: first-launch detection, tutorial orchestration, settings and slot authoring UX, configuration generation, and execution feedback. The design must preserve existing plugin architecture and muscle-memory assumptions while adding a clearer path from installation to first successful action.

## Goals / Non-Goals

**Goals:**
- Provide a short first-run flow that gets users to at least one successful Switch Mode action and one successful Command Mode action.
- Generate a usable default configuration during first launch instead of presenting an empty or fully manual setup.
- Replace plugin-first slot creation with an intent-first entry path for the most common slot types.
- Surface action execution outcomes in user-facing language for common success and failure cases.
- Keep the implementation compatible with current plugin metadata, slot models, and configuration persistence.

**Non-Goals:**
- No redesign of the radial menu interaction model.
- No plugin system rewrite, plugin marketplace, or new plugin runtime model.
- No cloud sync, telemetry backend, or account system.
- No removal of advanced plugin-first editing for power users.
- No attempt to solve all onboarding needs for every plugin; this change focuses on the highest-frequency built-in scenarios.

## Decisions

### 1. Ship onboarding as a composed flow, not a single monolithic wizard

**Decision:** Implement onboarding as three cooperating layers:
- a startup coordinator that decides whether onboarding should run
- a first-launch setup wizard that creates the initial configuration
- a guided tutorial that teaches first successful actions after setup

**Rationale:** Startup setup and interactive tutorial have different responsibilities. The setup wizard is best for collecting choices and generating defaults. The tutorial is best for reinforcing mental models through guided success. Keeping them separate reduces complexity and allows each to be skipped or resumed independently.

**Alternatives considered:**
- One large wizard for everything: simpler to discover, but harder to maintain and poorly suited to interactive in-app guidance.
- Tutorial only, no setup wizard: leaves users with too much manual configuration work.

### 2. Keep default configuration generation deterministic and template-driven

**Decision:** Generate initial slots from a small set of built-in templates keyed by user profile and selected apps, rather than trying to infer a personalized setup automatically.

**Rationale:** Deterministic templates are easy to test, explain, and evolve. They align with Pulsar's static configuration philosophy and avoid hidden logic that users cannot understand or adjust.

**Alternatives considered:**
- AI-generated or heuristic-generated defaults: flexible, but too opaque and high-risk for a P0 onboarding path.
- No defaults: preserves purity, but fails the goal of immediate usefulness.

### 3. Introduce an intent-to-plugin mapping layer in slot authoring

**Decision:** Add a scenario-based authoring surface that maps a small set of common user intents onto existing plugin/action pairs.

The initial supported intents are:
- Switch to or launch an app
- Open a program, file, folder, or URL
- Send keys or insert text
- Fill a saved credential

**Rationale:** This preserves the current plugin architecture while reducing cognitive load for common cases. It also avoids duplicating plugin execution logic because the output is still a standard slot configuration.

**Alternatives considered:**
- Rebuild slot configuration around a new domain model unrelated to plugins: cleaner UX, but much larger migration cost.
- Keep only plugin-first editing: lowest implementation cost, but does not solve onboarding friction.

### 4. Add a dedicated user-facing feedback contract above plugin results

**Decision:** Normalize common plugin execution outcomes into user-facing feedback types with short titles, plain-language messages, and optional recovery hints.

The feedback contract should distinguish:
- success
- recoverable failure
- configuration error
- temporary unavailability

Feedback is derived from existing execution results and known failure modes without exposing secrets or raw technical exception data.

**Rationale:** Existing logs and plugin-specific messages are not a reliable user experience. A thin normalization layer lets the product explain outcomes consistently while reusing current plugin behavior.

**Alternatives considered:**
- Let each plugin own all user-facing messages: flexible, but produces inconsistent UX.
- Only show logs or tray notifications: insufficient for onboarding and learnability.

### 5. Preserve advanced editing as a secondary path

**Decision:** The new scenario-based flow becomes the default entry point for creating slots, but advanced users must still be able to access plugin-first editing and the full parameter model.

**Rationale:** Pulsar already serves advanced scenarios such as VBA and bookmarklet execution. Removing detailed editing would regress power-user workflows and create artificial product ceilings.

**Alternatives considered:**
- Replace advanced editing entirely: simpler surface, but blocks existing advanced use cases.

### 6. Gate onboarding by persisted first-run state, not transient runtime checks

**Decision:** Persist a small onboarding state model covering whether the user has completed, skipped, or partially completed first-run onboarding and tutorial milestones.

**Rationale:** First-run experience must survive restarts and partial progress. Persisted state also enables later iteration on resume behavior without changing the overall architecture.

**Alternatives considered:**
- Infer onboarding completion from existing config only: brittle because users may have partial config without understanding the product.

## Risks / Trade-offs

- Tutorial trigger brittleness across WPF windows and pages -> Reuse the existing marker and trigger registry approach, and keep the initial tutorial flow narrow and deterministic.
- Template-generated defaults may not match every user's environment -> Limit first version to selectable, common applications and allow quick editing after generation.
- Scenario-based authoring may diverge from plugin metadata over time -> Keep mappings thin and backed by canonical plugin/action pairs already supported by the system.
- Feedback normalization may hide actionable technical detail -> Show concise user text in-product while preserving detailed logs for debugging.
- Additional onboarding state can introduce startup complexity -> Keep the state model minimal and isolated behind a single service.

## Migration Plan

1. Add onboarding state persistence and startup coordinator without changing current startup behavior for existing users.
2. Implement deterministic default templates and first-launch setup wizard behind the startup coordinator.
3. Complete and narrow the guided tutorial flow so it works against the generated defaults and key settings screens.
4. Add scenario-based slot creation as the default path while preserving advanced editing access.
5. Add feedback normalization and wire it into common plugin execution paths.
6. Verify the flow on a clean profile, then verify existing configured users do not get forced back into onboarding.

Rollback strategy:
- Disable onboarding coordinator entry and retain standard startup.
- Keep generated configuration data compatible with existing slot/configuration models so no data migration rollback is required.

## Open Questions

1. Should the first-launch wizard generate only Switch Mode defaults, or both Switch Mode and one starter Command Mode example?
   - Tentative answer: generate both, because the product needs to teach the two-mode mental model early.

2. Should tutorial completion be marked after full completion or after the first successful action milestone?
   - Tentative answer: store milestone progress separately and treat full tutorial completion as distinct from first success.

3. Where should scenario-based slot creation live in the settings UX?
   - Tentative answer: make it the primary "Add Slot" entry point with an explicit advanced/manual option in the same flow.

4. Should user-facing feedback appear in the radial menu, tray notifications, or dialogs?
   - Tentative answer: use lightweight inline or transient UI first, with tray fallback for background or delayed results.
