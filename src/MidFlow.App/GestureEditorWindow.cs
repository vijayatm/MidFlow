using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MidFlow.Core.Gestures;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

namespace MidFlow.App;

internal sealed class GestureEditorWindow : Window
{
    private readonly TextBox _name = new() { MinWidth = 260, Margin = new Thickness(0, 4, 0, 10) };
    private readonly ComboBox _action = new() { Margin = new Thickness(0, 4, 0, 10) };
    private readonly TextBox _applicationPath = new() { Margin = new Thickness(0, 4, 6, 10), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _browse = new() { Content = "Browse…", Margin = new Thickness(0, 4, 0, 10) };
    private readonly Button _record = new() { Content = "Record gesture", Padding = new Thickness(10, 5, 10, 5), HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
    private readonly TextBlock _status = new() { Text = "Hold the middle mouse button, draw the gesture, then release.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 8) };
    private readonly Canvas _preview = new() { Height = 116, Background = new SolidColorBrush(Color.FromRgb(247, 247, 247)), ClipToBounds = true };
    private GesturePath? _path;
    private string _id = Guid.NewGuid().ToString("N");

    internal GestureDefinition? Result { get; private set; }

    internal GestureEditorWindow(GestureDefinition? existing = null)
    {
        Title = existing is null ? "Add gesture" : "Edit gesture";
        Width = 500;
        Height = 545;
        MinWidth = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        _action.Items.Add(new ActionItem("Back", GestureActionKind.Back));
        _action.Items.Add(new ActionItem("Forward", GestureActionKind.Forward));
        _action.Items.Add(new ActionItem("Launch application", GestureActionKind.LaunchApplication));
        _action.SelectionChanged += (_, _) => UpdateActionControls();
        _browse.Click += Browse_Click;
        _record.Click += Record_Click;
        _preview.SizeChanged += (_, _) => DrawPreview();
        AppSettings.GestureCaptured += GestureCaptured;
        Closed += (_, _) => AppSettings.GestureCaptured -= GestureCaptured;

        if (existing is not null)
        {
            _id = existing.Id;
            _name.Text = existing.Name;
            _path = existing.Path;
            _applicationPath.Text = existing.Action.ApplicationPath ?? "";
            _action.SelectedItem = _action.Items.Cast<ActionItem>().First(x => x.Kind == existing.Action.Kind);
            _status.Text = "Saved gesture loaded. Record again to replace its shape.";
            DrawPreview();
        }
        else _action.SelectedIndex = 0;

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = "Gesture name", FontWeight = FontWeights.SemiBold });
        Grid.SetRow(_name, 1); root.Children.Add(_name);
        var actionPanel = new StackPanel();
        actionPanel.Children.Add(new TextBlock { Text = "Action", FontWeight = FontWeights.SemiBold });
        actionPanel.Children.Add(_action);
        Grid.SetRow(actionPanel, 2); root.Children.Add(actionPanel);
        var appPanel = new DockPanel();
        appPanel.Children.Add(new TextBlock { Text = "Application file", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        DockPanel.SetDock(_browse, Dock.Right);
        appPanel.Children.Add(_browse);
        appPanel.Children.Add(_applicationPath);
        Grid.SetRow(appPanel, 3); root.Children.Add(appPanel);
        Grid.SetRow(_record, 4); root.Children.Add(_record);
        var previewPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        previewPanel.Children.Add(_status);
        previewPanel.Children.Add(_preview);
        Grid.SetRow(previewPanel, 5); root.Children.Add(previewPanel);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        footer.Children.Add(new Button { Content = "Cancel", Width = 88, Margin = new Thickness(0, 0, 8, 0), IsCancel = true });
        var save = new Button { Content = "Save gesture", Width = 100, IsDefault = true };
        save.Click += Save_Click;
        footer.Children.Add(save);
        Grid.SetRow(footer, 6); root.Children.Add(footer);
        Content = root;
        UpdateActionControls();
    }

    private void UpdateActionControls()
    {
        var show = _action.SelectedItem is ActionItem { Kind: GestureActionKind.LaunchApplication };
        _applicationPath.IsEnabled = show;
        _browse.IsEnabled = show;
        if (_applicationPath.Parent is DockPanel panel) panel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an application to launch", Filter = "Applications (*.exe)|*.exe",
            DefaultExt = ".exe", CheckFileExists = true, Multiselect = false, ValidateNames = true
        };
        if (picker.ShowDialog(this) == true) _applicationPath.Text = picker.FileName;
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_path is not null && MessageBox.Show(this,
                "Recording a new shape will replace the current gesture. Continue?", "Record gesture",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (!AppSettings.BeginGestureCapture())
        {
            MessageBox.Show(this, "A gesture recording is already in progress.", "Record gesture", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _path = null;
        DrawPreview();
        _status.Text = "Recording is armed. Hold the middle mouse button, draw a clear shape, then release.";
        _record.IsEnabled = false;
    }

    private void GestureCaptured(IReadOnlyList<GesturePoint> points)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _record.IsEnabled = true;
            _path = GesturePath.Create(points);
            _status.Text = _path is null
                ? "That movement was too short to save. Record again and draw a larger, distinct shape."
                : "Gesture captured. Review the preview, then save.";
            if (_path is { IsValid: true } &&
                Math.Abs(_path.Points[^1].X - _path.Points[0].X) >
                Math.Abs(_path.Points[^1].Y - _path.Points[0].Y) * 1.5)
                _status.Text += " Similar horizontal drags will use this custom action before Back/Forward.";
            DrawPreview();
        });
    }

    private void DrawPreview()
    {
        _preview.Children.Clear();
        if (_path is not { IsValid: true } || _preview.ActualWidth < 4) return;
        var points = new PointCollection();
        var span = Math.Max(4, Math.Min(_preview.ActualWidth, _preview.ActualHeight) - 20);
        foreach (var p in _path.Points)
            points.Add(new Point((_preview.ActualWidth / 2) + (p.X * span), (_preview.ActualHeight / 2) + (p.Y * span)));
        _preview.Children.Add(new Polyline { Points = points, Stroke = new SolidColorBrush(Color.FromRgb(44, 118, 210)), StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = _name.Text.Trim();
        var action = (_action.SelectedItem as ActionItem)?.Kind;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 48 || _path is null || action is null)
        {
            MessageBox.Show(this, "Enter a short name, record a gesture, and choose an action.", "Gesture", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        string? target = null;
        if (action == GestureActionKind.LaunchApplication)
        {
            target = _applicationPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(target) || !System.IO.Path.IsPathFullyQualified(target) || !System.IO.File.Exists(target))
            {
                MessageBox.Show(this, "Choose an existing application file to launch.", "Gesture", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        Result = new GestureDefinition(_id, name, true, _path, new GestureActionDefinition(action.Value, target));
        if (!Result.IsValid)
        {
            MessageBox.Show(this, "The gesture details are invalid. Review the name and application path.", "Gesture", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        AppSettings.CancelGestureCapture();
        base.OnClosed(e);
    }

    private sealed record ActionItem(string Label, GestureActionKind Kind)
    {
        public override string ToString() => Label;
    }
}
