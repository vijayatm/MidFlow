using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;

namespace MidFlow.App;

internal sealed class FirstRunWindow : Window
{
    internal FirstRunWindow()
    {
        Title = "Welcome to MidFlow";
        Width = 520;
        Height = 440;
        MinWidth = 480;
        MinHeight = 400;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Closing += (_, _) => CompleteWelcome();

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock
        {
            Text = "Welcome to MidFlow",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        var intro = new TextBlock
        {
            Text = "Use the middle mouse button to trigger a gesture. Here’s the basic flow:",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray
        };
        Grid.SetRow(intro, 1);
        root.Children.Add(intro);

        var steps = new StackPanel { Margin = new Thickness(0, 18, 0, 16) };
        steps.Children.Add(Step("1", "Hold the middle button", "Start on the page or app you want to control."));
        steps.Children.Add(Step("2", "Drag in a direction or draw a shape", "MidFlow reads the movement while the button is held."));
        steps.Children.Add(Step("3", "Release to perform the mapped action", "A stationary middle click is replayed. Drag practice in Settings uses the left button and never performs an action."));
        var note = new TextBlock
        {
            Text = "Back and Forward are ready by default. Up, Down, and custom shapes are optional. You can configure apps separately; each app profile inherits global settings until you override them.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0)
        };
        steps.Children.Add(note);
        Grid.SetRow(steps, 2);
        root.Children.Add(steps);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var later = new Button { Content = "Not now", IsCancel = true, Width = 92, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 5, 8, 5) };
        var settings = new Button { Content = "Open settings", IsDefault = true, Padding = new Thickness(12, 5, 12, 5) };
        settings.Click += (_, _) =>
        {
            var owner = Owner;
            Close();
            var window = new SettingsWindow { Owner = owner };
            window.ShowDialog();
        };
        actions.Children.Add(later);
        actions.Children.Add(settings);
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);
        Content = root;
    }

    private static UIElement Step(string number, string title, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var badge = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 240, 251)),
            Child = new TextBlock { Text = number, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold }
        };
        row.Children.Add(badge);
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DimGray });
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private void CompleteWelcome()
    {
        try { AppSettings.CompleteWelcome(); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"MidFlow could not save the welcome status.\n\n{ex.Message}",
                "Welcome to MidFlow", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
