## Why

Pulsar has been repositioned as a "heavy-duty office automation workbench" (M0/M1), but the path from first launch to a working automation is still "build it yourself": users must manually create slots, write macros, and configure credentials. The office-action preset pack removes that friction by shipping ready-to-use action packs (Excel/WPS macro templates, common form fills, sign-in flows) that install in one click — directly serving the M2 acceptance goal "a new user completes their first automation within 10 minutes".

## What Changes

- **Preset pack mechanism**: a defined preset-pack format (metadata + action set), packaging layout, discovery/registry, and one-click install/uninstall that writes the resulting command slots into `Profiles.json` (via the existing `ConfigEditSession` revision-guarded path).
- **Preset pack catalog**: a built-in catalog of first-party packs served out of the box, plus the ability to load additional packs from a plugin-style package directory.
- **First-party packs (initial content)**:
  - Excel/WPS macro templates (hosted on `com.pulsar.vbarunner`).
  - Common form fill / sign-in flows (hosted on `com.pulsar.pki` / `com.pulsar.bookmarklet`).
- **Install-time permissions**: installing a pack that touches PKI or web-script capabilities performs the same permission gating as external plugins (grant before activation).
- **Onboarding linkage**: the first-launch experience surfaces the preset pack catalog as the recommended entry, so the initial config generation can seed from a pack selection.

## Capabilities

### New Capabilities
- `office-action-presets`: Defines the preset-pack model (metadata + slot actions + prerequisite checks), the built-in catalog, the install/uninstall lifecycle (including permission gating and `Profiles.json` writes), and the first-launch linkage that seeds initial configuration from a pack selection.

### Modified Capabilities
- None. The first-launch linkage is defined as part of the new `office-action-presets` capability (a pack can serve as the seed for onboarding initial-config generation); no existing capability's requirements change.

## Impact

- **Configuration**: `Profiles.json` gains preset-pack install state (installed pack ids); writes go through `ConfigEditSession` (revision-guarded) — no direct file mutation.
- **Services**: new `PresetCatalogService` / preset install orchestration in `Services/`; reuse `PluginPermissionService` for pack permission gating.
- **Plugins**: packs host actions on existing plugins (`com.pulsar.vbarunner`, `com.pulsar.pki`, `com.pulsar.bookmarklet`); no breaking change to plugin contracts.
- **Onboarding**: first-launch wizard surfaces the catalog; `OnboardingTemplateService.BuildInitialConfig()` optionally seeds from a pack.
- **Localization**: new preset-pack display strings in `Strings.resx` + `Strings.zh-CN.resx`.
- **Assets**: pack content shipped under a new `Assets/Presets/` directory.
