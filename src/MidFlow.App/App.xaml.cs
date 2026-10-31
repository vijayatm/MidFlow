using System.Windows;
using Application = System.Windows.Application;

namespace MidFlow.App;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.Show();
        if (AppSettings.ShouldShowWelcome)
        {
            var welcome = new FirstRunWindow { Owner = _mainWindow };
            welcome.ShowDialog();
        }
    }
}
