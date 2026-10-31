using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using MidFlow.Core.Gestures;
using MidFlow.Core.Shortcuts;

namespace MidFlow.App;

internal sealed record TransferGlobals(bool GesturesEnabled, bool ShowTrail, bool ShowResultFeedback, bool ReduceVisualEffects,
    int MinimumHorizontalMovementPx, int MaximumVerticalMovementPx,
    int MaximumGestureDurationMs, int ClickMovementThresholdPx,
    ShortcutPair GlobalShortcuts, List<GestureDefinition> GlobalGestures);

internal sealed record TransferProfiles(
    Dictionary<string, bool>? ApplicationEnabled,
    Dictionary<string, ShortcutOverride>? ShortcutProfiles,
    Dictionary<string, GestureProfile>? GestureProfiles,
    Dictionary<string, string>? ApplicationPaths);

internal sealed record SettingsTransferPackage(int FormatVersion, string Scope, DateTime ExportedUtc,
    TransferGlobals? Globals, TransferProfiles? Profiles);

internal static class SettingsTransfer
{
    internal const int CurrentFormatVersion = 1;
    internal const string AllScope = "All";
    internal const string ProfilesScope = "Profiles";
    private const long MaximumFileSizeBytes = 20 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    internal static SettingsTransferPackage CreateAll(TransferGlobals globals,
        IReadOnlyDictionary<string, bool> enabled,
        IReadOnlyDictionary<string, ShortcutOverride> shortcuts,
        IReadOnlyDictionary<string, GestureProfile> gestures,
        IReadOnlyDictionary<string, string> paths) =>
        new(CurrentFormatVersion, AllScope, DateTime.UtcNow, globals,
            new TransferProfiles(Copy(enabled), Copy(shortcuts), Copy(gestures), Copy(paths)));

