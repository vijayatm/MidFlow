# Changelog

## 0.1.0 - 2026-11-01

- Added Relaxed, Balanced, and Precise gesture sensitivity presets with automatic Custom detection; made result messages, cursor trail, and reduced visual effects independent options.
- Added brief post-gesture result messages, a reduced-effects trail renderer, and visible start/end markers on normalized custom gesture previews.
- Preserved older settings and full-settings transfer compatibility when visual feedback options are absent.
- Grouped settings into General, Applications, Appearance, and Diagnostics tabs; added profile sorting, inherited-state cues, inline sensitivity feedback, and unsaved-change handling.
- Added a first-run guide, safe gesture practice, previewable Loop and Zigzag starter shapes, and a confirmed recommended-default reset.
- Added foreground application/profile status and create/open-profile actions to the tray menu.
- Added executable-path references for app profiles, resolved process-name previews, copy and reset-to-inherited actions, and actionable missing-file feedback.
- Added versioned full-settings and selected-profile packages, validated import previews, and timestamped recovery backups with a restore flow.
- Added pre-migration backups for compatibility conversion of older application gesture profiles.
- Merged directional shortcuts, app enablement, and custom shape management into one global and per-application gesture profile screen.
- Added Up and Down directional recognition with independently configurable keyboard chords; both start disabled.
- Added per-gesture app overrides, inherited state, and reset-to-global behavior for custom shapes.
- Added recorded custom path gestures with global and per-application scopes, inheritance, and Back / Forward / application launch actions.
- Added gesture previews, profile editing, bounded path capture, worker-side matching, and ambiguity checks.
- Preserved existing horizontal Back / Forward defaults and legacy settings compatibility.
- Added Core tests for path matching, normalization, ambiguity handling, and gesture profile resolution.
- Added recordable global Back and Forward keyboard shortcuts with per-application overrides and disable support.
- Merged per-application gesture enable/disable controls into the application shortcuts list and dimmed disabled apps.
- Preserved legacy settings compatibility and the existing `Alt+Left` / `Alt+Right` defaults.
- Added Core shortcut validation and resolution tests.
