## 1. Preview Contract

- [x] 1.1 Audit the current radial-menu preview pipeline in `RadialMenuVisualStateCoordinator`, `PreviewService`, and `WindowService` and map each existing failure path to the new layered preview states.
- [x] 1.2 Introduce an internal preview result contract that distinguishes live preview, snapshot fallback, and icon fallback outcomes without overloading `null` as the only signal.
- [x] 1.3 Update the preview-facing service interfaces so callers can request a resolved preview state instead of only a captured bitmap.

## 2. Preview Providers And Cache

- [x] 2.1 Implement a primary DWM-thumbnail-backed preview provider for the radial-menu center preview host.
- [x] 2.2 Retain and adapt the existing `PrintWindow` capture path as a secondary snapshot provider that can refresh last-known-good preview data.
- [x] 2.3 Replace session-only preview cache clearing with last-known-good cache semantics, including explicit invalidation for destroyed or invalid target windows.
- [x] 2.4 Ensure higher-priority provider failures do not erase an already valid lower-priority preview representation.

## 3. Radial Menu Integration

- [x] 3.1 Update `RadialMenuVisualStateCoordinator` to resolve preview state through the layered pipeline for window-backed hover targets.
- [x] 3.2 Update `RadialMenuViewModel` and the radial menu preview host so the center slot renders correctly for live preview, snapshot fallback, and icon fallback states.
- [x] 3.3 Remove or narrow the current unconditional preview cache clearing on menu show so later sessions can reuse last-known-good snapshots.
- [x] 3.4 Ensure minimized, cloaked, invalid, and non-previewable windows degrade cleanly without affecting slot selection or execution.

## 4. Validation

- [x] 4.1 Add or update automated tests around preview resolution ordering, cache preservation, and invalidation behavior.
- [ ] 4.2 Validate the preview experience manually against normal desktop windows, long-idle Remote Desktop windows, minimized windows, and windows with no available live preview.
- [x] 4.3 Run the relevant test suite and `dotnet build Pulsar/Pulsar/Pulsar.csproj` to confirm the change compiles and the preview contract remains stable.
