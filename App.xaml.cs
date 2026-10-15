using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using RMMonitor.Services;

namespace RMMonitor;

public partial class App : System.Windows.Application
{
    private static Mutex? _instanceMutex;
    private static bool _ownsMutex;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int message, nint wParam, nint lParam);

    private const int WmActivateExistingInstance = 0x8001;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, name: @"Local\RM-Monitor-SingleInstance", createdNew: out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            // Reuse the existing UI instead of silently doing nothing when launched twice.
            ActivateExistingWindow();
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

    private static void ActivateExistingWindow()
    {
        // The title follows the selected UI language, so try every supported translation.
        foreach (var title in new[] { "RM Monitor", "HW Metrics — Hardware Studio", "HW Metrics", "HWMetrics" }.Concat(Localizer.AllVariants("HW Metrics — Hardware Studio")))
        {
            var handle = FindWindow(null, title);
            if (handle == 0) continue;

            // Ask the existing WPF window to restore itself so tray-specific state
            // (Opacity and ShowInTaskbar) is reset through its normal UI path.
            SendMessage(handle, WmActivateExistingInstance, 0, 0);
            return;
        }
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
