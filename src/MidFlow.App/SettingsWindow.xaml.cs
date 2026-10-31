using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MidFlow.Core.Gestures;
using MidFlow.Core.Shortcuts;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Grid = System.Windows.Controls.Grid;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using Polyline = System.Windows.Shapes.Polyline;

namespace MidFlow.App;

public partial class SettingsWindow : Window
{
    private ShortcutPair _globalShortcuts = ShortcutPair.Defaults;
    private readonly Dictionary<string, ShortcutOverride> _shortcutProfiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _applicationEnabled = new(StringComparer.OrdinalIgnoreCase);
    private List<GestureDefinition> _globalGestures = [];
    private readonly Dictionary<string, GestureProfile> _gestureProfiles = new(StringComparer.OrdinalIgnoreCase);
    private Button? _captureButton;
    private ShortcutCaptureTarget? _captureTarget;
    private bool _refreshingProfiles;

    public SettingsWindow()
    {
        InitializeComponent();
        GesturesCheckBox.IsChecked = AppSettings.GesturesEnabled;
        TrailCheckBox.IsChecked = AppSettings.ShowTrail;
        MinimumHorizontalTextBox.Text = AppSettings.MinimumHorizontalMovementPx.ToString();
        MaximumVerticalTextBox.Text = AppSettings.MaximumVerticalMovementPx.ToString();
        MaximumDurationTextBox.Text = AppSettings.MaximumGestureDurationMs.ToString();
        ClickThresholdTextBox.Text = AppSettings.ClickMovementThresholdPx.ToString();
        _globalShortcuts = AppSettings.GlobalShortcuts;
        foreach (var item in AppSettings.ShortcutProfiles) _shortcutProfiles[item.Key] = item.Value;
        foreach (var item in AppSettings.Profiles) _applicationEnabled[item.Key] = item.Value;
        _globalGestures = AppSettings.GlobalGestures.ToList();
        foreach (var item in AppSettings.GestureProfiles) _gestureProfiles[item.Key] = item.Value;
        foreach (var app in _shortcutProfiles.Keys.Concat(_gestureProfiles.Keys))
            _applicationEnabled.TryAdd(app, true);
        RefreshProfileList();
    }

    private string? SelectedProcess => (ProfileListBox.SelectedItem as ListBoxItem)?.Tag as string;

