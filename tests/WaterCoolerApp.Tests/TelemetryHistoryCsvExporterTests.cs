using System.Globalization;
using RMMonitor.Models;
using RMMonitor.Services;
using Xunit;

namespace RMMonitor.Tests;

public sealed class TelemetryHistoryCsvExporterTests
{
    [Fact]
    public void Build_TemperatureExportContainsOnlyTimestampAndSelectedTemperatureColumns()
    {
        var newer = CreateSnapshot(
            new DateTime(2026, 10, 13, 12, 0, 0),
            cpuTemperature: 51.5, gpuTemperature: 63.25, cpuLoad: 30, gpuLoad: 40);
        var older = CreateSnapshot(
            new DateTime(2026, 10, 13, 11, 59, 0),
            cpuTemperature: 48, gpuTemperature: 60, cpuLoad: 20, gpuLoad: 35);

        var csv = TelemetryHistoryCsvExporter.Build(
            new[] { newer, older }, exportTemperature: true,
            showCpu: true, showGpu: false, showRam: false);

        Assert.Equal(new[]
        {
            "Horário;CPU (°C)",
            "2026-10-13 11:59:00;48",
            "2026-10-13 12:00:00;51.5"
        }, Lines(csv));
        Assert.DoesNotContain("GPU", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Utilização", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Componente", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Unidade", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UtilizationExportContainsOnlyTimestampAndSelectedPercentages()
    {
        var snapshot = CreateSnapshot(
            new DateTime(2026, 10, 13, 12, 0, 0),
            cpuTemperature: 50, gpuTemperature: 60, cpuLoad: 22.5, gpuLoad: 77.75,
            memoryUsed: 12, memoryTotal: 16);

        var csv = TelemetryHistoryCsvExporter.Build(
            new[] { snapshot }, exportTemperature: false,
            showCpu: false, showGpu: true, showRam: true);

        Assert.Equal(new[]
        {
            "Horário;GPU (%);Memória (%)",
            "2026-10-13 12:00:00;77.75;75"
        }, Lines(csv));
        Assert.DoesNotContain("CPU (%)", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Temperatura", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("°C", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_OmitsMissingNonFiniteAndInvalidMemoryValues()
    {
        var snapshot = CreateSnapshot(
            new DateTime(2026, 10, 13, 12, 0, 0),
            cpuTemperature: double.NaN, gpuTemperature: null,
            cpuLoad: double.PositiveInfinity, gpuLoad: null,
            memoryUsed: 8, memoryTotal: 0);

        var csv = TelemetryHistoryCsvExporter.Build(
            new[] { snapshot }, exportTemperature: false,
            showCpu: true, showGpu: true, showRam: true);

        Assert.Equal("Horário;CPU (%);GPU (%);Memória (%)", Lines(csv)[0]);
        Assert.Single(Lines(csv));
    }

    [Fact]
    public void Build_EmptyHistoryReturnsSelectedColumnHeadersOnly()
    {
        var csv = TelemetryHistoryCsvExporter.Build(
            Array.Empty<SensorSnapshot>(), exportTemperature: true,
            showCpu: true, showGpu: true, showRam: false);

        Assert.Equal(new[] { "Horário;CPU (°C);GPU (°C)" }, Lines(csv));
    }

    [Fact]
    public void Build_UsesInvariantDecimalSeparatorRegardlessOfCurrentCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var snapshot = CreateSnapshot(
                new DateTime(2026, 10, 13, 12, 0, 0),
                cpuTemperature: 51.75, gpuTemperature: null, cpuLoad: null, gpuLoad: null);

            var csv = TelemetryHistoryCsvExporter.Build(
                new[] { snapshot }, exportTemperature: true,
                showCpu: true, showGpu: false, showRam: false);

            Assert.Contains("2026-10-13 12:00:00;51.75", csv, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static string[] Lines(string csv) =>
        csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    private static SensorSnapshot CreateSnapshot(
        DateTime updatedAt,
        double? cpuTemperature,
        double? gpuTemperature,
        double? cpuLoad,
        double? gpuLoad,
        double? memoryUsed = 8,
        double? memoryTotal = 16) =>
        new(
            cpuTemperature, cpuLoad, gpuTemperature, gpuLoad,
            memoryUsed, memoryTotal, null,
            "Test CPU", "Test GPU", null, "C:",
            null, null, null, null, "C:", Array.Empty<StorageVolumeSnapshot>(),
            updatedAt, "Test");
}
