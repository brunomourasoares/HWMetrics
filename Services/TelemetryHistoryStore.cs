using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WaterCoolerApp.Models;

namespace WaterCoolerApp.Services;

/// <summary>
/// Persists a bounded window of telemetry in the user's local application data directory.
/// </summary>
public sealed class TelemetryHistoryStore
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _filePath;
    private readonly string? _legacyFilePath;

    public TelemetryHistoryStore(string? filePath = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _filePath = filePath ?? Path.Combine(localAppData, "HWMetrics", "telemetry-history.json");
        _legacyFilePath = filePath is null
            ? Path.Combine(localAppData, "AquaCore", "telemetry-history.json")
            : null;
    }

    public IReadOnlyList<SensorSnapshot> Load(DateTime now, TimeSpan? retention = null)
    {
        var window = NormalizeRetention(retention);
        try
        {
            var sourcePath = File.Exists(_filePath)
                ? _filePath
                : _legacyFilePath is not null && File.Exists(_legacyFilePath)
                    ? _legacyFilePath
                    : null;
            if (sourcePath is null) return Array.Empty<SensorSnapshot>();
            var snapshots = JsonSerializer.Deserialize<List<SensorSnapshot>>(File.ReadAllText(sourcePath), JsonOptions);
            if (snapshots is null)
            {
                WriteSnapshots(Array.Empty<SensorSnapshot>());
                return Array.Empty<SensorSnapshot>();
            }

            var recent = Filter(snapshots, now, window);
            // Migrate legacy history into the new branded directory and prune invalid entries.
            if (sourcePath != _filePath || recent.Count != snapshots.Count)
                WriteSnapshots(recent);

            return recent;
        }
        catch (JsonException)
        {
            // Replace corrupt JSON so it is not left in place indefinitely if telemetry is unavailable.
            try
            {
                WriteSnapshots(Array.Empty<SensorSnapshot>());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                // Recovery is best-effort; a read-only or unavailable file must not block monitoring.
            }

            return Array.Empty<SensorSnapshot>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // A missing or inaccessible history file must never prevent hardware monitoring from starting.
            return Array.Empty<SensorSnapshot>();
        }
    }

    public void Save(IEnumerable<SensorSnapshot> snapshots, DateTime now, TimeSpan? retention = null)
    {
        try
        {
            var recent = Filter(snapshots, now, NormalizeRetention(retention));
            WriteSnapshots(recent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            // Persistence is best-effort; telemetry and the UI remain available if disk writes fail.
        }
    }

    private List<SensorSnapshot> Filter(IEnumerable<SensorSnapshot> snapshots, DateTime now, TimeSpan retention)
    {
        var cutoff = now - retention;
        return snapshots
            .Where(snapshot => snapshot is not null && snapshot.UpdatedAt >= cutoff && snapshot.UpdatedAt <= now)
            .OrderBy(snapshot => snapshot.UpdatedAt)
            .ToList();
    }

    private void WriteSnapshots(IReadOnlyCollection<SensorSnapshot> snapshots)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = _filePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshots, JsonOptions));
        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    private static TimeSpan NormalizeRetention(TimeSpan? retention) =>
        retention is { Ticks: > 0 } value ? value : DefaultRetention;
}
