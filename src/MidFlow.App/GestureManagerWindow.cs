using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MidFlow.Core.Gestures;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

namespace MidFlow.App;

internal sealed class GestureManagerWindow : Window
{
    private readonly List<GestureDefinition> _global;
    private readonly Dictionary<string, GestureProfile> _profiles;
    private readonly ComboBox _scope = new();
    private readonly StackPanel _rows = new();
    private readonly TextBlock _hint = new();
    private readonly Button _add = new() { Content = "Add gesture", Padding = new Thickness(12, 5, 12, 5) };
    private readonly Button _customize = new() { Content = "Customize for this app", Padding = new Thickness(12, 5, 12, 5) };

    internal IReadOnlyList<GestureDefinition> GlobalGestures => _global.ToArray();
    internal IReadOnlyDictionary<string, GestureProfile> GestureProfiles =>
        new Dictionary<string, GestureProfile>(_profiles, StringComparer.OrdinalIgnoreCase);

    internal GestureManagerWindow(IEnumerable<GestureDefinition> global,
        IReadOnlyDictionary<string, GestureProfile> profiles, IEnumerable<string> applications)
    {
        Title = "Custom gestures";
        Width = 720;
        Height = 560;
        MinWidth = 600;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        _global = global.ToList();
        _profiles = new Dictionary<string, GestureProfile>(profiles, StringComparer.OrdinalIgnoreCase);

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = "Custom gestures", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });

        var scopeRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        scopeRow.Children.Add(new TextBlock { Text = "Applies to", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        _scope.MinWidth = 230;
        _scope.SelectionChanged += (_, _) => RefreshRows();
        _scope.Items.Add(new ScopeItem("All applications", null));
        foreach (var app in applications.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            _scope.Items.Add(new ScopeItem(app, app));
        _scope.SelectedIndex = 0;
        scopeRow.Children.Add(_scope);
        Grid.SetRow(scopeRow, 1);
        root.Children.Add(scopeRow);

        var content = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        _add.Click += Add_Click;
        _customize.Click += Customize_Click;
        actions.Children.Add(_add);
        actions.Children.Add(_customize);
        DockPanel.SetDock(actions, Dock.Top);
        content.Children.Add(actions);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _rows };
        content.Children.Add(scroll);
        Grid.SetRow(content, 2);
        root.Children.Add(content);

        _hint.TextWrapping = TextWrapping.Wrap;
        _hint.Foreground = Brushes.Gray;
        _hint.Margin = new Thickness(0, 10, 0, 10);
        Grid.SetRow(_hint, 3);
        root.Children.Add(_hint);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
        var save = new Button { Content = "Done", Width = 90, IsDefault = true };
        save.Click += (_, _) =>
        {
            if (HasConflict(_global) || _profiles.Values.Where(p => !p.InheritGlobal).Any(p => HasConflict(p.Gestures)))
            {
                MessageBox.Show(this, "Two gestures in the same scope are too similar. Edit or remove one before saving.",
                    "Similar gestures", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        };
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);
        Content = root;
        RefreshRows();
    }

    private void RefreshRows()
    {
        if (_rows is null || _scope.SelectedItem is not ScopeItem scope) return;
        _rows.Children.Clear();
        var gestures = GetList(scope.Key);
        var editable = scope.Key is null || !_profiles.TryGetValue(scope.Key, out var profile) || !profile.InheritGlobal;
        _add.IsEnabled = editable;
        _customize.Visibility = scope.Key is null || editable ? Visibility.Collapsed : Visibility.Visible;
        _hint.Text = scope.Key is null
            ? "Global gestures apply by default. An application can inherit these mappings or use its own set. Click Save in Settings to apply changes."
            : editable
                ? "This application has its own gesture set. Uncheck a mapping to disable it for this app. Click Save in Settings to apply changes."
                : "This application currently inherits global gestures. Customize it to make app-specific changes. Click Save in Settings to apply changes.";

        if (gestures.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = editable ? "No custom gestures yet. Add one to get started." : "No global gestures are configured.", Foreground = Brushes.Gray, Margin = new Thickness(8, 18, 8, 18) });
            return;
        }
        foreach (var gesture in gestures.ToArray())
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var enabled = new CheckBox { IsChecked = gesture.Enabled, VerticalAlignment = VerticalAlignment.Center, IsEnabled = editable, Tag = gesture.Id };
            enabled.Checked += (_, _) => SetEnabled(gesture.Id, true);
            enabled.Unchecked += (_, _) => SetEnabled(gesture.Id, false);
            row.Children.Add(enabled);
            var name = new TextBlock { Text = gesture.Name, VerticalAlignment = VerticalAlignment.Center, Opacity = gesture.Enabled ? 1 : 0.48 };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            var action = new TextBlock { Text = ActionText(gesture.Action), Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Opacity = gesture.Enabled ? 1 : 0.48 };
            Grid.SetColumn(action, 2);
            row.Children.Add(action);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var edit = new Button { Content = "Edit", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(4, 0, 0, 0), IsEnabled = editable };
            edit.Click += (_, _) => EditGesture(scope.Key, gesture);
            var remove = new Button { Content = "Remove", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(4, 0, 0, 0), IsEnabled = editable };
            remove.Click += (_, _) => { gestures.RemoveAll(x => x.Id == gesture.Id); SetScopeList(scope.Key, gestures); RefreshRows(); };
            buttons.Children.Add(edit);
            buttons.Children.Add(remove);
            Grid.SetColumn(buttons, 3);
            row.Children.Add(buttons);
            _rows.Children.Add(new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(225, 225, 225)), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(3), Child = row });
        }
    }

    private List<GestureDefinition> GetList(string? scope)
    {
        if (scope is null) return _global;
        if (!_profiles.TryGetValue(scope, out var profile))
        {
            profile = GestureProfile.Inherited;
            _profiles[scope] = profile;
        }
        return profile.InheritGlobal ? _global : profile.Gestures.ToList();
    }

    private void Customize_Click(object sender, RoutedEventArgs e)
    {
        if (_scope.SelectedItem is not ScopeItem { Key: not null } scope) return;
        _profiles[scope.Key] = new GestureProfile(false, _global.ToArray());
        RefreshRows();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_scope.SelectedItem is not ScopeItem scope) return;
        var list = GetList(scope.Key);
        var editor = new GestureEditorWindow { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } result)
        {
            list.Add(result);
            SetScopeList(scope.Key, list);
            RefreshRows();
        }
    }

    private void EditGesture(string? scope, GestureDefinition gesture)
    {
        var editor = new GestureEditorWindow(gesture) { Owner = this };
        if (editor.ShowDialog() == true && editor.Result is { } result)
        {
            var list = GetList(scope);
            var index = list.FindIndex(x => x.Id == gesture.Id);
            if (index >= 0) list[index] = result;
            SetScopeList(scope, list);
            RefreshRows();
        }
    }

    private void SetEnabled(string id, bool enabled)
    {
        if (_scope.SelectedItem is not ScopeItem scope) return;
        var list = GetList(scope.Key);
        var index = list.FindIndex(x => x.Id == id);
        if (index >= 0) list[index] = list[index] with { Enabled = enabled };
        SetScopeList(scope.Key, list);
        RefreshRows();
    }

    private void SetScopeList(string? scope, List<GestureDefinition> list)
    {
        if (scope is null) return;
        _profiles[scope] = new GestureProfile(false, list.ToArray());
    }

    private static bool HasConflict(IReadOnlyList<GestureDefinition> gestures)
    {
        for (var i = 0; i < gestures.Count; i++)
            for (var j = i + 1; j < gestures.Count; j++)
                if (gestures[i].Enabled && gestures[j].Enabled &&
                    gestures[i].Path.DistanceTo(gestures[j].Path) < 0.255) return true;
        return false;
    }

    private static string ActionText(GestureActionDefinition action) => action.Kind switch
    {
        GestureActionKind.Back => "Back",
        GestureActionKind.Forward => "Forward",
        GestureActionKind.LaunchApplication => $"Launch {System.IO.Path.GetFileName(action.ApplicationPath)}",
        _ => "Unknown action"
    };

    private sealed record ScopeItem(string Label, string? Key)
    {
        public override string ToString() => Label;
    }
}
