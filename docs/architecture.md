# MidFlow architecture

## Current projects

- **MidFlow.App** is the Windows-only WPF executable. It owns the notification-area icon, settings window, low-level mouse hook, action injection, overlay rendering, diagnostics, and settings file I/O.
- **MidFlow.Core** is a platform-independent .NET library. It currently owns `GestureSession` and `GestureDirection`, including Phase 7's classification thresholds and reasons.

The app references Core. Core has no WPF, WinForms, or Win32 dependency. This is the first extraction step; configuration and profile resolution remain in the app for now.

## Runtime flow

```text
App.OnStartup
  └─ MainWindow (hidden WPF host and click-through overlay)
       ├─ NotifyIcon + tray menu
       ├─ foreground-process polling
       ├─ WPF composition callback → mouse sample queue → TrailSurface
       └─ LowLevelMouseHook
            ├─ hook thread → Core GestureSession → consume/suppress input
            ├─ action worker → SendInput
            └─ DiagnosticLog queue → file writer

SettingsWindow → AppSettings → %APPDATA%\MouseFlow\settings.json
```

The global hook and all OS integration stay in `MidFlow.App`. The action worker remains separate, so `SendInput` does not run in the low-level hook callback.

## Migration direction

Extract pure, deterministic configuration and profile rules into Core as later work needs them, with tests added before behavior changes. Keep WPF, Forms, and native interop in App. Move one cohesive unit at a time and preserve old settings JSON defaults and paths.
