## 1. SVG icon loading

- [x] 1.1 Extend `IconHelper.GetIconFromPath` with an `.svg` branch: extract first `d="..."` path data → `Geometry.Parse` → `DrawingImage` (frozen), cached via the existing `ConcurrentDictionary`; add `IconHelperSvgTests` covering valid path loads, malformed path returns null, cache reuse
- [x] 1.2 Verify PNG/ICO/JPG/BMP and EXE/LNK extraction paths are unchanged and existing icon tests still pass

## 2. CustomIconStore

- [x] 2.1 Add `Services/Interfaces/ICustomIconStore.cs` (Import/GetIcon/List/Delete) and `Services/CustomIconStore.cs` persisting to `%AppData%\Pulsar\CustomIcons\` with timestamp+random filename keys, lazy dir creation, missing/corrupt file resilience
- [x] 2.2 Add `CustomIconStoreTests`: import persists + survives new store instance (restart simulation), list returns imported icons, delete removes file, GetIcon on missing file returns null, malformed filename skipped in list
- [x] 2.3 Register `ICustomIconStore` in `App.xaml.cs` `ConfigureServices` and verify resolution succeeds

## 3. Icon picker import entry

- [x] 3.1 Inject `ICustomIconStore` into `IconPickerViewModel` (optional param) and add `ImportIconCommand`: `OpenFileDialog` (SVG/PNG/ICO/JPG/BMP filter, following SettingsViewModel.cs:984 pattern) → import → refresh list → select new key; hide import when store is null
- [x] 3.2 Add import section + button to `IconPickerContent.xaml`; verify existing `IconPickerViewModel` tests still pass unchanged (no store injected)
- [x] 3.3 Add `IconPickerImportTests`: import makes icon selectable + key set, cancel changes nothing, store-null hides import command

## 4. Tests & verification

- [x] 4.1 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — full suite green (no regressions in icon/settings tests)
- [x] 4.2 Build `Pulsar/Pulsar/Pulsar.csproj` — 0 errors
- [x] 4.3 Manual QA (requires human): import SVG + PNG icons, assign to a slot, restart persists; malformed SVG falls back without error
