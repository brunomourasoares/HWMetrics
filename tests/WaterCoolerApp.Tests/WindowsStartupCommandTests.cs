using WaterCoolerApp;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class WindowsStartupCommandTests
{
    [Fact]
    public void BuildWindowsStartupCommand_QuotesDirectExecutableAndStartsInTray()
    {
        var command = MainWindow.BuildWindowsStartupCommand(
            @"C:\Program Files\Aqua Core\WaterCoolerApp.exe",
            @"C:\Program Files\Aqua Core\WaterCoolerApp.dll");

        Assert.Equal("\"C:\\Program Files\\Aqua Core\\WaterCoolerApp.exe\" --tray", command);
    }

    [Fact]
    public void BuildWindowsStartupCommand_IncludesAssemblyWhenUsingDotnetHost()
    {
        var command = MainWindow.BuildWindowsStartupCommand(
            @"C:\Program Files\dotnet\dotnet.exe",
            @"C:\Program Files\Aqua Core\WaterCoolerApp.dll");

        Assert.Equal("\"C:\\Program Files\\dotnet\\dotnet.exe\" \"C:\\Program Files\\Aqua Core\\WaterCoolerApp.dll\" --tray", command);
    }

    [Fact]
    public void BuildWindowsStartupCommand_RejectsMissingPaths()
    {
        Assert.Throws<ArgumentException>(() => MainWindow.BuildWindowsStartupCommand("", null));
        Assert.Throws<ArgumentException>(() => MainWindow.BuildWindowsStartupCommand(
            @"C:\Program Files\dotnet\dotnet.exe", null));
    }
}
