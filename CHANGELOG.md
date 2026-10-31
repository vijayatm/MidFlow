# Changelog

## Unreleased

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
