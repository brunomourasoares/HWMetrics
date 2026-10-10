# Display protocol notes

The supplied decompilation indicates VID `0xAA88`, PID `0x8666`, and an 11-byte buffer. Byte 1 is populated from the CPU temperature cast to a byte in the original application. This project includes a payload builder for analysis only. It deliberately does not open the HID device or send reports until report length, firmware interpretation, and safe behavior have been verified on the actual cooler.

The current display panel is a visual preview of CPU temperature only. Fan and pump controls are intentionally not active because the analyzed packet does not establish valid control commands.
