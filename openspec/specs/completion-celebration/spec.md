## ADDED Requirements

### Requirement: Confetti animation plays on tutorial completion
When the tutorial reaches the final completion step (step 6), the overlay window SHALL display a confetti particle animation that lasts approximately 2–3 seconds to provide a sense of achievement.

#### Scenario: Confetti starts on completion step
- **WHEN** the final tutorial step is displayed (step with `PrimaryAction: CompleteTutorial`)
- **THEN** a confetti particle animation SHALL start automatically
- **AND** the animation SHALL play for 2–3 seconds then self-dispose

#### Scenario: Confetti is non-interactive
- **WHEN** the confetti animation is playing
- **THEN** user interaction (clicking Finish, Skip, or moving the mouse) SHALL NOT be blocked or delayed
- **AND** clicking Finish SHALL stop the animation and close the overlay

### Requirement: Confetti uses lightweight DrawingVisual particle system
The confetti SHALL be implemented using a `DrawingVisual`-based particle system within the `TutorialOverlayWindow`, rendering 40–60 particles with random colors, initial velocities, rotation, gravity, and fade-out.

#### Scenario: Particles render and animate
- **WHEN** confetti starts
- **THEN** 40–60 particles SHALL appear at the top of the screen with random horizontal offsets
- **AND** each particle SHALL have a random color from a predefined palette (gold, green, blue, pink, purple)
- **AND** each particle SHALL fall with gravity-like acceleration and gradually fade to 0 opacity over 2–3 seconds

#### Scenario: No confetti on skipped tutorial or manual completion bypass
- **WHEN** the user skips the tutorial or the tutorial completes without reaching the final celebration step
- **THEN** no confetti SHALL play
