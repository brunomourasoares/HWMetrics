using System.Windows;

namespace WaterCoolerApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow
        {
            StartHidden = e.Args.Any(arg => string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase))
        };
        MainWindow = window;
        window.Show();
    }
}
