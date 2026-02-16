# VRXC Integration - Technical Changes

## Overview

This document details the technical changes made to integrate VRXC (VRx Controller) protocol support into FPVTracksideCore-ELRS.

## Branch

`vrxc-integration` (based on `elrs-integration`)

## Files Modified

### New Files

1. **`Timing/ELRS/VRXCProtocol.cs`** (NEW)
   - MSP v2 packet parser for ELRS Backpack protocol
   - Handles `MSP_ELRS_BACKPACK_SET_RECORDING_STATE` commands
   - Implements DVB-S2 CRC8 checksum validation
   - Events: `OnStartRaceCommand`, `OnStopRaceCommand`, `OnBackpackVersion`

2. **`docs/VRXC_INTEGRATION.md`** (NEW)
   - Complete user documentation
   - Hardware requirements
   - Setup instructions
   - Troubleshooting guide
   - Protocol technical details

3. **`docs/VRXC_QUICKSTART.md`** (NEW)
   - 5-minute quick start guide
   - Step-by-step setup
   - Common troubleshooting

4. **`docs/VRXC_CHANGES.md`** (NEW - this file)
   - Technical changelog
   - Implementation details

### Modified Files

1. **`Timing/ELRS/ELRSTimingSystem.cs`**
   - **Old**: Used `CRSFProtocol` to parse RC channel data
   - **New**: Uses `VRXCProtocol` to parse MSP command packets
   - **Key Changes**:
     - Replaced CRSF channel monitoring with MSP command handling
     - Changed from edge detection on channel values to direct command events
     - Removed channel-based trigger logic
     - Added backpack version tracking
     - Updated status display to show VRXC protocol info
     - Changed `MaxPilots` from 8 to 1 (race director control, not lap timing)

2. **`Timing/ELRS/ELRSSettings.cs`**
   - **Removed Settings**:
     - `TriggerChannel` (no longer parsing channels)
     - `ThresholdValue` (no longer using channel thresholds)
     - `TriggerOnHigh` (no longer using edge detection)
     - `VirtualReceivers` (VRXC is for race control, not lap timing)
   - **Kept Settings**:
     - `SerialPort` (still needed for USB connection)
     - `BaudRate` (still 420000 for MSP)
     - `DebounceMs` (still needed to prevent double triggers)
   - **Updated**:
     - Category names from "ELRS" to "VRXC"
     - Descriptions to reflect MSP protocol usage
     - ToString() to display "VRXC/ELRS"

### Unchanged Files

- `Timing/ELRS/CRSFProtocol.cs` - Kept for reference, not used
- `Timing/ITimingSystem.cs` - Interface unchanged
- All other Trackside core files

## Protocol Comparison

### Old CRSF Implementation

```
ELRS Receiver → CRSF Serial → Parse 16 channels → 
Check channel X value → Compare to threshold → 
Edge detection → Trigger race
```

**Data Format**: CRSF RC Channels Packed (0x16)
- 16 channels × 11 bits = 176 bits = 22 bytes
- Channel values: 172-1811 (CRSF range)
- Mapped to: 1000-2000µs (standard PWM)

### New VRXC Implementation

```
Transmitter DVR Switch → ESPNow → ESP32 Backpack → 
MSP Serial → Parse MSP packet → Extract command → 
Trigger race
```

**Data Format**: MSP v2 Command Packets
- Header: `$X<type><flags><function><length>`
- Payload: Command-specific data
- CRC: DVB-S2 CRC8 checksum

**Key Commands**:
```c#
MSP_ELRS_BACKPACK_SET_RECORDING_STATE (0x0305)
  Payload: 0x00 = Stop race
  Payload: 0x01 = Start race

MSP_ELRS_GET_BACKPACK_VERSION (0x0010)
  Response: Firmware version string
```

## Implementation Details

### VRXCProtocol.cs Architecture

