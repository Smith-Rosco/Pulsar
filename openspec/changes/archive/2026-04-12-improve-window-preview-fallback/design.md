## Context

The current radial-menu preview behavior treats hover preview as an on-demand screenshot operation. `RadialMenuVisualStateCoordinator` only requests a preview for valid, non-minimized windows, `PreviewService` caches successful captures for the current session, and `WindowService.CaptureWindowAsync` relies on `PrintWindow` to materialize a `BitmapSource`.

That architecture works for many normal desktop windows, but it conflates two different concepts: a window being a valid switch target, and a window being immediately screenshotable. The gap is most visible with long-idle windows and Remote Desktop windows, where the handle remains valid but the window may not expose a stable surface for `PrintWindow` at hover time. Because the view model clears preview cache when the radial menu opens, a failed fresh capture leaves no preview to display and the center slot drops straight to the app icon.

This change is cross-cutting because it touches native preview interop, preview caching semantics, UI fallback behavior, and the radial-menu hover pipeline.

## Goals / Non-Goals

**Goals:**
- Make hover preview behavior deterministic even when fresh capture is unavailable.
- Separate preview resolution from window validity so windows remain switchable without being penalized for capture failures.
- Prefer a system-managed live preview path when available, while keeping a static snapshot fallback path.
- Preserve a graceful UI degradation order: live preview, last-known-good snapshot, then icon.
- Define clear invalidation rules so cached previews remain useful without becoming unbounded or misleading.

**Non-Goals:**
- Redesign the overall window-switching selection engine or slot layout behavior.
- Guarantee true live preview for every window class on Windows.
- Change activation semantics, quick-switch behavior, or Alt-Tab candidate enumeration.
- Solve unrelated capture issues outside the radial-menu window-preview surface.

## Decisions

### Decision: Model preview as a layered resolution pipeline

The preview subsystem should resolve the best available representation for a target window instead of treating `PrintWindow` success as the only meaningful outcome.

Resolution order:
1. Live system-managed preview when the target window supports it.
2. Last-known-good static snapshot when a live preview cannot be established.
3. App or process icon as the final fallback.

Rationale:
- This matches user expectations better than a binary preview-or-icon model.
- It acknowledges that previewability is dynamic and weaker than switchability.
- It reduces flicker and avoids regressions where a window that had a good preview yesterday suddenly appears “broken” because one capture attempt failed.

Alternatives considered:
- Keep single-shot `PrintWindow` and retry more aggressively. Rejected because it improves success rate only marginally and does not fix the underlying model mismatch.
- Force activation or restore before capture. Rejected because preview should not create focus-stealing side effects.

### Decision: Prefer DWM thumbnail interop for the primary preview path

The primary preview path should use DWM thumbnail APIs when a target window can be represented through the compositor, with the radial menu acting as the destination host.

Rationale:
- DWM thumbnails are closer to native Windows preview semantics than app-level screenshot capture.
- The compositor path is a better fit for inactive windows than asking the target window to synchronously paint into `PrintWindow`.
- The repository already exposes DWM thumbnail interop, which lowers implementation risk.

Alternatives considered:
- Replace `PrintWindow` with another screenshot method. Rejected because GDI-based capture remains fundamentally less aligned with compositor-managed window preview behavior.
- Continue using only static snapshots. Rejected because it fails to preserve the “live preview” feel when the system can provide one.

### Decision: Preserve static snapshot capture as a secondary path and persistent cache source

`PrintWindow`-style capture should remain in the system, but it should become a fallback and cache-refresh mechanism rather than the main preview path.

Rationale:
- Some windows may fail thumbnail registration or may not produce a usable live preview in the center host.
- Static snapshots remain valuable as a “last known good” representation across menu sessions.
- Retaining the existing capture machinery limits risk and provides an incremental migration path.

Alternatives considered:
- Remove snapshot capture entirely. Rejected because it would create unnecessary regressions for windows where live thumbnail behavior is unavailable.

### Decision: Replace session-only cache semantics with last-known-good semantics

The preview cache should retain the most recent successful static preview per window handle beyond a single radial-menu session, with invalidation tied to window lifecycle and explicit preview refresh events rather than unconditional `Show()` clearing.

Expected cache behavior:
- Successful snapshot capture updates the entry for that `hwnd`.
- Live thumbnail failure does not erase an existing cached snapshot.
- Invalid or destroyed windows invalidate their cached preview.
- Optional staleness metadata can inform telemetry or future UI treatment, but stale snapshots remain displayable until invalidated.

Rationale:
- The current per-session clear makes every radial-menu open depend on fresh capture availability.
- A last-known-good cache creates stable fallback behavior without increasing user-facing surprise.

Alternatives considered:
- Keep clearing cache every session. Rejected because it defeats the purpose of having a meaningful fallback layer.
- Keep an unbounded permanent cache. Rejected because invalidation and lifecycle ownership become unclear.

### Decision: Make fallback state explicit in the preview contract

The preview-facing surface should distinguish among at least three outcomes: live preview, snapshot preview, and icon fallback. The UI does not need to expose all three states textually, but the internal contract should, so the view model can reason about transitions without overloading `null` as the only failure signal.

Rationale:
- A richer contract prevents accidental regressions where one provider failure wipes out a lower-priority preview that was still valid.
- It improves diagnosability and makes testing easier.

Alternatives considered:
- Keep returning only `BitmapSource?`. Rejected because it collapses resolution state into one nullable image and makes graceful degradation harder to express.

## Risks / Trade-offs

- [Risk] DWM thumbnails may not work uniformly across all window classes or host visual configurations. → Mitigation: keep snapshot and icon fallback paths intact and treat thumbnail support as opportunistic, not mandatory.
- [Risk] Persistent cached snapshots can become visually stale and misrepresent current window state. → Mitigation: define explicit invalidation on window destruction and refresh successful captures opportunistically; accept staleness as preferable to empty preview for hover UX.
- [Risk] Preview state management becomes more complex than the current nullable-image flow. → Mitigation: introduce a narrow preview result contract and keep provider responsibilities explicit.
- [Risk] Hosting compositor-backed previews inside the radial menu may require careful window-lifetime cleanup. → Mitigation: centralize thumbnail registration/unregistration ownership in a dedicated preview service layer.

## Migration Plan

1. Introduce the spec and internal contract for layered preview resolution.
2. Refactor preview services so providers can report live preview, snapshot, and icon fallback outcomes explicitly.
3. Integrate DWM thumbnail hosting for the center preview surface while retaining existing snapshot capture code.
4. Change cache lifetime from per-session clearing to last-known-good invalidation.
5. Update the radial-menu UI/view model to render based on resolved preview state instead of only `CenterPreviewImage == null`.
6. Validate behavior against normal windows, minimized windows, cloaked windows, and long-idle Remote Desktop windows.

## Open Questions

- Should stale snapshot age ever be surfaced visually, or should it remain an internal concern only?
- Should cache keys remain strictly `hwnd`-based, or should process/window identity heuristics be used when handles churn?
- Is the center preview host best implemented directly in the existing radial menu window, or should a dedicated helper host own DWM thumbnail registration details?
