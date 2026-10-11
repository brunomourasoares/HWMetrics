using System.Diagnostics;
using System.Globalization;
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
    private readonly HashSet<string> _loggedSensors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loggedHardwareErrors = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private long? _previousIdleTime;
    private long? _previousKernelTime;
    private long? _previousUserTime;
    private DateTime _lastStorageReadAt = DateTime.MinValue;
    private (string? SystemDriveName, double? UsedGiB, double? TotalGiB,
        double? FreeGiB, double? UsedPercent, string VolumesSummary,
        IReadOnlyList<StorageVolumeSnapshot> Volumes)? _cachedStorage;

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
            string? cpuModel = null, gpuModel = null, storageModel = null;
            string? storageModel = null;
            var cpuTempPriority = -1;
            var cpuLoadPriority = -1;
            var gpuTempPriority = -1;
            var gpuLoadPriority = -1;
            double cpuCoreLoadSum = 0;
            var cpuCoreLoadCount = 0;

            foreach (var hardware in _computer.Hardware)
                Visit(hardware, ref cpuTemp, ref cpuTempPriority, ref cpuLoad, ref cpuLoadPriority,
                    ref cpuCoreLoadSum, ref cpuCoreLoadCount,
                    ref gpuTemp, ref gpuTempPriority, ref gpuLoad, ref gpuLoadPriority,
                    ref usedGiB, ref totalGiB, ref driveTemp, ref cpuModel, ref gpuModel, ref storageModel);

            // If no aggregate CPU load sensor exists, report the mean of the per-core
            // readings rather than whichever core happened to appear first.
            if (cpuLoad is null && cpuCoreLoadCount > 0)
                cpuLoad = cpuCoreLoadSum / cpuCoreLoadCount;

            // Some motherboards expose the CPU thermistor through Super I/O instead of
            // the CPU hardware node. Visit() accepts those explicitly labelled CPU sensors
            // as a lower-priority fallback. CPU utilization also has a Windows API fallback.
            cpuLoad ??= ReadCpuLoadPercent();

            if (cpuTemp is null)
                Debug.WriteLine("[HardwareMonitoring] Nenhum sensor de temperatura da CPU forneceu um valor válido nesta leitura.");
    
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
    
            var now = DateTime.Now;
            if (_cachedStorage is null || now - _lastStorageReadAt >= TimeSpan.FromSeconds(5))
            {
                _cachedStorage = ReadStorage();
                _lastStorageReadAt = now;
            }

            var storage = _cachedStorage.Value;
            return new SensorSnapshot(cpuTemp, NormalizePercent(cpuLoad), gpuTemp, NormalizePercent(gpuLoad),
                NormalizeMemoryUsed(usedGiB, totalGiB), NormalizeMemoryTotal(totalGiB),
                driveTemp, cpuModel, gpuModel, storageModel, storage.SystemDriveName, storage.UsedGiB, storage.TotalGiB,
                storage.FreeGiB, storage.UsedPercent, storage.VolumesSummary, storage.Volumes,
                now, "Leitura concluída");
        }
    }

    private void Visit(IHardware hardware,
        ref double? cpuTemp, ref int cpuTempPriority,
        ref double? cpuLoad, ref int cpuLoadPriority,
        ref double cpuCoreLoadSum, ref int cpuCoreLoadCount,
        ref double? gpuTemp, ref int gpuTempPriority,
        ref double? gpuLoad, ref int gpuLoadPriority,
        ref double? usedGiB, ref double? totalGiB, ref double? driveTemp,
        ref string? cpuModel, ref string? gpuModel, ref string? storageModel)
    {
        try
        {
            if (hardware.HardwareType == HardwareType.Cpu) cpuModel ??= CleanModel(hardware.Name);
            else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel) gpuModel ??= CleanModel(hardware.Name);
            else if (hardware.HardwareType == HardwareType.Storage)
            {
                var candidate = CleanModel(hardware.Name);
                if (IsUsefulStorageModel(candidate))
                {
                    storageModel ??= candidate;
                }
            }
            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                var rawValue = sensor.Value;
                var sensorKey = $"{hardware.Identifier}|{sensor.Identifier}|{sensor.SensorType}";
                if (_loggedSensors.Add(sensorKey))
                {
                    var loggedValue = rawValue?.ToString("0.##", CultureInfo.InvariantCulture) ?? "null";
                    Debug.WriteLine($"[HardwareMonitoring] Sensor encontrado: hardware={hardware.HardwareType}, nome='{hardware.Name}', tipo={sensor.SensorType}, sensor='{sensor.Name}', valorInicial={loggedValue}");
                }
                if (rawValue is not float value || !float.IsFinite(value)) continue;
                var name = sensor.Name;
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        if (!IsValidTemperature(value)) break;
                        var cpuPriority = GetCpuTemperaturePriority(name, hardware.HardwareType);
                        if (cpuPriority >= 0 && cpuPriority > cpuTempPriority)
                        {
                            cpuTemp = value;
                            cpuTempPriority = cpuPriority;
                        }
                        else if (IsGpu(hardware.HardwareType))
                        {
                            var priority = GetGpuTemperaturePriority(name);
                            if (priority > gpuTempPriority) { gpuTemp = value; gpuTempPriority = priority; }
                        }
                        else if (hardware.HardwareType == HardwareType.Storage)
                            driveTemp ??= value;
                        break;

                    case SensorType.Load:
                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            var priority = GetCpuLoadPriority(name);
                            if (priority == 30)
                            {
                                cpuCoreLoadSum += value;
                                cpuCoreLoadCount++;
                            }
                            else if (priority >= 70 && priority > cpuLoadPriority)
                            {
                                cpuLoad = value;
                                cpuLoadPriority = priority;
                            }
                        }
                        else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                        {
                            var priority = GetGpuLoadPriority(name);
                            if (priority > gpuLoadPriority) { gpuLoad = value; gpuLoadPriority = priority; }
                        }
                        break;

                    case SensorType.Data when hardware.HardwareType == HardwareType.Memory:
                        if (name.Contains("used", StringComparison.OrdinalIgnoreCase)) usedGiB ??= value;
                        if (name.Contains("total", StringComparison.OrdinalIgnoreCase)) totalGiB ??= value;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            var hardwareKey = $"{hardware.Identifier}|{hardware.HardwareType}";
            if (_loggedHardwareErrors.Add(hardwareKey))
                Debug.WriteLine($"[HardwareMonitoring] Falha ao atualizar hardware '{hardware.Name}' ({hardware.HardwareType}): {ex}");
        }

        foreach (var child in hardware.SubHardware)
            Visit(child, ref cpuTemp, ref cpuTempPriority, ref cpuLoad, ref cpuLoadPriority,
                ref cpuCoreLoadSum, ref cpuCoreLoadCount,
                ref gpuTemp, ref gpuTempPriority, ref gpuLoad, ref gpuLoadPriority,
                ref usedGiB, ref totalGiB, ref driveTemp, ref cpuModel, ref gpuModel, ref storageModel);
    }


    private static bool IsGpu(HardwareType type) =>
        type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    internal static int GetCpuTemperaturePriority(string name, HardwareType hardwareType)
    {
        // Prefer direct CPU package/die/core sensors whenever available.
        if (hardwareType == HardwareType.Cpu)
        {
            if (name.Contains("package", StringComparison.OrdinalIgnoreCase)) return 100;
            if (name.Contains("tdie", StringComparison.OrdinalIgnoreCase)) return 95;
            if (name.Contains("tctl", StringComparison.OrdinalIgnoreCase)) return 90;
            if (name.Contains("core max", StringComparison.OrdinalIgnoreCase)) return 85;
            if (name.Contains("core average", StringComparison.OrdinalIgnoreCase) || name.Contains("average", StringComparison.OrdinalIgnoreCase)) return 80;
            if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) return 70;
            return 60;
        }

        // Some boards expose only a CPU-labelled socket/thermistor under Motherboard
        // or Super I/O. Do not mistake generic motherboard/VRM temperatures for CPU.
        if (hardwareType is HardwareType.Motherboard or HardwareType.SuperIO)
        {
            if (!name.Contains("cpu", StringComparison.OrdinalIgnoreCase) && !name.Contains("processor", StringComparison.OrdinalIgnoreCase)) return -1;
            if (name.Contains("package", StringComparison.OrdinalIgnoreCase)) return 55;
            if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) return 50;
            if (name.Contains("socket", StringComparison.OrdinalIgnoreCase)) return 45;
            return 40;
        }

        return -1;
    }

    internal static int GetCpuLoadPriority(string name)
    {
        if (name.Contains("total", StringComparison.OrdinalIgnoreCase)) return 100;
        if (name.Contains("average", StringComparison.OrdinalIgnoreCase)) return 70;
        // Test "core" before "cpu": names such as "CPU Core #1" are per-core
        // values and must not outrank an aggregate CPU utilization sensor.
        if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) return 30;
        if (name.Contains("cpu", StringComparison.OrdinalIgnoreCase)) return 80;
        return -1;
    }

    internal static bool IsValidTemperature(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 150;

    private static int GetGpuTemperaturePriority(string name)
    {
        if (name.Contains("gpu core", StringComparison.OrdinalIgnoreCase) || name.Contains("core temperature", StringComparison.OrdinalIgnoreCase)) return 100;
        if (name.Contains("gpu temperature", StringComparison.OrdinalIgnoreCase) || name.Equals("temperature", StringComparison.OrdinalIgnoreCase)) return 90;
        if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) return 80;
        if (name.Contains("junction", StringComparison.OrdinalIgnoreCase) || name.Contains("hot spot", StringComparison.OrdinalIgnoreCase) || name.Contains("hotspot", StringComparison.OrdinalIgnoreCase)) return 50;
        if (name.Contains("memory", StringComparison.OrdinalIgnoreCase)) return 20;
        return 10;
    }

    private static int GetGpuLoadPriority(string name)
    {
        if (name.Contains("total", StringComparison.OrdinalIgnoreCase)) return 100;
        if (name.Contains("core", StringComparison.OrdinalIgnoreCase)) return 80;
        if (name.Contains("gpu", StringComparison.OrdinalIgnoreCase)) return 70;
        return 10;
    }

    private static string? CleanModel(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static bool IsUsefulStorageModel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var value = name.Trim();
        return !value.Equals("Storage", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("Generic", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("Generic Storage", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("NVMe", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("S.M.A.R.T.", StringComparison.OrdinalIgnoreCase);
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

    private double? ReadCpuLoadPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return null;

        // Kernel time includes idle time on Windows.
        var currentIdle = idle;
        var currentKernel = kernel;
        var currentUser = user;
        var previousIdle = _previousIdleTime;
        var previousKernel = _previousKernelTime;
        var previousUser = _previousUserTime;
        _previousIdleTime = currentIdle;
        _previousKernelTime = currentKernel;
        _previousUserTime = currentUser;

        if (!previousIdle.HasValue || !previousKernel.HasValue || !previousUser.HasValue)
            return null;

        var idleDelta = currentIdle - previousIdle.Value;
        var totalDelta = (currentKernel - previousKernel.Value) + (currentUser - previousUser.Value);
        if (idleDelta < 0 || totalDelta <= 0 || idleDelta > totalDelta)
            return null;

        return Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0d, 100d);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

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
