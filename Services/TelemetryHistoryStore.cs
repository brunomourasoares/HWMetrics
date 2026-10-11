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

    public TelemetryHistoryStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AquaCore", "telemetry-history.json");
    }

    public IReadOnlyList<SensorSnapshot> Load(DateTime now, TimeSpan? retention = null)
    {
        var window = NormalizeRetention(retention);
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<SensorSnapshot>();
            var snapshots = JsonSerializer.Deserialize<List<SensorSnapshot>>(File.ReadAllText(_filePath), JsonOptions);
            if (snapshots is null)
            {
                WriteSnapshots(Array.Empty<SensorSnapshot>());
                return Array.Empty<SensorSnapshot>();
            }

            var recent = Filter(snapshots, now, window);
            // Rewrite the file when it contains expired, future-dated, or null entries so
            // unwanted records do not remain on disk between normal save cycles.
            if (recent.Count != snapshots.Count)
                WriteSnapshots(recent);

            return recent;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            // A missing or malformed history file must never prevent hardware monitoring from starting.
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
