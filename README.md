# MidFlow

### Middle-button gestures for Windows

Turn a middle-button drag into a fast, memorable shortcut:

```text
Hold the middle button → drag left → release → Back
Hold the middle button → drag right → release → Forward
```

MidFlow runs quietly in the notification area and works across Windows apps. It gives the mouse a small, configurable gesture vocabulary without replacing the normal middle-click: a stationary middle click is replayed as usual, while an intentional gesture performs its mapped action.

> **Windows only · native WPF app · lightweight · per-application profiles**

## Why MidFlow?

MidFlow is useful when a repeated keyboard shortcut is just far enough away to interrupt your flow.

- Browse pages without moving your hand to the keyboard: drag left for **Back**, right for **Forward**.
- Add **Up** and **Down** gestures for shortcuts you use frequently.
- Give individual applications their own mappings while everything else follows the global defaults.
- Record a custom mouse path and map it to navigation, a shortcut, or launching an application.
- See the gesture trail and direction feedback as you perform it, or reduce visual effects for a quieter experience.
- Disable gestures globally or for one application from Settings or the tray menu.

## How it works

1. Press and hold the middle mouse button.
2. Drag in a direction, or draw a saved custom shape.
3. MidFlow shows a lightweight cursor trail and identifies the gesture.
4. Release the button to run the mapped action.

MidFlow recognizes a gesture only when the movement is deliberate and quick enough. Unrecognized drags are suppressed rather than accidentally passed through, and a stationary press is replayed as a normal middle click. Custom shapes are checked before the four-way directional fallback.

### Default directional mappings

| Gesture | Default action |
| --- | --- |
| Middle drag left | Back (`Alt+Left`) |
| Middle drag right | Forward (`Alt+Right`) |
| Middle drag up | Disabled until configured |
| Middle drag down | Disabled until configured |

The mappings are recordable, independently disableable, and can be overridden per application.

## Install

The simplest option is to download **`MidFlow-Setup-x64.exe`** from the [latest GitHub Release](https://github.com/vijayatm/MidFlow/releases/latest). The per-user installer:

- does not require administrator rights;
- includes the .NET runtime, so a separate runtime install is not needed;
- adds Start Menu shortcuts and optionally a desktop shortcut; and
- keeps your settings when you upgrade.

Windows may show a SmartScreen warning because the installer is not currently Authenticode-signed.

Prefer a portable install? Download **`MidFlow-win-x64.zip`**, extract it, and run `MidFlow.App.exe`. Removing the extracted folder removes the portable application. Both distributions use the same settings location: `%APPDATA%\MouseFlow`.

## First five minutes

1. Start MidFlow. Its icon appears in the Windows notification area; Windows may place it in the hidden-icon overflow area.
2. Try a middle-button drag left or right in a browser or file manager.
3. Double-click the tray icon to open **Settings**.
4. In **Applications**, choose **Global defaults** to change the default shortcuts.
5. Use **Try a gesture safely** to practice. Practice uses the left mouse button and reports the action that would be selected without sending a shortcut or launching anything.
6. Click **Save** when you are ready. Settings are not written while you are only editing a draft.

You can enable or disable gestures, toggle the trail, open the log folder, or open Settings from the tray menu. The tray also shows the foreground process and whether it is using global settings or an application profile.

## Configure it your way

### Sensitivity and feedback

Choose **Relaxed**, **Balanced**, or **Precise** sensitivity, or adjust the values yourself. Manual changes switch the selector to **Custom**. The defaults are designed to distinguish an intentional gesture from a normal middle click:

| Setting | Default |
| --- | ---: |
| Minimum horizontal movement | 70 px |
| Maximum vertical drift | 120 px |
| Maximum gesture duration | 1000 ms |
| Click jitter threshold | 5 px |
| Horizontal dominance ratio | 1.5 |
| Trail lifetime | 260 ms |
| Maximum trail points | 36 |

In **Appearance**, the cursor trail, result messages, and reduced visual effects can be controlled independently.

### Per-application profiles

Application profiles are matched by process name. An application not listed follows the global gesture setting.

- Add a process name manually or browse to its `.exe`.
- Override each directional shortcut independently.
- Disable all gestures for one application without changing global behavior.
- Copy a profile to another process name.
- Use **Reset to inherited** to return a profile to global behavior.

The browsed executable path is saved as helpful reference metadata; it does not create an executable alias. Missing references are flagged with repair guidance.

### Custom shapes

Choose **Add custom shape**, hold the middle button, draw a path, preview it, and assign an action:

- Back, Forward, Up, or Down;
- a recorded keyboard direction; or
- launching a selected application.

Custom shapes can be global or application-specific. **Add starter shapes** provides previewable Loop → Back and Zigzag → Forward examples. Similar active shapes in the same profile are rejected so one stroke does not have two plausible meanings.

## Back up and move your setup

Open **Diagnostics → Profile backup and transfer** to:

- export all settings;
- export selected application profiles;
- preview and validate an import; and
- restore a timestamped recovery backup.

MidFlow creates a recovery package before an import, restore, or compatibility migration. Existing settings remain compatible with the legacy path `%APPDATA%\MouseFlow\settings.json`.

## Build from source

Requirements: 64-bit Windows and the **.NET 10 SDK**.

```powershell
git clone https://github.com/vijayatm/MidFlow.git
cd MidFlow
dotnet build .\MidFlow.sln -m:1
dotnet test .\MidFlow.sln
dotnet run --project .\src\MidFlow.App\MidFlow.App.csproj
```

To create the self-contained Windows release payload used by the portable ZIP and installer:

```powershell
dotnet publish .\src\MidFlow.App\MidFlow.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -o .\artifacts\win-x64
```

The repository also contains repeatable scripts under `scripts/` and the release workflow under `.github/workflows/`.

## Project structure

```text
src/
  MidFlow.App/        WPF UI, tray integration, Win32 input, overlay, persistence
  MidFlow.Core/       Platform-independent gesture, shortcut, and profile rules
  MidFlow.Core.Tests/ xUnit tests for recognition, paths, profiles, and shortcuts
docs/                 Architecture and roadmap
scripts/              Build and release helpers
```

The low-level hook stays in `MidFlow.App`; deterministic gesture recognition, path matching, shortcut validation, and profile resolution live in `MidFlow.Core`. See [docs/architecture.md](docs/architecture.md) for the runtime flow and [docs/roadmap.md](docs/roadmap.md) for planned work.

## Troubleshooting

- **The tray icon is missing:** check Windows' hidden notification icons, or launch MidFlow again from the Start Menu.
- **A gesture does not trigger:** open **Try a gesture safely** and check the resolved profile, enabled state, direction, and shortcut.
- **An application behaves differently:** check whether its process has an application profile and whether the relevant mapping is inherited, overridden, or disabled.
- **A launch gesture stopped working:** open the profile and browse to the target `.exe` again.
- **You need diagnostics:** choose **Open log folder** from the tray menu or use the Diagnostics tab.

## Project status

MidFlow is an actively developed Windows utility. See the repository history and [CHANGELOG.md](CHANGELOG.md) for the current release details.
