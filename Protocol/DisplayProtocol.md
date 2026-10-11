# Display protocol notes

Reverse engineering of the original software indicates HID VID `0xAA88`, PID `0x8666`, and an 11-byte telemetry payload recovered from the original `tifPipe` path. The payload fields are:

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

The original software's legacy HID writer prepended Report ID 0 in its output path. However, the physical device interface discovered during validation advertises an 11-byte output report, and the app was verified against that interface by writing the 11-byte payload directly. The current transport therefore requires an output report length of at least 11 bytes and writes exactly the 11-byte payload without adding a separate byte. Do not change this framing without testing against the physical cooler.

The app reads sensors, encodes one snapshot, and sends it directly to the matching HID device. A successful Windows HID write only means the write completed without an exception; it does not prove that the physical display rendered the values. Validation on the actual cooler is still required. Fan and pump control commands are not implemented because the analyzed telemetry packet does not establish a safe control protocol.
