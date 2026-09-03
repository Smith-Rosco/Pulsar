## 1. Pillar priority mapping

- [x] 1.1 Add `WorkbenchPillarCatalog` under `Services/` mapping the three pillars to plugin ids (`com.pulsar.vbarunner` / `com.pulsar.bookmarklet` / `com.pulsar.pki`) and entry ids, with a background bucket for everything else; verify a unit test returns the three pillars ahead of system entries
- [x] 1.2 Verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` passes with the new catalog

## 2. Settings navigation reorder

- [x] 2.1 Update `SettingsPageCatalog` registration order so workbench entries lead and analytics/about trail, exposing grouping via the registration; verify a unit test asserts the new deterministic page order
- [x] 2.2 Verify `SettingsWindow` renders navigation in catalog order (manual smoke: open Settings, confirm order)

## 3. Plugin list grouping & ordering

- [x] 3.1 Wire `PluginManagerViewModel`'s `GroupedPlugins` rebuild to order groups by pillar priority (pillar group first, system group after, deterministic member order); verify a unit test that pillar plugins appear in the leading group and system plugins in the trailing group
- [x] 3.2 Update any existing plugin-list assertions to the new deterministic order and verify the plugin management tests pass

## 4. First-launch presentation order

- [x] 4.1 Reorder `FirstLaunchSetupWizardViewModel`'s usage-profile presentation so office-automation options appear first; verify a unit test asserts the option order and that selection still drives `BuildInitialConfig` unchanged

## 5. Localization & integration

- [x] 5.1 Add/refresh workbench-framed entry titles/descriptions in `Strings.resx` + `Strings.zh-CN.resx` and verify both files carry the keys with no hardcoded strings in code/XAML
- [x] 5.2 Run full test suite (`dotnet test`) and verify all tests pass with 0 warnings/errors; manual smoke: open Settings + Plugins + first-launch to confirm the new order
