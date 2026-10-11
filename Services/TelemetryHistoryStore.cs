using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WaterCoolerApp.Models;

namespace WaterCoolerApp.Services;

/// <summary>
/// Persists recent telemetry in the user's local application data directory.
/// The history is intentionally limited to the dashboard's rolling 30-minute window.
/// </summary>
public sealed class TelemetryHistoryStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _filePath;

    public TelemetryHistoryStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AquaCore", "telemetry-history.json");
    }

    public IReadOnlyList<SensorSnapshot> Load(DateTime now)
    {
        try
        {
            if (!File.Exists(_filePath)) return Array.Empty<SensorSnapshot>();
            var snapshots = JsonSerializer.Deserialize<List<SensorSnapshot>>(File.ReadAllText(_filePath), JsonOptions);
            if (snapshots is null) return Array.Empty<SensorSnapshot>();
            var cutoff = now - Retention;
            return snapshots
                .Where(snapshot => snapshot is not null && snapshot.UpdatedAt >= cutoff && snapshot.UpdatedAt <= now.AddMinutes(1))
                .OrderBy(snapshot => snapshot.UpdatedAt)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // A missing or malformed history file must never prevent hardware monitoring from starting.
            return Array.Empty<SensorSnapshot>();
        }
    }

    public void Save(IEnumerable<SensorSnapshot> snapshots, DateTime now)
    {
        var cutoff = now - Retention;
        var recent = snapshots
            .Where(snapshot => snapshot is not null && snapshot.UpdatedAt >= cutoff && snapshot.UpdatedAt <= now.AddMinutes(1))
            .OrderBy(snapshot => snapshot.UpdatedAt)
            .ToArray();

        try
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(recent, JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // Persistence is best-effort; telemetry and the UI remain available if disk writes fail.
        }
    }
}
