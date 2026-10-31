# MidFlow

MidFlow is a lightweight native Windows utility for middle-button mouse gestures. This repository is the Phase 7 baseline reorganized into an application project and a platform-independent core library. Phase 8 shortcut mappings have not been implemented.

## Existing features

- Global middle-button Back and Forward gestures.
- Stationary middle-click replay and suppression of unrecognized drags.
- Transparent cursor-trail overlay with direction feedback.
- Notification-area controls and a settings window.
- Per-application enable/disable profiles.
- Configurable gesture sensitivity and diagnostic logs.

## Build

Requires the .NET 10 SDK and Windows Desktop runtime.

```powershell
./scripts/build.ps1
```

## Settings compatibility

MidFlow continues to read and write `%APPDATA%\MouseFlow\settings.json` to preserve existing Phase 7 settings and profiles. The application name has changed; this settings location remains the legacy compatibility path.

## Repository layout

```text
src/
  MidFlow.App/       WPF UI, tray integration, Win32 input, rendering, persistence
  MidFlow.Core/      Platform-independent gesture recognition
docs/                Architecture and roadmap
scripts/             Development scripts
```

See [docs/architecture.md](docs/architecture.md) and [docs/roadmap.md](docs/roadmap.md).
