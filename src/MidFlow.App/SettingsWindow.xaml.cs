using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MidFlow.Core.Shortcuts;
using MessageBox = System.Windows.MessageBox;

namespace MidFlow.App;

public partial class SettingsWindow : Window
{
    private KeyboardShortcut _globalBack = KeyboardShortcut.DefaultBack;
    private KeyboardShortcut _globalForward = KeyboardShortcut.DefaultForward;
    private readonly Dictionary<string, ShortcutOverride> _shortcutProfiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _applicationEnabled = new(StringComparer.OrdinalIgnoreCase);
    private System.Windows.Controls.Button? _captureButton;
    private string? _captureProcess;
    private bool _captureBack;

    public SettingsWindow()
    {
        InitializeComponent();
        GesturesCheckBox.IsChecked = AppSettings.GesturesEnabled;
        TrailCheckBox.IsChecked = AppSettings.ShowTrail;
        MinimumHorizontalTextBox.Text = AppSettings.MinimumHorizontalMovementPx.ToString();
        MaximumVerticalTextBox.Text = AppSettings.MaximumVerticalMovementPx.ToString();
        MaximumDurationTextBox.Text = AppSettings.MaximumGestureDurationMs.ToString();
        ClickThresholdTextBox.Text = AppSettings.ClickMovementThresholdPx.ToString();
        _applicationEnabled.Clear();
        foreach (var item in AppSettings.Profiles) _applicationEnabled[item.Key] = item.Value;
        var defaults = AppSettings.GlobalShortcuts;
        _globalBack = defaults.Back;
        _globalForward = defaults.Forward;
        _shortcutProfiles.Clear();
        foreach (var item in AppSettings.ShortcutProfiles) _shortcutProfiles[item.Key] = item.Value;
        foreach (var process in _shortcutProfiles.Keys)
            _applicationEnabled.TryAdd(process, true);
        GlobalBackText.Content = Display(_globalBack);
        GlobalForwardText.Content = Display(_globalForward);
        RefreshOverrides();
    }

