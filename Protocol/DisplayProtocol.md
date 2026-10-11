# Display protocol notes

Reverse engineering of the original software indicates HID VID `0xAA88`, PID `0x8666`, and an 11-byte telemetry payload sent through the `tifPipe` named pipe. The original payload fields are:

| Payload byte | Meaning | Current app behavior |
|---:|---|---|
| 0 | Header/reserved | 0 |
| 1 | CPU temperature in °C | Required; values outside 0–100 °C are not sent |
| 2 | GPU temperature in °C | Valid 0–150 °C reading, otherwise 0 |
| 3 | CPU utilization % | Valid 0–100% reading, otherwise 0 |
| 4 | Memory utilization % | Calculated from used/total memory, otherwise 0 |
| 5 | Motherboard temperature in °C | 0; the current snapshot does not expose this value |
| 6 | GPU utilization % | Valid 0–100% reading, otherwise 0 |
| 7–10 | Reserved | 0 |

The legacy HID writer prepended Report ID 0 to the 11-byte payload. The app therefore discovers the matching HID device, queries its maximum output report length, requires at least 12 bytes, then writes a report containing Report ID 0 followed by the payload. If the device is absent, cannot be opened, or reports an incompatible output length, transmission is not attempted. After a write error the stream is closed so the next polling cycle can try to reconnect.

This reconstructs the observed telemetry path without the original multi-process arrangement: the app reads sensors, encodes the payload, and sends it directly to HID. A successful Windows HID write does not prove that the physical display rendered the values; the cooler must still be tested on hardware. Fan and pump control commands are not implemented because the analyzed telemetry packet does not establish a safe control protocol.
