# VRXC Integration for FPV Trackside

## Overview

FPV Trackside now supports **VRXC (VRx Controller)** protocol for race timing control via ELRS Backpack. This allows race directors to start and stop races directly from their transmitter using the DVR Recording switch, which communicates with the timing system through an ESP32 running VRXC/ELRS Backpack firmware.

## What is VRXC?

VRXC is an ExpressLRS Backpack protocol extension that enables bidirectional communication between race timing systems (like RotorHazard or FPV Trackside) and FPV equipment. It uses MSP (MultiWii Serial Protocol) over a serial connection to send commands and data.

### Key Features

- **Race Director Control**: Start/stop races from transmitter using DVR Rec switch
- **No RF Timing Required**: Control commands are sent via serial MSP, not RC channels
- **Integration with ELRS Ecosystem**: Works with existing ELRS Backpack hardware
- **Simple Setup**: Just flash firmware and connect via USB

## Hardware Requirements

### Supported ESP32 Devices

Any ESP32 development board compatible with ELRS Backpack firmware:

| Device | Notes |
|--------|-------|
| **ESP32-DevKitC-1U** | ✅ Recommended - External antenna support |
| ESP32-S3-DevKitC-1U | ✅ Works well, newer hardware |
| ESP32-C3-DevKitM-1U | ✅ Compact option |
| NuclearHazard Board | ✅ Purpose-built timing hardware |
| Generic ESP32 boards | ⚠️ May work but antenna performance varies |

**Recommended**: ESP32-DevKitC-1U with external antenna connector for best range.

### Required Equipment

1. **ESP32 Board** (see above)
2. **USB Cable** (USB-A to Micro-USB or USB-C depending on board)
3. **Transmitter with ELRS Backpack** (configured with DVR Rec switch)
4. **Computer running FPV Trackside**

## Firmware Installation

### Step 1: Flash VRXC/ELRS Backpack Firmware

You have two options for flashing:

#### Option A: ExpressLRS Configurator (Recommended)

