namespace MidFlow.Core.Shortcuts;

[Flags]
public enum ShortcutModifiers { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }

public sealed record KeyboardShortcut(bool Enabled, ushort Key, ShortcutModifiers Modifiers)
{
    public static KeyboardShortcut Disabled { get; } = new(false, 0, ShortcutModifiers.None);
    public static KeyboardShortcut DefaultBack { get; } = new(true, 0x25, ShortcutModifiers.Alt);
    public static KeyboardShortcut DefaultForward { get; } = new(true, 0x27, ShortcutModifiers.Alt);

    public bool IsValid => !Enabled || (Key != 0 &&
        (Modifiers & ~(ShortcutModifiers.Ctrl | ShortcutModifiers.Alt | ShortcutModifiers.Shift | ShortcutModifiers.Win)) == 0);
}

public sealed record ShortcutPair(KeyboardShortcut Back, KeyboardShortcut Forward)
{
    public static ShortcutPair Defaults { get; } = new(KeyboardShortcut.DefaultBack, KeyboardShortcut.DefaultForward);
    public bool IsValid => Back is not null && Forward is not null && Back.IsValid && Forward.IsValid;
}

public sealed record ShortcutOverride(KeyboardShortcut? Back, KeyboardShortcut? Forward)
{
    public bool IsValid => (Back?.IsValid ?? true) && (Forward?.IsValid ?? true);
}

public static class ShortcutResolver
{
    public static ShortcutPair Resolve(string? processName, ShortcutPair global,
        IReadOnlyDictionary<string, ShortcutOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(overrides);
        if (!string.IsNullOrWhiteSpace(processName))
        {
            var name = Path.GetFileNameWithoutExtension(processName.Trim());
            var match = overrides.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase));
            if (match.Value is not null)
                return new ShortcutPair(match.Value.Back ?? global.Back, match.Value.Forward ?? global.Forward);
        }
        return global;
    }
}
