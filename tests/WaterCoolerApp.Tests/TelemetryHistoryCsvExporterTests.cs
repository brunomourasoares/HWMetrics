using System.Globalization;
using WaterCoolerApp.Models;
using WaterCoolerApp.Services;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class TelemetryHistoryCsvExporterTests
{
    [Fact]
    public void Build_TemperatureExportIncludesOnlySelectedCpuAndGpuSeriesInChronologicalOrder()
    {
        var newer = CreateSnapshot(
            new DateTime(2026, 10, 13, 12, 0, 0),
            cpuTemperature: 51.5,
            gpuTemperature: 63.25,
            cpuLoad: 30,
            gpuLoad: 40);
        var older = CreateSnapshot(
            new DateTime(2026, 10, 13, 11, 59, 0),
            cpuTemperature: 48,
            gpuTemperature: 60,
            cpuLoad: 20,
            gpuLoad: 35);

        var csv = TelemetryHistoryCsvExporter.Build(
            new[] { newer, older }, exportTemperature: true,
            showCpu: true, showGpu: false, showRam: false);

        var rows = Lines(csv);
        Assert.Equal("Tipo;Horário;Componente;Valor;Unidade", rows[0]);
        Assert.Equal(3, rows.Length);
        Assert.Contains("Temperatura;2026-10-13 11:59:00;CPU;48;°C", rows);
        Assert.Contains("Temperatura;2026-10-13 12:00:00;CPU;51.5;°C", rows);
        Assert.DoesNotContain(rows, row => row.Contains(";GPU;", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains("Utilização", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_UtilizationExportIncludesSelectedRamAsPercentage()
    {
        var snapshot = CreateSnapshot(
            new DateTime(2026, 10, 13, 12, 0, 0),
            cpuTemperature: 50,
            gpuTemperature: 60,
            cpuLoad: 22.5,
            gpuLoad: 77.75,
            memoryUsed: 12,
            memoryTotal: 16);

        var csv = TelemetryHistoryCsvExporter.Build(
            new[] { snapshot }, exportTemperature: false,
            showCpu: false, showGpu: true, showRam: true);

        var rows = Lines(csv);
        Assert.Equal(3, rows.Length);
        Assert.Contains("Utilização;2026-10-13 12:00:00;GPU;77.75;%", rows);
        Assert.Contains("Utilização;2026-10-13 12:00:00;Memória;75;%", rows);
        Assert.DoesNotContain(rows, row => row.Contains(";CPU;", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, row => row.Contains("Temperatura", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_OmitsMissingNonFiniteAndInvalidMemoryValues()
    {
        var snapshot = CreateSnapshot(
            new DateTime(2026, 10, 13, 12, 0, 0),
            cpuTemperature: double.NaN,
            gpuTemperature: null,
            cpuLoad: double.PositiveInfinity,
            gpuLoad: null,
            memoryUsed: 8,
            memoryTotal: 0);

        var csv = TelemetryHistoryCsvExporter.Build(
            new[] { snapshot }, exportTemperature: false,
            showCpu: true, showGpu: true, showRam: true);

        Assert.Equal("Tipo;Horário;Componente;Valor;Unidade", Lines(csv)[0]);
        Assert.Single(Lines(csv));
    }

    [Fact]
    public void Build_EmptyHistoryReturnsHeaderOnly()
    {
        var csv = TelemetryHistoryCsvExporter.Build(
            Array.Empty<SensorSnapshot>(), exportTemperature: true,
            showCpu: true, showGpu: true, showRam: false);

        Assert.Equal(new[] { "Tipo;Horário;Componente;Valor;Unidade" }, Lines(csv));
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
                cpuTemperature: 51.75,
                gpuTemperature: null,
                cpuLoad: null,
                gpuLoad: null);

            var csv = TelemetryHistoryCsvExporter.Build(
                new[] { snapshot }, exportTemperature: true,
                showCpu: true, showGpu: false, showRam: false);

            Assert.Contains("CPU;51.75;°C", csv, StringComparison.Ordinal);
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
