# MidFlow roadmap

## Current baseline

Phase 7 gesture sensitivity and per-application enable/disable profiles are retained. The repository has been split into `MidFlow.App` and `MidFlow.Core`; gesture recognition, shortcut models, and profile resolution live in Core.

## Phase 8 — Per-application shortcut mappings

- Configure independent directional keyboard chords.
- Allow a directional binding to be disabled.
- Provide global defaults and optional per-application overrides.
- Preserve existing enable/disable profiles and legacy settings JSON.
- Validate mappings before saving and keep `SendInput` on the action worker.
- Add core tests for mapping validation and action resolution as part of feature work.

Phase 8 began with global and per-application Back/Forward chord mappings. Existing settings retain the `Alt+Left` / `Alt+Right` defaults, and shortcut injection stays on the action worker.

## Phase 9 — Custom gestures and application profiles

- Combine directional shortcuts, per-app enablement, and custom shapes in one gesture profile screen.
- Support Left, Right, Up, and Down directional bindings. Keep Up and Down disabled by default.
- Record and normalize custom paths while holding the middle mouse button.
- Assign Back, Forward, Up, Down, or launch a user-selected executable.
- Manage global bindings and per-application inherited or independent overrides.
- Show gesture previews, enabled state, inheritance, and similarity feedback in Settings.
- Match custom paths on the action worker; preserve directional shortcut fallback when no custom path matches.
- Keep stationary middle-click replay and suppression of slow or unrecognized drags.
- Migrate old Back/Forward settings into the unified profile without changing the legacy path or defaults.
- Add deterministic Core tests for four-way classification, normalization, matching, ambiguity, validation, and profile resolution.

## Later phases

- Phase 10: gesture recognition usability, reliability, diagnostics, and performance review.
- Phase 11: visual customization.
- Phase 12: Windows startup and single-instance integration.
- Phase 13: release packaging and distribution.
