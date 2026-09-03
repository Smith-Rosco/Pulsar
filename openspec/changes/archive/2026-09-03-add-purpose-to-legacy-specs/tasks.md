## 1. Preparation

- [x] 1.1 Capture baseline: run `openspec validate --all`, record the 40 failing spec IDs, and scan all spec files for duplicate `### Requirement:` headings across files (pre-existing conditions to report, not fix). Verify: baseline list saved and matches the 40 specs listed in section 2.

## 2. Normalize legacy specs (flattened headers + Purpose, 5 batches of 8)

- [x] 2.1 Batch A: bookmarklet-runner-execution, cascade-submenu-layout, command-key-parsing, command-process-launch, completion-celebration, drag-session-wheel-paging, first-launch-setup-wizard, global-mouse-interception — add `# title`, `## Purpose`, single `## Requirements`; verify each file has exactly one `## Purpose` and one `## Requirements`, no remaining `## ADDED/MODIFIED Requirements` headers, and requirement/scenario bodies unchanged.
- [x] 2.2 Batch B: guided-onboarding-tutorial, internal-plugin-command-contract, language-switching, localization-infrastructure, localized-core-strings, onboarding-error-resilience, onboarding-state-integrity, plugin-action-semantics — same transformation and verification as 2.1.
- [x] 2.3 Batch C: plugin-display-identity, radial-menu, scenario-core, scenario-excel-vba, scenario-prerequisite-validation, slot-editor-density-and-layout, slot-editor-information-hierarchy, slot-editor-progressive-disclosure — same transformation and verification as 2.1.
- [x] 2.4 Batch D: slot-type-card-model, smart-detection-state-integrity, smart-switch-launch-feedback, sound-feedback, staged-startup-coordination, sub-radial-window-title, submenu-coordinator-strategy, unified-slot-type-picker — same transformation and verification as 2.1.
- [x] 2.5 Batch E: usage-analytics-ui, user-facing-action-feedback, window-activation-confirmation, window-preview-fallback, window-switch-selection-core, window-switching-architecture, window-switching-behavior-contract, winswitcher-blacklist-dialog-performance — same transformation and verification as 2.1.

## 3. Validation & Review

- [x] 3.1 Run `openspec validate --all` and verify 85 passed / 0 failed. Verify: command output shows zero `✗` lines.
- [x] 3.2 Re-run duplicate `### Requirement:` heading scan and compare against the 1.1 baseline; confirm no new duplicates were introduced and list any pre-existing ones in the final report. Verify: diff against baseline is empty.
- [x] 3.3 Review `git diff --stat openspec/specs` and confirm exactly the 40 spec.md files changed with no unrelated files touched. Verify: `git status --porcelain openspec/specs` lists only spec.md modifications.