```
┌─────────────────────────────────────────┐
│       Serial Port Reader Thread         │
│  (Continuous buffer fill & processing)  │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│        ProcessBuffer() Method           │
│  • Find MSP header ($X)                 │
│  • Extract packet length                │
│  • Verify CRC                           │
│  • Parse function & payload             │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│     ProcessMSPPacket() Method           │
│  • Switch on function code              │
│  • Decode payload                       │
│  • Fire appropriate event               │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│         Event Handlers                  │
│  • OnStartRaceCommand                   │
│  • OnStopRaceCommand                    │
│  • OnBackpackVersion                    │
│  • OnError                              │
└─────────────────────────────────────────┘
```

### MSP Packet Structure

```
Byte    Field           Description
----    -----           -----------
0       Header          '$' (0x24)
1       Header          'X' (0x58)
2       Type            '<' (0x3C) = Command, '>' (0x3E) = Response
3       Flags           Usually 0x00
4-5     Function        16-bit function code (little-endian)
6-7     Length          16-bit payload length (little-endian)
8...    Payload         Variable length data
N       CRC             DVB-S2 CRC8 checksum
```

### CRC8 DVB-S2 Algorithm

```c#
byte CRC8_DVB_S2(byte crc, byte data)
{
    crc ^= data;
    for (int i = 0; i < 8; i++)
    {
        if ((crc & 0x80) != 0)
            crc = (byte)((crc << 1) ^ 0xD5);
        else
            crc = (byte)(crc << 1);
    }
    return crc;
}
```

Polynomial: 0xD5
Checksum covers: flags, function, length, payload

### Debounce Logic

Both implementations use debounce to prevent double-triggers:

```c#
if ((now - lastTriggerTime).TotalMilliseconds < elrsSettings.DebounceMs)
{
    Logger.TimingLog.Log(this, "Debounce", "Command ignored (too soon)", 
                         Logger.LogType.Notice);
    return;
}
lastTriggerTime = now;
```

Default: 500ms (configurable)

## Testing Recommendations

### Unit Tests (TODO)

1. **MSP Packet Parsing**
   - Test valid packets with correct CRC
   - Test invalid packets with wrong CRC
   - Test incomplete packets (buffer edge cases)
   - Test multi-packet streams

2. **Command Processing**
   - Test START command (0x01 payload)
   - Test STOP command (0x00 payload)
   - Test version response parsing
   - Test unknown function codes

3. **Debounce Logic**
   - Test rapid command sequences
   - Verify debounce timing accuracy
   - Test concurrent command processing

### Integration Tests

1. **Hardware Tests**
   - Connect ESP32-DevKitC-1U
   - Flash VRXC firmware
   - Verify serial connection
   - Test command transmission

2. **End-to-End Tests**
   - Bind transmitter to timer backpack
   - Toggle DVR Rec switch
   - Verify race starts/stops in Trackside
   - Test under race conditions

### Regression Tests

Ensure existing Trackside functionality still works:
- Lap timing with other timing systems
- Race management (start/stop/save)
- Settings persistence
- Multi-pilot races

## Known Limitations

1. **Not Lap Timing**: VRXC is for race *control* (start/stop), not lap detection
   - Use with another timing system for actual lap times (ImmersionRC, RotorHazard, etc.)
   - VRXC triggers race for all pilots simultaneously

2. **Single Race Director**: Only one transmitter can control timing
   - Bind phrase must match exactly
   - No multi-director support (yet)

3. **USB Cable Required**: Not wireless
   - ESP32 must be USB-connected to timing computer
   - Range limited by USB cable length (use extension if needed)

4. **DVR Rec Switch**: Uses specific transmitter switch
   - Cannot customize which switch (hardcoded in backpack firmware)
   - Does not interfere with actual DVR recording

## Future Enhancements

### Potential Features

1. **Pilot Ready Status**
   - VRXC protocol supports pilot ready commands
   - Could integrate with Trackside pilot ready UI
   - MSP function code: `MSP_ELRS_BACKPACK_GET_RECORDING_STATE`

2. **OSD Integration**
   - Send race information to HDZero goggles
   - Race countdown, lap times, positions
   - MSP function code: `MSP_ELRS_SET_OSD`

