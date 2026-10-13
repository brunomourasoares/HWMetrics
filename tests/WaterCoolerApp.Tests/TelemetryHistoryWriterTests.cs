using WaterCoolerApp.Models;
using WaterCoolerApp.Services;
using Xunit;

namespace WaterCoolerApp.Tests;

public sealed class TelemetryHistoryWriterTests
{
    [Fact]
    public async Task DisposeAsync_FlushesLatestQueuedSnapshotBeforeReturning()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HWMetricsTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "telemetry-history.json");
        var now = DateTime.Now;
        var store = new TelemetryHistoryStore(path);
        var writer = new TelemetryHistoryWriter(store);

        try
        {
            writer.Enqueue(new[] { CreateSnapshot(now.AddMinutes(-1), 41) }, now);
            writer.Enqueue(new[] { CreateSnapshot(now, 67) }, now);

            await writer.DisposeAsync();

            var loaded = store.Load(now);
            var snapshot = Assert.Single(loaded);
            Assert.Equal(67, snapshot.CpuTemperatureC);
        }
        finally
        {
            await writer.DisposeAsync();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Enqueue_AfterDisposeThrowsObjectDisposedException()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HWMetricsTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "telemetry-history.json");
        var writer = new TelemetryHistoryWriter(new TelemetryHistoryStore(path));

        await writer.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() =>
            writer.Enqueue(Array.Empty<SensorSnapshot>(), DateTime.Now));

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static SensorSnapshot CreateSnapshot(DateTime updatedAt, double cpuTemperature) =>
        new(
            cpuTemperature, 25, 55, 40,
            8, 16, null,
            "Test CPU", "Test GPU", "Test disk", "C:",
            100, 500, 400, 20, "C:", Array.Empty<StorageVolumeSnapshot>(),
            updatedAt, "Test");
}
