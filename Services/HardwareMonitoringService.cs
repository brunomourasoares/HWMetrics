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
            var storageModels = new List<string>();
            var cpuTempPriority = -1;
            var cpuLoadPriority = -1;
            var gpuTempPriority = -1;
            var gpuLoadPriority = -1;
            var cpuCoreLoads = new List<double>();

            foreach (var hardware in _computer.Hardware)
                Visit(hardware, ref cpuTemp, ref cpuTempPriority, ref cpuLoad, ref cpuLoadPriority,
                    cpuCoreLoads, ref gpuTemp, ref gpuTempPriority, ref gpuLoad, ref gpuLoadPriority,
                    ref usedGiB, ref totalGiB, ref driveTemp, ref cpuModel, ref gpuModel, ref storageModel, storageModels);

            // If no aggregate CPU load sensor exists, report the mean of the per-core
            // readings rather than whichever core happened to appear first.
            if (cpuLoad is null && cpuCoreLoads.Count > 0)
                cpuLoad = cpuCoreLoads.Average();

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
    
            var storage = ReadStorage();
            return new SensorSnapshot(cpuTemp, NormalizePercent(cpuLoad), gpuTemp, NormalizePercent(gpuLoad),
                NormalizeMemoryUsed(usedGiB, totalGiB), NormalizeMemoryTotal(totalGiB),
                driveTemp, cpuModel, gpuModel, storageModels.FirstOrDefault() ?? storageModel, storage.SystemDriveName, storage.UsedGiB, storage.TotalGiB,
                storage.FreeGiB, storage.UsedPercent, storage.VolumesSummary, storage.Volumes,
                DateTime.Now, "Leitura concluída");
        }
    }

    private void Visit(IHardware hardware,
        ref double? cpuTemp, ref int cpuTempPriority,
        ref double? cpuLoad, ref int cpuLoadPriority, List<double> cpuCoreLoads,
        ref double? gpuTemp, ref int gpuTempPriority,
        ref double? gpuLoad, ref int gpuLoadPriority,
        ref double? usedGiB, ref double? totalGiB, ref double? driveTemp,
        ref string? cpuModel, ref string? gpuModel, ref string? storageModel, List<string> storageModels)
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
                    if (!storageModels.Contains(candidate!, StringComparer.OrdinalIgnoreCase)) storageModels.Add(candidate!);
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
                var name = sensor.Name.ToLowerInvariant();
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
                                cpuCoreLoads.Add(value);
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
                        if (name.Contains("used")) usedGiB ??= value;
                        if (name.Contains("total")) totalGiB ??= value;
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
            Visit(child, ref cpuTemp, ref cpuTempPriority, ref cpuLoad, ref cpuLoadPriority, cpuCoreLoads,
                ref gpuTemp, ref gpuTempPriority, ref gpuLoad, ref gpuLoadPriority,
                ref usedGiB, ref totalGiB, ref driveTemp, ref cpuModel, ref gpuModel, ref storageModel, storageModels);
    }


    private static bool IsGpu(HardwareType type) =>
        type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    internal static int GetCpuTemperaturePriority(string name, HardwareType hardwareType)
    {
        // Prefer direct CPU package/die/core sensors whenever available.
        if (hardwareType == HardwareType.Cpu)
        {
            if (name.Contains("package")) return 100;
            if (name.Contains("tdie")) return 95;
            if (name.Contains("tctl")) return 90;
            if (name.Contains("core max")) return 85;
            if (name.Contains("core average") || name.Contains("average")) return 80;
            if (name.Contains("core")) return 70;
            return 60;
        }

        // Some boards expose only a CPU-labelled socket/thermistor under Motherboard
        // or Super I/O. Do not mistake generic motherboard/VRM temperatures for CPU.
        if (hardwareType is HardwareType.Motherboard or HardwareType.SuperIO)
        {
            if (!name.Contains("cpu") && !name.Contains("processor")) return -1;
            if (name.Contains("package")) return 55;
            if (name.Contains("core")) return 50;
            if (name.Contains("socket")) return 45;
            return 40;
        }

        return -1;
    }

    internal static int GetCpuLoadPriority(string name)
    {
        if (name.Contains("total")) return 100;
        if (name.Contains("average")) return 70;
        // Test "core" before "cpu": names such as "CPU Core #1" are per-core
        // values and must not outrank an aggregate CPU utilization sensor.
        if (name.Contains("core")) return 30;
        if (name.Contains("cpu")) return 80;
        return -1;
    }

    internal static bool IsValidTemperature(double value) =>
        double.IsFinite(value) && value is >= 0 and <= 150;

    private static int GetGpuTemperaturePriority(string name)
    {
        if (name.Contains("gpu core") || name.Contains("core temperature")) return 100;
        if (name.Contains("gpu temperature") || name == "temperature") return 90;
        if (name.Contains("core")) return 80;
        if (name.Contains("junction") || name.Contains("hot spot") || name.Contains("hotspot")) return 50;
        if (name.Contains("memory")) return 20;
        return 10;
    }

    private static int GetGpuLoadPriority(string name)
    {
        if (name.Contains("total")) return 100;
        if (name.Contains("core")) return 80;
        if (name.Contains("gpu")) return 70;
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
