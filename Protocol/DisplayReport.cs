namespace WaterCoolerApp.Protocol;

/// <summary>Builds the observed 11-byte payload only; intentionally does not transmit it.</summary>
public static class DisplayReport
{
    public const int PayloadLength = 11;
    public const int CpuTemperatureIndex = 1;
    public static byte[] CreateCpuTemperatureOnly(double temperatureCelsius)
    {
        if (!double.IsFinite(temperatureCelsius) || temperatureCelsius is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(temperatureCelsius), "Temperatura fora da faixa esperada de 0 a 100 °C.");
        var payload = new byte[PayloadLength];
        payload[0] = 0;
        payload[CpuTemperatureIndex] = checked((byte)temperatureCelsius); // reproduz o cast do software original
        return payload;
    }
}
