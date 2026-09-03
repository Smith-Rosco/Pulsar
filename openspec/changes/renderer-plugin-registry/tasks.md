## 1. Registry & factory

- [x] 1.1 Create `Core/Rendering/IRadialRendererRegistry.cs` + `RadialRendererRegistry.cs`: thread-safe register/unregister by `(rendererId, ownerId)`, `UnregisterOwner`, `TryGet`, `Registrations`, `Changed` event, reserved-id rejection, optional `canRegisterOwner` delegate
- [x] 1.2 Extend `StyleRendererFactory` with optional `IRadialRendererRegistry` (resolution order: registry → built-in set → Default) and `GetAvailableRenderers()` union enumeration; keep the single-argument constructor compatible
- [x] 1.3 Add `RadialRendererRegistryTests` (register/duplicate/reserved/unknown-owner rejection, unregister, UnregisterOwner idempotency, Changed event) and extend `StyleRendererFactoryTests` (plugin id resolves, plugin removal → Default fallback, reserved id cannot shadow built-ins)

## 2. Permission & kernel lifecycle

- [x] 2.1 Add `PluginPermissions.UiRender = "ui.render"` to the known-token catalog
- [x] 2.2 Wire registry in `App.xaml.cs`: reserved ids from the three built-in `RendererId` constants, `canRegisterOwner` delegate checking `PluginProfile.GrantedPermissions` via `IConfigService`
- [x] 2.3 Inject optional `IRadialRendererRegistry` into `PluginRuntimeKernel` and call `UnregisterOwner(pluginId)` after disable and in `UnloadAllAsync`; add kernel test asserting cleanup on disable and unload

## 3. Cache invalidation & settings UI

- [x] 3.1 `SlotOrb`: lazily subscribe the registry `Changed` event and reset the static renderer cache so removal falls back safely on next hover
- [x] 3.2 `SettingsViewModel.General`: replace hard-coded renderer options with `RendererOptions` (built-ins localized via `Settings.Appearance.RendererStyle.*`, plugin entries show raw Id) refreshed on `Changed`
- [x] 3.3 `SettingsGeneralPage.xaml`: bind the renderer `ComboBox` to `RendererOptions` (`SelectedValuePath="Id"`), keep `RendererStyle` persistence through the edit-session draft unchanged
- [x] 3.4 Verify no new user-facing strings are hardcoded (plugin renderers display their Id; built-in labels reuse existing resx keys)

## 4. Docs & verification

- [x] 4.1 Update `PLUGIN_DEVELOPMENT.md`: renderer contribution walkthrough (manifest `ui.render` permission, register in `OnEnableAsync`, UI-thread note for `RenderDecorations`)
- [x] 4.2 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` — 0 errors, 0 new warnings
- [x] 4.3 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — full suite green
- [ ] 4.4 Manual QA (requires human): load a sample renderer plugin, grant `ui.render`, select its renderer in settings, verify menu rendering; disable/uninstall plugin and verify safe fallback to the previously selected built-in renderer
