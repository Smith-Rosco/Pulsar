## 1. Theme token architecture

- [x] 1.1 Define the authoritative slot tone brush keys in the injected theme resources used by standard windows/pages and the radial host.
- [x] 1.2 Remove or refactor duplicate slot tone brush declarations in `SlotStyles.xaml` so semantic color ownership is unambiguous.

## 2. Slot tone resolution

- [x] 2.1 Update slot tone resolution to use host-safe themed resources instead of `Application.Current` global lookup.
- [x] 2.2 Preserve the existing slot presentation key contract or migrate it consistently so `TypeToneKey` and `HealthToneKey` still resolve to visible semantic defaults.

## 3. Surface migration

- [x] 3.1 Apply the updated slot tone contract to slot-related settings pages, dialogs, and radial menu badge rendering.
- [x] 3.2 Replace remaining affected Wpf.Ui `Appearance`-based buttons with explicit Pulsar button styles where contrast must remain deterministic.

## 4. Validation

- [x] 4.1 Verify slot type and health badges remain readable in slot creation and full configuration surfaces under supported themes.
- [x] 4.2 Verify radial menu slot badges resolve the same semantic tone contract without border-only or transparent-text regressions.
- [x] 4.3 Run the relevant build and test coverage needed to confirm the UI resource contract change does not break compilation or slot presentation behavior.