    private void RefreshProfileList(string? selectedProcess = null)
    {
        _refreshingProfiles = true;
        ProfileListBox.Items.Clear();
        ProfileListBox.Items.Add(new ListBoxItem { Content = "Global defaults", Tag = null, Padding = new Thickness(8, 6, 8, 6) });
        var query = ProfileSearchBox.Text.Trim();
        var apps = _applicationEnabled.Keys.Concat(_shortcutProfiles.Keys).Concat(_gestureProfiles.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => string.IsNullOrWhiteSpace(query) || x.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        foreach (var app in apps)
        {
            var enabled = _applicationEnabled.GetValueOrDefault(app, true);
            ProfileListBox.Items.Add(new ListBoxItem
            {
                Content = enabled ? app : $"{app}  (disabled)",
                Tag = app,
                Opacity = enabled ? 1 : 0.52,
                Padding = new Thickness(8, 6, 8, 6)
            });
        }
        var selected = ProfileListBox.Items.Cast<ListBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, selectedProcess, StringComparison.OrdinalIgnoreCase));
        ProfileListBox.SelectedItem = selected ?? ProfileListBox.Items[0];
        _refreshingProfiles = false;
        RefreshProfileEditor();
    }

    private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingProfiles) return;
        _captureButton = null;
        _captureTarget = null;
        RefreshProfileEditor();
    }

    private void ProfileSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_refreshingProfiles) RefreshProfileList(SelectedProcess);
    }

    private void RefreshProfileEditor()
    {
        var process = SelectedProcess;
        var isGlobal = process is null;
        var enabled = isGlobal || _applicationEnabled.GetValueOrDefault(process!, true);
        SelectedProfileTitle.Text = isGlobal ? "Global defaults" : process!;
        RemoveApplicationButton.Visibility = isGlobal ? Visibility.Collapsed : Visibility.Visible;
        ApplicationEnabledCheckBox.Visibility = isGlobal ? Visibility.Collapsed : Visibility.Visible;
        _refreshingProfiles = true;
        ApplicationEnabledCheckBox.IsChecked = enabled;
        ApplicationEnabledCheckBox.IsEnabled = !isGlobal;
        _refreshingProfiles = false;
        ProfileDescription.Text = isGlobal
            ? "Used by applications without an override. Existing Back and Forward shortcuts are preserved; Up and Down start disabled."
            : "This app inherits each global binding until you override it. Custom gestures can be changed independently.";
        GestureRowsPanel.IsEnabled = enabled;
        AddCustomGestureButton.IsEnabled = enabled;
        GestureRowsPanel.Children.Clear();

        AddDirectionRow(process, GestureDirection.Left, "←  Back");
        AddDirectionRow(process, GestureDirection.Right, "→  Forward");
        AddDirectionRow(process, GestureDirection.Up, "↑  Up");
        AddDirectionRow(process, GestureDirection.Down, "↓  Down");

        var section = new TextBlock
        {
            Text = "CUSTOM SHAPES",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.Gray,
            Margin = new Thickness(4, 16, 4, 6)
        };
        GestureRowsPanel.Children.Add(section);

        var gestures = process is null
            ? _globalGestures.ToArray()
            : GestureResolver.Resolve(process, _globalGestures, _gestureProfiles).ToArray();
        if (gestures.Length == 0)
        {
            GestureRowsPanel.Children.Add(new TextBlock
            {
                Text = process is null ? "No custom shapes yet." : "No custom shapes are available in this profile.",
                Foreground = Brushes.Gray,
                Margin = new Thickness(8, 12, 8, 12)
            });
        }
        foreach (var gesture in gestures) AddCustomGestureRow(process, gesture);
    }

    private void AddDirectionRow(string? process, GestureDirection direction, string label)
    {
        var effective = process is null
            ? ShortcutFor(_globalShortcuts, direction)
            : ShortcutResolver.Resolve(process, _globalShortcuts, _shortcutProfiles) is { } resolved
                ? ShortcutFor(resolved, direction)
                : KeyboardShortcut.Disabled;
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });

        var overrideValue = process is null ? effective : ShortcutOverrideFor(process, direction);
        var binding = new Button
        {
            Content = process is not null && overrideValue is null ? $"Inherits {Display(effective)}" : Display(effective),
            Tag = new ShortcutCaptureTarget(process, direction),
            Margin = new Thickness(3, 2, 3, 2),
            ToolTip = process is null
                ? "Click and press a chord. Escape disables; Backspace restores the default."
                : "Click and press a chord. Escape disables; Backspace inherits the global binding."
        };
        binding.Click += BeginShortcutCapture_Click;
        binding.PreviewKeyDown += ShortcutCapture_KeyDown;
        Grid.SetColumn(binding, 1);
        row.Children.Add(binding);

        var defaultButton = new Button
        {
            Content = process is null ? "Default" : "Inherit",
            IsEnabled = process is null || overrideValue is not null,
            Tag = new ShortcutCaptureTarget(process, direction),
            Margin = new Thickness(3, 2, 3, 2),
            ToolTip = process is null ? "Restore the default binding" : "Use the global binding"
        };
        defaultButton.Click += ResetShortcut_Click;
        Grid.SetColumn(defaultButton, 2);
        row.Children.Add(defaultButton);

        var status = new TextBlock
        {
            Text = effective.Enabled ? "Enabled" : "Disabled",
            Foreground = effective.Enabled ? Brushes.SeaGreen : Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        Grid.SetColumn(status, 3);
        row.Children.Add(status);
        AddRowBorder(row);
    }

    private void AddCustomGestureRow(string? process, GestureDefinition gesture)
    {
        var hasGlobal = _globalGestures.Any(x => string.Equals(x.Id, gesture.Id, StringComparison.OrdinalIgnoreCase));
        var local = process is not null ? LocalGesture(process, gesture.Id) : null;
        var isOverride = local is not null && hasGlobal;
        var isInherited = process is not null && hasGlobal && local is null;
        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var toggle = new CheckBox
        {
            IsChecked = gesture.Enabled,
            Tag = (process, gesture),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            ToolTip = "Enable or disable this custom gesture"
        };
        toggle.Checked += CustomGestureEnabled_Changed;
        toggle.Unchecked += CustomGestureEnabled_Changed;
        row.Children.Add(toggle);
        var preview = CreateGesturePreview(gesture.Path);
        Grid.SetColumn(preview, 1);
        row.Children.Add(preview);
        var name = new TextBlock
        {
            Text = $"{gesture.Name}  ·  {ActionText(gesture.Action)}",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = gesture.Enabled ? 1 : 0.48,
            ToolTip = $"{gesture.Name}: {ActionText(gesture.Action)}"
        };
        Grid.SetColumn(name, 2);
        row.Children.Add(name);
        var source = new TextBlock
        {
            Text = process is null ? "Global" : isOverride ? "App override" : isInherited ? "Inherited" : "App only",
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        Grid.SetColumn(source, 3);
        row.Children.Add(source);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var edit = new Button { Content = "Edit", Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(2) };
        edit.Click += (_, _) => EditCustomGesture(process, gesture);
        buttons.Children.Add(edit);
        if (process is null)
        {
            var remove = new Button { Content = "Remove", Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(2) };
            remove.Click += (_, _) => { _globalGestures.RemoveAll(x => x.Id == gesture.Id); RefreshProfileEditor(); };
            buttons.Children.Add(remove);
        }
        else if (isOverride)
        {
            var reset = new Button { Content = "Reset", Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(2), ToolTip = "Remove this app override and inherit the global gesture" };
            reset.Click += (_, _) => { RemoveLocalGesture(process, gesture.Id); RefreshProfileEditor(); };
            buttons.Children.Add(reset);
        }
        else if (isInherited)
        {
            var disable = new Button { Content = "Disable", Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(2) };
            disable.Click += (_, _) => DisableInheritedGesture(process, gesture);
            buttons.Children.Add(disable);
        }
        else
        {
            var remove = new Button { Content = "Remove", Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(2) };
            remove.Click += (_, _) => { RemoveLocalGesture(process, gesture.Id); RefreshProfileEditor(); };
            buttons.Children.Add(remove);
        }
        Grid.SetColumn(buttons, 4);
        row.Children.Add(buttons);
        AddRowBorder(row);
    }

    private static Canvas CreateGesturePreview(GesturePath path)
    {
        var canvas = new Canvas { Width = 34, Height = 28, Margin = new Thickness(3, 0, 5, 0), ClipToBounds = true };
        var points = new PointCollection();
        const double span = 22;
        foreach (var point in path.Points)
            points.Add(new Point(17 + point.X * span, 14 + point.Y * span));
        canvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromRgb(60, 130, 205)),
            StrokeThickness = 1.8,
            StrokeLineJoin = PenLineJoin.Round
        });
        return canvas;
    }

    private void AddRowBorder(Grid row) => GestureRowsPanel.Children.Add(new Border
    {
        BorderBrush = new SolidColorBrush(Color.FromRgb(225, 225, 225)),
        BorderThickness = new Thickness(0, 0, 0, 1),
        Padding = new Thickness(2),
        Child = row
    });

    private void AddApplication_Click(object sender, RoutedEventArgs e)
    {
        AddApplication();
    }

    private void ApplicationTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddApplication();
    }

    private void AddApplication()
    {
        var process = Path.GetFileNameWithoutExtension(ApplicationTextBox.Text.Trim());
        if (string.IsNullOrWhiteSpace(process))
        {
            MessageBox.Show(this, "Enter an application process name or browse to an application executable.",
                "Gesture profiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_applicationEnabled.ContainsKey(process) || _shortcutProfiles.ContainsKey(process) || _gestureProfiles.ContainsKey(process))
        {
            MessageBox.Show(this, $"{process} already has a profile.", "Gesture profiles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _applicationEnabled[process] = true;
        _shortcutProfiles[process] = new ShortcutOverride(null, null, null, null);
        _gestureProfiles[process] = GestureProfile.Inherited;
        ApplicationTextBox.Clear();
        _refreshingProfiles = true;
        ProfileSearchBox.Clear();
        _refreshingProfiles = false;
        RefreshProfileList(process);
    }

    private void BrowseApplication_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an application",
            Filter = "Applications (*.exe)|*.exe",
            DefaultExt = ".exe",
            CheckFileExists = true,
            Multiselect = false,
            ValidateNames = true
        };
        if (picker.ShowDialog(this) == true) ApplicationTextBox.Text = Path.GetFileNameWithoutExtension(picker.FileName);
    }

    private void RemoveApplication_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess is not { } process) return;
        _applicationEnabled.Remove(process);
        _shortcutProfiles.Remove(process);
        _gestureProfiles.Remove(process);
        RefreshProfileList();
    }

    private void ApplicationEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshingProfiles) return;
        if (SelectedProcess is not { } process) return;
        _applicationEnabled[process] = ApplicationEnabledCheckBox.IsChecked == true;
        RefreshProfileList(process);
    }

    private void BeginShortcutCapture_Click(object sender, RoutedEventArgs e)
    {
        _captureButton = (Button)sender;
        _captureTarget = (ShortcutCaptureTarget)_captureButton.Tag;
        _captureButton.Content = "Press shortcut…";
        _captureButton.Focus();
    }

    private void ShortcutCapture_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_captureButton is null || _captureTarget is null) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        KeyboardShortcut? shortcut;
        if (key == Key.Escape) shortcut = KeyboardShortcut.Disabled;
        else if (key == Key.Back)
            shortcut = _captureTarget.Process is null ? DefaultShortcut(_captureTarget.Direction) : null;
        else
        {
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
                Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            var modifiers = ShortcutModifiers.None;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) modifiers |= ShortcutModifiers.Ctrl;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) modifiers |= ShortcutModifiers.Alt;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) modifiers |= ShortcutModifiers.Shift;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) modifiers |= ShortcutModifiers.Win;
            shortcut = new KeyboardShortcut(true, checked((ushort)KeyInterop.VirtualKeyFromKey(key)), modifiers);
        }
        StoreShortcut(_captureTarget, shortcut);
        _captureButton = null;
        _captureTarget = null;
        RefreshProfileEditor();
    }

    private void ResetShortcut_Click(object sender, RoutedEventArgs e)
    {
        var target = (ShortcutCaptureTarget)((Button)sender).Tag;
        StoreShortcut(target, target.Process is null ? DefaultShortcut(target.Direction) : null);
        RefreshProfileEditor();
    }

    private void StoreShortcut(ShortcutCaptureTarget target, KeyboardShortcut? shortcut)
    {
        if (target.Process is null)
        {
            var value = shortcut ?? DefaultShortcut(target.Direction);
            _globalShortcuts = target.Direction switch
            {
                GestureDirection.Left => _globalShortcuts with { Back = value },
                GestureDirection.Right => _globalShortcuts with { Forward = value },
                GestureDirection.Up => _globalShortcuts with { Up = value },
                GestureDirection.Down => _globalShortcuts with { Down = value },
                _ => _globalShortcuts
            };
            return;
        }
        var existing = _shortcutProfiles.GetValueOrDefault(target.Process) ?? new ShortcutOverride(null, null, null, null);
        _shortcutProfiles[target.Process] = target.Direction switch
        {
            GestureDirection.Left => existing with { Back = shortcut },
            GestureDirection.Right => existing with { Forward = shortcut },
            GestureDirection.Up => existing with { Up = shortcut },
            GestureDirection.Down => existing with { Down = shortcut },
            _ => existing
        };
    }

    private void CustomGestureEnabled_Changed(object sender, RoutedEventArgs e)
    {
        var toggle = (CheckBox)sender;
        var (process, gesture) = ((string? Process, GestureDefinition Gesture))toggle.Tag;
        SetGesture(process, gesture with { Enabled = toggle.IsChecked == true });
    }

    private void AddCustomGesture_Click(object sender, RoutedEventArgs e)
    {
        var editor = new GestureEditorWindow { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } gesture)
        {
            SetGesture(SelectedProcess, gesture);
        }
    }

    private void EditCustomGesture(string? process, GestureDefinition gesture)
    {
        var editor = new GestureEditorWindow(gesture) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } updated) SetGesture(process, updated);
    }

    private void SetGesture(string? process, GestureDefinition gesture)
    {
        if (process is null)
        {
            var index = _globalGestures.FindIndex(x => string.Equals(x.Id, gesture.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) _globalGestures.Add(gesture);
            else _globalGestures[index] = gesture;
        }
        else
        {
            var profile = _gestureProfiles.GetValueOrDefault(process) ?? GestureProfile.Inherited;
            var local = profile.Gestures.ToList();
            var index = local.FindIndex(x => string.Equals(x.Id, gesture.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) local.Add(gesture);
            else local[index] = gesture;
            _gestureProfiles[process] = new GestureProfile(true, local);
        }
        RefreshProfileEditor();
    }

    private void RemoveLocalGesture(string process, string id)
    {
        var profile = _gestureProfiles.GetValueOrDefault(process) ?? GestureProfile.Inherited;
        _gestureProfiles[process] = new GestureProfile(true,
            profile.Gestures.Where(x => !string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    private void DisableInheritedGesture(string process, GestureDefinition gesture)
    {
        SetGesture(process, gesture with { Enabled = false });
    }

    private GestureDefinition? LocalGesture(string process, string id) =>
        _gestureProfiles.TryGetValue(process, out var profile)
            ? profile.Gestures.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
            : null;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MinimumHorizontalTextBox.Text, out var minimumHorizontal) || minimumHorizontal is < 20 or > 300 ||
            !int.TryParse(MaximumVerticalTextBox.Text, out var maximumVertical) || maximumVertical is < 20 or > 300 ||
            !int.TryParse(MaximumDurationTextBox.Text, out var maximumDuration) || maximumDuration is < 250 or > 3000 ||
            !int.TryParse(ClickThresholdTextBox.Text, out var clickThreshold) || clickThreshold is < 2 or > 15)
        {
            MessageBox.Show(this, "Gesture values are outside the allowed ranges.", "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!_globalShortcuts.IsValid || _shortcutProfiles.Values.Any(x => !x.IsValid) ||
            _globalGestures.Any(x => !x.IsValid) || HasConflicts(_globalGestures) ||
            _gestureProfiles.Any(x => HasConflicts(GestureResolver.Resolve(x.Key, _globalGestures, _gestureProfiles))))
        {
            MessageBox.Show(this, "Review shortcut mappings and make sure active custom shapes are distinct within each profile.",
                "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppSettings.MinimumHorizontalMovementPx = minimumHorizontal;
        AppSettings.MaximumVerticalMovementPx = maximumVertical;
        AppSettings.MaximumGestureDurationMs = maximumDuration;
        AppSettings.ClickMovementThresholdPx = clickThreshold;
        AppSettings.GesturesEnabled = GesturesCheckBox.IsChecked == true;
        AppSettings.ShowTrail = TrailCheckBox.IsChecked == true;
        AppSettings.SetProfiles(_applicationEnabled);
        try
        {
            AppSettings.SetShortcuts(_globalShortcuts, _shortcutProfiles);
            AppSettings.SetGestureMappings(_globalGestures, _gestureProfiles);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, $"Review the profile mappings before saving.\n\n{ex.Message}",
                "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AppSettings.Save();
        Close();
    }

    private static KeyboardShortcut ShortcutFor(ShortcutPair pair, GestureDirection direction) => direction switch
    {
        GestureDirection.Left => pair.Back,
        GestureDirection.Right => pair.Forward,
        GestureDirection.Up => pair.UpOrDisabled,
        GestureDirection.Down => pair.DownOrDisabled,
        _ => KeyboardShortcut.Disabled
    };

    private KeyboardShortcut? ShortcutOverrideFor(string process, GestureDirection direction)
    {
        if (!_shortcutProfiles.TryGetValue(process, out var value)) return null;
        return direction switch
        {
            GestureDirection.Left => value.Back,
            GestureDirection.Right => value.Forward,
            GestureDirection.Up => value.Up,
            GestureDirection.Down => value.Down,
            _ => null
        };
    }

    private static KeyboardShortcut DefaultShortcut(GestureDirection direction) => direction switch
    {
        GestureDirection.Left => KeyboardShortcut.DefaultBack,
        GestureDirection.Right => KeyboardShortcut.DefaultForward,
        _ => KeyboardShortcut.Disabled
    };

    private static string Display(KeyboardShortcut shortcut)
    {
        if (!shortcut.Enabled) return "Disabled";
        var pieces = new List<string>();
        if ((shortcut.Modifiers & ShortcutModifiers.Ctrl) != 0) pieces.Add("Ctrl");
        if ((shortcut.Modifiers & ShortcutModifiers.Alt) != 0) pieces.Add("Alt");
        if ((shortcut.Modifiers & ShortcutModifiers.Shift) != 0) pieces.Add("Shift");
        if ((shortcut.Modifiers & ShortcutModifiers.Win) != 0) pieces.Add("Win");
        pieces.Add(KeyInterop.KeyFromVirtualKey(shortcut.Key).ToString());
        return string.Join("+", pieces);
    }

    private static string ActionText(GestureActionDefinition action) => action.Kind switch
    {
        GestureActionKind.Back => "Back",
        GestureActionKind.Forward => "Forward",
        GestureActionKind.Up => "Up",
        GestureActionKind.Down => "Down",
        GestureActionKind.LaunchApplication => $"Launch {Path.GetFileName(action.ApplicationPath)}",
        _ => "Unknown action"
    };

    private static bool HasConflicts(IReadOnlyList<GestureDefinition> gestures)
    {
        if (gestures.Select(g => g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != gestures.Count) return true;
        for (var i = 0; i < gestures.Count; i++)
            for (var j = i + 1; j < gestures.Count; j++)
                if (gestures[i].Enabled && gestures[j].Enabled && gestures[i].Path.DistanceTo(gestures[j].Path) < 0.255)
                    return true;
        return false;
    }

    private sealed record ShortcutCaptureTarget(string? Process, GestureDirection Direction);
}
