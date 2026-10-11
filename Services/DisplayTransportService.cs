using HidSharp;
using WaterCoolerApp.Models;
using WaterCoolerApp.Protocol;

namespace WaterCoolerApp.Services;

public enum DisplayTransportStatus
{
    Sent,
    DeviceNotFound,
    InvalidTelemetry,
    IncompatibleReport,
    OpenFailed,
    Error
}

/// <summary>
/// Owns the USB/HID output connection. It only sends the observed telemetry packet;
/// fan and pump control commands are intentionally not implemented.
/// </summary>
public sealed class DisplayTransportService : IDisposable
{
    public const int VendorId = 0xAA88;
    public const int ProductId = 0x8666;
    public const int MinimumOutputReportLength = DisplayReport.PayloadLength + 1; // Report ID + payload

    private readonly object _sync = new();
    private HidStream? _stream;
    private int _outputReportLength;
    private bool _disposed;

    public DisplayTransportStatus Send(SensorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Never send an invented zero-degree reading when the CPU sensor is unavailable.
        if (snapshot.CpuTemperatureC is not double cpuTemperature ||
            !double.IsFinite(cpuTemperature) || cpuTemperature is < 0 or > 100)
            return DisplayTransportStatus.InvalidTelemetry;

        var payload = DisplayReport.Create(snapshot);
        lock (_sync)
        {
            if (_disposed) return DisplayTransportStatus.Error;
            try
            {
                if (_stream is null)
                {
                    var device = DeviceList.Local.GetHidDevices(VendorId, ProductId).FirstOrDefault();
                    if (device is null) return DisplayTransportStatus.DeviceNotFound;

                    _outputReportLength = device.GetMaxOutputReportLength();
                    if (_outputReportLength < MinimumOutputReportLength)
                        return DisplayTransportStatus.IncompatibleReport;

                    if (!device.TryOpen(out var openedStream))
                        return DisplayTransportStatus.OpenFailed;

                    _stream = openedStream;
                }

                // HID output reports include the Report ID as byte 0. The legacy
                // sender prepended Report ID 0 to the 11-byte pipe payload.
                var report = new byte[_outputReportLength];
                report[0] = 0;
                Buffer.BlockCopy(payload, 0, report, 1, payload.Length);
                _stream.Write(report, 0, report.Length);
                _stream.Flush();
                return DisplayTransportStatus.Sent;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DisplayTransport] HID write failed: {ex}");
                ResetStream();
                return DisplayTransportStatus.Error;
            }
        }
    }

    private void ResetStream()
    {
        try { _stream?.Dispose(); }
        catch (Exception) { }
        _stream = null;
        _outputReportLength = 0;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            ResetStream();
        }
    }
}