3. **Bidirectional Commands**
   - Send data back to transmitter
   - Display race status on TX screen
   - Requires ELRS Lua script updates

4. **Multi-Director Support**
   - Allow multiple transmitters
   - Configurable permissions (start/stop/both)
   - Conflict resolution logic

5. **Wireless Option**
   - Serial-over-network bridge
   - WiFi or Ethernet connectivity
   - Remove USB cable requirement

## Compilation Notes

**Requirements**:
- .NET 9.0 SDK
- C# 12.0+
- NuGet packages:
  - `System.IO.Ports` (v6.0.0) - Already in project
  - `MonoGame.Framework.DesktopGL` (v3.8.4.1) - Already in project

**Build Commands**:
```bash
cd Timing
dotnet build
```

**Dependencies**:
- `Tools.csproj` (logger dependency)
- No new external dependencies added

## Migration from CRSF to VRXC

If you previously used CRSF channel-based timing:

### Settings Migration

| Old Setting | New Setting | Notes |
|------------|-------------|-------|
| Serial Port | Serial Port | Same - ESP32 USB port |
| Baud Rate | Baud Rate | Same - 420000 |
| Trigger Channel | ❌ Removed | Not used in VRXC |
| Threshold Value | ❌ Removed | Not used in VRXC |
| Trigger On High | ❌ Removed | Not used in VRXC |
| Debounce Ms | Debounce Ms | Same - prevent double triggers |
| Virtual Receivers | ❌ Removed | VRXC is race control, not timing |

### Firmware Migration

1. **Old**: ELRS receiver firmware (RX targets)
2. **New**: ELRS Backpack firmware (Backpack → RotorHazard targets)
3. **Important**: Must reflash ESP32 with correct target!

### Functionality Changes

| Feature | CRSF | VRXC |
|---------|------|------|
| Race start | Channel > threshold | DVR Rec switch UP |
| Race stop | Channel < threshold | DVR Rec switch DOWN |
| Lap detection | ❌ No | ❌ No (use other timing system) |
| Latency | ~5-10ms (RC frame rate) | ~1ms (direct serial) |
| Reliability | RF link dependent | USB wired (100%) |
| Setup complexity | Medium | Easy |

## Commit History

1. **Initial VRXC protocol implementation**
   - Added `VRXCProtocol.cs`
   - MSP v2 parser with CRC validation
   - Serial communication handling

2. **Updated ELRSTimingSystem for VRXC**
   - Replaced CRSF with VRXC protocol
   - Updated command handling
   - Simplified trigger logic

3. **Simplified ELRSSettings**
   - Removed channel-based settings
   - Updated UI labels for VRXC
   - Kept essential serial settings

4. **Added comprehensive documentation**
   - User guide (VRXC_INTEGRATION.md)
   - Quick start (VRXC_QUICKSTART.md)
   - Technical changes (this file)

## References

### External Documentation

- **VRXC GitHub**: https://github.com/i-am-grub/vrxc_elrs
- **VRXC Protocol Spec**: https://docs.google.com/document/d/1u3c7OTiO4sFL2snI-hIo-uRSLfgBK4h16UrbA08Pd6U
- **ExpressLRS Backpack**: https://www.expresslrs.org/hardware/backpack/
- **MSP Protocol**: MultiWii Serial Protocol v2
- **DVB-S2 CRC8**: Digital Video Broadcasting standard checksum

### Code References

Original Python implementation:
- `vrxc_elrs/custom_plugins/vrxc_elrs/elrs_backpack.py`
- `vrxc_elrs/custom_plugins/vrxc_elrs/msp.py`

C# implementation:
- `Timing/ELRS/VRXCProtocol.cs`
- `Timing/ELRS/ELRSTimingSystem.cs`

## Author

**macmittereder**
- GitHub: https://github.com/macmittereder
- Repository: https://github.com/macmittereder/FPVTracksideCore-ELRS
- Branch: `vrxc-integration`

## License

Same as FPV Trackside Core (see main LICENSE.md)
