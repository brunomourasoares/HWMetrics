namespace RMMonitor.Models;

public sealed record StorageVolumeSnapshot(
    string Name,
    double UsedGiB,
    double TotalGiB,
    double FreeGiB,
    double UsedPercent,
    string ProductModel = "");

public sealed record SensorSnapshot(
    double? CpuTemperatureC,
    double? CpuLoadPercent,
    double? GpuTemperatureC,
    double? GpuLoadPercent,
    double? MemoryUsedGiB,
    double? MemoryTotalGiB,
    double? StorageTemperatureC,
    string? CpuModel,
    string? GpuModel,
    string? StorageModel,
    string? SystemDriveName,
    double? StorageUsedGiB,
    double? StorageTotalGiB,
    double? StorageFreeGiB,
    double? StorageUsedPercent,
    string StorageVolumesSummary,
    IReadOnlyList<StorageVolumeSnapshot> StorageVolumes,
    DateTime UpdatedAt,
    string Status,
    double? CpuFrequencyMHz = null,
    double? GpuFrequencyMHz = null);
