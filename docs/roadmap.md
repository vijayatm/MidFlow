# MidFlow roadmap

This roadmap replaces the original Phase 8–14 checklist with a product-focused plan. It separates the features already present from proposed work and groups future work by user outcome. The order is a suggested sequence; each phase should be scoped and reviewed before implementation.

## Product direction

Make MidFlow easy to understand, quick to configure, predictable across applications, and dependable during everyday use. Keep it a lightweight native Windows utility: gesture recognition and input handling should stay responsive, settings should remain recoverable, and new features should earn their idle CPU and memory cost.

## Shipped foundation

The current app already provides:

- Global enablement, adjustable gesture sensitivity, and a cursor trail with direction feedback.
- Left, Right, Up, and Down directional gestures; Up and Down are disabled by default.
- Recordable keyboard chords, including per-application overrides and disabled mappings.
- Per-application enablement and inherited or overridden gesture profiles.
- Recorded custom paths with Back, Forward, Up, Down, and launch-application actions.
- A settings window, notification-area icon, diagnostic logging, and settings persistence.
- Legacy compatibility with `%APPDATA%\MouseFlow\settings.json` and the existing Back/Forward defaults.

## Delivery phases

### Phase 1 — Settings that are easier to scan (delivered)

Improve the existing profile editor so common tasks take fewer clicks and profile state is obvious.

- Reorganize settings into clear sections: General, Gestures, Applications, Appearance, and Diagnostics.
- Show each gesture as a compact row with direction or preview, action, enabled state, and an inline edit action.
- Make inherited, overridden, and disabled values visibly distinct, with a direct “Reset to global” action.
- Add profile search and sorting by application name and enabled state; retain a clear empty state and add-app action.
- Keep unsaved edits visible, add a discard confirmation when closing with changes, and provide inline validation near invalid fields.
- Improve window resizing, keyboard navigation, focus order, high-DPI scaling, and screen-reader names.

**Acceptance:** a user can identify which profile is being edited and whether each value is inherited without opening a dialog; accidental window closure does not silently lose edits.

### Phase 2 — First-run setup and learn-by-doing (delivered)

Help new users understand the middle-button gesture model and safely verify their setup.

- Add a short first-run guide explaining the middle-button hold, drag, release, and stationary-click behavior.
- Offer a guided test area that previews a gesture and its resolved action without sending keyboard input or launching an app.
- Provide a small set of optional starter custom shapes and let users preview them before adding them.
- Explain when an application profile uses global settings and how to add or remove an override.
- Add a “restore recommended defaults” action with a clear summary of what will be reset.

**Acceptance:** users can complete setup, understand how to test a mapping safely, and return to defaults without editing JSON.

### Phase 3 — More useful application profiles (delivered)

Make per-app behavior practical to create and maintain across installed software.

- Show the foreground application name and current profile state in the tray menu or settings status area.
- Add an optional “create profile for the current app” shortcut from the tray or settings.
- Support choosing an executable through Browse and show the resolved process name before saving.
- Detect duplicate profiles and invalid or missing executable paths with actionable messages.
- Add profile copy/duplicate and reset-to-inherited actions for apps with similar configurations.
- Keep process-name matching explicit; consider multiple executable aliases only if real user scenarios require them.

**Acceptance:** users can create, identify, duplicate, reset, and remove profiles without guessing which process name MidFlow uses.

### Phase 4 — Profile portability and recovery (delivered)

Make settings easier to back up, move, and recover after a bad edit or upgrade.

- Export and import profiles as a versioned JSON file, with a preview of changes before applying.
- Support exporting all settings or selected application profiles.
- Validate imported data and report skipped or incompatible entries instead of partially applying silently.
- Create a recoverable backup before migrations or bulk imports; provide a restore action.
- Document the legacy settings path and migration behavior in the UI and release notes.

**Acceptance:** export/import round-trips preserve effective global and app behavior, and invalid imports leave existing settings intact.

### Phase 5 — Gesture tuning and visual feedback

**Delivered:** named Relaxed, Balanced, and Precise presets with Custom detection; independent cursor trail, result-message, and reduced-effects options; brief action-result messages from the action worker; simplified trail rendering for reduced effects; and start/end markers in the normalized custom-path preview. New preferences are optional in legacy settings and included in full-settings transfers.

Give users control over how gestures feel while keeping recognition understandable.

- Add named sensitivity presets (for example, Relaxed, Balanced, and Precise) that map to validated settings.
- Explain movement threshold, drift, and duration in plain language with reset-to-default actions.
- Preview the recorded custom path and its normalized recognition shape before saving.
- Let users adjust custom-gesture sensitivity only if usability evidence shows a need for per-shape thresholds.
- Add an optional brief on-screen result indicator for recognized action, disabled mapping, or rejected ambiguous gesture.
- Keep trail and feedback options independently configurable, including a reduced-visual-effects choice.

