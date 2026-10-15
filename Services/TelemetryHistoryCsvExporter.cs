using System.Globalization;
using System.Text;
using RMMonitor.Models;

namespace RMMonitor.Services;

/// <summary>
/// Builds compact, filter-aware CSV exports with timestamps and only the selected metric series.
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

        var columns = new List<(string Header, Func<SensorSnapshot, double?> Value)>();
        if (exportTemperature)
        {
            if (showCpu) columns.Add(("CPU (°C)", snapshot => snapshot.CpuTemperatureC));
            if (showGpu) columns.Add(("GPU (°C)", snapshot => snapshot.GpuTemperatureC));
        }
        else
        {
            if (showCpu) columns.Add(("CPU (%)", snapshot => snapshot.CpuLoadPercent));
            if (showGpu) columns.Add(("GPU (%)", snapshot => snapshot.GpuLoadPercent));
            if (showRam) columns.Add(("Memória (%)", GetMemoryUsedPercent));
        }

        var csv = new StringBuilder();
        csv.Append("Horário");
        foreach (var column in columns)
            csv.Append(';').Append(column.Header);
        csv.AppendLine();

        foreach (var snapshot in snapshots.Where(item => item is not null).OrderBy(item => item.UpdatedAt))
        {
            var values = columns.Select(column => column.Value(snapshot))
                .Select(value => IsFinite(value) ? value : null)
                .ToArray();

            // Avoid exporting rows where every selected metric is unavailable.
            if (values.Length == 0 || values.All(value => value is null))
                continue;

            csv.Append(snapshot.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            foreach (var value in values)
            {
                csv.Append(';');
                if (value.HasValue)
                    csv.Append(value.Value.ToString("0.##", CultureInfo.InvariantCulture));
            }
            csv.AppendLine();
        }

        return csv.ToString();
    }

    private static double? GetMemoryUsedPercent(SensorSnapshot snapshot)
    {
        if (!IsFinite(snapshot.MemoryUsedGiB) ||
            !IsFinite(snapshot.MemoryTotalGiB) ||
            snapshot.MemoryTotalGiB!.Value <= 0)
            return null;

        var percentage = snapshot.MemoryUsedGiB!.Value / snapshot.MemoryTotalGiB!.Value * 100d;
        return double.IsFinite(percentage) ? percentage : null;
    }

    private static bool IsFinite(double? value) =>
        value.HasValue && double.IsFinite(value.Value);
}
