using System.Linq;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace MidFlow.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        GesturesCheckBox.IsChecked = AppSettings.GesturesEnabled;
        TrailCheckBox.IsChecked = AppSettings.ShowTrail;
        MinimumHorizontalTextBox.Text = AppSettings.MinimumHorizontalMovementPx.ToString();
        MaximumVerticalTextBox.Text = AppSettings.MaximumVerticalMovementPx.ToString();
        MaximumDurationTextBox.Text = AppSettings.MaximumGestureDurationMs.ToString();
        ClickThresholdTextBox.Text = AppSettings.ClickMovementThresholdPx.ToString();
        ProfilesTextBox.Text = string.Join(Environment.NewLine,
            AppSettings.Profiles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Key}={(x.Value ? "enabled" : "disabled")}"));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var profiles = new List<KeyValuePair<string, bool>>();
        foreach (var rawLine in ProfilesTextBox.Text.Split(new[] { "\r\n", "\n" },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !(parts[1].Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
                  parts[1].Equals("disabled", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Invalid profile line: {rawLine}\nUse process-name=enabled or process-name=disabled.",
                    "MidFlow Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            profiles.Add(new KeyValuePair<string, bool>(parts[0],
                parts[1].Equals("enabled", StringComparison.OrdinalIgnoreCase)));
        }

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
        AppSettings.SetProfiles(profiles);
        AppSettings.Save();
        Close();
    }
}
