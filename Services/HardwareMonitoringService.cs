using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using WaterCoolerApp.Models;
using System.IO;

namespace WaterCoolerApp.Services;

/// <summary>Reads telemetry only. It never writes to USB or changes fan/pump speeds.</summary>
public sealed class HardwareMonitoringService : IDisposable
{
    private readonly Computer _computer;
    private readonly object _sync = new();
    private bool _disposed;

    public HardwareMonitoringService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true
        };
        _computer.Open();
    }

    public SensorSnapshot Read()
    {
        lock (_sync)
        {
        ObjectDisposedException.ThrowIf(_disposed, this);
        double? cpuTemp = null, cpuLoad = null, gpuTemp = null, gpuLoad = null;
        double? usedGiB = null, totalGiB = null, driveTemp = null;

        foreach (var hardware in _computer.Hardware)
            Visit(hardware, ref cpuTemp, ref cpuLoad, ref gpuTemp, ref gpuLoad,
                ref usedGiB, ref totalGiB, ref driveTemp);

        // LibreHardwareMonitor does not expose physical memory consistently on every system.
        // Use the Windows memory API as a reliable fallback for the RAM card.
        if (usedGiB is null || totalGiB is null)
        {
            var memory = ReadPhysicalMemory();

            // Fill only missing values: keep a valid LibreHardwareMonitor reading
            // instead of replacing both fields when just one field is unavailable.
            usedGiB ??= memory.UsedGiB;
            totalGiB ??= memory.TotalGiB;
        }

        var storage = ReadStorage();
        return new SensorSnapshot(cpuTemp, NormalizePercent(cpuLoad), gpuTemp, NormalizePercent(gpuLoad),
            NormalizeMemoryUsed(usedGiB, totalGiB), NormalizeMemoryTotal(totalGiB),
            driveTemp, storage.SystemDriveName, storage.UsedGiB, storage.TotalGiB,
            storage.FreeGiB, storage.UsedPercent, storage.VolumesSummary, storage.Volumes,
            DateTime.Now, "Leitura concluída");
        }
    }

    private static void Visit(IHardware hardware, ref double? cpuTemp, ref double? cpuLoad,
        ref double? gpuTemp, ref double? gpuLoad, ref double? usedGiB, ref double? totalGiB,
        ref double? driveTemp)
    {
        try
        {
            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.Value is not float value || !float.IsFinite(value)) continue;
                var name = sensor.Name.ToLowerInvariant();
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        if (hardware.HardwareType == HardwareType.Cpu &&
                            (cpuTemp is null || name.Contains("package") || name.Contains("tctl")))
                            cpuTemp = value;
                        else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                        {
                            // Prefer the GPU core sensor when available; keep the first valid
                            // temperature as a fallback for drivers with generic sensor names.
                            if (gpuTemp is null || (name.Contains("core") && !name.Contains("hot spot") && !name.Contains("hotspot")))
                                gpuTemp = value;
                        }
                        else if (hardware.HardwareType == HardwareType.Storage)
                            driveTemp ??= value;
                        break;

                    case SensorType.Load:
                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            // Do not leave CPU utilization blank just because a driver does not
                            // name its aggregate sensor "Total"; replace the fallback if it appears.
                            if (cpuLoad is null || name.Contains("total"))
                                cpuLoad = value;
                        }
                        else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                        {
                            // Prefer core/aggregate GPU load, while retaining a usable fallback.
                            if (gpuLoad is null || name.Contains("core") || name.Contains("total"))
                                gpuLoad = value;
                        }
                        break;

                    case SensorType.Data when hardware.HardwareType == HardwareType.Memory:
                        if (name.Contains("used")) usedGiB ??= value;
                        if (name.Contains("total")) totalGiB ??= value;
                        break;
                }
            }
        }
        catch
        {
            // A failed controller/driver must not prevent readings from other devices.
        }

        foreach (var child in hardware.SubHardware)
            Visit(child, ref cpuTemp, ref cpuLoad, ref gpuTemp, ref gpuLoad,
                ref usedGiB, ref totalGiB, ref driveTemp);
    }


    private static (string? SystemDriveName, double? UsedGiB, double? TotalGiB,
        double? FreeGiB, double? UsedPercent, string VolumesSummary,
        IReadOnlyList<StorageVolumeSnapshot> Volumes) ReadStorage()
    {
        const double bytesPerGiB = 1024d * 1024d * 1024d;
        var drives = new List<(DriveInfo Drive, double Total, double Free, double Used)>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || drive.TotalSize <= 0) continue;
                var total = drive.TotalSize / bytesPerGiB;
                var free = drive.AvailableFreeSpace / bytesPerGiB;
                drives.Add((drive, total, free, Math.Max(0, total - free)));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        if (drives.Count == 0)
            return (null, null, null, null, null, "No local volumes detected",
                Array.Empty<StorageVolumeSnapshot>());

        // Stable ordering prevents the rotating UI from changing drive identity between reads.
        drives = drives.OrderBy(item => item.Drive.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var volumes = drives.Select(item =>
        {
            var used = Math.Clamp(item.Used, 0d, item.Total);
            var free = Math.Clamp(item.Free, 0d, item.Total);
            var percent = item.Total <= 0 ? 0d : Math.Clamp(used / item.Total * 100d, 0d, 100d);
            return new StorageVolumeSnapshot(item.Drive.Name.TrimEnd('\\'), used, item.Total, free, percent);
        }).ToArray();

        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
        var selected = volumes.FirstOrDefault(item =>
            string.Equals(item.Name + Path.DirectorySeparatorChar, systemRoot, StringComparison.OrdinalIgnoreCase))
            ?? volumes[0];
        var summary = string.Join("   ·   ", volumes.Select(item =>
            $"{item.Name}  {item.UsedGiB:0.0}/{item.TotalGiB:0.0} GB"));
        return (selected.Name, selected.UsedGiB, selected.TotalGiB, selected.FreeGiB,
            selected.UsedPercent, summary, volumes);
    }

    private static double? NormalizePercent(double? value) =>
        value.HasValue && double.IsFinite(value.Value) ? Math.Clamp(value.Value, 0d, 100d) : null;

    private static double? NormalizeMemoryUsed(double? used, double? total)
    {
        if (!used.HasValue || !total.HasValue || !double.IsFinite(used.Value) ||
            !double.IsFinite(total.Value) || total.Value <= 0) return null;
        return Math.Clamp(used.Value, 0d, total.Value);
    }

    private static double? NormalizeMemoryTotal(double? total) =>
        total.HasValue && double.IsFinite(total.Value) && total.Value > 0 ? total : null;

    private static (double? UsedGiB, double? TotalGiB) ReadPhysicalMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0) return (null, null);
        const double bytesPerGiB = 1024d * 1024d * 1024d;
        var total = status.TotalPhysical / bytesPerGiB;
        var available = status.AvailablePhysical / bytesPerGiB;
        return (Math.Max(0, total - available), total);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _computer.Close();
        }
    }
}
