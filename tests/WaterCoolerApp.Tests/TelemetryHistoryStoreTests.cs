using System.Text.Json;
using WaterCoolerApp.Models;
using WaterCoolerApp.Services;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class TelemetryHistoryStoreTests
{
    [Fact]
    public void SaveAndLoad_KeepOnlySnapshotsInsideRetentionWindow()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AquaCoreTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "telemetry-history.json");
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Local);
        var store = new TelemetryHistoryStore(path);

        try
        {
            store.Save(new[]
            {
                CreateSnapshot(now.AddMinutes(-31)),
                CreateSnapshot(now.AddMinutes(-10)),
                CreateSnapshot(now.AddMinutes(2))
            }, now);

            var loaded = store.Load(now);

            var snapshot = Assert.Single(loaded);
            Assert.Equal(now.AddMinutes(-10), snapshot.UpdatedAt);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_IgnoresNullEntriesInHistoryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AquaCoreTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "telemetry-history.json");
        var now = DateTime.Now;
        Directory.CreateDirectory(directory);

        try
        {
            var snapshots = new SensorSnapshot?[] { null, CreateSnapshot(now.AddMinutes(-5)) };
            File.WriteAllText(path, JsonSerializer.Serialize(snapshots));
            var store = new TelemetryHistoryStore(path);

            var loaded = Assert.Single(store.Load(now));
            Assert.Equal(now.AddMinutes(-5), loaded.UpdatedAt);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsEmptyWhenHistoryFileIsMalformed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AquaCoreTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "telemetry-history.json");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(path, "{ not valid json");
            var store = new TelemetryHistoryStore(path);

            Assert.Empty(store.Load(DateTime.Now));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static SensorSnapshot CreateSnapshot(DateTime updatedAt) => new(
        CpuTemperatureC: 45,
        CpuLoadPercent: 20,
        GpuTemperatureC: 50,
        GpuLoadPercent: 30,
        MemoryUsedGiB: 8,
        MemoryTotalGiB: 16,
        StorageTemperatureC: null,
        CpuModel: "Test CPU",
        GpuModel: "Test GPU",
        StorageModel: "Test Drive",
        SystemDriveName: "C:",
        StorageUsedGiB: 100,
        StorageTotalGiB: 500,
        StorageFreeGiB: 400,
        StorageUsedPercent: 20,
        StorageVolumesSummary: "C: 20%",
        StorageVolumes: Array.Empty<StorageVolumeSnapshot>(),
        UpdatedAt: updatedAt,
        Status: "Test");
}