1. Download and install [ExpressLRS Configurator](https://github.com/ExpressLRS/ExpressLRS-Configurator/releases)
2. Connect your ESP32 board via USB
3. Open ExpressLRS Configurator
4. Select **"Backpack"** from the left menu
5. Choose firmware version **1.5.0 or newer**
6. Select device category: **"RotorHazard"**
7. Select your specific device target (e.g., "ESP32 DevKitC")
8. Select flashing method: **"UART"**
9. Enter your **Backpack Bind Phrase** (must match race director's transmitter backpack)
10. Select the COM port for your ESP32
11. Click **"Build & Flash"**

#### Option B: ExpressLRS Web Flasher

1. Visit [ExpressLRS Web Flasher](https://expresslrs.github.io/web-flasher/)
2. Connect ESP32 via USB
3. Select **"Race Timer"** under Backpack Firmware section
4. Choose version **1.5.0+**
5. Select **"RotorHazard"** device category
6. Select your device target
7. Enter **Backpack Bind Phrase**
8. Select COM port and flash

### Step 2: Bind Backpack to Transmitter

The backpack must be bound to your race director's transmitter backpack:

**Important**: The backpack bind phrase you entered during flashing must match the transmitter's backpack bind phrase.

#### Verify Binding

1. Connect ESP32 to FPV Trackside computer via USB
2. In FPV Trackside, configure ELRS timing system (see below)
3. Open ELRS Lua script on transmitter
4. Navigate to **Backpack** settings
5. Set **DVR Rec** to an available AUX channel
6. Test by toggling the DVR Rec switch - you should see commands in Trackside logs

## FPV Trackside Configuration

### Enable VRXC Timing System

1. Launch **FPV Trackside**
2. Go to **Settings** → **Timing System**
3. Select **"ELRS/VRXC"** from the timing system dropdown
4. Click **Settings** to configure

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| **Serial Port** | COM port connected to ESP32 (e.g., COM3, /dev/ttyUSB0) | Auto-detected |
| **Baud Rate** | Serial communication speed (must match backpack) | 420000 |
| **Debounce (ms)** | Minimum time between commands to prevent double-triggers | 500 |

### Finding Your Serial Port

**Windows:**
- Open Device Manager → Ports (COM & LPT)
- Look for "USB Serial Device" or "CP210x" or "CH340"
- Note the COM port number (e.g., COM3, COM4)

**Linux:**
- Run: `ls /dev/ttyUSB* /dev/ttyACM*`
- Usually `/dev/ttyUSB0` or `/dev/ttyACM0`
- You may need to add your user to dialout group: `sudo usermod -a -G dialout $USER`

**macOS:**
- Run: `ls /dev/cu.usb*`
- Usually `/dev/cu.usbserial-*` or `/dev/cu.SLAB_USBtoUART`

### Connection Test

1. Click **Connect** in Timing System settings
2. Check the status indicators:
   - ✅ "Connected (COMx)" - Serial connection successful
   - ✅ "Backpack: 1.x.x" - Firmware version detected
   - ✅ "VRXC Protocol (MSP)" - Protocol active

## Usage

### Starting a Race from Transmitter

1. **Setup Race**: Configure pilots and race settings in Trackside as normal
2. **Arm Race**: Click "Start Detection" or ready the race
3. **Trigger from TX**: 
   - Flip the **DVR Rec switch UP** on your transmitter
   - This sends the START command via VRXC
   - Race begins immediately

### Stopping a Race from Transmitter

1. While race is running
2. **Trigger from TX**:
   - Flip the **DVR Rec switch DOWN** on your transmitter
   - This sends the STOP command via VRXC
   - Race stops immediately

### DVR Rec Switch Behavior

The DVR Rec switch on your transmitter backpack controls race timing:

| Switch Position | VRXC Command | Trackside Action |
|----------------|--------------|------------------|
| **UP (Armed)** | `RECORDING_STATE_START (0x01)` | Start race (all pilots) |
| **DOWN (Disarmed)** | `RECORDING_STATE_STOP (0x00)` | Stop race |

**Note**: This does NOT interfere with actual DVR recording functionality - it just monitors switch state.

## Troubleshooting

### Problem: "Connection Failed"

**Solutions:**
- Verify ESP32 is connected via USB
- Check serial port name is correct
- Try different USB cable (some are charge-only)
- On Linux, ensure user has dialout permissions
- Restart FPV Trackside

### Problem: "No Backpack Version Detected"

**Solutions:**
- Firmware may not be flashed correctly - reflash with RotorHazard target
- Baud rate mismatch - ensure 420000 on both sides
- Try unplugging/replugging USB
- Check USB drivers are installed (CP210x or CH340 drivers)

### Problem: "Commands Not Working"

**Solutions:**
- Verify backpack bind phrases match (timer and transmitter)
- Check DVR Rec switch is configured in transmitter ELRS Lua script
- Enable "Listening for race commands" by starting detection
- Check Trackside logs for command reception
- Verify debounce setting isn't too high

### Problem: "Double Triggers"

**Solutions:**
- Increase debounce time (try 1000ms)
- Ensure switch has good detents (not bouncing mechanically)
- Check transmitter isn't sending duplicate commands

### Problem: "Port Already in Use"

**Solutions:**
- Close other programs using the serial port (RotorHazard, terminal, etc.)
- Disconnect and reconnect ESP32
- Restart computer

## Protocol Details

### MSP Packet Structure

VRXC uses MSP v2 packets:

```
$X<type><flags><function_lo><function_hi><payload_len_lo><payload_len_hi><payload><crc>
```

### Race Control Commands

| MSP Function | Hex | Payload | Direction | Purpose |
|--------------|-----|---------|-----------|---------|
| `MSP_ELRS_BACKPACK_SET_RECORDING_STATE` | 0x0305 | 0x01 | Backpack → Timer | Start race |
| `MSP_ELRS_BACKPACK_SET_RECORDING_STATE` | 0x0305 | 0x00 | Backpack → Timer | Stop race |
| `MSP_ELRS_GET_BACKPACK_VERSION` | 0x0010 | - | Timer → Backpack | Request version |

### Command Flow

1. **Race Director toggles DVR Rec switch** on transmitter
2. **Transmitter backpack** sends state change over ESPNow to **Timer backpack**
3. **Timer backpack (ESP32)** receives state via ELRS Backpack protocol
4. **Timer backpack** sends MSP command over serial to **FPV Trackside**
5. **Trackside** parses MSP packet and triggers race start/stop

## Advanced Configuration

### Multiple Timers

You can use multiple ESP32 backpacks with different bind phrases for:
- Backup timing systems
- Multiple race director controls
- Distributed timing setups

Just flash each ESP32 with a different bind phrase and connect to different COM ports.

### Custom Debounce Tuning

Adjust debounce based on your switch quality:
- **Fast mechanical switches**: 200-300ms
- **Standard switches**: 500ms (default)
- **Noisy switches**: 1000-2000ms

### Integration with Other Systems

VRXC protocol can be extended to support:
- Pilot ready status (future feature)
- Race announcements
- Custom OSD messages
- Real-time telemetry

## Differences from CRSF/Channel-Based Timing

| Feature | CRSF (Old) | VRXC (New) |
|---------|------------|------------|
| **Protocol** | Raw CRSF RC channels | MSP over serial |
| **Data Source** | RC channel values (1000-2000µs) | Command packets |
| **Configuration** | Trigger channel, threshold | Just serial port |
| **Latency** | RC frame rate (~5-10ms) | Direct serial (~1ms) |
| **Reliability** | Depends on RC link quality | Wired USB connection |
| **Setup Complexity** | Medium (channel mapping) | Simple (bind phrase only) |

## Contributing

Found a bug or want to add features? 

Repository: `https://github.com/macmittereder/FPVTracksideCore-ELRS`
Branch: `vrxc-integration`

## Credits

- **VRXC Protocol**: [i-am-grub/vrxc_elrs](https://github.com/i-am-grub/vrxc_elrs)
- **ExpressLRS**: [ExpressLRS Project](https://www.expresslrs.org/)
- **FPV Trackside**: Original timing system by Trackside team
- **Integration Author**: macmittereder

## License

Same as FPV Trackside Core - see main LICENSE file.

## Support

For issues specific to VRXC integration:
- Open an issue on GitHub: `macmittereder/FPVTracksideCore-ELRS`
- Include: OS, ESP32 model, Trackside logs, firmware version

For VRXC protocol questions:
- ELRS Discord: `https://discord.gg/expresslrs`
- VRXC GitHub: `https://github.com/i-am-grub/vrxc_elrs`
