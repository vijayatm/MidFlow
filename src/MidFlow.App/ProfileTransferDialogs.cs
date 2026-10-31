using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using Brushes = System.Windows.Media.Brushes;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using FontFamily = System.Windows.Media.FontFamily;

namespace MidFlow.App;

internal sealed class ProfileSelectionWindow : Window
{
    private readonly List<CheckBox> _choices = [];
    internal IReadOnlyList<string> SelectedNames => _choices.Where(x => x.IsChecked == true)
        .Select(x => (string)x.Tag).ToArray();

    internal ProfileSelectionWindow(IEnumerable<string> profileNames)
    {
        Title = "Export application profiles";
        Width = 420;
        Height = 520;
        MinWidth = 360;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var description = new TextBlock
        {
            Text = "Select one or more application profiles. The package captures effective shortcut and shape mappings plus enabled state. The receiving computer keeps its own global enable switch and sensitivity settings.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        root.Children.Add(description);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var list = new StackPanel();
        foreach (var name in profileNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var choice = new CheckBox { Content = name, Tag = name, Padding = new Thickness(4), Margin = new Thickness(2) };
            _choices.Add(choice);
            list.Children.Add(choice);
        }
        scroll.Content = list;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(new Button { Content = "Cancel", Width = 84, Margin = new Thickness(0, 0, 8, 0), IsCancel = true, Padding = new Thickness(8, 5, 8, 5) });
        var export = new Button { Content = "Continue", Width = 92, IsDefault = true, Padding = new Thickness(8, 5, 8, 5) };
        export.Click += (_, _) =>
        {
            if (SelectedNames.Count == 0)
            {
                MessageBox.Show(this, "Select at least one application profile.", "Export profiles", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
        };
        footer.Children.Add(export);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;
    }
}

internal sealed class TransferPreviewWindow : Window
{
    internal TransferPreviewWindow(string title, string heading, string details, string applyLabel)
    {
        Title = title;
        Width = 620;
        Height = 540;
        MinWidth = 500;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock { Text = heading, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var note = new TextBlock
        {
            Text = "Review the changes below. MidFlow will make a recovery backup before updating this settings draft. Click Save in Settings to commit the imported data.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(note, 1);
        root.Children.Add(note);
        var preview = new TextBox
        {
            Text = details,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(10),
            FontFamily = new FontFamily("Consolas")
        };
        Grid.SetRow(preview, 2);
        root.Children.Add(preview);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(new Button { Content = "Cancel", Width = 84, Margin = new Thickness(0, 0, 8, 0), IsCancel = true, Padding = new Thickness(8, 5, 8, 5) });
        var apply = new Button { Content = applyLabel, MinWidth = 108, IsDefault = true, Padding = new Thickness(10, 5, 10, 5) };
        apply.Click += (_, _) => { DialogResult = true; Close(); };
        footer.Children.Add(apply);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        Content = root;
    }
}
