# MidFlow architecture

## Current projects

- **MidFlow.App** is the Windows-only WPF executable. It owns the notification-area icon, settings window, low-level mouse hook, action injection, overlay rendering, diagnostics, and settings file I/O.
- **MidFlow.Core** is a platform-independent .NET library. It owns gesture recognition and shortcut models, validation, and per-process shortcut resolution.

The app references Core. Core has no WPF, WinForms, or Win32 dependency. Application enable/disable settings and JSON persistence remain in the app; shortcut value types and resolution stay in Core.

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

The global hook and all OS integration stay in `MidFlow.App`. It resolves the Back/Forward shortcut mapping for the foreground process and queues that chord. The action worker sends the key chord through `SendInput`, so input injection does not run in the low-level hook callback. Shortcut models and profile resolution live in `MidFlow.Core`.

## Migration direction

Extract pure, deterministic configuration and profile rules into Core as later work needs them, with tests added before behavior changes. Keep WPF, Forms, and native interop in App. Move one cohesive unit at a time and preserve old settings JSON defaults and paths.
