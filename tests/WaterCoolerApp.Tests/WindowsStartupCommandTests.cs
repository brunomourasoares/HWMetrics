using RMMonitor;
using Xunit;

namespace RMMonitor.Tests;

public sealed class WindowsStartupCommandTests
{
    [Fact]
    public void BuildWindowsStartupCommand_QuotesDirectExecutableAndStartsInTray()
    {
        var command = MainWindow.BuildWindowsStartupCommand(
            @"C:\Program Files\Aqua Core\RMMonitor.exe",
            @"C:\Program Files\Aqua Core\RMMonitor.dll");

        Assert.Equal("\"C:\\Program Files\\Aqua Core\\RMMonitor.exe\" --tray", command);
    }

    [Fact]
    public void BuildWindowsStartupCommand_IncludesAssemblyWhenUsingDotnetHost()
    {
        var command = MainWindow.BuildWindowsStartupCommand(
            @"C:\Program Files\dotnet\dotnet.exe",
            @"C:\Program Files\Aqua Core\RMMonitor.dll");

        Assert.Equal("\"C:\\Program Files\\dotnet\\dotnet.exe\" \"C:\\Program Files\\Aqua Core\\RMMonitor.dll\" --tray", command);
    }

    [Fact]
    public void BuildWindowsStartupCommand_RejectsMissingPaths()
    {
        Assert.Throws<ArgumentException>(() => MainWindow.BuildWindowsStartupCommand("", null));
        Assert.Throws<ArgumentException>(() => MainWindow.BuildWindowsStartupCommand(
            @"C:\Program Files\dotnet\dotnet.exe", null));
    }
}
