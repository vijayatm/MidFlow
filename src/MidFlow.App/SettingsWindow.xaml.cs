using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Diagnostics;
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
    private readonly Dictionary<string, string> _applicationPaths = new(StringComparer.OrdinalIgnoreCase);
    private List<GestureDefinition> _globalGestures = [];
    private readonly Dictionary<string, GestureProfile> _gestureProfiles = new(StringComparer.OrdinalIgnoreCase);
    private Button? _captureButton;
    private ShortcutCaptureTarget? _captureTarget;
    private bool _refreshingProfiles;
    private bool _loadingSettings = true;
    private bool _hasUnsavedChanges;
    private string? _selectedExecutablePath;
    private bool _updatingApplicationInput;
    private bool _updatingSensitivityPreset;

    public SettingsWindow(string? initialProcess = null, bool createProfile = false)
    {
        InitializeComponent();
        GesturesCheckBox.IsChecked = AppSettings.GesturesEnabled;
        TrailCheckBox.IsChecked = AppSettings.ShowTrail;
        ResultFeedbackCheckBox.IsChecked = AppSettings.ShowResultFeedback;
        ReducedEffectsCheckBox.IsChecked = AppSettings.ReduceVisualEffects;
        MinimumHorizontalTextBox.Text = AppSettings.MinimumHorizontalMovementPx.ToString();
        MaximumVerticalTextBox.Text = AppSettings.MaximumVerticalMovementPx.ToString();
        MaximumDurationTextBox.Text = AppSettings.MaximumGestureDurationMs.ToString();
        ClickThresholdTextBox.Text = AppSettings.ClickMovementThresholdPx.ToString();
        UpdateSensitivityPreset();
        _globalShortcuts = AppSettings.GlobalShortcuts;
        foreach (var item in AppSettings.ShortcutProfiles) _shortcutProfiles[item.Key] = item.Value;
        foreach (var item in AppSettings.Profiles) _applicationEnabled[item.Key] = item.Value;
        foreach (var item in AppSettings.ApplicationPaths) _applicationPaths[item.Key] = item.Value;
        _globalGestures = AppSettings.GlobalGestures.ToList();
        foreach (var item in AppSettings.GestureProfiles) _gestureProfiles[item.Key] = item.Value;
        foreach (var app in _shortcutProfiles.Keys.Concat(_gestureProfiles.Keys).Concat(_applicationPaths.Keys))
            _applicationEnabled.TryAdd(app, true);
        RefreshProfileList();
        _loadingSettings = false;
        SetUnsavedChanges(false);
        if (!string.IsNullOrWhiteSpace(initialProcess))
        {
            initialProcess = Path.GetFileNameWithoutExtension(initialProcess.Trim());
            ApplicationsTab.IsSelected = true;
            if (createProfile && !HasApplicationProfile(initialProcess))
            {
                _applicationEnabled[initialProcess] = true;
                _shortcutProfiles[initialProcess] = new ShortcutOverride(null, null, null, null);
                _gestureProfiles[initialProcess] = GestureProfile.Inherited;
                MarkUnsavedChanges();
            }
            if (HasApplicationProfile(initialProcess)) RefreshProfileList(initialProcess);
            else
            {
                ApplicationTextBox.Text = initialProcess;
                ApplicationTextBox.Focus();
            }
        }
    }

    private string? SelectedProcess => (ProfileListBox.SelectedItem as ListBoxItem)?.Tag as string;

    private void RefreshProfileList(string? selectedProcess = null)
    {
        _refreshingProfiles = true;
        ProfileListBox.Items.Clear();
        ProfileListBox.Items.Add(new ListBoxItem { Content = "Global defaults", Tag = null, Padding = new Thickness(8, 6, 8, 6) });
        var query = ProfileSearchBox.Text.Trim();
        var apps = _applicationEnabled.Keys.Concat(_shortcutProfiles.Keys).Concat(_gestureProfiles.Keys).Concat(_applicationPaths.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => string.IsNullOrWhiteSpace(query) || x.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => ProfileSortBox.SelectedIndex == 1 && !_applicationEnabled.GetValueOrDefault(x, true) ? 1 : 0)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase);
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
        CopyApplicationButton.IsEnabled = SelectedProcess is not null;
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

    private void ProfileSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingSettings && !_refreshingProfiles) RefreshProfileList(SelectedProcess);
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
            : "Each shortcut and custom shape inherits the global setting until you override it. Choose a row to edit, or reset it to global. This app’s enable switch pauses every gesture here.";
        if (!isGlobal)
        {
            var enablement = !_applicationEnabled.TryGetValue(process!, out var appEnabled)
                ? "App enablement inherits the global setting."
                : appEnabled ? "This profile explicitly allows gestures when the global switch is on." : "Gestures are disabled for this app profile.";
            var executable = _applicationPaths.TryGetValue(process!, out var executablePath)
                ? File.Exists(executablePath)
                    ? $"Selected executable: {executablePath}. Matching still uses the process name."
                    : $"Saved executable not found: {executablePath}. This profile still matches by process name. Browse to the correct .exe and choose Add app to update its path."
                : "No executable path is saved; matching uses the process name.";
            ProfileDescription.Text = $"{enablement} Each shortcut and custom shape inherits the global setting until you override it. {executable}";
        }
        GestureRowsPanel.IsEnabled = enabled;
        AddCustomGestureButton.IsEnabled = enabled;
        AddStarterGesturesButton.IsEnabled = isGlobal;
        ResetToInheritedButton.Visibility = isGlobal ? Visibility.Collapsed : Visibility.Visible;
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
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });

        var overrideValue = process is null ? effective : ShortcutOverrideFor(process, direction);
        var isInherited = process is not null && overrideValue is null;
        var binding = new Button
        {
            Content = isInherited ? $"Inherited · {Display(effective)}" : Display(effective),
            Tag = new ShortcutCaptureTarget(process, direction),
            Margin = new Thickness(3, 2, 3, 2),
            Opacity = isInherited ? 0.8 : 1,
            ToolTip = process is null
                ? "Click and press a chord. Escape disables; Backspace restores the default."
                : "Click and press a chord. Escape disables; Backspace inherits the global binding."
        };
        System.Windows.Automation.AutomationProperties.SetName(binding,
            $"{label} shortcut: {(isInherited ? "inherited " : "")}{Display(effective)}");
        binding.Click += BeginShortcutCapture_Click;
        binding.PreviewKeyDown += ShortcutCapture_KeyDown;
        Grid.SetColumn(binding, 1);
        row.Children.Add(binding);

        var defaultButton = new Button
        {
            Content = process is null ? "Default" : "Reset to global",
            IsEnabled = process is null || overrideValue is not null,
            Tag = new ShortcutCaptureTarget(process, direction),
            Margin = new Thickness(3, 2, 3, 2),
            ToolTip = process is null ? "Restore the default binding" : "Remove this override and use the global binding"
        };
        defaultButton.Click += ResetShortcut_Click;
        Grid.SetColumn(defaultButton, 2);
        row.Children.Add(defaultButton);

        var status = new TextBlock
        {
            Text = isInherited ? "Inherited" : process is null ? (effective.Enabled ? "Enabled" : "Disabled") : $"Override · {(effective.Enabled ? "Enabled" : "Disabled")}",
            Foreground = isInherited ? Brushes.DarkSlateBlue : effective.Enabled ? Brushes.SeaGreen : Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        Grid.SetColumn(status, 3);
        row.Children.Add(status);
        System.Windows.Automation.AutomationProperties.SetName(row, $"{label} shortcut settings. {(isInherited ? "Inherited from global profile." : "Application override.")}");
        AddRowBorder(row);
    }

    private void AddCustomGestureRow(string? process, GestureDefinition gesture)
    {
        var hasGlobal = _globalGestures.Any(x => string.Equals(x.Id, gesture.Id, StringComparison.OrdinalIgnoreCase));
        var local = process is not null ? LocalGesture(process, gesture.Id) : null;
        var isOverride = local is not null && hasGlobal;
        var isInherited = process is not null && hasGlobal && local is null;
        var missingLaunchTarget = gesture.Action.Kind == GestureActionKind.LaunchApplication &&
            !File.Exists(gesture.Action.ApplicationPath);
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
            Foreground = missingLaunchTarget ? Brushes.Firebrick : System.Windows.SystemColors.ControlTextBrush,
            ToolTip = missingLaunchTarget
                ? $"The launch executable is missing. Edit this gesture and browse to an existing .exe. Current path: {gesture.Action.ApplicationPath}"
                : $"{gesture.Name}: {ActionText(gesture.Action)}"
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
            remove.Click += (_, _) => { _globalGestures.RemoveAll(x => x.Id == gesture.Id); MarkUnsavedChanges(); RefreshProfileEditor(); };
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

    private void ApplicationTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddApplication();
    }

    private void AddApplication()
    {
        if (!TryResolveApplicationInput(out var process, out var executablePath)) return;
        if (HasApplicationProfile(process))
        {
            if (executablePath is not null && !string.Equals(_applicationPaths.GetValueOrDefault(process), executablePath, StringComparison.OrdinalIgnoreCase))
            {
                var updatePath = MessageBox.Show(this,
                    $"The {process} process profile already exists. Update its saved executable reference to:\n{executablePath}\n\nThis does not change profile matching, which remains based on process name?",
                    "Update executable reference", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (updatePath != MessageBoxResult.Yes)
                {
                    ClearApplicationInput();
                    ClearProfileSearch();
                    RefreshProfileList(process);
                    return;
                }
                _applicationPaths[process] = executablePath;
                MarkUnsavedChanges();
                ClearApplicationInput();
                RefreshProfileList(process);
                MessageBox.Show(this, $"Updated the executable reference for the {process} process profile. Gesture matching still uses the process name only.",
                    "Gesture profiles", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ClearApplicationInput();
            ClearProfileSearch();
            RefreshProfileList(process);
            MessageBox.Show(this, $"The {process} profile already exists and is now selected. Profiles are unique by process name.",
                "Gesture profiles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _applicationEnabled[process] = true;
        _shortcutProfiles[process] = new ShortcutOverride(null, null, null, null);
        _gestureProfiles[process] = GestureProfile.Inherited;
        if (executablePath is not null) _applicationPaths[process] = executablePath;
        MarkUnsavedChanges();
        ClearApplicationInput();
        ClearProfileSearch();
        RefreshProfileList(process);
    }

    private bool TryResolveApplicationInput(out string process, out string? executablePath)
    {
        process = string.Empty;
        executablePath = null;
        var input = ApplicationTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            MessageBox.Show(this, "Enter a process name or browse to an existing application executable.",
                "Application profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (_selectedExecutablePath is not null)
        {
            if (!File.Exists(_selectedExecutablePath))
            {
                MessageBox.Show(this, $"The selected executable is missing:\n{_selectedExecutablePath}\n\nBrowse to an existing .exe and try again.",
                    "Application profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateApplicationResolution();
                return false;
            }
            executablePath = Path.GetFullPath(_selectedExecutablePath);
            process = Path.GetFileNameWithoutExtension(input);
        }
        else if (input.Contains(Path.DirectorySeparatorChar) || input.Contains(Path.AltDirectorySeparatorChar))
        {
            if (!string.Equals(Path.GetExtension(input), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "An executable path must end in .exe. For a process name, enter the name without its folder path.",
                    "Application profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!File.Exists(input))
            {
                MessageBox.Show(this, $"The executable was not found:\n{input}\n\nBrowse to an existing .exe or enter only the process name.",
                    "Application profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            executablePath = Path.GetFullPath(input);
            process = Path.GetFileNameWithoutExtension(input);
        }
        else
        {
            process = Path.GetFileNameWithoutExtension(input);
        }

        if (string.IsNullOrWhiteSpace(process) || process.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, "Enter a valid Windows process name or choose an existing .exe file.",
                "Application profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            process = string.Empty;
            executablePath = null;
            return false;
        }
        if (process is "." or ".." || process.Length > 260)
        {
            MessageBox.Show(this, "The process name must be a valid Windows executable name.",
                "Application profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            process = string.Empty;
            executablePath = null;
            return false;
        }
        return true;
    }

    private bool HasApplicationProfile(string process) => _applicationEnabled.ContainsKey(process) ||
        _shortcutProfiles.ContainsKey(process) || _gestureProfiles.ContainsKey(process) || _applicationPaths.ContainsKey(process);

    private void ApplicationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingApplicationInput) return;
        if (_selectedExecutablePath is not null && !string.Equals(
                Path.GetFileNameWithoutExtension(_selectedExecutablePath), ApplicationTextBox.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            _selectedExecutablePath = null;
        UpdateApplicationResolution();
    }

    private void UpdateApplicationResolution()
    {
        var input = ApplicationTextBox.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            ApplicationResolutionText.Text = "Enter a target process name. Add app creates an inherited profile; Copy selected copies the current profile. Matching uses process name only.";
            ApplicationResolutionText.Foreground = Brushes.Gray;
        }
        else if (_selectedExecutablePath is not null)
        {
            var exists = File.Exists(_selectedExecutablePath);
            ApplicationResolutionText.Text = exists
                ? $"Process profile: {Path.GetFileNameWithoutExtension(_selectedExecutablePath)} · selected executable: {_selectedExecutablePath}. Matching uses process name only."
                : $"Selected executable is missing: {_selectedExecutablePath}. Browse to an existing .exe.";
            ApplicationResolutionText.Foreground = exists ? Brushes.Gray : Brushes.Firebrick;
        }
        else if (input.Contains(Path.DirectorySeparatorChar) || input.Contains(Path.AltDirectorySeparatorChar))
        {
            var exists = string.Equals(Path.GetExtension(input), ".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(input);
            ApplicationResolutionText.Text = exists
                ? $"Process profile: {Path.GetFileNameWithoutExtension(input)} · matching uses process name only."
                : "This path must point to an existing .exe. Use Browse to choose a valid application.";
            ApplicationResolutionText.Foreground = exists ? Brushes.Gray : Brushes.Firebrick;
        }
        else
        {
            ApplicationResolutionText.Text = $"Process profile: {Path.GetFileNameWithoutExtension(input)} · matching uses process name only.";
            ApplicationResolutionText.Foreground = Brushes.Gray;
        }
    }

    private void ClearApplicationInput()
    {
        _selectedExecutablePath = null;
        _updatingApplicationInput = true;
        ApplicationTextBox.Clear();
        _updatingApplicationInput = false;
        UpdateApplicationResolution();
    }

    private void ClearProfileSearch()
    {
        _refreshingProfiles = true;
        ProfileSearchBox.Clear();
        _refreshingProfiles = false;
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
        if (picker.ShowDialog(this) == true)
        {
            _selectedExecutablePath = Path.GetFullPath(picker.FileName);
            ApplicationTextBox.Text = Path.GetFileNameWithoutExtension(picker.FileName);
            UpdateApplicationResolution();
        }
    }

    private void CopyApplication_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess is not { } source) return;
        if (!TryResolveApplicationInput(out var target, out var executablePath)) return;
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Choose a different process name for the copied profile.",
                "Copy application profile", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (HasApplicationProfile(target))
        {
            ClearApplicationInput();
            ClearProfileSearch();
            RefreshProfileList(target);
            MessageBox.Show(this, $"The {target} profile already exists and is now selected. Copying never overwrites an existing profile.",
                "Copy application profile", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_applicationEnabled.TryGetValue(source, out var enabled)) _applicationEnabled[target] = enabled;
        _shortcutProfiles[target] = _shortcutProfiles.GetValueOrDefault(source) ?? new ShortcutOverride(null, null, null, null);
        var sourceGestures = _gestureProfiles.GetValueOrDefault(source) ?? GestureProfile.Inherited;
        _gestureProfiles[target] = sourceGestures with { Gestures = sourceGestures.Gestures.ToArray() };
        if (executablePath is not null) _applicationPaths[target] = executablePath;
        MarkUnsavedChanges();
        ClearApplicationInput();
        ClearProfileSearch();
        RefreshProfileList(target);
    }

    private void RemoveApplication_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess is not { } process) return;
        _applicationEnabled.Remove(process);
        _shortcutProfiles.Remove(process);
        _gestureProfiles.Remove(process);
        _applicationPaths.Remove(process);
        MarkUnsavedChanges();
        RefreshProfileList();
    }

    private void ResetToInherited_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess is not { } process) return;
        var result = MessageBox.Show(this,
            $"Clear {process}'s shortcut overrides, custom-shape overrides, and app-level disable setting so it follows the global settings? The application profile and its selected executable reference will remain.",
            "Reset application profile", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        _applicationEnabled.Remove(process);
        _shortcutProfiles[process] = new ShortcutOverride(null, null, null, null);
        _gestureProfiles[process] = GestureProfile.Inherited;
        MarkUnsavedChanges();
        RefreshProfileList(process);
    }

    private void ApplicationEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshingProfiles) return;
        if (SelectedProcess is not { } process) return;
        _applicationEnabled[process] = ApplicationEnabledCheckBox.IsChecked == true;
        MarkUnsavedChanges();
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
        CopyApplicationButton.IsEnabled = SelectedProcess is not null;
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
        MarkUnsavedChanges();
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
        if (_loadingSettings) return;
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

    private void AddStarterGestures_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProcess is not null)
        {
            MessageBox.Show(this, "Starter shapes are added to Global defaults and inherited by applications. Select Global defaults first.",
                "Starter custom shapes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var picker = new StarterGesturesWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.Result is not { } selected) return;
        if (selected.Any(item => _globalGestures.Any(existing => string.Equals(existing.Id, item.Id, StringComparison.OrdinalIgnoreCase))))
        {
            MessageBox.Show(this, "One or more selected starter shapes are already in Global defaults.",
                "Starter custom shapes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var combined = _globalGestures.Concat(selected).ToArray();
        if (HasConflicts(combined))
        {
            MessageBox.Show(this, "A selected starter shape is too similar to an active global custom shape. Remove or disable the conflicting shape, then try again.",
                "Starter custom shapes", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var conflictingProfile = _gestureProfiles.Keys.FirstOrDefault(process =>
            HasConflicts(GestureResolver.Resolve(process, combined, _gestureProfiles)));
        if (conflictingProfile is not null)
        {
            MessageBox.Show(this, $"A starter shape would conflict with an active custom shape in the {conflictingProfile} profile. Resolve that conflict before adding the starter shapes.",
                "Starter custom shapes", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _globalGestures.AddRange(selected);
        MarkUnsavedChanges();
        RefreshProfileEditor();
    }

    private void PracticeGesture_Click(object sender, RoutedEventArgs e)
    {
        var process = SelectedProcess;
        var gestures = process is null
            ? _globalGestures.ToArray()
            : GestureResolver.Resolve(process, _globalGestures, _gestureProfiles).ToArray();
        var shortcuts = process is null
            ? _globalShortcuts
            : ShortcutResolver.Resolve(process, _globalShortcuts, _shortcutProfiles);
        var enabled = process is null || _applicationEnabled.GetValueOrDefault(process, true);
        var movementValid = ValidateSensitivity(MinimumHorizontalTextBox, MinimumHorizontalError, 20, 300, "Enter a value from 20 to 300 px.", out var minimumMovement);
        var driftValid = ValidateSensitivity(MaximumVerticalTextBox, MaximumVerticalError, 20, 300, "Enter a value from 20 to 300 px.", out var maximumDrift);
        var durationValid = ValidateSensitivity(MaximumDurationTextBox, MaximumDurationError, 250, 3000, "Enter a value from 250 to 3000 ms.", out var maximumDuration);
        var clickValid = ValidateSensitivity(ClickThresholdTextBox, ClickThresholdError, 2, 15, "Enter a value from 2 to 15 px.", out var clickThreshold);
        var valid = movementValid && driftValid && durationValid && clickValid;
        if (!valid)
        {
            MessageBox.Show(this, "Correct the highlighted sensitivity values before practicing so the preview uses your current settings.",
                "Try a gesture safely", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dialog = new GesturePracticeWindow(process ?? "Global defaults", gestures, shortcuts,
            GesturesCheckBox.IsChecked == true, enabled,
            minimumMovement, maximumDrift, maximumDuration, clickThreshold)
        { Owner = this };
        dialog.ShowDialog();
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this,
            "Restore the recommended defaults in this settings draft? This will enable gestures and the cursor trail, reset sensitivity to 70 px movement / 120 px drift / 1000 ms duration / 5 px click jitter, restore Alt+Left and Alt+Right shortcuts, disable Up and Down, remove all application profiles and custom shapes, and clear all per-app overrides. These changes take effect only when you click Save.",
            "Restore recommended defaults", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        _globalShortcuts = ShortcutPair.Defaults;
        _shortcutProfiles.Clear();
        _applicationEnabled.Clear();
        _applicationPaths.Clear();
        _globalGestures.Clear();
        _gestureProfiles.Clear();
        GesturesCheckBox.IsChecked = true;
        TrailCheckBox.IsChecked = true;
        ResultFeedbackCheckBox.IsChecked = false;
        ReducedEffectsCheckBox.IsChecked = false;
        MinimumHorizontalTextBox.Text = "70";
        MaximumVerticalTextBox.Text = "120";
        MaximumDurationTextBox.Text = "1000";
        ClickThresholdTextBox.Text = "5";
        SensitivityPresetBox.SelectedIndex = 1;
        foreach (var error in new[] { MinimumHorizontalError, MaximumVerticalError, MaximumDurationError, ClickThresholdError })
        {
            error.Text = string.Empty;
            error.Visibility = Visibility.Collapsed;
        }
        ProfileSearchBox.Clear();
        MarkUnsavedChanges();
        RefreshProfileList();
    }

    private void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCreateCurrentPackage(out var package)) return;
        var picker = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export all MidFlow settings",
            Filter = "MidFlow settings package (*.midflow.json)|*.midflow.json|JSON files (*.json)|*.json",
            DefaultExt = ".midflow.json",
            AddExtension = true,
            FileName = "MidFlow-settings.midflow.json"
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(picker.FileName, SettingsTransfer.Serialize(package));
            MessageBox.Show(this, $"All settings were exported to:\n{picker.FileName}", "Export settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowTransferError("The settings package could not be exported.", ex); }
    }

    private void ExportProfiles_Click(object sender, RoutedEventArgs e)
    {
        var names = ApplicationProfileNames().ToArray();
        if (names.Length == 0)
        {
            MessageBox.Show(this, "There are no application profiles to export.", "Export profiles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var selection = new ProfileSelectionWindow(names) { Owner = this };
        if (selection.ShowDialog() != true) return;
        var package = SettingsTransfer.CreateSelectedProfiles(selection.SelectedNames, GesturesCheckBox.IsChecked == true, _globalShortcuts,
            _globalGestures, _applicationEnabled, _shortcutProfiles, _gestureProfiles, _applicationPaths);
        if (!SettingsTransfer.Validate(package, out var validationError))
        {
            MessageBox.Show(this, validationError, "Export profiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var picker = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export selected application profiles",
            Filter = "MidFlow profile package (*.midflow.json)|*.midflow.json|JSON files (*.json)|*.json",
            DefaultExt = ".midflow.json",
            AddExtension = true,
            FileName = "MidFlow-profiles.midflow.json"
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(picker.FileName, SettingsTransfer.Serialize(package));
            MessageBox.Show(this, $"{selection.SelectedNames.Count} application profile(s) were exported to:\n{picker.FileName}",
                "Export profiles", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowTransferError("The profile package could not be exported.", ex); }
    }

    private void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import MidFlow settings or profiles",
            Filter = "MidFlow and JSON packages (*.midflow.json;*.json)|*.midflow.json;*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var incoming = SettingsTransfer.Read(picker.FileName);
            if (!SettingsTransfer.Validate(incoming, out var error))
            {
                MessageBox.Show(this, error, "Import settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryCreateCurrentPackage(out var current)) return;
            var candidate = incoming.Scope == SettingsTransfer.AllScope
                ? incoming
                : SettingsTransfer.MergeProfiles(current, incoming);
            if (!SettingsTransfer.Validate(candidate, out error))
            {
                MessageBox.Show(this, $"The import would create invalid profile settings and was not applied.\n\n{error}",
                    "Import settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var preview = new TransferPreviewWindow("Preview settings import",
                incoming.Scope == SettingsTransfer.AllScope ? "Replace all current settings?" : "Add or replace selected app profiles?",
                BuildTransferPreview(picker.FileName, incoming, candidate), "Import to draft") { Owner = this };
            if (preview.ShowDialog() != true) return;
            var backup = SettingsTransfer.WriteAutomaticBackup(current, "BeforeImport");
            ApplyTransferPackage(candidate);
            MessageBox.Show(this, $"Import is ready in the Settings draft. Click Save to apply it.\n\nRecovery backup: {backup}",
                "Import settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowTransferError("The settings package could not be imported.", ex); }
    }

    private void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(AppSettings.BackupDirectory))
        {
            MessageBox.Show(this, "No MidFlow recovery backups have been created yet.", "Restore backup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore a MidFlow recovery backup",
            InitialDirectory = AppSettings.BackupDirectory,
            Filter = "MidFlow recovery backups (*.midflow.json)|*.midflow.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var package = SettingsTransfer.Read(picker.FileName);
            if (package.Scope != SettingsTransfer.AllScope)
            {
                MessageBox.Show(this, "A recovery backup must contain all settings.",
                    "Restore backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!SettingsTransfer.Validate(package, out var error))
            {
                MessageBox.Show(this, error, "Restore backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TryCreateCurrentPackage(out var current)) return;
            var preview = new TransferPreviewWindow("Preview backup restore", "Restore this complete settings snapshot?",
                BuildTransferPreview(picker.FileName, package, package), "Restore to draft") { Owner = this };
            if (preview.ShowDialog() != true) return;
            var beforeRestore = SettingsTransfer.WriteAutomaticBackup(current, "BeforeRestore");
            ApplyTransferPackage(package);
            MessageBox.Show(this, $"Backup restored to the Settings draft. Click Save to apply it.\n\nCurrent settings backup: {beforeRestore}",
                "Restore backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowTransferError("The recovery backup could not be restored.", ex); }
    }

    private bool TryCreateCurrentPackage(out SettingsTransferPackage package)
    {
        package = null!;
        if (!TryReadCurrentSensitivity(out var globals)) return false;
        package = SettingsTransfer.CreateAll(globals, _applicationEnabled, _shortcutProfiles, _gestureProfiles, _applicationPaths);
        if (SettingsTransfer.Validate(package, out var error)) return true;
        MessageBox.Show(this, $"Current settings must be valid before export or import.\n\n{error}",
            "Settings transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private bool TryReadCurrentSensitivity(out TransferGlobals globals)
    {
        var minimumValid = ValidateSensitivity(MinimumHorizontalTextBox, MinimumHorizontalError, 20, 300, "Enter a value from 20 to 300 px.", out var minimum);
        var driftValid = ValidateSensitivity(MaximumVerticalTextBox, MaximumVerticalError, 20, 300, "Enter a value from 20 to 300 px.", out var drift);
        var durationValid = ValidateSensitivity(MaximumDurationTextBox, MaximumDurationError, 250, 3000, "Enter a value from 250 to 3000 ms.", out var duration);
        var clickValid = ValidateSensitivity(ClickThresholdTextBox, ClickThresholdError, 2, 15, "Enter a value from 2 to 15 px.", out var click);
        globals = new TransferGlobals(GesturesCheckBox.IsChecked == true, TrailCheckBox.IsChecked == true,
            ResultFeedbackCheckBox.IsChecked == true, ReducedEffectsCheckBox.IsChecked == true,
            minimum, drift, duration, click, _globalShortcuts, _globalGestures.ToList());
        if (minimumValid && driftValid && durationValid && clickValid) return true;
        MessageBox.Show(this, "Correct the highlighted sensitivity values before exporting or importing settings.",
            "Settings transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private IEnumerable<string> ApplicationProfileNames() =>
        _applicationEnabled.Keys.Concat(_shortcutProfiles.Keys).Concat(_gestureProfiles.Keys).Concat(_applicationPaths.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

    private static string BuildTransferPreview(string filePath, SettingsTransferPackage incoming, SettingsTransferPackage effective)
    {
        var profileData = incoming.Profiles!;
        var profiles = SettingsTransfer.ProfileNames(profileData).ToArray();
        var lines = new List<string>
        {
            $"File: {filePath}",
            $"Package scope: {incoming.Scope}",
            $"Format version: {incoming.FormatVersion}",
            $"Exported: {incoming.ExportedUtc.ToLocalTime():g}",
            incoming.Scope == SettingsTransfer.AllScope
                ? "Change: replace global settings and all current application profiles."
                : "Change: add or replace the listed application profiles; keep global settings and other profiles.",
            string.Empty
        };
        if (incoming.Globals is { } globals)
        {
            lines.Add($"Gestures: {(globals.GesturesEnabled ? "enabled" : "disabled")}; trail: {(globals.ShowTrail ? "shown" : "hidden")}");
            lines.Add($"Result messages: {(globals.ShowResultFeedback ? "shown" : "hidden")}; reduced effects: {(globals.ReduceVisualEffects ? "on" : "off")}");
            lines.Add($"Sensitivity: movement {globals.MinimumHorizontalMovementPx} px, drift {globals.MaximumVerticalMovementPx} px, duration {globals.MaximumGestureDurationMs} ms, click jitter {globals.ClickMovementThresholdPx} px");
            lines.Add($"Global shortcuts: Back {DescribeShortcut(globals.GlobalShortcuts.Back)}, Forward {DescribeShortcut(globals.GlobalShortcuts.Forward)}, Up {DescribeShortcut(globals.GlobalShortcuts.UpOrDisabled)}, Down {DescribeShortcut(globals.GlobalShortcuts.DownOrDisabled)}");
            lines.Add($"Global custom shapes: {globals.GlobalGestures.Count}");
        }
        lines.Add($"Application profiles ({profiles.Length}):");
        foreach (var name in profiles)
        {
            var enabled = profileData.ApplicationEnabled!.TryGetValue(name, out var appEnabled) ? appEnabled : true;
            var shortcuts = profileData.ShortcutProfiles!.GetValueOrDefault(name);
            var gestures = profileData.GestureProfiles!.GetValueOrDefault(name);
            lines.Add($"  • {name} — {(enabled ? "enabled" : "disabled")}; {gestures?.Gestures.Count ?? 0} custom shape(s)");
            if (shortcuts is not null)
                lines.Add($"      shortcuts: Back {DescribeOverride(shortcuts.Back)}, Forward {DescribeOverride(shortcuts.Forward)}, Up {DescribeOverride(shortcuts.Up)}, Down {DescribeOverride(shortcuts.Down)}");
            if (profileData.ApplicationPaths!.TryGetValue(name, out var executable))
                lines.Add($"      executable reference: {(File.Exists(executable) ? executable : $"MISSING — {executable}")}");
        }
        var gestureActions = new List<string>();
        if (incoming.Globals is { } actionGlobals)
            gestureActions.AddRange(actionGlobals.GlobalGestures.Select(x => FormatGestureAction("Global", x)));
        foreach (var item in profileData.GestureProfiles!)
            gestureActions.AddRange(item.Value.Gestures.Select(x => FormatGestureAction(item.Key, x)));
        if (gestureActions.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"Custom gesture actions ({gestureActions.Count}):");
            lines.AddRange(gestureActions.Take(200).Select(x => $"  → {x}"));
            if (gestureActions.Count > 200) lines.Add($"  … and {gestureActions.Count - 200} more");
        }
        var missing = SettingsTransfer.MissingExecutableReferences(incoming);
        if (missing.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"Missing executable references ({missing.Count}) — these entries remain importable but need repair:");
            lines.AddRange(missing.Take(30).Select(x => $"  ! {x}"));
            if (missing.Count > 30) lines.Add($"  … and {missing.Count - 30} more");
        }
        if (incoming.Scope == SettingsTransfer.ProfilesScope)
            lines.Add("Imported profiles carry resolved shortcut and shape mappings plus their effective enabled state. The destination’s global enable switch and sensitivity thresholds remain in effect.");
        lines.Add($"Profiles after import: {SettingsTransfer.ProfileNames(effective.Profiles!).Count}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeShortcut(KeyboardShortcut shortcut) =>
        Display(shortcut);

    private static string DescribeOverride(KeyboardShortcut? shortcut) => shortcut is null ? "Inherits global" : Display(shortcut);

    private static string FormatGestureAction(string profile, GestureDefinition gesture)
    {
        var action = gesture.Action.Kind == GestureActionKind.LaunchApplication
            ? $"Launch {gesture.Action.ApplicationPath}"
            : gesture.Action.Kind.ToString();
        return $"{profile}: {gesture.Name} → {action} ({(gesture.Enabled ? "enabled" : "disabled")})";
    }

    private void ApplyTransferPackage(SettingsTransferPackage package)
    {
        var globals = package.Globals!;
        var profiles = package.Profiles!;
        _globalShortcuts = globals.GlobalShortcuts;
        _globalGestures = globals.GlobalGestures.ToList();
        ReplaceDictionary(_applicationEnabled, profiles.ApplicationEnabled!);
        ReplaceDictionary(_shortcutProfiles, profiles.ShortcutProfiles!);
        ReplaceDictionary(_gestureProfiles, profiles.GestureProfiles!);
        ReplaceDictionary(_applicationPaths, profiles.ApplicationPaths!);
        GesturesCheckBox.IsChecked = globals.GesturesEnabled;
        TrailCheckBox.IsChecked = globals.ShowTrail;
        ResultFeedbackCheckBox.IsChecked = globals.ShowResultFeedback;
        ReducedEffectsCheckBox.IsChecked = globals.ReduceVisualEffects;
        MinimumHorizontalTextBox.Text = globals.MinimumHorizontalMovementPx.ToString();
        MaximumVerticalTextBox.Text = globals.MaximumVerticalMovementPx.ToString();
        MaximumDurationTextBox.Text = globals.MaximumGestureDurationMs.ToString();
        ClickThresholdTextBox.Text = globals.ClickMovementThresholdPx.ToString();
        ClearProfileSearch();
        MarkUnsavedChanges();
        RefreshProfileList(SettingsTransfer.ProfileNames(profiles).FirstOrDefault());
    }

    private static void ReplaceDictionary<TValue>(Dictionary<string, TValue> destination,
        IReadOnlyDictionary<string, TValue> source)
    {
        destination.Clear();
        foreach (var item in source) destination[item.Key] = item.Value;
    }

    private void ShowTransferError(string message, Exception exception) =>
        MessageBox.Show(this, $"{message}\n\n{exception.Message}", "Settings transfer", MessageBoxButton.OK, MessageBoxImage.Error);

    private void EditCustomGesture(string? process, GestureDefinition gesture)
    {
        var editor = new GestureEditorWindow(gesture) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } updated) SetGesture(process, updated);
    }

    private void SetGesture(string? process, GestureDefinition gesture)
    {
        MarkUnsavedChanges();
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
        MarkUnsavedChanges();
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
        var minimumValid = ValidateSensitivity(MinimumHorizontalTextBox, MinimumHorizontalError, 20, 300, "Enter a value from 20 to 300 px.", out var minimumHorizontal);
        var driftValid = ValidateSensitivity(MaximumVerticalTextBox, MaximumVerticalError, 20, 300, "Enter a value from 20 to 300 px.", out var maximumVertical);
        var durationValid = ValidateSensitivity(MaximumDurationTextBox, MaximumDurationError, 250, 3000, "Enter a value from 250 to 3000 ms.", out var maximumDuration);
        var clickValid = ValidateSensitivity(ClickThresholdTextBox, ClickThresholdError, 2, 15, "Enter a value from 2 to 15 px.", out var clickThreshold);
        if (!minimumValid || !driftValid || !durationValid || !clickValid)
        {
            (minimumValid ? (driftValid ? (durationValid ? ClickThresholdTextBox : MaximumDurationTextBox) : MaximumVerticalTextBox) : MinimumHorizontalTextBox).Focus();
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
        AppSettings.ShowResultFeedback = ResultFeedbackCheckBox.IsChecked == true;
        AppSettings.ReduceVisualEffects = ReducedEffectsCheckBox.IsChecked == true;
        AppSettings.SetProfiles(_applicationEnabled);
        try
        {
            AppSettings.SetShortcuts(_globalShortcuts, _shortcutProfiles);
            AppSettings.SetGestureMappings(_globalGestures, _gestureProfiles);
            AppSettings.SetApplicationPaths(_applicationPaths);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, $"Review the profile mappings before saving.\n\n{ex.Message}",
                "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        AppSettings.Save();
        SetUnsavedChanges(false);
    }

    private void SettingsChanged(object sender, RoutedEventArgs e)
    {
        if (!_loadingSettings) MarkUnsavedChanges();
    }

    private void Sensitivity_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loadingSettings) MarkUnsavedChanges();
        if (!_loadingSettings && !_updatingSensitivityPreset) UpdateSensitivityPreset();
    }

    private void SensitivityPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings || SensitivityPresetBox.SelectedIndex is < 0 or > 2) return;
        var values = SensitivityPresetBox.SelectedIndex switch
        {
            0 => (50, 150, 1400, 7),
            1 => (70, 120, 1000, 5),
            _ => (100, 80, 800, 3)
        };
        _updatingSensitivityPreset = true;
        MinimumHorizontalTextBox.Text = values.Item1.ToString();
        MaximumVerticalTextBox.Text = values.Item2.ToString();
        MaximumDurationTextBox.Text = values.Item3.ToString();
        ClickThresholdTextBox.Text = values.Item4.ToString();
        _updatingSensitivityPreset = false;
        MarkUnsavedChanges();
    }

    private void UpdateSensitivityPreset()
    {
        var preset = (MinimumHorizontalTextBox.Text, MaximumVerticalTextBox.Text, MaximumDurationTextBox.Text, ClickThresholdTextBox.Text) switch
        {
            ("50", "150", "1400", "7") => 0,
            ("70", "120", "1000", "5") => 1,
            ("100", "80", "800", "3") => 2,
            _ => 3
        };
        _updatingSensitivityPreset = true;
        SensitivityPresetBox.SelectedIndex = preset;
        _updatingSensitivityPreset = false;
    }

    private void Sensitivity_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, MinimumHorizontalTextBox))
            ValidateSensitivity(MinimumHorizontalTextBox, MinimumHorizontalError, 20, 300, "Enter a value from 20 to 300 px.", out _);
        else if (ReferenceEquals(sender, MaximumVerticalTextBox))
            ValidateSensitivity(MaximumVerticalTextBox, MaximumVerticalError, 20, 300, "Enter a value from 20 to 300 px.", out _);
        else if (ReferenceEquals(sender, MaximumDurationTextBox))
            ValidateSensitivity(MaximumDurationTextBox, MaximumDurationError, 250, 3000, "Enter a value from 250 to 3000 ms.", out _);
        else if (ReferenceEquals(sender, ClickThresholdTextBox))
            ValidateSensitivity(ClickThresholdTextBox, ClickThresholdError, 2, 15, "Enter a value from 2 to 15 px.", out _);
    }

    private static bool ValidateSensitivity(TextBox input, TextBlock error, int minimum, int maximum, string message, out int value)
    {
        var valid = int.TryParse(input.Text, out value) && value >= minimum && value <= maximum;
        input.BorderBrush = valid ? System.Windows.SystemColors.ControlDarkBrush : Brushes.Firebrick;
        input.BorderThickness = valid ? new Thickness(1) : new Thickness(2);
        input.ToolTip = valid ? null : message;
        error.Text = valid ? string.Empty : message;
        error.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        return valid;
    }

    private void SetUnsavedChanges(bool dirty)
    {
        _hasUnsavedChanges = dirty;
        UnsavedChangesText.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = dirty;
    }

    private void MarkUnsavedChanges() => SetUnsavedChanges(true);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void SettingsWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_hasUnsavedChanges) return;
        var result = MessageBox.Show(this, "Discard your unsaved settings changes?", "Unsaved changes",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) e.Cancel = true;
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppSettings.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open the diagnostic log folder.\n\n{ex.Message}",
                "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        GestureActionKind.LaunchApplication => File.Exists(action.ApplicationPath)
            ? $"Launch {Path.GetFileName(action.ApplicationPath)}"
            : $"Launch {Path.GetFileName(action.ApplicationPath)} (file missing)",
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