    private void ShortcutCapture_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            var target = (System.Windows.Controls.Button)sender;
            target.Content = "Disabled";
            switch (target.Tag?.ToString())
            {
                case "GlobalBack": _globalBack = KeyboardShortcut.Disabled; break;
                case "GlobalForward": _globalForward = KeyboardShortcut.Disabled; break;
            }
            e.Handled = true;
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var modifiers = ShortcutModifiers.None;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) modifiers |= ShortcutModifiers.Ctrl;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) modifiers |= ShortcutModifiers.Alt;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) modifiers |= ShortcutModifiers.Shift;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) modifiers |= ShortcutModifiers.Win;
        var shortcut = new KeyboardShortcut(true, checked((ushort)KeyInterop.VirtualKeyFromKey(key)), modifiers);
        var button = (System.Windows.Controls.Button)sender;
        button.Content = Display(shortcut);
        switch (button.Tag?.ToString())
        {
            case "GlobalBack": _globalBack = shortcut; break;
            case "GlobalForward": _globalForward = shortcut; break;
        }
    }

    private void BeginGlobalShortcutCapture_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        button.Content = "Press shortcut…";
        button.Focus();
    }

    private void DisableShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag?.ToString() == "GlobalBack") { _globalBack = KeyboardShortcut.Disabled; GlobalBackText.Content = "Disabled"; }
        else { _globalForward = KeyboardShortcut.Disabled; GlobalForwardText.Content = "Disabled"; }
    }

    private void AddApplication_Click(object sender, RoutedEventArgs e)
    {
        var process = ShortcutProcessText.Text.Trim();
        if (string.IsNullOrWhiteSpace(process))
        {
            MessageBox.Show("Enter an application process name first.", "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        process = Path.GetFileNameWithoutExtension(process);
        if (string.IsNullOrWhiteSpace(process))
        {
            MessageBox.Show("Choose or enter a valid application name.", "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_applicationEnabled.ContainsKey(process) || _shortcutProfiles.ContainsKey(process))
        {
            MessageBox.Show($"{process} already has an application shortcut row. Edit its Back or Forward shortcut below.",
                "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _shortcutProfiles[process] = new ShortcutOverride(null, null);
        _applicationEnabled[process] = true;
        ShortcutProcessText.Clear();
        RefreshOverrides();
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
            ShortcutProcessText.Text = Path.GetFileNameWithoutExtension(picker.FileName);
    }

    private void RefreshOverrides()
    {
        ShortcutRowsPanel.Children.Clear();
        var processNames = _applicationEnabled.Keys.Concat(_shortcutProfiles.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        var hasRows = false;
        foreach (var process in processNames)
        {
            hasRows = true;
            var row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(75) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(36) });
            var enabled = _applicationEnabled.GetValueOrDefault(process, true);
            var mapping = _shortcutProfiles.GetValueOrDefault(process) ?? new ShortcutOverride(null, null);
            row.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = process, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0),
                Opacity = enabled ? 1 : 0.48
            });
            AddShortcutButton(row, process, isBack: true, mapping.Back, 1, enabled);
            AddShortcutButton(row, process, isBack: false, mapping.Forward, 2, enabled);
            var toggle = new System.Windows.Controls.CheckBox
            {
                IsChecked = enabled, Tag = process, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, ToolTip = "Enable gestures for this app"
            };
            toggle.Checked += ApplicationEnabled_Changed;
            toggle.Unchecked += ApplicationEnabled_Changed;
            System.Windows.Controls.Grid.SetColumn(toggle, 3);
            row.Children.Add(toggle);
            var remove = new System.Windows.Controls.Button
            {
                Content = "×", Tag = process, Padding = new Thickness(0), ToolTip = "Remove application settings"
            };
            remove.Click += RemoveApplication_Click;
            System.Windows.Controls.Grid.SetColumn(remove, 4);
            row.Children.Add(remove);
            var separator = new System.Windows.Controls.Border
            {
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 225, 225)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 1, 0, 1),
                Child = row
            };
            ShortcutRowsPanel.Children.Add(separator);
        }
        if (!hasRows)
            ShortcutRowsPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "No application overrides yet. Add an app to customize its shortcuts or gesture access.",
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(8, 14, 8, 14),
                TextWrapping = TextWrapping.Wrap
            });
    }

    private void AddShortcutButton(System.Windows.Controls.Grid row, string process, bool isBack,
        KeyboardShortcut? shortcut, int column, bool applicationEnabled)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = shortcut is null ? "Add Hotkey" : Display(shortcut),
            Tag = (process, isBack),
            Margin = new Thickness(3, 1, 3, 1),
            ToolTip = "Click to record or update. Escape disables; Backspace inherits the global shortcut.",
            IsEnabled = applicationEnabled,
            Opacity = applicationEnabled ? 1 : 0.48
        };
        button.Click += BeginAppShortcutCapture_Click;
        button.PreviewKeyDown += AppShortcutCapture_KeyDown;
        System.Windows.Controls.Grid.SetColumn(button, column);
        row.Children.Add(button);
    }

    private void BeginAppShortcutCapture_Click(object sender, RoutedEventArgs e)
    {
        _captureButton = (System.Windows.Controls.Button)sender;
        var target = ((string Process, bool IsBack))_captureButton.Tag;
        _captureProcess = target.Process;
        _captureBack = target.IsBack;
        _captureButton.Content = "Press shortcut…";
        _captureButton.Focus();
    }

    private void AppShortcutCapture_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_captureButton is null || _captureProcess is null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        KeyboardShortcut? shortcut;
        if (key == Key.Escape) shortcut = KeyboardShortcut.Disabled;
        else if (key == Key.Back) shortcut = null;
        else
        {
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
                Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                e.Handled = true;
                return;
            }
            var modifiers = ShortcutModifiers.None;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) modifiers |= ShortcutModifiers.Ctrl;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) modifiers |= ShortcutModifiers.Alt;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) modifiers |= ShortcutModifiers.Shift;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) modifiers |= ShortcutModifiers.Win;
            shortcut = new KeyboardShortcut(true, checked((ushort)KeyInterop.VirtualKeyFromKey(key)), modifiers);
        }

        var existing = _shortcutProfiles.GetValueOrDefault(_captureProcess) ?? new ShortcutOverride(null, null);
        _shortcutProfiles[_captureProcess] = _captureBack
            ? existing with { Back = shortcut }
            : existing with { Forward = shortcut };
        e.Handled = true;
        _captureButton = null;
        _captureProcess = null;
        RefreshOverrides();
    }

    private void RemoveApplication_Click(object sender, RoutedEventArgs e)
    {
        var process = (string)((System.Windows.Controls.Button)sender).Tag;
        if (string.Equals(_captureProcess, process, StringComparison.OrdinalIgnoreCase))
        {
            _captureButton = null;
            _captureProcess = null;
        }
        _shortcutProfiles.Remove(process);
        _applicationEnabled.Remove(process);
        RefreshOverrides();
    }

    private void ApplicationEnabled_Changed(object sender, RoutedEventArgs e)
    {
        var toggle = (System.Windows.Controls.CheckBox)sender;
        var process = (string)toggle.Tag;
        _applicationEnabled[process] = toggle.IsChecked == true;
        if (toggle.IsChecked != true && string.Equals(_captureProcess, process, StringComparison.OrdinalIgnoreCase))
        {
            _captureButton = null;
            _captureProcess = null;
        }
        RefreshOverrides();
    }

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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MinimumHorizontalTextBox.Text, out var minimumHorizontal) ||
            minimumHorizontal is < 20 or > 300 ||
            !int.TryParse(MaximumVerticalTextBox.Text, out var maximumVertical) ||
            maximumVertical is < 20 or > 300 ||
            !int.TryParse(MaximumDurationTextBox.Text, out var maximumDuration) ||
            maximumDuration is < 250 or > 3000 ||
            !int.TryParse(ClickThresholdTextBox.Text, out var clickThreshold) ||
            clickThreshold is < 2 or > 15)
        {
            MessageBox.Show("Gesture values are outside the allowed ranges.\nHorizontal: 20–300 px; vertical: 20–300 px; duration: 250–3000 ms; click jitter: 2–15 px.",
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
        AppSettings.SetShortcuts(new ShortcutPair(_globalBack, _globalForward), _shortcutProfiles);
        AppSettings.Save();
        Close();
    }
}
