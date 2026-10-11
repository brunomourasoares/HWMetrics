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
    private readonly HashSet<string> _loggedSensors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loggedHardwareErrors = new(StringComparer.OrdinalIgnoreCase);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        double? cpuTemp = null, cpuLoad = null, gpuTemp = null, gpuLoad = null;
        double? usedGiB = null, totalGiB = null, driveTemp = null;
        var cpuTempPriority = -1;
        var cpuLoadPriority = -1;
        var gpuTempPriority = -1;
        var gpuLoadPriority = -1;

        foreach (var hardware in _computer.Hardware)
            Visit(hardware, ref cpuTemp, ref cpuTempPriority, ref cpuLoad, ref cpuLoadPriority,
                ref gpuTemp, ref gpuTempPriority, ref gpuLoad, ref gpuLoadPriority,
                ref usedGiB, ref totalGiB, ref driveTemp);

        if (cpuTemp is null)
            Debug.WriteLine("[HardwareMonitoring] Nenhum sensor de temperatura da CPU forneceu um valor válido nesta leitura.");

        // LibreHardwareMonitor does not expose physical memory consistently on every system.
        // Use the Windows memory API as a reliable fallback for the RAM card.
        if (usedGiB is null || totalGiB is null)
        {
            var memory = ReadPhysicalMemory();
            usedGiB = memory.UsedGiB;
            totalGiB = memory.TotalGiB;
        }

        var storage = ReadStorage();
        return new SensorSnapshot(cpuTemp, cpuLoad, gpuTemp, gpuLoad, usedGiB, totalGiB,
            driveTemp, storage.SystemDriveName, storage.UsedGiB, storage.TotalGiB,
            storage.FreeGiB, storage.UsedPercent, storage.VolumesSummary,
            DateTime.Now, "Leitura concluída");
    }

    private void Visit(IHardware hardware,
        ref double? cpuTemp, ref int cpuTempPriority,
        ref double? cpuLoad, ref int cpuLoadPriority,
        ref double? gpuTemp, ref int gpuTempPriority,
        ref double? gpuLoad, ref int gpuLoadPriority,
        ref double? usedGiB, ref double? totalGiB, ref double? driveTemp)
    {
        try
        {
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

                if (rawValue is not float value || !float.IsFinite(value))
                    continue;

                var name = sensor.Name.ToLowerInvariant();
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            var priority = GetCpuTemperaturePriority(name);
                            if (priority > cpuTempPriority)
                            {
                                cpuTemp = value;
                                cpuTempPriority = priority;
                            }
                        }
                        else if (IsGpu(hardware.HardwareType))
                        {
                            var priority = GetGpuTemperaturePriority(name);
                            if (priority > gpuTempPriority)
                            {
                                gpuTemp = value;
                                gpuTempPriority = priority;
                            }
                        }
                        else if (hardware.HardwareType == HardwareType.Storage)
                        {
                            driveTemp ??= value;
                        }
                        break;

                    case SensorType.Load:
                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            var priority = GetCpuLoadPriority(name);
                            if (priority > cpuLoadPriority)
                            {
                                cpuLoad = value;
                                cpuLoadPriority = priority;
                            }
                        }
                        else if (IsGpu(hardware.HardwareType))
                        {
                            var priority = GetGpuLoadPriority(name);
                            if (priority > gpuLoadPriority)
                            {
                                gpuLoad = value;
                                gpuLoadPriority = priority;
                            }
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
            Visit(child, ref cpuTemp, ref cpuTempPriority, ref cpuLoad, ref cpuLoadPriority,
                ref gpuTemp, ref gpuTempPriority, ref gpuLoad, ref gpuLoadPriority,
                ref usedGiB, ref totalGiB, ref driveTemp);
    }

    private static bool IsGpu(HardwareType type) =>
        type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static int GetCpuTemperaturePriority(string name)
    {
        if (name.Contains("package")) return 100;
        if (name.Contains("tdie")) return 95;
        if (name.Contains("tctl")) return 90;
        if (name.Contains("core max")) return 85;
        if (name.Contains("core average") || name.Contains("average")) return 80;
        if (name.Contains("core")) return 70;
        if (name.Contains("cpu")) return 60;
        return 10;
    }

    private static int GetCpuLoadPriority(string name)
    {
        if (name.Contains("total")) return 100;
        if (name.Contains("cpu")) return 80;
        return 10;
    }

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

    private static (string? SystemDriveName, double? UsedGiB, double? TotalGiB,
        double? FreeGiB, double? UsedPercent, string VolumesSummary) ReadStorage()
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
            return (null, null, null, null, null, "No local volumes detected");

        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
        var selected = drives.FirstOrDefault(item =>
            string.Equals(item.Drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase));
        if (selected.Drive is null) selected = drives[0];

        var summary = string.Join("   ·   ", drives.Select(item =>
            $"{item.Drive.Name.TrimEnd('\\')}  {item.Used:0.0}/{item.Total:0.0} GB"));
        var percent = selected.Total <= 0 ? 0 : Math.Clamp(selected.Used / selected.Total * 100d, 0d, 100d);
        return (selected.Drive.Name.TrimEnd('\\'), selected.Used, selected.Total, selected.Free, percent, summary);
    }

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
        if (_disposed) return;
        _disposed = true;
        _computer.Close();
    }
}
