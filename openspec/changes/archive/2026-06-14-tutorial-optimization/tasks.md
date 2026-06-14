## 1. Rewrite Tutorial Copy (English + Chinese)

- [x] 1.1 Rewrite English `Tutorial.WelcomeDesc` — warm, conversational, name the Notepad workflow upfront
- [x] 1.2 Rewrite English `Tutorial.SwitchModeDesc` + `Tutorial.SwitchModeHint` — concrete "Press Ctrl+Q and click Notepad"
- [x] 1.3 Rewrite English `Tutorial.FirstSwitchDesc` + `Tutorial.FirstSwitchHint` — celebrate the switch success, direct to next step
- [x] 1.4 Rewrite English `Tutorial.CommandModeDesc` + `Tutorial.CommandModeHint` — concrete "Press Ctrl+Shift+Q and click Insert Sample Text"
- [x] 1.5 Rewrite English `Tutorial.FirstCommandDesc` + `Tutorial.FirstCommandHint` — celebrate the command success
- [x] 1.6 Rewrite English `Tutorial.OnboardingCompleteDesc` — warm finish, "you just combined both modes"
- [x] 1.7 Rewrite English auxiliary strings (`Tutorial.AutoContinue`, `Tutorial.NoActionDetectedHint`, `Tutorial.Next`)
- [x] 1.8 Translate all rewritten strings into natural Chinese in `Strings.zh-CN.resx`

## 2. Update TutorialSteps.json with New Scenario

- [x] 2.1 Rewrite step 1 (`step1_onboarding_welcome`) description to reference the Notepad workflow
- [x] 2.2 Rewrite step 2 (`step2_switch_mode_intro`) to say "Press Ctrl+Q and click the Notepad tile"
- [x] 2.3 Rewrite step 3 (`step3_switch_mode_success`) to celebrate the switch and prompt opening Command Mode
- [x] 2.4 Rewrite step 4 (`step4_command_mode_intro`) to say "Press Ctrl+Shift+Q and click 'Insert Sample Text'"
- [x] 2.5 Rewrite step 5 (`step5_command_mode_success`) to celebrate the combined workflow
- [x] 2.6 Rewrite step 6 (`step6_completion`) with warm summary and Finish button
- [x] 2.7 Verify `waitHintText`/`waitHintKey` references match the new resource keys

## 3. Hide Next Button on WaitForAction Steps

- [x] 3.1 In `TutorialStepCard.SetStep()`, add logic: if `step.Type == WaitForAction`, set `NextButton.Visibility = Collapsed`
- [x] 3.2 In `WaitStepHintTimeout` timeout callback (or `TutorialStepCard.ShowManualContinueButton`), restore `NextButton.Visibility = Visible`
- [x] 3.3 Verify: trigger firing before timeout auto-advances correctly without needing Next button

## 4. Smooth Crossfade Transition Between Steps

- [x] 4.1 Create crossfade Storyboard resource in `TutorialStepCard.xaml` (fade-out 200ms, fade-in 200ms)
- [x] 4.2 In `TutorialOrchestrator.ShowStepAsync()`, restructure: build new card while old card is still in the ContentPresenter, run crossfade, then remove old card
- [x] 4.3 Remove redundant `_overlayManager.Show()` call when window is already visible
- [x] 4.4 Verify: no blank gap or flash during step forward/back/skip transitions

## 5. Add Success Feedback (Green Border Flash)

- [x] 5.1 In `TutorialStepCard.xaml`, add a `ColorAnimation` resource targeting `CardBorder.BorderBrush`
- [x] 5.2 Expose a `PlaySuccessAnimation()` method on `TutorialStepCard`
- [x] 5.3 Call `PlaySuccessAnimation()` from `TutorialOrchestrator.OnTriggerFired()` before advancing
- [x] 5.4 Ensure the animation does NOT play on manual Next/Continue clicks

## 6. Implement Confetti Animation

- [x] 6.1 Create `ConfettiParticle` class (position, velocity, rotation, color, opacity, lifetime fields)
- [x] 6.2 Create `ConfettiRenderer` class using `DrawingVisual` + `CompositionTarget.Rendering` or `DispatcherTimer`
- [x] 6.3 Integrate `ConfettiRenderer` into `TutorialOverlayWindow` — add a transparent overlay canvas layer
- [x] 6.4 Wire confetti start to the completion step display in `TutorialOrchestrator`
- [x] 6.5 Ensure confetti stops cleanly when user clicks Finish or Skip
- [x] 6.6 Verify: no confetti plays on skipped tutorial or non-completion paths

## 7. Add "Restart Tutorial" Button in Settings

- [x] 7.1 Identify the best Settings page for the button (General or About page)
- [x] 7.2 Add `ResetTutorialCommand` in the corresponding ViewModel
- [x] 7.3 Wire command to reset `OnboardingState` → `"SetupWizardComplete"`, clear `HasCompletedTutorial`
- [x] 7.4 Show confirmation message box before reset
- [x] 7.5 Add XAML button with label "Reset Tutorial" (using localization key)

## 8. Verify and Test

- [x] 8.1 Run `dotnet build` and fix any compilation errors
- [ ] 8.2 Walk through the full tutorial flow end-to-end (welcome → switch → command → complete)
- [ ] 8.3 Walk through timeout path (wait 30s on a step, verify Next appears)
- [ ] 8.4 Walk through skip path (verify confetti does NOT play)
- [ ] 8.5 Walk through Settings → Reset Tutorial flow
- [ ] 8.6 Verify Chinese locale renders all rewritten strings correctly
- [x] 8.7 Run existing tutorial unit tests (`Pulsar.Tests/Tutorial/`)
- [x] 8.8 Run `dotnet test` to ensure no regressions
