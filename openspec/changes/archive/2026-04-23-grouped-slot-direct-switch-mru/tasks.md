## 1. Selection Contract

- [x] 1.1 Add a dedicated window-selection intent for grouped root-slot direct-trigger execution in the shared switching contracts.
- [x] 1.2 Update the shared selection engine so the new intent returns the MRU target when the current foreground window is outside the target process group.
- [x] 1.3 Update the shared selection engine so the new intent skips the current in-process foreground window and falls back gracefully when no alternate candidate exists.

## 2. Radial Menu Integration

- [x] 2.1 Update grouped root-slot execution to use the new direct-trigger intent for modifier-release execution.
- [x] 2.2 Preserve left-click drill-down behavior for grouped root slots so submenu entry remains unchanged.
- [x] 2.3 Verify grouped submenu default preview and stable submenu ordering continue to use their existing behavior.

## 3. Verification

- [x] 3.1 Add or update window-selection core tests covering out-of-app MRU return, in-app rotation to another recent window, and single-candidate fallback.
- [x] 3.2 Add or update radial-menu/grouped-slot tests to confirm modifier-release direct execution and left-click drill-down remain behaviorally distinct.
- [x] 3.3 Run the relevant test suite and build to verify the change compiles and the new behavior is covered.
