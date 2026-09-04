# visual-ai-iteration-loop

## Purpose

Provides a vision-capable AI loop that consumes diagnostic packages from the E2E framework, proposes UI fixes, and iterates until the deterministic E2E suite accepts the change; it also performs occlusion detection on captured screenshots against UIA bounding boxes.

## Requirements

### Requirement: Diagnostic Package Consumption

The system SHALL accept a diagnostic package as the sole input to an iteration turn: the AI reads the failed step, the UIA tree snapshot, the screenshot, and the log excerpt, and proposes concrete code changes in response.

#### Scenario: Failed run feeds one iteration
- **WHEN** the E2E framework produces a diagnostic package for a failed UI test
- **THEN** the loop delivers that package to the AI as the basis for a fix proposal

### Requirement: AI Proposes, E2E Verifies

The system SHALL treat the AI as a proposal generator only: AI-suggested fixes are applied, rebuilt, and re-run through the same E2E case, and the result is judged solely by the E2E framework. The AI SHALL never self-certify its own output as passing.

#### Scenario: Loop converges to green
- **WHEN** the AI proposes a fix, the fix is rebuilt, and the E2E case passes
- **THEN** the iteration stops and the change is accepted

#### Scenario: Loop re-iterates on failure
- **WHEN** the AI proposes a fix and the re-run of the E2E case still fails
- **THEN** the loop produces a fresh diagnostic package and begins another iteration with the AI

#### Scenario: Max iterations reached
- **WHEN** the loop exceeds a configured maximum iteration count without a passing run
- **THEN** the loop stops, reports the last diagnostic package, and marks the change as un-iterated/failed for human review

### Requirement: Occlusion Detection

The system SHALL detect element occlusion by combining a stable screenshot with UI Automation bounding boxes: a visual-capable model SHALL report overlapping elements when their interactive regions overlap and at least one element's rendered content is obscured.

#### Scenario: Interactive overlap reported
- **WHEN** two interactive elements overlap such that one obscures the other's interactive region
- **THEN** the system emits a structured occlusion report naming both elements, the overlap area, and a suggested layout fix

#### Scenario: Expected overlay not reported
- **WHEN** an overlay (such as a menu layer or tooltip) intentionally covers underlying content by design
- **THEN** the overlay is not reported as an occlusion defect

#### Scenario: Non-interactive visual overlap not reported
- **WHEN** purely decorative elements overlap without obscuring any interactive region
- **THEN** no occlusion defect is reported

### Requirement: Occlusion Acceptance Gate

The system SHALL treat a clean occlusion report (zero interactive-overlap defects) on a target view as an acceptance criterion for that view's visual regression check.

#### Scenario: Layout change re-checked
- **WHEN** an AI or developer layout change affects a target view
- **THEN** the occlusion check re-runs on that view and the change is not accepted until zero interactive-overlap defects are reported

### Requirement: Structured Report Format

The system SHALL emit occlusion reports in a structured, machine-readable format containing at least element identifiers, overlap geometry, and the occlusion classification, so that reports can be diffed against a previous baseline.

#### Scenario: Report diffable against baseline
- **WHEN** a view is checked twice with the same layout
- **THEN** the two occlusion reports are identical in structure and content
