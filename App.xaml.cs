using System.Threading;
using System.Windows;

namespace WaterCoolerApp;

public partial class App : System.Windows.Application
{
    private static Mutex? _instanceMutex;
    private static bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, name: @"Local\\HW-Metrics-SingleInstance", createdNew: out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            // Another instance already owns the mutex. Do not initialize a second window,
            // sensor poller, tray icon, or HID connection.
            Shutdown();
            return;
        }

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

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        _instanceMutex = null;
        _ownsMutex = false;
        base.OnExit(e);
    }
}