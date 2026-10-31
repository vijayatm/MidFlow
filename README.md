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

### Publish a Windows download on GitHub

The repository includes `.github/workflows/release.yml`. Because the release job uses a self-hosted Windows runner, set up your PC once, then publish a release whenever you push a version tag:

1. Install the **.NET 10 SDK** machine-wide on the Windows PC, and confirm `dotnet --info` works from a standard PowerShell window. The release job uses the SDK already installed on this PC; it does not install .NET for each job. Keep the PC online while a release is building.
2. In the repository, open **Settings → Actions → Runners → New self-hosted runner**, select **Windows** and **x64**, then follow GitHub's displayed download and configuration commands on that PC. Register it for this repository and configure it to run as a Windows service so it is available after sign-in or restart. If you install or update .NET after configuring the service, restart the runner service so it sees the machine-wide PATH. Keep the runner software updated.
3. In the repository, open **Settings → Actions → General** and make sure Actions are allowed. Under **Workflow permissions**, allow read and write access if required; the workflow requests `contents: write` to create releases.
4. Push the workflow to GitHub on the default branch (`main`). Confirm the runner appears as **Idle** under **Settings → Actions → Runners**.
5. From PowerShell in the repository, create and push a new, unused version tag for the commit you want to publish. For example:

   ```powershell
   git switch main
   git pull
   git tag v7.1.1
   git push origin v7.1.1
   ```

   The workflow runs for tags matching `v*`. The `v7.1.0` tag has already been used; choose the next version or another unused tag.
6. In GitHub, open **Actions** and wait for **Windows release** to finish. Then open **Releases** and select the release for that tag.
7. Download `MidFlow-win-x64.zip` from the release's **Assets** section. Extract it and run `MidFlow.App.exe`.

The workflow runs only on pushed version tags, but this is a public repository: never add pull request or other untrusted code workflows to this self-hosted runner. A self-hosted runner executes repository workflow code on your PC. The workflow builds a self-contained `win-x64` app, so users do not need to install the .NET runtime separately. The ZIP is a portable application package, not a setup wizard installer. The executable download is hosted as a GitHub Release asset; GitHub Packages is for registries such as NuGet and container images.

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
