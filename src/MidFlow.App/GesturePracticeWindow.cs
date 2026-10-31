using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MidFlow.Core.Gestures;
using MidFlow.Core.Shortcuts;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;

namespace MidFlow.App;

internal sealed class GesturePracticeWindow : Window
{
    private readonly Canvas _canvas = new() { Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)), ClipToBounds = true, MinHeight = 220 };
    private readonly TextBlock _result = new() { Text = "Draw in the area with the left mouse button to preview the resolved action.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), MinHeight = 42 };
    private readonly IReadOnlyList<GestureDefinition> _gestures;
    private readonly ShortcutPair _shortcuts;
    private readonly bool _globallyEnabled;
    private readonly bool _applicationEnabled;
    private readonly string _profileName;
    private readonly int _minimumMovement;
    private readonly int _maximumDrift;
    private readonly int _maximumDuration;
    private readonly int _clickThreshold;
    private GestureSession? _session;
    private readonly List<Point> _drawnPoints = [];
    private Polyline? _line;

    internal GesturePracticeWindow(string profileName, IReadOnlyList<GestureDefinition> gestures,
        ShortcutPair shortcuts, bool globallyEnabled, bool applicationEnabled,
        int minimumMovement, int maximumDrift, int maximumDuration, int clickThreshold)
    {
        _profileName = profileName;
        _gestures = gestures;
        _shortcuts = shortcuts;
        _globallyEnabled = globallyEnabled;
        _applicationEnabled = applicationEnabled;
        _minimumMovement = minimumMovement;
        _maximumDrift = maximumDrift;
        _maximumDuration = maximumDuration;
        _clickThreshold = clickThreshold;
        Title = "Try a gesture safely";
        Width = 650;
        Height = 500;
        MinWidth = 520;
        MinHeight = 410;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock
        {
            Text = $"Practice for {_profileName}. Draw with the left mouse button; release to see what would happen. MidFlow will not send a shortcut or launch an application.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
        _canvas.LostMouseCapture += (_, _) => EndDrawing(false);
        Grid.SetRow(_canvas, 1);
        root.Children.Add(_canvas);
        Grid.SetRow(_result, 2);
        root.Children.Add(_result);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var clear = new Button { Content = "Clear", Width = 84, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        clear.Click += (_, _) => ClearPractice();
        actions.Children.Add(clear);
        actions.Children.Add(new Button { Content = "Close", Width = 84, IsCancel = true, Padding = new Thickness(8, 4, 8, 4) });
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);
        Content = root;
        AppSettings.BeginGesturePractice();
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ClearDrawing();
        var point = e.GetPosition(_canvas);
        var pixels = ToPixels(point);
        _session = new GestureSession(pixels.X, pixels.Y, _clickThreshold, _minimumMovement, _maximumDrift, _maximumDuration);
        AddPoint(point);
        _canvas.CaptureMouse();
        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_session is null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(_canvas);
        var pixels = ToPixels(point);
        _session.Update(pixels.X, pixels.Y);
        AddPoint(point);
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_session is null) return;
        var pixels = ToPixels(e.GetPosition(_canvas));
        _session.Update(pixels.X, pixels.Y);
        AddPoint(e.GetPosition(_canvas));
        EndDrawing(true);
        _canvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void EndDrawing(bool evaluate)
    {
        if (_session is null) return;
        var session = _session;
        _session = null;
        if (evaluate) ShowResolution(session);
    }

    private void ShowResolution(GestureSession session)
    {
        var result = session.Evaluate();
        var path = GesturePath.Create(session.GetRecordedPoints());
        var custom = path is null ? null : GestureResolver.Match(path, _gestures);
        if (!_globallyEnabled || !_applicationEnabled)
        {
            _result.Text = !_globallyEnabled
                ? "Gestures are globally disabled. This stroke would not trigger an action."
                : $"Gestures are disabled for {_profileName}. This stroke would not trigger an action.";
            return;
        }
        if (custom is not null)
        {
            _result.Text = $"Would match custom shape “{custom.Name}” ({DescribeAction(custom.Action)}). No action was sent.";
            return;
        }
        if (result.Direction == GestureDirection.None)
        {
            _result.Text = path is null
                ? "The stroke was too short or small to compare with custom shapes. Try a larger, clearer path. No action would run."
                : $"No action would run ({Reason(result.Reason)}). Try a clearer stroke or review sensitivity.";
            return;
        }
        var shortcut = ShortcutFor(result.Direction);
        var name = result.Direction switch
        {
            GestureDirection.Left => "Left / Back",
            GestureDirection.Right => "Right / Forward",
            GestureDirection.Up => "Up",
            GestureDirection.Down => "Down",
            _ => result.Direction.ToString()
        };
        _result.Text = shortcut.Enabled
            ? $"Would resolve to {name} and send {Display(shortcut)}. No shortcut was sent."
            : $"The {name} direction was recognized, but its shortcut is disabled. No action would run.";
    }

    private string DescribeAction(GestureActionDefinition action) => action.Kind switch
    {
        GestureActionKind.Back => ShortcutDescription("Back", _shortcuts.Back),
        GestureActionKind.Forward => ShortcutDescription("Forward", _shortcuts.Forward),
        GestureActionKind.Up => ShortcutDescription("Up", _shortcuts.UpOrDisabled),
        GestureActionKind.Down => ShortcutDescription("Down", _shortcuts.DownOrDisabled),
        GestureActionKind.LaunchApplication => $"would launch {System.IO.Path.GetFileName(action.ApplicationPath)}",
        _ => "unsupported action"
    };

    private static string ShortcutDescription(string label, KeyboardShortcut shortcut) =>
        shortcut.Enabled ? $"{label}, {Display(shortcut)}" : $"{label}, shortcut disabled";

    private KeyboardShortcut ShortcutFor(GestureDirection direction) => direction switch
    {
        GestureDirection.Left => _shortcuts.Back,
        GestureDirection.Right => _shortcuts.Forward,
        GestureDirection.Up => _shortcuts.UpOrDisabled,
        GestureDirection.Down => _shortcuts.DownOrDisabled,
        _ => KeyboardShortcut.Disabled
    };

    private static string Display(KeyboardShortcut shortcut)
    {
        var pieces = new List<string>();
        if ((shortcut.Modifiers & ShortcutModifiers.Ctrl) != 0) pieces.Add("Ctrl");
        if ((shortcut.Modifiers & ShortcutModifiers.Alt) != 0) pieces.Add("Alt");
        if ((shortcut.Modifiers & ShortcutModifiers.Shift) != 0) pieces.Add("Shift");
        if ((shortcut.Modifiers & ShortcutModifiers.Win) != 0) pieces.Add("Win");
        pieces.Add(KeyInterop.KeyFromVirtualKey(shortcut.Key).ToString());
        return string.Join("+", pieces);
    }

    private static string Reason(string value) => value switch
    {
        "TooSlow" => "the stroke took longer than the configured maximum",
        "HorizontalDistanceTooShort" => "the stroke did not travel far enough",
        "CrossAxisDriftTooLarge" => "the stroke drifted too far across the other axis",
        "NotAxisDominant" => "the stroke was diagonal or ambiguous",
        _ => value
    };

    private (int X, int Y) ToPixels(Point point)
    {
        var dpi = VisualTreeHelper.GetDpi(_canvas);
        return ((int)Math.Round(point.X * dpi.DpiScaleX), (int)Math.Round(point.Y * dpi.DpiScaleY));
    }

    private void AddPoint(Point point)
    {
        if (_drawnPoints.Count >= 128) return;
        _drawnPoints.Add(point);
        if (_drawnPoints.Count > 1)
        {
            _line ??= new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(44, 118, 210)), StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round };
            if (_line.Points.Count == 0) _line.Points.Add(_drawnPoints[0]);
            _line.Points.Add(point);
            if (!_canvas.Children.Contains(_line)) _canvas.Children.Add(_line);
        }
    }

    private void ClearDrawing()
    {
        _line = null;
        _drawnPoints.Clear();
        _canvas.Children.Clear();
    }

    private void ClearPractice()
    {
        EndDrawing(false);
        ClearDrawing();
        _result.Text = "Draw in the area with the left mouse button to preview the resolved action.";
    }

    protected override void OnClosed(EventArgs e)
    {
        AppSettings.EndGesturePractice();
        base.OnClosed(e);
    }
}
