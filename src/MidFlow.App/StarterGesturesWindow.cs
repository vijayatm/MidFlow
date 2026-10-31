using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MidFlow.Core.Gestures;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace MidFlow.App;

internal sealed class StarterGesturesWindow : Window
{
    private readonly CheckBox _loop = new() { Content = "Loop", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _zigzag = new() { Content = "Zigzag", Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };

    internal IReadOnlyList<GestureDefinition>? Result { get; private set; }

    internal StarterGesturesWindow()
    {
        Title = "Starter custom shapes";
        Width = 540;
        Height = 440;
        MinWidth = 500;
        MinHeight = 400;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock
        {
            Text = "Choose optional starter shapes. They’ll be added to Global defaults and inherited by apps.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var cards = new StackPanel();
        cards.Children.Add(Card(_loop, "Loop", "Back", CreateLoop()));
        cards.Children.Add(Card(_zigzag, "Zigzag", "Forward", CreateZigzag()));
        Grid.SetRow(cards, 1);
        root.Children.Add(cards);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(new Button { Content = "Cancel", Width = 86, Margin = new Thickness(0, 0, 8, 0), IsCancel = true, Padding = new Thickness(8, 5, 8, 5) });
        var add = new Button { Content = "Add selected", Width = 110, IsDefault = true, Padding = new Thickness(8, 5, 8, 5) };
        add.Click += (_, _) =>
        {
            var chosen = new List<GestureDefinition>();
            if (_loop.IsChecked == true) chosen.Add(CreateLoop());
            if (_zigzag.IsChecked == true) chosen.Add(CreateZigzag());
            if (chosen.Count == 0)
            {
                MessageBox.Show(this, "Select at least one starter shape.", "Starter custom shapes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Result = chosen;
            DialogResult = true;
            Close();
        };
        footer.Children.Add(add);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;
    }

    private static Border Card(CheckBox selection, string name, string action, GestureDefinition gesture)
    {
        var row = new Grid { Margin = new Thickness(8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        row.Children.Add(selection);
        var preview = Preview(gesture.Path);
        Grid.SetColumn(preview, 1);
        row.Children.Add(preview);
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold });
        info.Children.Add(new TextBlock { Text = $"Maps to {action}", Foreground = Brushes.DimGray });
        Grid.SetColumn(info, 2);
        row.Children.Add(info);
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = row
        };
    }

    private static Canvas Preview(GesturePath path)
    {
        var canvas = new Canvas { Width = 180, Height = 110, Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)), ClipToBounds = true };
        var points = new PointCollection();
        foreach (var point in path.Points) points.Add(new Point(90 + (point.X * 42), 55 + (point.Y * 42)));
        canvas.Children.Add(new Polyline { Points = points, Stroke = new SolidColorBrush(Color.FromRgb(44, 118, 210)), StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round });
        return canvas;
    }

    private static GestureDefinition CreateLoop() =>
        new("starter.loop.back", "Loop", true, MakePath(Enumerable.Range(0, 49).Select(i =>
        {
            var angle = 2 * Math.PI * i / 48;
            return new GesturePoint(Math.Cos(angle) * 100, Math.Sin(angle) * 100);
        })), new GestureActionDefinition(GestureActionKind.Back));

    private static GestureDefinition CreateZigzag() =>
        new("starter.zigzag.forward", "Zigzag", true, MakePath(Interpolate(
            new GesturePoint(-100, -60), new GesturePoint(100, -60), new GesturePoint(-100, 0),
            new GesturePoint(100, 0), new GesturePoint(-100, 60), new GesturePoint(100, 60))),
            new GestureActionDefinition(GestureActionKind.Forward));

    private static GesturePath MakePath(IEnumerable<GesturePoint> points) =>
        GesturePath.Create(points.ToArray()) ?? throw new InvalidOperationException("Starter gesture path is invalid.");

    private static IEnumerable<GesturePoint> Interpolate(params GesturePoint[] anchors)
    {
        for (var i = 1; i < anchors.Length; i++)
        {
            var start = anchors[i - 1];
            var end = anchors[i];
            for (var step = 0; step < 12; step++)
            {
                var t = step / 12d;
                yield return new GesturePoint(start.X + ((end.X - start.X) * t), start.Y + ((end.Y - start.Y) * t));
            }
        }
        yield return anchors[^1];
    }
}
