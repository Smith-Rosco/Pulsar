## Context

The tutorial overlay system (`TutorialOverlayWindow`, `TutorialOrchestrator`, `OverlayManager`) was built with a solid architecture (state machine, trigger engine, spotlight controller, overlay manager) but suffers from UX gaps in step transitions, content quality, and completion feedback. Currently, every step transition calls `CleanupStepCard()` → `SetCardContent()` → `Show()` on an already-visible window, producing a visible flash/blank gap that users perceive as "window closes and reopens." The WaitForAction steps (2–5) keep the Next button visible, making the entire auto-advance mechanism optional. No success or completion feedback exists.

The project already uses WPF `Storyboard` for the card entrance animation (fade-in + scale), so animation infrastructure exists. The codebase uses `DrawingVisual` with `BitmapCache` for the spotlight effect — a similar approach can be reused for confetti. No external animation library is used; adding one for confetti would be the first third-party dependency in the UI layer.

## Goals / Non-Goals

**Goals:**
- Eliminate overlay window visual discontinuity during step transitions
- Force users to perform actions on WaitForAction steps (hide Next; show only after 30s timeout)
- Replace tutorial scenario with a concrete app-switch-then-command workflow (Notepad)
- Rewrite all tutorial copy with warm, human tone (English + Chinese)
- Add confetti celebration animation on the completion step
- Add visible success feedback when a step auto-advances
- Provide a way to restart the tutorial after completion

**Non-Goals:**
- Full confetti/particle engine (only a bounded, single-use celebration on completion)
- Rearchitecting the trigger system or overlay state machine
- New dialog or settings page — the "Restart Tutorial" button goes in an existing surface
- Changing the onboarding wizard or profile setup flow

## Decisions

### Decision 1: Crossfade transition instead of lifecycle reset

**Choice**: Keep the overlay window open across steps. In `ShowStepAsync()`, instead of `CleanupStepCard()` → `SetCardContent()` → `Show()`, create the new card while the old card is still visible, run a crossfade `Storyboard` (old card opacity 1→0, new card 0→1 over 200ms), then remove the old card.

**Rationale**: Zero window lifecycle changes. The overlay window is already correctly positioned and configured — there is no reason to touch it. A 200ms crossfade is lightweight (two opacity animations on `ContentPresenter` children) and eliminates the blank-gap flash.

**Alternatives considered**:
- Reuse existing entrance animation: doesn't solve the blank gap because the old card is destroyed first.
- Slide transition: more complex coordinate math in the floating card; crossfade is simpler and sufficient.

### Decision 2: Hide Next button on WaitForAction steps

**Choice**: In `TutorialStepCard.SetStep()`, when `step.Type == WaitForAction`, set `NextButton.Visibility = Collapsed`. Show it only after the 30-second hint timeout fires (alongside the manual Continue button).

**Rationale**: The entire point of WaitForAction is to have the user perform the hotkey/action. If the Next button is always visible, the user will click it and bypass the learning. Tying its visibility to the timeout gives the user a genuine chance to discover the action first, but provides an escape hatch if something goes wrong.

**Implementation**: `_waitStepHintTimeout.Start()` already has a timeout callback. Add a line there to set `NextButton.Visibility = Visible`.

### Decision 3: Notepad insertion scenario

**Choice**: Replace the current abstract "choose any generated slot" steps (steps 3 and 5) with a concrete workflow:
- Step 2: "Press Ctrl+Q. You'll see app slots. Click the Notepad icon."
- Step 3: (Auto-triggered by ActionExecuted "Switch") "Pulsar switched to Notepad! Now press Ctrl+Shift+Q."
- Step 4: "You'll see command slots. Click 'Insert Sample Text'."
- Step 5: (Auto-triggered by ActionExecuted "Command") "Pulsar typed text into Notepad. You just combined both modes!"

**Rationale**: This demonstrates Pulsar's unique value: Switch Mode gets you to the app, Command Mode does something inside it. Users remember a story, not a list of abstract features.

**Dependency**: The onboarding template service (`OnboardingTemplateService`) must create a Notepad switch slot and an Insert-Text command slot during setup wizard. If the user didn't pick Notepad, fall back to the most recently selected app.

### Decision 4: Confetti via lightweight `DrawingVisual` particle system (no external library)

**Choice**: Implement confetti as a WPF `DrawingVisual` subclass managed by the `TutorialOverlayWindow`. 40–60 particles with random initial velocity, gravity, rotation, and fade. Runs for ~2 seconds using `CompositionTarget.Rendering` or `DispatcherTimer` at 30fps, then self-disposes.

**Rationale**: The codebase already uses `DrawingVisual` for the spotlight. Adding a NuGet dependency (e.g., `Particle.NET` or `ConfettiFX`) for a single 2-second animation is disproportionate. A hand-written particle system is ~150 lines, fully controllable, zero external risk, and maintains the project's lean dependency profile.

**Alternatives considered**:
- `LottieSharp` / `Lottie` animations: requires a Lottie file asset and a renderer library; overkill.
- `MediaElement` with a GIF/MOV: introduces file-format maintenance; blurry on HiDPI.
- Third-party NuGet: adds supply-chain risk for 40 particles. Not worth it.

### Decision 5: Success feedback — brief green flash on step card

**Choice**: When a trigger fires and the step auto-advances, briefly pulse the card border green (0ms → `#27AE60` border, hold 150ms, fade to transparent over 300ms). Use a `ColorAnimation` on `CardBorder.BorderBrush`.

**Rationale**: Users reported not noticing when a step auto-advanced. A green flash is subtle but unmistakable. It reuses WPF animation infrastructure already present in the codebase.

**Alternatives considered**:
- Checkmark icon overlay: requires layout changes; green border is simpler and non-intrusive.
- Sound effect: would require audio assets and cross-fade logic; over-engineering for this scope.

### Decision 6: "Restart Tutorial" button in Settings

**Choice**: Add a button labeled `Reset Tutorial` to the existing Settings page that is most related to onboarding (e.g., the General or About page), rather than creating a new page or dialog. Clicking it resets `OnboardingState` → `"SetupWizardComplete"`, clears `HasCompletedTutorial`, and re-triggers the tutorial.

**Rationale**: Minimum UI surface area. The user doesn't need a full dialog — just a confirmation message box (`"This will restart the tutorial. Continue?"`).

## Risks / Trade-offs

- **[UX] Crossfade conflicts with card size change**: If different steps have different `CardSizeMode` or fixed dimensions, crossfading between differently-sized cards may cause layout jump. **Mitigation**: Use `SizeToContent` and let WPF handle layout; the crossfade only affects opacity, not size. If jump is visible, switch to a 100ms instant swap instead.
- **[Engagement] Forced Next hiding may frustrate power users**: Users who already know what to do may resent being forced to wait. **Mitigation**: 30-second timeout is generous; the Next button appears after timeout. Users can also click Skip at any time.
- **[Confetti] Performance on low-end hardware**: 60 particles at 30fps with `DrawingVisual` is negligible, but `CompositionTarget.Rendering` fires on every vsync even when idle. **Mitigation**: Start the timer only when confetti plays, stop it immediately after the animation ends. Profile with Perforator if needed.
- **[Content] Notepad may not exist on non-English Windows or non-Windows systems**: The tutorial assumes Notepad is available. **Mitigation**: Use `Environment.SystemDirectory` + `"\notepad.exe"` and verify with `File.Exists`. If absent, fall back to the first generated app slot.