**Acceptance:** tuning remains bounded and validated, the preview reflects the saved gesture, and overlays can be disabled without affecting recognition.

### Phase 6 — Everyday control and accessibility

Make MidFlow’s state and controls easy to reach without requiring the settings window.

- Expand tray actions for enable/disable, open settings, and pause for a chosen duration.
- Add a configurable keyboard toggle only if it can avoid conflicts and remain opt-in.
- Add accessible names, consistent keyboard shortcuts inside settings, visible focus, and high-contrast-friendly states.
- Consider a compact status window showing global state, foreground app, and the active profile.
- Add options for launching minimized and opening settings on demand.

**Acceptance:** users can see whether MidFlow is active and pause or resume it quickly, with all controls usable by keyboard.

### Phase 7 — Reliability, diagnostics, and performance

Make input handling easier to trust and problems easier to diagnose.

- Audit hook lifecycle, cancellation, action-queue behavior, app shutdown, and unmanaged resource disposal.
- Review foreground-application detection and eliminate unnecessary polling where event-driven APIs are practical.
- Add bounded, privacy-conscious diagnostics for recognition decisions, queue delays, and action failures.
- Provide a user-accessible diagnostics view and a “copy support report” action that excludes gesture paths and app data by default.
- Add deterministic Core coverage for settings validation, profile inheritance, gesture edge cases, and migration compatibility.
- Measure idle CPU, memory, and gesture-to-action latency before and after performance changes.

**Acceptance:** failures are observable, diagnostics are bounded and user-controlled, and measurements show no material regression to idle cost or input latency.

### Phase 8 — Windows startup and lifecycle

Let users choose how MidFlow starts and behaves across sign-in and relaunch.

- Add an opt-in Windows sign-in launch setting with clear status and a way to disable it.
- Enforce one running instance and focus the existing instance when launched again.
- Define behavior for logoff, shutdown, settings migration, and interrupted updates.
- Keep startup integration within supported Windows mechanisms and avoid background services.

**Acceptance:** startup is optional, single-instance behavior is predictable, and uninstalling or disabling startup leaves no active registration behind.

### Phase 9 — Visual customization

Let users tailor feedback without obscuring the pointer or distracting from work.

- Offer a small set of trail colors, thicknesses, and durations with an immediate preview.
- Allow direction feedback to be simplified or hidden independently from the trail.
- Respect Windows scaling and contrast preferences; avoid color-only status cues.
- Keep visual settings optional and ensure overlay rendering remains click-through and independent of recognition.

**Acceptance:** appearance changes do not affect gesture results, remain legible at common display scales, and can be reset easily.

### Phase 10 — Packaging and release experience

Make installation, upgrade, and removal understandable for non-developer users.

- **Phase 10.1 — Release contract:** Keep the portable ZIP and add a per-user installer that needs no administrator rights. The installer is the recommended path for most users. Preserve `%APPDATA%\MouseFlow` settings and backups during upgrades and uninstall by default; offer explicit data removal during uninstall. Make version tags the release version source. Updates are user-initiated from GitHub Releases; no background checks or downloads.
- **Phase 10.2 — Installer:** Package the self-contained x64 app with Inno Setup. Create Start Menu shortcuts, support in-place upgrades, close the running app cleanly when setup needs to replace it, and register a standard uninstaller. Do not add sign-in startup as part of installation.
- **Phase 10.3 — Update entry point:** Add a clearly named app action that opens the latest GitHub Release page. Users review release notes and run the installer to update; keep the portable ZIP available for users who prefer it.
- **Phase 10.4 — Release automation and guidance:** Build the installer and portable ZIP from the same tagged commit and publish both as release assets. Document fresh install, upgrade, uninstall, portable use, and settings retention. Keep signing and any automatic update delivery as separate future decisions.
- **Phase 10.5 — Lifecycle verification:** Check a clean install, launch, upgrade over an older version with existing profiles, uninstall with and without data removal, and standalone portable launch on a clean Windows profile. Record runtime and architecture requirements.

**Acceptance:** users can install or run a release without command-line steps, upgrade without losing profiles, get to the current release from the app, and uninstall without stale app files while choosing whether to retain settings and backups.

## Product-wide constraints

- Preserve the legacy settings path and existing settings compatibility unless a migration is deliberate, documented, and recoverable.
- Keep current gesture defaults unless a specific change is measured and explicitly adopted.
- Keep low-level hook work short; queue recognition and action work where appropriate, and never perform synchronous `SendInput` in the hook callback.
- Keep OS integration in `MidFlow.App` and deterministic gesture/profile logic in `MidFlow.Core`.
- Avoid hardcoded application-specific shortcuts, unnecessary dependencies, background services, and idle polling.
- Include observable acceptance criteria and relevant validation in each feature proposal before implementation.
