## 1. Preset pack model & catalog

- [ ] 1.1 Add `PresetPack` model (`Id`, `TitleKey`, `DescriptionKey`, `CommandSlotTemplates`, `PrerequisiteProvider`, `RequiredPermissions`) under `Features/Presets/Models/` and verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` passes
- [ ] 1.2 Add `IPresetCatalogService` + `PresetCatalogService` (static first-party registration, `All`/`GetById`) mirroring `TutorialScenarioRegistry` and verify a unit test enumerates the initial packs and looks one up by id (null on miss)
- [ ] 1.3 Add `Assets/Presets/` payload directories for the 3 initial packs (macro / form-fill / sign-in) and verify each pack's payload path resolves under the repo Assets folder

## 2. Install / uninstall lifecycle

- [ ] 2.1 Add `IPresetInstallService` with `InstallAsync(pack)` that converts `CommandSlotTemplate`s to `PluginSlot`s and appends them to the Global profile's `CommandMode` via `ConfigEditSession`, recording `installedPresetPacks` (id+version); verify unit test: 2-template pack adds exactly 2 slots + records installed state
- [ ] 2.2 Add same-version re-install rejection and verify a unit test that a second install of the same version throws with a readable message and no config change
- [ ] 2.3 Add `UninstallAsync(pack)` that removes only the pack's traced slots and clears installed state; verify a unit test that unrelated user slots survive uninstall and that uninstalling a non-installed pack reports not-installed
- [ ] 2.4 Verify a concurrent-edit test: install during an in-flight config edit follows the existing revision rebase path (no silent overwrite)

## 3. Permission gating

- [ ] 3.1 Wire pack install to `IPluginPermissionService` so packs declaring PKI/web-script/VBA permissions block until granted; verify unit test that an ungranted pack writes no slots and a granted pack completes

## 4. Prerequisite validation

- [ ] 4.1 Support optional `PrerequisiteProvider` on packs (reuse existing checker contract for Excel/VBA/browser) and verify a unit test that an unmet prerequisite blocks install with a readable reason

## 5. First-launch seeding

- [ ] 5.1 Add `OnboardingTemplateService.BuildInitialConfig(PresetPack, selectedApps)` overload and verify a unit test that a selected pack seeds the Global `CommandMode` slots and that no selection falls back to the default scenario

## 6. Localization, wiring & integration

- [ ] 6.1 Add pack display strings to `Strings.resx` + `Strings.zh-CN.resx` (no hardcoded user-facing text) and verify both files contain the new keys
- [ ] 6.2 Register new services in `App.xaml.cs` DI and verify app starts with 0 exceptions
- [ ] 6.3 Run full test suite (`dotnet test Pulsar/Pulsar.Tests/...`) and verify all preset-related tests pass with 0 warnings/errors
