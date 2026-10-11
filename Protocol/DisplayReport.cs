namespace WaterCoolerApp.Protocol;

using WaterCoolerApp.Models;

/// <summary>
/// Encodes the 11-byte payload observed in the original software.
/// Byte 5 (motherboard temperature) is zero because this app does not currently
/// expose a motherboard-temperature field in its telemetry snapshot.
/// </summary>
public static class DisplayReport
{
    public const int PayloadLength = 11;
    public const int CpuTemperatureIndex = 1;
    public const int GpuTemperatureIndex = 2;
    public const int CpuLoadIndex = 3;
    public const int MemoryLoadIndex = 4;
    public const int MotherboardTemperatureIndex = 5;
    public const int GpuLoadIndex = 6;

    public static byte[] Create(SensorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return CreateTelemetry(
            snapshot.CpuTemperatureC,
            snapshot.GpuTemperatureC,
            snapshot.CpuLoadPercent,
            GetMemoryLoadPercent(snapshot.MemoryUsedGiB, snapshot.MemoryTotalGiB),
            motherboardTemperatureC: null,
            snapshot.GpuLoadPercent);
    }

    public static byte[] CreateCpuTemperatureOnly(double temperatureCelsius) =>
        CreateTelemetry(temperatureCelsius, null, null, null, null, null);

    public static byte[] CreateTelemetry(
        double? cpuTemperatureCelsius,
        double? gpuTemperatureCelsius,
        double? cpuLoadPercent,
        double? memoryLoadPercent,
        double? motherboardTemperatureCelsius,
        double? gpuLoadPercent)
    {
        if (!IsInRange(cpuTemperatureCelsius, 0, 100))
            throw new ArgumentOutOfRangeException(nameof(cpuTemperatureCelsius),
                "A temperatura da CPU precisa ser válida e estar entre 0 e 100 °C.");

        var payload = new byte[PayloadLength];
        payload[0] = 0;
        payload[CpuTemperatureIndex] = ToByte(cpuTemperatureCelsius, 0, 100);
        payload[GpuTemperatureIndex] = ToByte(gpuTemperatureCelsius, 0, 150);
        payload[CpuLoadIndex] = ToByte(cpuLoadPercent, 0, 100);
        payload[MemoryLoadIndex] = ToByte(memoryLoadPercent, 0, 100);
        payload[MotherboardTemperatureIndex] = ToByte(motherboardTemperatureCelsius, 0, 150);
        payload[GpuLoadIndex] = ToByte(gpuLoadPercent, 0, 100);
        // Bytes 7–10 are reserved in the original protocol and remain zero.
        return payload;
    }

    private static double? GetMemoryLoadPercent(double? usedGiB, double? totalGiB)
    {
        if (usedGiB is not double used || totalGiB is not double total ||
            !double.IsFinite(used) || !double.IsFinite(total) || total <= 0 || used < 0)
            return null;
        return Math.Clamp(used / total * 100d, 0d, 100d);
    }

    private static bool IsInRange(double? value, double minimum, double maximum) =>
        value is double number && double.IsFinite(number) && number >= minimum && number <= maximum;

    private static byte ToByte(double? value, double minimum, double maximum) =>
        IsInRange(value, minimum, maximum) ? checked((byte)value!.Value) : (byte)0;
}
