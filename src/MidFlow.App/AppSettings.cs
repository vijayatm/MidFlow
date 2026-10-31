using System.Diagnostics;
using System.IO;
using System.Text.Json;
using MidFlow.Core.Shortcuts;
using MidFlow.Core.Gestures;

namespace MidFlow.App;

internal static class AppSettings
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseFlow");
    private static readonly string SettingsPath = Path.Combine(DirectoryPath, "settings.json");
    private static int _gesturesEnabled = 1;
    private static int _showTrail = 1;
    private static int _showResultFeedback;
    private static int _reduceVisualEffects;
    private static int _minimumHorizontalMovementPx = 70;
    private static int _maximumVerticalMovementPx = 120;
    private static int _maximumGestureDurationMs = 1000;
    private static int _clickMovementThresholdPx = 5;
    private static bool _welcomeCompleted = true;
    private static Dictionary<string, bool> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _applicationPaths = new(StringComparer.OrdinalIgnoreCase);
    private static ShortcutPair _globalShortcuts = ShortcutPair.Defaults;
    private static Dictionary<string, ShortcutOverride> _shortcutProfiles = new(StringComparer.OrdinalIgnoreCase);
    private static List<GestureDefinition> _globalGestures = [];
    private static Dictionary<string, GestureProfile> _gestureProfiles = new(StringComparer.OrdinalIgnoreCase);
    private static int _gestureCaptureRequested;
    private static int _gesturePracticeActive;

    internal static event Action? Changed;
    internal static event Action<IReadOnlyList<GesturePoint>>? GestureCaptured;
    internal static bool GestureCapturePending => Volatile.Read(ref _gestureCaptureRequested) == 1;
    internal static bool GesturePracticeActive => Volatile.Read(ref _gesturePracticeActive) == 1;
    internal static void BeginGesturePractice() => Interlocked.Exchange(ref _gesturePracticeActive, 1);
    internal static void EndGesturePractice() => Interlocked.Exchange(ref _gesturePracticeActive, 0);
    internal static bool BeginGestureCapture() => Interlocked.Exchange(ref _gestureCaptureRequested, 1) == 0;
    internal static bool TryBeginGestureCaptureSession() => Interlocked.Exchange(ref _gestureCaptureRequested, 0) == 1;
    internal static void CancelGestureCapture() => Interlocked.Exchange(ref _gestureCaptureRequested, 0);
    internal static void CompleteGestureCapture(IReadOnlyList<GesturePoint> points) => GestureCaptured?.Invoke(points);
    internal static bool GesturesEnabled
    {
        get => Volatile.Read(ref _gesturesEnabled) == 1;
        set => Volatile.Write(ref _gesturesEnabled, value ? 1 : 0);
    }
    internal static bool ShowTrail
    {
        get => Volatile.Read(ref _showTrail) == 1;
        set => Volatile.Write(ref _showTrail, value ? 1 : 0);
    }
    internal static bool ShowResultFeedback
    {
        get => Volatile.Read(ref _showResultFeedback) == 1;
        set => Volatile.Write(ref _showResultFeedback, value ? 1 : 0);
    }
    internal static bool ReduceVisualEffects
    {
        get => Volatile.Read(ref _reduceVisualEffects) == 1;
        set => Volatile.Write(ref _reduceVisualEffects, value ? 1 : 0);
    }
    internal static int MinimumHorizontalMovementPx
    {
        get => Volatile.Read(ref _minimumHorizontalMovementPx);
        set => Volatile.Write(ref _minimumHorizontalMovementPx, Math.Clamp(value, 20, 300));
    }
    internal static int MaximumVerticalMovementPx
    {
        get => Volatile.Read(ref _maximumVerticalMovementPx);
        set => Volatile.Write(ref _maximumVerticalMovementPx, Math.Clamp(value, 20, 300));
    }
    internal static int MaximumGestureDurationMs
    {
        get => Volatile.Read(ref _maximumGestureDurationMs);
        set => Volatile.Write(ref _maximumGestureDurationMs, Math.Clamp(value, 250, 3000));
    }
    internal static int ClickMovementThresholdPx
    {
        get => Volatile.Read(ref _clickMovementThresholdPx);
        set => Volatile.Write(ref _clickMovementThresholdPx, Math.Clamp(value, 2, 15));
    }
    internal static string LogDirectory => Path.Combine(DirectoryPath, "Logs");
    internal static string BackupDirectory => Path.Combine(DirectoryPath, "Backups");
    internal static bool ShouldShowWelcome { get { lock (Sync) return !_welcomeCompleted; } }
    internal static ShortcutPair GlobalShortcuts { get { lock (Sync) return _globalShortcuts; } }
    internal static IReadOnlyDictionary<string, ShortcutOverride> ShortcutProfiles
    { get { lock (Sync) return new Dictionary<string, ShortcutOverride>(_shortcutProfiles, StringComparer.OrdinalIgnoreCase); } }

    internal static ShortcutPair ShortcutsForProcess(string processName)
    { lock (Sync) return ShortcutResolver.Resolve(processName, _globalShortcuts, _shortcutProfiles); }

    internal static IReadOnlyList<GestureDefinition> GesturesForProcess(string processName)
    { lock (Sync) return GestureResolver.Resolve(processName, _globalGestures, _gestureProfiles).ToArray(); }

    internal static IReadOnlyList<GestureDefinition> GlobalGestures
    { get { lock (Sync) return _globalGestures.ToArray(); } }

    internal static IReadOnlyDictionary<string, GestureProfile> GestureProfiles
    { get { lock (Sync) return new Dictionary<string, GestureProfile>(_gestureProfiles, StringComparer.OrdinalIgnoreCase); } }

    internal static void SetGestureMappings(IEnumerable<GestureDefinition> global,
        IEnumerable<KeyValuePair<string, GestureProfile>> profiles)
    {
        var globalItems = global.ToList();
        var profileItems = new Dictionary<string, GestureProfile>(StringComparer.OrdinalIgnoreCase);
        if (globalItems.Any(g => !g.IsValid) || HasDuplicateIds(globalItems) || HasConflicts(globalItems))
            throw new ArgumentException("A global gesture is invalid or conflicts with another gesture.", nameof(global));
        foreach (var item in profiles)
        {
            var name = Path.GetFileNameWithoutExtension(item.Key.Trim());
            if (string.IsNullOrWhiteSpace(name) || item.Value.Gestures.Any(g => !g.IsValid) ||
                HasDuplicateIds(item.Value.Gestures))
                throw new ArgumentException("An application gesture mapping is invalid or conflicting.", nameof(profiles));
            profileItems[name] = item.Value with { Gestures = item.Value.Gestures.ToArray() };
        }
        foreach (var item in profileItems)
            if (HasConflicts(GestureResolver.Resolve(item.Key, globalItems, profileItems)))
                throw new ArgumentException("An application gesture mapping conflicts with another active gesture.", nameof(profiles));
        lock (Sync) { _globalGestures = globalItems; _gestureProfiles = profileItems; }
    }

    private static bool HasDuplicateIds(IReadOnlyList<GestureDefinition> gestures) =>
        gestures.Select(g => g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != gestures.Count;

    private static bool HasConflicts(IReadOnlyList<GestureDefinition> gestures)
    {
        for (var i = 0; i < gestures.Count; i++)
            for (var j = i + 1; j < gestures.Count; j++)
                if (gestures[i].Enabled && gestures[j].Enabled &&
                    gestures[i].Path.DistanceTo(gestures[j].Path) < 0.255) return true;
        return false;
    }

    internal static void SetShortcuts(ShortcutPair global, IEnumerable<KeyValuePair<string, ShortcutOverride>> profiles)
    {
        if (!global.IsValid) throw new ArgumentException("The global shortcut mapping is invalid.", nameof(global));
        global = global with { Up = global.UpOrDisabled, Down = global.DownOrDisabled };
        var updated = new Dictionary<string, ShortcutOverride>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in profiles)
        {
            var name = Path.GetFileNameWithoutExtension(item.Key.Trim());
            if (string.IsNullOrWhiteSpace(name) || !item.Value.IsValid)
                throw new ArgumentException("An application shortcut mapping is invalid.", nameof(profiles));
            updated[name] = item.Value;
        }
        lock (Sync) { _globalShortcuts = global; _shortcutProfiles = updated; }
    }

    internal static IReadOnlyDictionary<string, bool> Profiles
    {
        get { lock (Sync) return new Dictionary<string, bool>(_profiles, StringComparer.OrdinalIgnoreCase); }
    }

    internal static IReadOnlyDictionary<string, string> ApplicationPaths
    { get { lock (Sync) return new Dictionary<string, string>(_applicationPaths, StringComparer.OrdinalIgnoreCase); } }

    internal static bool HasApplicationProfile(string processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName.Trim());
        lock (Sync)
            return _profiles.ContainsKey(name) || _shortcutProfiles.ContainsKey(name) ||
                _gestureProfiles.ContainsKey(name) || _applicationPaths.ContainsKey(name);
    }

    internal static void SetApplicationPaths(IEnumerable<KeyValuePair<string, string>> paths)
    {
        var updated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in paths)
        {
            var name = Path.GetFileNameWithoutExtension(item.Key.Trim());
            if (!IsValidProcessName(name) || string.IsNullOrWhiteSpace(item.Value) || !Path.IsPathFullyQualified(item.Value))
                throw new ArgumentException("An application executable path is invalid.", nameof(paths));
            updated[name] = Path.GetFullPath(item.Value);
        }
        lock (Sync) _applicationPaths = updated;
    }

    private static bool IsValidProcessName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 260 &&
        name is not "." and not ".." && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    internal static bool IsEnabledForProcess(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return GesturesEnabled;
        lock (Sync)
            return GesturesEnabled && (!_profiles.TryGetValue(processName, out var enabled) || enabled);
    }

    internal static void SetProfiles(IEnumerable<KeyValuePair<string, bool>> profiles)
    {
        var updated = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in profiles)
        {
            var name = Path.GetFileNameWithoutExtension(item.Key.Trim());
            if (!string.IsNullOrWhiteSpace(name))
                updated[name] = item.Value;
        }
        lock (Sync) _profiles = updated;
    }

    static AppSettings() => Load();

    internal static void CompleteWelcome()
    {
        lock (Sync) _welcomeCompleted = true;
        Save();
    }

    internal static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                _welcomeCompleted = false;
                return;
            }
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsPath));
            if (data is null) return;
            _welcomeCompleted = data.WelcomeCompleted ?? true;
            GesturesEnabled = data.GesturesEnabled;
            ShowTrail = data.ShowTrail;
            ShowResultFeedback = data.ShowResultFeedback ?? false;
            ReduceVisualEffects = data.ReduceVisualEffects ?? false;
            SetProfiles(data.Profiles ?? []);
            var validApplicationPaths = new List<KeyValuePair<string, string>>();
            foreach (var item in data.ApplicationPaths ?? new Dictionary<string, string>())
            {
                try
                {
                    if (IsValidProcessName(Path.GetFileNameWithoutExtension(item.Key.Trim())) &&
                        !string.IsNullOrWhiteSpace(item.Value) && Path.IsPathFullyQualified(item.Value))
                        validApplicationPaths.Add(new KeyValuePair<string, string>(item.Key, Path.GetFullPath(item.Value)));
                }
                catch { DiagnosticLog.Write("SETTINGS invalidApplicationPath=true"); }
            }
            SetApplicationPaths(validApplicationPaths);
            MinimumHorizontalMovementPx = data.MinimumHorizontalMovementPx ?? 70;
            MaximumVerticalMovementPx = data.MaximumVerticalMovementPx ?? 120;
            MaximumGestureDurationMs = data.MaximumGestureDurationMs ?? 1000;
            ClickMovementThresholdPx = data.ClickMovementThresholdPx ?? 5;
            var global = data.GlobalShortcuts ?? ShortcutPair.Defaults;
            var mappings = (data.ShortcutProfiles ?? new()).Where(x => x.Value is not null)
                .Select(x => new KeyValuePair<string, ShortcutOverride>(x.Key, x.Value!));
            if (global.IsValid && mappings.All(x => x.Value.IsValid)) SetShortcuts(global, mappings);
            var gestures = data.Gestures ?? [];
            var oldProfiles = (data.GestureProfiles ?? new()).Where(x => x.Value is not null).ToList();
            if (oldProfiles.Any(x => !x.Value!.InheritGlobal))
            {
                try { SettingsTransfer.WriteAutomaticBackup(SettingsTransfer.FromStoredSettings(data), "BeforeMigration"); }
                catch (Exception ex) { DiagnosticLog.Write($"SETTINGS migrationBackupFailed=true errorType={ex.GetType().Name}"); }
            }
            var gestureProfiles = oldProfiles.Select(x =>
            {
                var profile = x.Value!;
                if (!profile.InheritGlobal)
                {
                    var local = profile.Gestures.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
                    foreach (var inherited in gestures)
                        local.TryAdd(inherited.Id, inherited with { Enabled = false });
                    profile = new GestureProfile(true, local.Values.ToArray());
                }
                return new KeyValuePair<string, GestureProfile>(x.Key, profile);
            });
            try { SetGestureMappings(gestures, gestureProfiles); }
            catch (ArgumentException) { DiagnosticLog.Write("SETTINGS invalidGestureMappings=true"); }
        }
        catch { /* Invalid settings fall back to safe defaults. */ }
    }

    internal static void Save()
    {
        lock (Sync)
        {
            Directory.CreateDirectory(DirectoryPath);
            var data = new SettingsData(GesturesEnabled, ShowTrail,
                new Dictionary<string, bool>(_profiles, StringComparer.OrdinalIgnoreCase),
                MinimumHorizontalMovementPx, MaximumVerticalMovementPx,
                MaximumGestureDurationMs, ClickMovementThresholdPx);
            data = data with { GlobalShortcuts = _globalShortcuts, ShortcutProfiles = new(_shortcutProfiles, StringComparer.OrdinalIgnoreCase) };
            data = data with { Gestures = _globalGestures, GestureProfiles = new(_gestureProfiles, StringComparer.OrdinalIgnoreCase) };
            data = data with { WelcomeCompleted = _welcomeCompleted };
            data = data with { ApplicationPaths = new(_applicationPaths, StringComparer.OrdinalIgnoreCase) };
            data = data with { ShowResultFeedback = ShowResultFeedback, ReduceVisualEffects = ReduceVisualEffects };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        Changed?.Invoke();
    }

    internal static void OpenLogFolder()
    {
        Directory.CreateDirectory(LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", LogDirectory) { UseShellExecute = true });
    }

    internal sealed record SettingsData(bool GesturesEnabled, bool ShowTrail,
        Dictionary<string, bool>? Profiles = null,
        int? MinimumHorizontalMovementPx = null,
        int? MaximumVerticalMovementPx = null,
        int? MaximumGestureDurationMs = null,
        int? ClickMovementThresholdPx = null,
        ShortcutPair? GlobalShortcuts = null,
        Dictionary<string, ShortcutOverride>? ShortcutProfiles = null,
        List<GestureDefinition>? Gestures = null,
        Dictionary<string, GestureProfile>? GestureProfiles = null,
        bool? WelcomeCompleted = null,
        Dictionary<string, string>? ApplicationPaths = null,
        bool? ShowResultFeedback = null,
        bool? ReduceVisualEffects = null);
}
