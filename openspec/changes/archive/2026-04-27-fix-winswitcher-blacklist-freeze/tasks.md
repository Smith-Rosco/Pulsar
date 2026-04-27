## 1. Lightweight Blacklist Data Path

- [x] 1.1 Add a lightweight running-state lookup path that can serve the WinSwitcher blacklist dialog without building full switchable window candidates.
- [x] 1.2 Update `ProcessBlacklistViewModel` to build its initial list from lightweight registry metadata and running-state membership instead of `GetActiveWindowsAsync()`.
- [x] 1.3 Ensure the blacklist dialog binds a usable process list before expensive icon work completes.

## 2. Icon Loading And UI Responsiveness

- [x] 2.1 Move blacklist dialog icon loading off the critical first-render path using placeholder or deferred icon behavior.
- [x] 2.2 Avoid per-row UI-thread stalls from incremental heavy icon resolution and verify slow or missing icons do not freeze the dialog.

## 3. Inventory Side-Effect Cleanup

- [x] 3.1 Remove or relocate process-registration side effects from configuration-facing discovery reads.
- [x] 3.2 Update any runtime flows that still require process registration so they invoke it explicitly instead of relying on hidden inventory-read side effects.

## 4. Blacklist Semantics Alignment

- [x] 4.1 Split discovery blacklist checks from explicit process-targeted activation paths in WinSwitcher window lookup.
- [x] 4.2 Update any related documentation or metadata text so runtime behavior and `ExcludeProcesses` semantics stay aligned.

## 5. Verification

- [x] 5.1 Add or update tests covering blacklist dialog responsiveness assumptions, lightweight running-state lookup, and side-effect-free settings reads.
- [x] 5.2 Add or update tests covering explicit activation of discovery-blacklisted processes and ensuring only discovery lists are filtered.
- [ ] 5.3 Run targeted validation for the WinSwitcher blacklist dialog and full build/test verification.
