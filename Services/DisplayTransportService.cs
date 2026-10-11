using System.IO;
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
/// Sends the observed telemetry payload to the USB/HID display.
/// Fan and pump control commands are intentionally not implemented.
/// </summary>
public sealed class DisplayTransportService : IDisposable
{
    public const int VendorId = 0xAA88;
    public const int ProductId = 0x8666;

    // The device currently advertises an 11-byte output report.
    public const int RequiredOutputReportLength = DisplayReport.PayloadLength;

    private readonly object _sync = new();
    private HidStream? _stream;
    private int _outputReportLength;
    private bool _disposed;

    private static void WriteHidDiagnostic(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "WaterCoolerApp");

            Directory.CreateDirectory(directory);

            var logPath = Path.Combine(directory, "hid-diagnostic.log");

            File.AppendAllText(
                logPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging failures must not interrupt HID communication.
        }
    }

    public DisplayTransportStatus Send(SensorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.CpuTemperatureC is not double cpuTemperature ||
            !double.IsFinite(cpuTemperature) ||
            cpuTemperature is < 0 or > 100)
        {
            WriteHidDiagnostic("CPU temperature unavailable or invalid.");
            return DisplayTransportStatus.InvalidTelemetry;
        }

        byte[] payload;

        try
        {
            payload = DisplayReport.Create(snapshot);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            WriteHidDiagnostic($"Invalid telemetry payload: {ex.Message}");
            return DisplayTransportStatus.InvalidTelemetry;
        }

        lock (_sync)
        {
            if (_disposed)
                return DisplayTransportStatus.Error;

            try
            {
                if (_stream is null)
                {
                    var devices = DeviceList.Local
                        .GetHidDevices(VendorId, ProductId)
                        .ToArray();

                    WriteHidDiagnostic(
                        $"Found {devices.Length} HID interface(s) for " +
                        $"VID=0x{VendorId:X4}, PID=0x{ProductId:X4}.");

                    if (devices.Length == 0)
                        return DisplayTransportStatus.DeviceNotFound;

                    var foundCompatibleReport = false;
                    var inspectionFailed = false;
                    var opened = false;

                    foreach (var device in devices)
                    {
                        try
                        {
                            var outputLength =
                                device.GetMaxOutputReportLength();

                            var inputLength =
                                device.GetMaxInputReportLength();

                            WriteHidDiagnostic(
                                $"Path={device.DevicePath}, " +
                                $"VID=0x{device.VendorID:X4}, " +
                                $"PID=0x{device.ProductID:X4}, " +
                                $"Output={outputLength}, " +
                                $"Input={inputLength}, " +
                                $"Required={RequiredOutputReportLength}");

                            if (outputLength != RequiredOutputReportLength)
                            {
                                WriteHidDiagnostic(
                                    $"Interface skipped: output report length " +
                                    $"{outputLength} does not match the required exact length " +
                                    $"of {RequiredOutputReportLength} bytes.");

                                continue;
                            }

                            foundCompatibleReport = true;

                            if (!device.TryOpen(out var openedStream))
                            {
                                WriteHidDiagnostic(
                                    $"Could not open HID interface: {device.DevicePath}");

                                continue;
                            }

                            _stream = openedStream;
                            _outputReportLength = outputLength;
                            opened = true;

                            WriteHidDiagnostic(
                                $"HID interface opened. Output report length: " +
                                $"{_outputReportLength} bytes.");

                            break;
                        }
                        catch (Exception ex)
                        {
                            inspectionFailed = true;

                            WriteHidDiagnostic(
                                $"HID interface inspection failed: {ex}");
                        }
                    }

                    if (!opened)
                    {
                        if (foundCompatibleReport)
                        {
                            WriteHidDiagnostic(
                                "Compatible output report found, but opening " +
                                "the interface failed.");

                            return DisplayTransportStatus.OpenFailed;
                        }

                        if (inspectionFailed)
                        {
                            WriteHidDiagnostic(
                                "HID interface inspection failed for one or " +
                                "more interfaces.");

                            return DisplayTransportStatus.Error;
                        }

                        WriteHidDiagnostic(
                            "No interface has a sufficiently large output report.");

                        return DisplayTransportStatus.IncompatibleReport;
                    }
                }

                var stream = _stream;

                if (stream is null)
                {
                    WriteHidDiagnostic(
                        "HID stream is unexpectedly null before writing.");

                    return DisplayTransportStatus.Error;
                }

                // The physical device advertises an 11-byte output report.
                // This sends the 11-byte payload that was verified on the device.
                if (_outputReportLength != DisplayReport.PayloadLength)
                {
                    WriteHidDiagnostic(
                        $"Unexpected output report length: {_outputReportLength}; " +
                        $"expected {DisplayReport.PayloadLength} bytes.");

                    ResetStream();
                    return DisplayTransportStatus.IncompatibleReport;
                }

                stream.Write(payload, 0, payload.Length);
                stream.Flush();

                WriteHidDiagnostic(
                    $"HID write completed without exception. " +
                    $"Bytes={payload.Length}. Device acknowledgement not verified.");

                return DisplayTransportStatus.Sent;
            }
            catch (Exception ex)
            {
                WriteHidDiagnostic($"HID communication failed: {ex}");

                ResetStream();

                return DisplayTransportStatus.Error;
            }
        }
    }

    private void ResetStream()
    {
        try
        {
            _stream?.Dispose();
        }
        catch (Exception ex)
        {
            WriteHidDiagnostic(
                $"Error disposing HID stream: {ex.Message}");
        }

        _stream = null;
        _outputReportLength = 0;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            ResetStream();
        }
    }
}
