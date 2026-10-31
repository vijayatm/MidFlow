# MidFlow

MidFlow is a lightweight native Windows utility for middle-button mouse gestures. One profile screen configures global and per-application directional shortcuts, custom shapes, and app enablement.

## Existing features

- Global middle-button Left/Back, Right/Forward, Up, and Down gestures.
- Stationary middle-click replay and suppression of unrecognized drags.
- Transparent cursor-trail overlay with direction feedback.
- Notification-area controls and a settings window.
- Per-application enable/disable profiles.
- Configurable gesture sensitivity and diagnostic logs.
- Recordable global Back / Forward keyboard chords with per-application overrides.
- Unified global and per-application gesture profiles for directional shortcuts and custom shapes.
- Recordable custom mouse paths with global or per-application scope.
- Custom gesture actions for Back, Forward, Up, Down, or launching a selected application.

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

The app appears in the Windows notification area. Use its tray icon to open settings or exit. Middle-button drags trigger configured directional or custom gestures when enabled. Windows may hide the icon in the overflow area.

To publish a Release build for 64-bit Windows:

```powershell
dotnet publish .\src\MidFlow.App\MidFlow.App.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\win-x64 -p:OutputPath=..\..\artifacts\win-x64\
```

The distribution folder is `artifacts\win-x64`. The command directs build and publish outputs to the same folder, avoiding duplicate copies under the project `bin` directories. This is a framework-dependent publish, so the target machine needs the .NET 10 Windows Desktop runtime. For development builds, run the `dotnet build` command above directly.

## Settings compatibility

MidFlow continues to read and write `%APPDATA%\MouseFlow\settings.json` to preserve existing Phase 7 settings and profiles. The application name has changed; this settings location remains the legacy compatibility path.

Keyboard shortcuts default to `Alt+Left` for Back and `Alt+Right` for Forward. In Settings, focus a shortcut field and press a chord to record it; press Escape to disable that mapping. Add an application by entering its process name or browsing to its `.exe`. Application overrides are independent by direction and inherit the global shortcut until you record or disable that direction. Existing settings files load with the default shortcuts.

Open **Gestures & shortcuts** in Settings and select **Global defaults** or an application profile. Configure a keyboard chord for Left/Back, Right/Forward, Up, and Down; Up and Down start disabled. App profiles inherit each directional shortcut independently, and the app enable switch suspends all gesture actions for that app. Use **Add custom shape** to record a path while holding the middle mouse button, preview it, and map it to Back, Forward, Up, Down, or launch an application. Global shapes are inherited by apps; edit or disable one in an app to create an override, or reset it to return to the global binding. Similar active shapes in the same profile are rejected to avoid ambiguous matches. Custom shapes are checked before directional gestures. A stationary middle click is still replayed, and slow or unrecognized drags remain suppressed.

## Repository layout

```text
src/
  MidFlow.App/       WPF UI, tray integration, Win32 input, rendering, persistence
  MidFlow.Core/      Platform-independent gesture and shortcut rules
  MidFlow.Core.Tests/ xUnit tests for gesture classification, profile resolution, and shortcut validation
docs/                Architecture and roadmap
scripts/             Development scripts
```

See [docs/architecture.md](docs/architecture.md) and [docs/roadmap.md](docs/roadmap.md).
