## ADDED Requirements

### Requirement: Layout animation
The system SHALL animate changes to radial layout parameters (radius, center size, slot size) using smooth interpolation.

#### Scenario: Lerp animation to target values
- **WHEN** `AnimateLayoutAsync(targetRadius, targetCenterSize, targetSlotSize)` is called
- **THEN** the animation SHALL interpolate from current values to target values over the specified duration (default 300ms)
- **AND** the animation SHALL use elastic easing for natural motion

#### Scenario: Animation completes at target
- **WHEN** an layout animation runs for the specified duration
- **THEN** all layout parameters SHALL equal their target values exactly

### Requirement: Bounce animation for boundary feedback
The system SHALL provide bounce animation when user attempts to scroll past the first or last page.

#### Scenario: Bounce at first page boundary
- **WHEN** `BounceAsync(BounceDirection.First)` is called
- **THEN** the system SHALL play a compress-and-elastic animation lasting ~120ms
- **AND** all slots SHALL briefly scale to 92% then bounce back to 100%

#### Scenario: Bounce at last page boundary
- **WHEN** `BounceAsync(BounceDirection.Last)` is called
- **THEN** the system SHALL play a compress-and-elastic animation identical to the first page boundary

### Requirement: Magnetism effect
The system SHALL calculate and apply magnetism offsets that pull slots slightly toward the cursor when within the magnetic radius.

#### Scenario: Apply magnetism when cursor near slot
- **WHEN** cursor is within 150px of a slot center
- **THEN** `UpdateMagnetism(cursorPosition)` SHALL calculate an offset proportional to (1 - distance/150)²
- **AND** the maximum offset SHALL be 18% of the distance

### Requirement: Animation lifecycle
The system SHALL support pausing and resuming all animations.

#### Scenario: Pause animations
- **WHEN** `Pause()` is called during an animation
- **THEN** all current animations SHALL freeze at their current frame
- **AND** no further interpolation SHALL occur until `Resume()` is called

#### Scenario: Resume animations
- **WHEN** `Resume()` is called after `Pause()`
- **THEN** animations SHALL continue from their paused state

### Requirement: Animation queuing
The system SHALL support queuing animations to run sequentially.

#### Scenario: Queue sequential animations
- **WHEN** `QueueAsync(animation1)` is called followed by `QueueAsync(animation2)` while animation1 is running
- **THEN** animation2 SHALL start only after animation1 completes

### Requirement: Cancellation support
The system SHALL support cancellation of running animations.

#### Scenario: Cancel animation via CancellationToken
- **WHEN** a CancellationToken is provided and `Cancel()` is called
- **THEN** the animation SHALL stop immediately at current frame
- **AND** no further interpolation SHALL occur
