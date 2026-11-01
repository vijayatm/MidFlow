# MidFlow

MidFlow is a lightweight native Windows utility for middle-button mouse gestures. One profile screen configures global and per-application directional shortcuts, custom shapes, and app enablement.

## Existing features

- Global middle-button Left/Back, Right/Forward, Up, and Down gestures.
- Stationary middle-click replay and suppression of unrecognized drags.
- Transparent cursor-trail overlay with direction feedback.
- Notification-area controls and a settings window.
- First-run gesture guide, safe action preview, optional starter shapes, and recommended-default reset.
- Settings grouped into General, Applications, Appearance, and Diagnostics with profile search and inherited-state cues.
- Tray status for the foreground app, with a shortcut to create or open its process-name profile.
- Application profile copy, reset-to-inherited, executable-path tracking, and missing-target guidance.
- Versioned full-settings and selected-profile export/import with validation, preview, and recovery backups.
- Per-application enable/disable profiles.
- Configurable gesture sensitivity and diagnostic logs.
- Relaxed, Balanced, and Precise sensitivity presets, plus independent result-message and reduced-effects controls.
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

### Install, upgrade, and uninstall

For most users, download `MidFlow-Setup-x64.exe` from the [latest GitHub Release](https://github.com/vijayatm/MidFlow/releases/latest) and run it. The installer is per-user, does not require administrator rights or a separate .NET runtime, creates a Start Menu shortcut, and offers an optional desktop shortcut. Windows may show a SmartScreen warning because the installer is not currently Authenticode-signed.

To upgrade, run the newer `MidFlow-Setup-x64.exe`; if MidFlow is running, Setup asks to close it before replacing the installed app files. Your settings, profiles, logs, and recovery backups remain under `%APPDATA%\MouseFlow` across upgrades. MidFlow does not download updates or check for them in the background. Choose **Get updates** in the tray menu or **Open latest release page** in Settings → Diagnostics to open the release page, review the notes, and download an update.

To uninstall, use **Settings → Apps → Installed apps → MidFlow → Uninstall** or **Start → MidFlow → Uninstall MidFlow**. The uninstaller asks whether to keep the settings, profiles, logs, and recovery backups. Keeping them is the default. Choosing removal deletes `%APPDATA%\MouseFlow` after the app is uninstalled.

The portable option remains available as `MidFlow-win-x64.zip`. Extract it to a folder you control and run `MidFlow.App.exe`; no installer or runtime is required. Removing the extracted folder removes the portable app. Its settings are still stored in the same per-user `%APPDATA%\MouseFlow` location, so uninstalling either distribution does not affect the other distribution's settings.

### Publish a Windows release

The `.github/workflows/release.yml` workflow builds both release assets on a clean GitHub-hosted Windows runner. It installs the .NET 10 SDK and Inno Setup, publishes the self-contained `win-x64` single-file app, then creates the portable ZIP and setup installer from the same payload. No self-hosted runner or machine-wide .NET setup is required.

After the workflow is on the default branch, create and push a new, unused stable version tag for the commit to publish:

```powershell
git switch main
git pull
git tag v7.1.7
git push origin v7.1.7
```

Tags must use `vMAJOR.MINOR.PATCH`; the tag sets the app and installer version. GitHub Actions publishes `MidFlow-Setup-x64.exe` and `MidFlow-win-x64.zip` to that tag's Release. The workflow needs the repository's standard `contents: write` permission to publish assets. Release notes are generated from GitHub's release metadata; review and edit them before announcing a release.

## Settings compatibility

MidFlow continues to read and write `%APPDATA%\MouseFlow\settings.json` to preserve existing Phase 7 settings and profiles. The application name has changed; this settings location remains the legacy compatibility path.

Keyboard shortcuts default to `Alt+Left` for Back and `Alt+Right` for Forward. In Settings, focus a shortcut field and press a chord to record it; press Escape to disable that mapping. Add an application by entering its process name or browsing to its `.exe`. Application overrides are independent by direction and inherit the global shortcut until you record or disable that direction. Existing settings files load with the default shortcuts.

On a clean first launch, a short welcome guide explains the middle-button flow and how app profiles inherit global settings. Existing settings files skip the welcome guide. Open the **Applications** tab in Settings and select **Global defaults** or an application profile. Configure a keyboard chord for Left/Back, Right/Forward, Up, and Down; Up and Down start disabled. App profiles inherit each directional shortcut independently, and the app enable switch suspends all gesture actions for that app.

Enter an application process name and choose **Add app** to create an inherited profile, or select an existing profile and choose **Copy selected** to copy its app-specific settings to another process name. **Reset to inherited** clears an app’s shortcut and custom-shape overrides and its app-level disable setting while keeping the profile entry. **Browse** shows the resolved process name before adding the profile. MidFlow matches profiles by process name; the browsed executable path is saved as a reference and does not create an alias. Missing saved executable references and missing custom-gesture launch targets are flagged with steps to repair them. The tray menu shows the foreground process, whether gestures are enabled, and whether global or app settings apply; its create/open action opens that profile in Settings.

Use **Add custom shape** to record a path while holding the middle mouse button, preview it, and map it to Back, Forward, Up, Down, or launch an application. **Add starter shapes** lets you preview and optionally add Loop → Back and Zigzag → Forward to the global profile. **Try a gesture safely** uses a left-button drawing area; it shows the resolved action without sending a shortcut or launching an application, and temporarily passes middle-button input through. Global shapes are inherited by apps; edit or disable one in an app to create an override, or reset it to global. Similar active shapes in the same profile are rejected to avoid ambiguous matches. Custom shapes are checked before directional gestures. Use **Restore recommended defaults** to review and reset global settings, shortcuts, profiles, and shapes; the reset is saved only when you click Save. A stationary middle click is still replayed, and slow or unrecognized drags remain suppressed.

In **Diagnostics → Profile backup and transfer**, export all settings or select one or more application profiles. Selected-profile packages carry resolved shortcuts, custom shapes, and enabled state. The receiving computer keeps its own global enable switch and sensitivity thresholds. Imports are validated and previewed; full-settings packages replace global settings and profiles, while profile packages add or replace only the listed app profiles. Before an import or restore, MidFlow saves a versioned recovery package under `%APPDATA%\MouseFlow\Backups`. Use **Restore backup** to preview and restore one. Compatibility migrations also create a recovery package before converting older profile data.

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
