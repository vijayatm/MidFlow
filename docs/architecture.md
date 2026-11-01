# MidFlow architecture

## Current projects

- **MidFlow.App** is the Windows-only WPF executable. It owns the notification-area icon, settings window, low-level mouse hook, action injection, overlay rendering, diagnostics, and settings file I/O.
- **MidFlow.Core** is a platform-independent .NET library. It owns four-way directional and recorded-path recognition, shortcut and gesture models, validation, and per-process profile resolution.

The app references Core. Core has no WPF, WinForms, or Win32 dependency. Application enable/disable settings and JSON persistence remain in the app; shortcut value types and resolution stay in Core.

## Runtime flow

```text
App.OnStartup
  └─ MainWindow (hidden WPF host and click-through overlay)
       ├─ NotifyIcon + tray menu (foreground process and effective profile state)
       ├─ foreground-process polling
       ├─ WPF composition callback → mouse sample queue → TrailSurface and result indicator
       └─ LowLevelMouseHook
            ├─ hook thread → Core GestureSession → consume/suppress input
            ├─ action worker → custom path matching → SendInput / launch selected app
            └─ DiagnosticLog queue → file writer

SettingsWindow → AppSettings → %APPDATA%\MouseFlow\settings.json
             ↘ SettingsTransfer → versioned import/export and Backups\*.midflow.json

Release tag vMAJOR.MINOR.PATCH → package-release.ps1 → self-contained win-x64 payload
                                 ├─ MidFlow-win-x64.zip (portable)
                                 └─ MidFlow-Setup-x64.exe (per-user Inno Setup installer)
```

The global hook and all OS integration stay in `MidFlow.App`. It captures a bounded path and queues the completed stroke. The action worker matches custom paths first, then uses the recognized Left, Right, Up, or Down shortcut when no custom mapping matches. It sends key chords through `SendInput` or launches a validated executable; path normalization, custom matching, and action execution do not run synchronously in the low-level hook callback. Gesture path normalization, matching, action models, directional shortcut resolution, and per-gesture profile inheritance live in `MidFlow.Core`. The safe practice window uses the Core resolvers to report the would-be action; while it is open, the hook passes physical middle-button input through and never queues actions.

Application profiles continue to resolve by process name. A browsed executable path is optional reference metadata used to show the resolved process and flag a missing file; it does not change profile matching or create executable aliases. This optional metadata is stored in `settings.json` alongside the existing profiles.

`SettingsTransfer` owns versioned JSON package serialization, import validation, merge planning, previews, and timestamped recovery packages. Full packages include global and app settings; selected-app packages materialize effective shortcut and custom-shape mappings and the effective enabled state. Destination global enablement and sensitivity still apply. Imports update the Settings draft only after the candidate configuration validates; a recovery package is written before that draft changes. The UI writes to the existing settings file only when the user clicks Save.

Gesture sensitivity presets write the same bounded numeric settings used by recognition; manually changing any value switches the selector to Custom. The cursor trail, reduced-effects rendering, and post-action result messages are separately configurable. Result messages are queued as mouse samples and displayed by the WPF surface; action outcomes are produced on the worker after mapping resolution and input injection, keeping UI work out of the low-level hook.

The tagged release version is passed into the app publish and installer builds so both release assets share one version. The installer targets the current user's `%LOCALAPPDATA%\Programs\MidFlow`, creates Start Menu shortcuts, and uses a stable Inno Setup AppId for upgrades and Windows uninstall registration. It does not register sign-in startup. Restart Manager can close MidFlow before files are replaced; the main window shutdown path disposes the hook and tray resources. Uninstall removes the installed program and asks whether to delete `%APPDATA%\MouseFlow`; keeping settings is the default. The portable ZIP uses the same self-contained executable and does not install or uninstall anything. An in-app release link opens GitHub Releases on user request; the app does not check for or download updates in the background.

## Migration direction

Extract pure, deterministic configuration and profile rules into Core as later work needs them, with tests added before behavior changes. Keep WPF, Forms, process launching, and native interop in App. The unified profile continues to read legacy Back/Forward fields and shortcut overrides; Up and Down are optional additions defaulting to disabled. Custom gesture settings remain optional JSON fields, and the legacy settings path is preserved.
