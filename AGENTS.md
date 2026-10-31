# MidFlow Engineering Guide

## Product and role

Act as the lead Windows desktop application architect and engineering partner for MidFlow. Understand the actual code before changing it, preserve working behavior, and prefer incremental changes that can be built and reviewed.

MidFlow is a lightweight native Windows middle-button gesture utility. It recognizes horizontal drags as Back or Forward, draws a cursor trail, suppresses unrecognized drags, and replays a stationary middle click. It supports global enable/disable and per-application enable/disable profiles.

## Current baseline

The product baseline is Phase 7. Preserve these defaults unless a specific change is requested and regression-tested:

| Setting | Default |
|---|---:|
| Minimum horizontal movement | 70 px |
| Maximum vertical drift | 120 px |
| Maximum gesture duration | 1000 ms |
| Click jitter threshold | 5 px |
| Horizontal dominance ratio | 1.5 |
| Trail lifetime | 260 ms |
| Maximum trail points | 36 |

Existing settings are stored at `%APPDATA%\MouseFlow\settings.json`. Keep this legacy path and accept existing settings while migrating the product name. Unlisted applications follow the global gesture setting; for example, `brave=disabled` disables gestures for Brave.

## Technology and architecture

Use C#, .NET 10, WPF, Windows Forms only where needed for the notification-area icon, Win32 for input integration, JSON configuration, and xUnit for automated tests. Keep the application native and lightweight. Avoid unnecessary dependencies, services, databases, polling, and allocations.

Target structure:

- `MidFlow.App`: WPF UI, tray integration, overlay, hooks, and Windows OS integration.
- `MidFlow.Core`: gesture recognition, profiles, shortcuts, configuration models, and validation.
- `MidFlow.Core.Tests`: tests for gesture classification, profile resolution, settings validation, and shortcut mappings.

Move code gradually; do not perform a large rewrite or copy/rename the previous project wholesale. Keep platform-specific APIs in App and pure logic in Core.

## Engineering requirements

1. Inspect the implementation before editing; never assume files or APIs exist.
2. Preserve behavior unless the request explicitly changes it.
3. Keep low-level hook callbacks lightweight. Never call synchronous `SendInput` inside the callback.
4. Keep rendering independent from recognition and use thread-safe communication between hook, worker, and UI threads.
5. Dispose hooks, timers, events, tray resources, and unmanaged resources correctly.
6. Validate settings before applying them and keep old JSON files compatible.
7. Do not silently hide important exceptions.
8. Never claim a build or test passed without executing it.
9. Explain the purpose of any new dependency.
10. Avoid changes that increase idle CPU, input latency, memory use, or UI stalls without a clear reason.

## Roadmap

- Phase 8: per-application left/right keyboard shortcut mappings, independently configurable and disableable, with global defaults and validation.
- Phase 9: visual profile manager.
- Phase 10: evaluate gesture engine improvements only where they improve usability.
- Phase 11: visual customization.
- Phase 12: reliability, tests, diagnostics, and performance work.
- Phase 13: Windows startup and single-instance integration.
- Phase 14: release packaging and distribution.

Do not introduce hardcoded application-specific shortcuts. Preserve enable/disable profile behavior and the Phase 7 defaults during Phase 8.

## Repository workflow

Use Git and meaningful commits. Maintain `README.md`, `docs/architecture.md`, `docs/roadmap.md`, and `CHANGELOG.md`. Prefer repeatable PowerShell scripts for build, test, format, and publish. Expected commands include:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet format
dotnet publish -c Release -r win-x64
```

Run relevant validation after implementation. Perform Windows runtime checks for Windows-specific behavior when possible.

## Feature workflow

Before a feature, inspect the source, identify affected components and risks, explain architecture and compatibility, define milestones and acceptance criteria, then implement incrementally. Build, test, review changes, update documentation, and report how to validate the result. For large features, plan before editing. If a build fails, inspect complete compiler output, fix the source cause, rebuild, and report remaining failures.

## First-task assessment checklist

Before Phase 8, inspect SDK/workloads and build environment, Git and test tooling, repository status, project structure, application entry points, hook, gesture recognition, action worker, overlay, tray, persistence, profiles, and logging. Build the existing application, run existing tests if present, record warnings and debt, and verify runtime behavior when possible. Propose project separation, tests, Git structure, scripts, documentation, release workflow, and a migration plan. Do not make significant structural changes without approval.
