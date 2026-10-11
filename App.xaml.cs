using System.Windows;

namespace WaterCoolerApp;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow
        {
            StartHidden = e.Args.Any(arg => string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase))
        };
        MainWindow = window;
        if (window.StartHidden)
        {
            // Prevent a visible flash and taskbar entry while --tray startup initializes.
            window.Opacity = 0;
            window.ShowInTaskbar = false;
        }
        window.Show();
    }
}
