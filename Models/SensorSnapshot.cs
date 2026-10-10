namespace WaterCoolerApp.Models;

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
    DateTime UpdatedAt,
    string Status);
