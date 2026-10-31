# MidFlow roadmap

## Current baseline

Phase 7 gesture sensitivity and per-application enable/disable profiles are retained. The repository has been split into `MidFlow.App` and `MidFlow.Core`; only gesture recognition has moved into Core so far.

## Phase 8 — Per-application shortcut mappings

- Configure independent left and right shortcut combinations.
- Allow either direction to be disabled.
- Provide global defaults and optional per-application overrides.
- Preserve existing enable/disable profiles and legacy settings JSON.
- Validate mappings before saving and keep `SendInput` on the action worker.
- Add core tests for mapping validation and action resolution as part of feature work.

Phase 8 implementation: the Settings window records keyboard chords for global defaults and per-process overrides. Each app direction can inherit its global default or be overridden; Escape disables a mapping. Enable/disable profiles now appear in the same application list, and disabled apps are dimmed. Existing settings files retain the `Alt+Left` / `Alt+Right` defaults, and shortcut injection stays on the action worker.

## Later phases

- Phase 9: visual profile manager.
- Phase 10: evaluate gesture engine improvements.
- Phase 11: visual customization.
- Phase 12: reliability, testing, diagnostics, and performance work.
- Phase 13: Windows startup and single-instance integration.
- Phase 14: release packaging and distribution.
