namespace WaterCoolerApp.Models;

public sealed record StorageVolumeSnapshot(
    string Name,
    double UsedGiB,
    double TotalGiB,
    double FreeGiB,
    double UsedPercent);

public sealed record SensorSnapshot(
    double? CpuTemperatureC,
    double? CpuLoadPercent,
    double? GpuTemperatureC,
    double? GpuLoadPercent,
    double? MemoryUsedGiB,
    double? MemoryTotalGiB,
    double? StorageTemperatureC,
    string? SystemDriveName,
    double? StorageUsedGiB,
    double? StorageTotalGiB,
    double? StorageFreeGiB,
    double? StorageUsedPercent,
    string StorageVolumesSummary,
    IReadOnlyList<StorageVolumeSnapshot> StorageVolumes,
    DateTime UpdatedAt,
    string Status);
