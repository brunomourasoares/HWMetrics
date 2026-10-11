using WaterCoolerApp.Services;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class HardwareSensorSelectionTests
{
    [Theory]
    [InlineData("CPU Total", 100)]
    [InlineData("CPU", 80)]
    [InlineData("Core Average", 70)]
    [InlineData("CPU Core #1", 30)]
    [InlineData("Core 0", 30)]
    [InlineData("Power", -1)]
    public void CpuLoadPriority_PrefersAggregateReadings(string sensorName, int expectedPriority)
    {
        Assert.Equal(expectedPriority,
            HardwareMonitoringService.GetCpuLoadPriority(sensorName.ToLowerInvariant()));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(42.5, true)]
    [InlineData(100, true)]
    [InlineData(150, true)]
    [InlineData(151, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void TemperatureValidation_RejectsInvalidReadings(double value, bool expected)
    {
        Assert.Equal(expected, HardwareMonitoringService.IsValidTemperature(value));
    }
}
