using LibreHardwareMonitor.Hardware;
using WaterCoolerApp.Services;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class CpuTemperaturePriorityTests
{
    [Theory]
    [InlineData("CPU Package", HardwareType.Cpu, 100)]
    [InlineData("Core Max", HardwareType.Cpu, 85)]
    [InlineData("CPU", HardwareType.Cpu, 60)]
    [InlineData("CPU", HardwareType.Motherboard, 40)]
    [InlineData("CPU Socket", HardwareType.SuperIO, 45)]
    [InlineData("Processor Package", HardwareType.Motherboard, 55)]
    [InlineData("System", HardwareType.Motherboard, -1)]
    [InlineData("VRM MOS", HardwareType.SuperIO, -1)]
    [InlineData("GPU Core", HardwareType.GpuNvidia, -1)]
    public void Priority_OnlyAcceptsCpuSensorsAndPrefersDirectCpuReadings(
        string sensorName, HardwareType hardwareType, int expectedPriority)
    {
        Assert.Equal(expectedPriority,
            HardwareMonitoringService.GetCpuTemperaturePriority(sensorName, hardwareType));
    }

    [Theory]
    [InlineData("CPU PACKAGE", HardwareType.Cpu, 100)]
    [InlineData("Gpu Core", HardwareType.GpuNvidia, -1)]
    public void PriorityMatchingIsCaseInsensitive(string sensorName, HardwareType hardwareType, int expectedPriority)
    {
        Assert.Equal(expectedPriority,
            HardwareMonitoringService.GetCpuTemperaturePriority(sensorName, hardwareType));
    }

    [Fact]
    public void DirectCpuPackageSensorOutranksMotherboardCpuThermistor()
    {
        var directCpuPriority = HardwareMonitoringService.GetCpuTemperaturePriority("cpu package", HardwareType.Cpu);
        var motherboardPriority = HardwareMonitoringService.GetCpuTemperaturePriority("cpu", HardwareType.Motherboard);

        Assert.True(directCpuPriority > motherboardPriority);
    }
}
