using MidFlow.Core.Shortcuts;
using Xunit;

namespace MidFlow.Core.Tests;

public class ShortcutResolverTests
{
    [Fact]
    public void MissingApplicationUsesGlobalDefaults()
    {
        Assert.Same(ShortcutPair.Defaults, ShortcutResolver.Resolve("unknown.exe",
            ShortcutPair.Defaults, new Dictionary<string, ShortcutOverride>()));
    }

    [Fact]
    public void ApplicationOverrideMatchesProcessNameWithoutExtensionAndIgnoresCase()
    {
        var custom = new ShortcutOverride(KeyboardShortcut.Disabled,
            new KeyboardShortcut(true, 0x42, ShortcutModifiers.Ctrl));
        var resolved = ShortcutResolver.Resolve("Browser.EXE", ShortcutPair.Defaults,
            new Dictionary<string, ShortcutOverride> { ["browser"] = custom });
        Assert.Equal(custom.Back, resolved.Back);
        Assert.Equal(custom.Forward, resolved.Forward);
        var inherited = ShortcutResolver.Resolve("browser", ShortcutPair.Defaults,
            new Dictionary<string, ShortcutOverride> { ["browser"] = new ShortcutOverride(custom.Back, null) });
        Assert.Equal(ShortcutPair.Defaults.Forward, inherited.Forward);
    }

    [Fact]
    public void ShortcutValidationAcceptsDisabledAndRejectsUnknownModifierBits()
    {
        Assert.True(KeyboardShortcut.Disabled.IsValid);
        Assert.False(new KeyboardShortcut(true, 0x41, (ShortcutModifiers)16).IsValid);
    }

    [Fact]
    public void VerticalShortcutsDefaultToDisabledAndCanBeOverriddenPerApplication()
    {
        Assert.False(ShortcutPair.Defaults.UpOrDisabled.Enabled);
        Assert.False(ShortcutPair.Defaults.DownOrDisabled.Enabled);
        var up = new KeyboardShortcut(true, 0x26, ShortcutModifiers.Ctrl);
        var resolved = ShortcutResolver.Resolve("browser.exe", ShortcutPair.Defaults,
            new Dictionary<string, ShortcutOverride> { ["browser"] = new ShortcutOverride(null, null, up, null) });
        Assert.Equal(up, resolved.Up);
        Assert.Equal(KeyboardShortcut.Disabled, resolved.DownOrDisabled);
    }
}
