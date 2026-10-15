using RMMonitor.Models;
using RMMonitor.Services;
using Xunit;

namespace RMMonitor.Tests;

public sealed class DisplayTransportServiceTests
{
    [Fact]
    public void Send_RejectsMissingCpuTemperatureBeforeHidDiscovery()
    {
        using var service = new DisplayTransportService();

        var status = service.Send(CreateSnapshot(cpuTemperature: null));

        Assert.Equal(DisplayTransportStatus.InvalidTelemetry, status);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void Send_RejectsInvalidCpuTemperatureBeforeHidDiscovery(double temperature)
    {
        using var service = new DisplayTransportService();

        var status = service.Send(CreateSnapshot(cpuTemperature: temperature));

        Assert.Equal(DisplayTransportStatus.InvalidTelemetry, status);
    }

    [Fact]
    public void Send_AfterDisposeReturnsErrorWithoutDiscoveringDevice()
    {
        var service = new DisplayTransportService();
        service.Dispose();

        var status = service.Send(CreateSnapshot(cpuTemperature: 45));

        Assert.Equal(DisplayTransportStatus.Error, status);
    }

    private static SensorSnapshot CreateSnapshot(double? cpuTemperature) =>
        new(
            cpuTemperature, 35, 55, 40,
            8, 16, null,
            "Test CPU", "Test GPU", "Test disk", "C:",
            100, 500, 400, 20, "C:", Array.Empty<StorageVolumeSnapshot>(),
            DateTime.Now, "Test");
}