    internal static SettingsTransferPackage CreateSelectedProfiles(IEnumerable<string> selectedNames,
        bool globalGesturesEnabled, ShortcutPair globalShortcuts, IReadOnlyList<GestureDefinition> globalGestures,
        IReadOnlyDictionary<string, bool> enabled,
        IReadOnlyDictionary<string, ShortcutOverride> shortcuts,
        IReadOnlyDictionary<string, GestureProfile> gestures,
        IReadOnlyDictionary<string, string> paths)
    {
        var names = selectedNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var selectedEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var selectedShortcuts = new Dictionary<string, ShortcutOverride>(StringComparer.OrdinalIgnoreCase);
        var selectedGestures = new Dictionary<string, GestureProfile>(StringComparer.OrdinalIgnoreCase);
        var selectedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var resolvedShortcuts = ShortcutResolver.Resolve(name, globalShortcuts, shortcuts);
            selectedEnabled[name] = enabled.GetValueOrDefault(name, true) && globalGesturesEnabled;
            selectedShortcuts[name] = new ShortcutOverride(resolvedShortcuts.Back, resolvedShortcuts.Forward,
                resolvedShortcuts.UpOrDisabled, resolvedShortcuts.DownOrDisabled);
            selectedGestures[name] = new GestureProfile(false,
                GestureResolver.Resolve(name, globalGestures, gestures).ToArray());
            if (paths.TryGetValue(name, out var executablePath)) selectedPaths[name] = executablePath;
        }
        return new SettingsTransferPackage(CurrentFormatVersion, ProfilesScope, DateTime.UtcNow, null,
            new TransferProfiles(selectedEnabled, selectedShortcuts, selectedGestures, selectedPaths));
    }

    internal static SettingsTransferPackage FromStoredSettings(AppSettings.SettingsData data)
    {
        var globalGestures = data.Gestures ?? [];
        var legacyGestureProfiles = (data.GestureProfiles ?? new Dictionary<string, GestureProfile>())
            .Where(x => x.Value is not null).Select(x =>
            {
                var profile = x.Value;
                if (!profile.InheritGlobal)
                {
                    var local = new Dictionary<string, GestureDefinition>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in profile.Gestures) local[item.Id] = item;
                    foreach (var inherited in globalGestures) local.TryAdd(inherited.Id, inherited with { Enabled = false });
                    profile = new GestureProfile(true, local.Values.ToArray());
                }
                return new KeyValuePair<string, GestureProfile>(NormalizeProcessName(x.Key), profile);
            }).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var globalShortcuts = data.GlobalShortcuts ?? ShortcutPair.Defaults;
        var shortcutProfiles = NormalizeMap(data.ShortcutProfiles ?? new Dictionary<string, ShortcutOverride>(), x => x);
        var enabled = NormalizeMap(data.Profiles ?? new Dictionary<string, bool>(), x => x);
        var paths = NormalizeMap(data.ApplicationPaths ?? new Dictionary<string, string>(), x => x);
        return CreateAll(new TransferGlobals(data.GesturesEnabled, data.ShowTrail, data.ShowResultFeedback ?? false, data.ReduceVisualEffects ?? false,
                data.MinimumHorizontalMovementPx ?? 70, data.MaximumVerticalMovementPx ?? 120,
                data.MaximumGestureDurationMs ?? 1000, data.ClickMovementThresholdPx ?? 5,
                globalShortcuts, globalGestures), enabled, shortcutProfiles, legacyGestureProfiles, paths);
    }

    internal static SettingsTransferPackage Read(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("The selected transfer file does not exist.", path);
        if (file.Length <= 0 || file.Length > MaximumFileSizeBytes)
            throw new InvalidDataException("The transfer file must be between 1 byte and 20 MB.");
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The selected file is not a MidFlow settings package.");

        if (document.RootElement.EnumerateObject().Any(x => string.Equals(x.Name,
                nameof(SettingsTransferPackage.FormatVersion), StringComparison.OrdinalIgnoreCase)))
            return JsonSerializer.Deserialize<SettingsTransferPackage>(json, JsonOptions)
                ?? throw new InvalidDataException("The settings package is empty.");

        var stored = JsonSerializer.Deserialize<AppSettings.SettingsData>(json, JsonOptions)
            ?? throw new InvalidDataException("The settings backup is empty.");
        return FromStoredSettings(stored);
    }

    internal static string Serialize(SettingsTransferPackage package) => JsonSerializer.Serialize(package, JsonOptions);

    internal static string WriteAutomaticBackup(SettingsTransferPackage package, string reason)
    {
        Directory.CreateDirectory(AppSettings.BackupDirectory);
        var safeReason = string.Concat(reason.Where(char.IsLetterOrDigit));
        string fileName;
        if (string.Equals(reason, "BeforeMigration", StringComparison.Ordinal))
        {
            var stable = Serialize(package with { ExportedUtc = DateTime.UnixEpoch });
            var digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stable)))[..16];
            fileName = $"MidFlow-{safeReason}-{digest}.midflow.json";
        }
        else fileName = $"MidFlow-{safeReason}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.midflow.json";
        var path = Path.Combine(AppSettings.BackupDirectory, fileName);
        if (File.Exists(path)) return path;
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, Serialize(package));
            try { File.Move(temporaryPath, path); }
            catch (IOException) when (File.Exists(path)) { }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return path;
    }

    internal static bool Validate(SettingsTransferPackage? package, out string error)
    {
        error = string.Empty;
        if (package is null) { error = "The package is empty."; return false; }
        if (package.FormatVersion != CurrentFormatVersion)
        {
            error = $"This package uses format version {package.FormatVersion}; MidFlow supports version {CurrentFormatVersion}.";
            return false;
        }
        if (package.Scope is not (AllScope or ProfilesScope))
        {
            error = "The package scope is not recognized.";
            return false;
        }
        if (package.Profiles is null || !ValidateProfiles(package.Profiles, package.Scope == ProfilesScope, out error)) return false;
        if (package.Scope == ProfilesScope)
        {
            if (package.Globals is not null) { error = "A selected-profile package must not replace global settings."; return false; }
            return true;
        }
        if (package.Globals is null) { error = "A full-settings package is missing its global settings."; return false; }
        var globals = package.Globals;
        if (globals.GlobalShortcuts is null || !globals.GlobalShortcuts.IsValid)
        { error = "The global shortcut settings are invalid."; return false; }
        if (globals.MinimumHorizontalMovementPx is < 20 or > 300 ||
            globals.MaximumVerticalMovementPx is < 20 or > 300 ||
            globals.MaximumGestureDurationMs is < 250 or > 3000 ||
            globals.ClickMovementThresholdPx is < 2 or > 15)
        { error = "One or more gesture sensitivity values are outside the allowed ranges."; return false; }
        if (!ValidateGestureList(globals.GlobalGestures, out error)) return false;
        var profiles = package.Profiles;
        var localGestureProfiles = profiles.GestureProfiles ?? new(StringComparer.OrdinalIgnoreCase);
        foreach (var item in localGestureProfiles)
        {
            var resolved = GestureResolver.Resolve(item.Key, globals.GlobalGestures,
                new Dictionary<string, GestureProfile>(StringComparer.OrdinalIgnoreCase) { [item.Key] = item.Value });
            if (HasConflicts(resolved))
            { error = $"The {item.Key} profile has custom shapes that conflict after inheritance is resolved."; return false; }
        }
        if (HasConflicts(globals.GlobalGestures)) { error = "Global custom shapes conflict with each other."; return false; }
        return true;
    }

    internal static SettingsTransferPackage MergeProfiles(SettingsTransferPackage currentAll,
        SettingsTransferPackage incomingProfiles)
    {
        var current = currentAll.Profiles!;
        var incoming = incomingProfiles.Profiles!;
        var enabled = Copy(current.ApplicationEnabled!);
        var shortcuts = Copy(current.ShortcutProfiles!);
        var gestures = Copy(current.GestureProfiles!);
        var paths = Copy(current.ApplicationPaths!);
        foreach (var item in incoming.ApplicationEnabled!) enabled[item.Key] = item.Value;
        foreach (var item in incoming.ShortcutProfiles!) shortcuts[item.Key] = item.Value;
        foreach (var item in incoming.GestureProfiles!) gestures[item.Key] = item.Value;
        foreach (var item in incoming.ApplicationPaths!) paths[item.Key] = item.Value;
        var incomingNames = ProfileNames(incoming);
        var namesWithPath = incoming.ApplicationPaths!.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in incomingNames)
            if (!namesWithPath.Contains(name)) paths.Remove(name);
        return currentAll with { ExportedUtc = DateTime.UtcNow, Profiles = new TransferProfiles(enabled, shortcuts, gestures, paths) };
    }

    internal static IReadOnlyList<string> ProfileNames(TransferProfiles profiles) =>
        (profiles.ApplicationEnabled?.Keys.AsEnumerable() ?? Enumerable.Empty<string>())
            .Concat(profiles.ShortcutProfiles?.Keys.AsEnumerable() ?? Enumerable.Empty<string>())
            .Concat(profiles.GestureProfiles?.Keys.AsEnumerable() ?? Enumerable.Empty<string>())
            .Concat(profiles.ApplicationPaths?.Keys.AsEnumerable() ?? Enumerable.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    internal static IReadOnlyList<string> MissingExecutableReferences(SettingsTransferPackage package)
    {
        var missing = new List<string>();
        if (package.Profiles?.ApplicationPaths is { } paths)
            missing.AddRange(paths.Where(x => !File.Exists(x.Value)).Select(x => $"{x.Key}: profile executable reference is missing"));
        var gestureSets = new List<(string Name, IReadOnlyList<GestureDefinition> Gestures)>();
        if (package.Globals is { } globals)
        {
            gestureSets.Add(("Global defaults", globals.GlobalGestures));
            if (package.Profiles?.GestureProfiles is { } fullProfiles)
                gestureSets.AddRange(fullProfiles.Select(x => (x.Key, (IReadOnlyList<GestureDefinition>)x.Value.Gestures)));
        }
        else if (package.Profiles?.GestureProfiles is { } profiles)
        {
            gestureSets.AddRange(profiles.Select(x => (x.Key, (IReadOnlyList<GestureDefinition>)x.Value.Gestures)));
        }
        foreach (var set in gestureSets)
            foreach (var gesture in set.Gestures.Where(x => x.Action.Kind == GestureActionKind.LaunchApplication && !File.Exists(x.Action.ApplicationPath)))
                missing.Add($"{set.Name}: {gesture.Name} launch target is missing");
        return missing;
    }

    private static bool ValidateProfiles(TransferProfiles profiles, bool selectedScope, out string error)
    {
        error = string.Empty;
        var enabled = profiles.ApplicationEnabled ?? new(StringComparer.OrdinalIgnoreCase);
        var shortcuts = profiles.ShortcutProfiles ?? new(StringComparer.OrdinalIgnoreCase);
        var gestures = profiles.GestureProfiles ?? new(StringComparer.OrdinalIgnoreCase);
        var paths = profiles.ApplicationPaths ?? new(StringComparer.OrdinalIgnoreCase);
        var names = ProfileNames(profiles);
        if (selectedScope && names.Count == 0) { error = "The selected-profile package contains no application profiles."; return false; }
        if (profiles.ApplicationEnabled is null || profiles.ShortcutProfiles is null ||
            profiles.GestureProfiles is null || profiles.ApplicationPaths is null)
        { error = "The package is missing one or more application profile maps."; return false; }
        if (names.Count > 1000) { error = "A package cannot contain more than 1,000 application profiles."; return false; }
        foreach (var name in names)
            if (!IsValidProcessName(name)) { error = $"The application profile name “{name}” is invalid."; return false; }
        if (HasDuplicateNames(enabled.Keys) || HasDuplicateNames(shortcuts.Keys) || HasDuplicateNames(gestures.Keys) || HasDuplicateNames(paths.Keys))
        { error = "The package contains duplicate application profile names that differ only by letter case."; return false; }
        foreach (var item in shortcuts)
        {
            if (item.Value is null || !item.Value.IsValid)
            { error = $"The shortcut mapping for {item.Key} is invalid."; return false; }
            if (selectedScope && (item.Value.Back is null || item.Value.Forward is null || item.Value.Up is null || item.Value.Down is null))
            { error = $"The portable mapping for {item.Key} must contain all four resolved directions."; return false; }
        }
        foreach (var item in gestures)
        {
            if (item.Value is null || item.Value.Gestures is null || item.Value.Gestures.Count > 1024 ||
                item.Value.Gestures.Any(x => !x.IsValid) || HasDuplicateGestureIds(item.Value.Gestures))
            { error = $"The custom gestures for {item.Key} are invalid."; return false; }
            if (selectedScope && item.Value.InheritGlobal)
            { error = $"The portable profile for {item.Key} must contain resolved gestures rather than inherited references."; return false; }
            if (selectedScope && HasConflicts(item.Value.Gestures))
            { error = $"The custom gestures for {item.Key} conflict with each other."; return false; }
        }
        foreach (var item in paths)
            if (string.IsNullOrWhiteSpace(item.Value) || !Path.IsPathFullyQualified(item.Value))
            { error = $"The executable reference for {item.Key} is not a fully qualified path."; return false; }
        if (selectedScope)
        {
            foreach (var name in names)
                if (!enabled.ContainsKey(name) || !shortcuts.ContainsKey(name) || !gestures.ContainsKey(name))
                { error = $"The portable profile for {name} is incomplete."; return false; }
        }
        return true;
    }

    private static bool ValidateGestureList(IReadOnlyList<GestureDefinition>? list, out string error)
    {
        error = string.Empty;
        if (list is null || list.Count > 1024 || list.Any(x => !x.IsValid) || HasDuplicateGestureIds(list))
        { error = "The package contains invalid or duplicate global custom gestures."; return false; }
        return true;
    }

    private static bool HasDuplicateGestureIds(IReadOnlyList<GestureDefinition> list) =>
        list.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != list.Count;

    private static bool HasConflicts(IReadOnlyList<GestureDefinition> gestures)
    {
        for (var i = 0; i < gestures.Count; i++)
            for (var j = i + 1; j < gestures.Count; j++)
                if (gestures[i].Enabled && gestures[j].Enabled && gestures[i].Path.DistanceTo(gestures[j].Path) < 0.255)
                    return true;
        return false;
    }

    private static bool HasDuplicateNames(IEnumerable<string> names) =>
        names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count();

    private static bool IsValidProcessName(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 260 &&
        value == Path.GetFileNameWithoutExtension(value) && value is not "." and not ".." &&
        value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static string NormalizeProcessName(string value) => Path.GetFileNameWithoutExtension(value.Trim());

    private static Dictionary<string, TValue> NormalizeMap<TValue>(IReadOnlyDictionary<string, TValue> source,
        Func<TValue, TValue> normalizeValue)
    {
        var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source) result[NormalizeProcessName(item.Key)] = normalizeValue(item.Value);
        return result;
    }

    private static Dictionary<string, TValue> Copy<TValue>(IReadOnlyDictionary<string, TValue> source)
    {
        var result = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source) result[item.Key] = item.Value;
        return result;
    }
}
