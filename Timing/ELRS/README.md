# ELRS Timing System - VRXC Protocol

## Overview

This timing system integrates FPV Trackside with **VRXC (VRx Controller)** protocol via ELRS Backpack, enabling race directors to control races from their transmitter.

## Files

### Active Implementation (VRXC)
- **`VRXCProtocol.cs`** - MSP v2 protocol parser for ELRS Backpack commands
- **`ELRSTimingSystem.cs`** - Main timing system integration (uses VRXC)
- **`ELRSSettings.cs`** - Configuration settings for serial connection

### Reference (Legacy)
- **`CRSFProtocol.cs`** - Legacy CRSF/RC channel parser (not used in VRXC mode)

## Quick Start

1. **Flash ESP32** with ELRS Backpack firmware (RotorHazard target)
2. **Connect** ESP32 via USB to timing computer
3. **Configure** Trackside:
   - Timing System: "ELRS/VRXC"
   - Serial Port: Select ESP32 port (e.g., COM3)
   - Baud Rate: 420000
4. **Test**: Toggle DVR Rec switch on transmitter

## Documentation

See `docs/` folder:
- **`VRXC_QUICKSTART.md`** - 5-minute setup guide
- **`VRXC_INTEGRATION.md`** - Complete user guide
- **`VRXC_CHANGES.md`** - Technical implementation details

## Protocol

### MSP Commands
- `MSP_ELRS_BACKPACK_SET_RECORDING_STATE (0x0305)`
  - `0x01` = Start race
  - `0x00` = Stop race
- `MSP_ELRS_GET_BACKPACK_VERSION (0x0010)`
  - Returns firmware version string

### Packet Format
```
$X<type><flags><function_lo><function_hi><length_lo><length_hi><payload><crc>
```

### CRC
DVB-S2 CRC8 (polynomial: 0xD5)

## Architecture

```
Race Director Transmitter (DVR Rec Switch)
            ↓
      ESPNow Protocol
            ↓
ESP32 Backpack (USB-connected to computer)
            ↓
    MSP Serial Protocol (420000 baud)
            ↓
    VRXCProtocol.cs (parses packets)
            ↓
    ELRSTimingSystem.cs (triggers events)
            ↓
  FPV Trackside (race start/stop)
```

## Dependencies

- `System.IO.Ports` (v6.0.0) - Serial port communication
- `Tools.csproj` - Logging utilities

## Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Serial Port | Auto | COM port for ESP32 (e.g., COM3, /dev/ttyUSB0) |
| Baud Rate | 420000 | Must match ELRS Backpack baud rate |
| Debounce (ms) | 500 | Minimum time between race commands |

## Known Limitations

- **Race Control Only**: Does not detect lap times (use with lap timing system)
- **Single Director**: One transmitter controls timing
- **USB Required**: Not wireless (ESP32 must be USB-connected)
- **DVR Rec Switch**: Uses specific transmitter switch (hardcoded in firmware)

## Troubleshooting

### Connection Failed
- Check serial port name
- Verify USB cable supports data (not charge-only)
- On Linux: Add user to `dialout` group
- Try different USB port

### No Commands Received
- Verify backpack bind phrase matches transmitter
- Check DVR Rec switch configured in ELRS Lua script
- Start detection before toggling switch
- Check Trackside logs for errors

### Double Triggers
- Increase debounce time (try 1000ms)
- Check switch for mechanical bounce
- Verify only one transmitter bound

## Future Features

- Pilot ready status monitoring
- OSD messages to HDZero goggles
- Bidirectional telemetry
- Multi-director support
- Wireless connection option

## References

- **VRXC GitHub**: https://github.com/i-am-grub/vrxc_elrs
- **ELRS Backpack**: https://www.expresslrs.org/hardware/backpack/
- **Protocol Spec**: https://docs.google.com/document/d/1u3c7OTiO4sFL2snI-hIo-uRSLfgBK4h16UrbA08Pd6U

## License

Same as FPV Trackside Core
