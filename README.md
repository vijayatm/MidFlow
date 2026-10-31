# MidFlow

MidFlow is a lightweight native Windows utility for middle-button mouse gestures. This repository is the Phase 7 baseline reorganized into an application project and a platform-independent core library. Phase 8 shortcut mappings have not been implemented.

## Existing features

- Global middle-button Back and Forward gestures.
- Stationary middle-click replay and suppression of unrecognized drags.
- Transparent cursor-trail overlay with direction feedback.
- Notification-area controls and a settings window.
- Per-application enable/disable profiles.
- Configurable gesture sensitivity and diagnostic logs.

## Set up on Windows

MidFlow is a native Windows desktop application. To build and run it from source:

1. Install the **.NET 10 SDK** on a 64-bit Windows machine. The SDK includes the tools needed to build the app; the Windows Desktop runtime is needed to run a framework-dependent build.
2. Clone this repository and open PowerShell in the `MidFlow` repository folder.
3. Build the solution:

   ```powershell
   dotnet build .\MidFlow.sln -m:1
   ```

4. Start the application:

   ```powershell
   dotnet run --project .\src\MidFlow.App\MidFlow.App.csproj
   ```

The app appears in the Windows notification area. Use its tray icon to open settings or exit. Middle-button horizontal drags trigger Back and Forward gestures when enabled. Windows may hide the icon in the overflow area.

To publish a Release build for 64-bit Windows:

```powershell
dotnet publish .\src\MidFlow.App\MidFlow.App.csproj -c Release -r win-x64
```

The published files are under `src\MidFlow.App\bin\Release\net10.0-windows\win-x64\publish`. This command creates a framework-dependent publish, so the target machine needs the .NET 10 Windows Desktop runtime. For development builds, no PowerShell execution-policy change is needed; the `dotnet build` command above runs directly.

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
