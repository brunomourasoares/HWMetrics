using WaterCoolerApp.Protocol;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class DisplayReportTests
{
    [Fact]
    public void CreateTelemetry_EncodesObservedElevenBytePayload()
    {
        var payload = DisplayReport.CreateTelemetry(67.8, 55.9, 83.2, 61.4, null, 42.9);

        Assert.Equal(DisplayReport.PayloadLength, payload.Length);
        Assert.Equal(0, payload[0]);
        Assert.Equal(67, payload[DisplayReport.CpuTemperatureIndex]);
        Assert.Equal(55, payload[DisplayReport.GpuTemperatureIndex]);
        Assert.Equal(83, payload[DisplayReport.CpuLoadIndex]);
        Assert.Equal(61, payload[DisplayReport.MemoryLoadIndex]);
        Assert.Equal(0, payload[DisplayReport.MotherboardTemperatureIndex]);
        Assert.Equal(42, payload[DisplayReport.GpuLoadIndex]);
        Assert.All(payload.Skip(7), value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CreateCpuTemperatureOnly_RejectsInvalidCpuTemperature(double temperature)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DisplayReport.CreateCpuTemperatureOnly(temperature));
    }

    [Fact]
    public void CreateTelemetry_RejectsMissingCpuTemperature()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DisplayReport.CreateTelemetry(null, 50, 20, 30, null, 40));
    }

    [Fact]
    public void CreateTelemetry_UsesZeroForOptionalOutOfRangeValues()
    {
        var payload = DisplayReport.CreateTelemetry(45, 200, 150, -1, null, double.NaN);

        Assert.Equal(45, payload[DisplayReport.CpuTemperatureIndex]);
        Assert.Equal(0, payload[DisplayReport.GpuTemperatureIndex]);
        Assert.Equal(0, payload[DisplayReport.CpuLoadIndex]);
        Assert.Equal(0, payload[DisplayReport.MemoryLoadIndex]);
        Assert.Equal(0, payload[DisplayReport.GpuLoadIndex]);
    }
}
