# MidFlow architecture

## Current projects

- **MidFlow.App** is the Windows-only WPF executable. It owns the notification-area icon, settings window, low-level mouse hook, action injection, overlay rendering, diagnostics, and settings file I/O.
- **MidFlow.Core** is a platform-independent .NET library. It owns horizontal and recorded-path gesture recognition, shortcut and gesture models, validation, and per-process profile resolution.

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
            ├─ action worker → custom path matching → SendInput / launch selected app
            └─ DiagnosticLog queue → file writer

SettingsWindow → AppSettings → %APPDATA%\MouseFlow\settings.json
```

The global hook and all OS integration stay in `MidFlow.App`. It captures a bounded path and queues the completed gesture with the current profile snapshot. The action worker matches custom paths, then falls back to the existing horizontal Back/Forward recognition when no custom mapping matches. It sends key chords through `SendInput` or launches a validated executable; neither recognition matching nor action execution runs synchronously in the low-level hook callback. Gesture path normalization, matching, action models, and profile resolution live in `MidFlow.Core`.

## Migration direction

Extract pure, deterministic configuration and profile rules into Core as later work needs them, with tests added before behavior changes. Keep WPF, Forms, process launching, and native interop in App. Custom gesture settings are optional JSON fields, so existing settings files keep the default Back/Forward mappings and the legacy path. Move one cohesive unit at a time and preserve old settings JSON defaults and paths.
