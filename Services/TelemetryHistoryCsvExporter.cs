using System.Globalization;
using System.Text;
using WaterCoolerApp.Models;

namespace WaterCoolerApp.Services;

/// <summary>
/// Builds CSV exports from telemetry snapshots without depending on WPF controls.
/// </summary>
public static class TelemetryHistoryCsvExporter
{
    public static string Build(
        IReadOnlyList<SensorSnapshot> snapshots,
        bool exportTemperature,
        bool showCpu,
        bool showGpu,
        bool showRam)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var csv = new StringBuilder();
        csv.AppendLine("Tipo;Horário;Componente;Valor;Unidade");

        foreach (var snapshot in snapshots.Where(item => item is not null).OrderBy(item => item.UpdatedAt))
        {
            if (snapshot is null)
                continue;

            var timestamp = snapshot.UpdatedAt.ToString(
                "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            if (exportTemperature)
            {
                if (showCpu && IsFinite(snapshot.CpuTemperatureC))
                    AppendRow(csv, "Temperatura", timestamp, "CPU", snapshot.CpuTemperatureC!.Value, "°C");
                if (showGpu && IsFinite(snapshot.GpuTemperatureC))
                    AppendRow(csv, "Temperatura", timestamp, "GPU", snapshot.GpuTemperatureC!.Value, "°C");
            }
            else
            {
                if (showCpu && IsFinite(snapshot.CpuLoadPercent))
                    AppendRow(csv, "Utilização", timestamp, "CPU", snapshot.CpuLoadPercent!.Value, "%");
                if (showGpu && IsFinite(snapshot.GpuLoadPercent))
                    AppendRow(csv, "Utilização", timestamp, "GPU", snapshot.GpuLoadPercent!.Value, "%");
                if (showRam && IsFinite(snapshot.MemoryUsedGiB) &&
                    IsFinite(snapshot.MemoryTotalGiB) && snapshot.MemoryTotalGiB > 0)
                {
                    var percentage = snapshot.MemoryUsedGiB!.Value / snapshot.MemoryTotalGiB!.Value * 100d;
                    if (double.IsFinite(percentage))
                        AppendRow(csv, "Utilização", timestamp, "Memória", percentage, "%");
                }
            }
        }

        return csv.ToString();
    }

    private static bool IsFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value);

    private static void AppendRow(
        StringBuilder csv,
        string type,
        string timestamp,
        string component,
        double value,
        string unit)
    {
        // Text fields are controlled labels/timestamps and cannot contain CSV delimiters.
        csv.Append(type).Append(';')
            .Append(timestamp).Append(';')
            .Append(component).Append(';')
            .Append(value.ToString("0.##", CultureInfo.InvariantCulture)).Append(';')
            .AppendLine(unit);
    }
}
