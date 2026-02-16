# VRXC Quick Start Guide

## 5-Minute Setup

### Prerequisites
- ✅ ESP32-DevKitC (or compatible) board
- ✅ USB cable
- ✅ Transmitter with ELRS Backpack configured
- ✅ FPV Trackside installed

---

## Step 1: Flash ESP32 (5 min)

1. **Download**: [ExpressLRS Configurator](https://github.com/ExpressLRS/ExpressLRS-Configurator/releases)
2. **Connect** ESP32 via USB
3. **Flash**:
   - Select: `Backpack` → `1.5.0+` → `RotorHazard` → `Your Device`
   - Method: `UART`
   - **IMPORTANT**: Enter your transmitter's backpack bind phrase
   - Select COM port → `Build & Flash`

**Bind Phrase**: Must match your transmitter's backpack bind phrase!

---

## Step 2: Configure Transmitter (2 min)

1. Open **ELRS Lua script** on transmitter
2. Go to **Backpack** menu
3. Set **DVR Rec** to an AUX channel (e.g., AUX3)
4. Assign a switch to that AUX channel

---

## Step 3: Configure Trackside (1 min)

1. Launch **FPV Trackside**
2. Go to **Settings** → **Timing System**
3. Select **"ELRS/VRXC"**
4. Click **Settings**:
   - Serial Port: Select your ESP32's COM port
   - Baud Rate: `420000` (default)
   - Debounce: `500` ms (default)
5. Click **Connect**

**Status Check**: Should see "Connected" + "Backpack: 1.x.x" + "VRXC Protocol (MSP)"

---

## Step 4: Test (30 sec)

1. In Trackside: Click **"Start Detection"** or ready a race
2. On transmitter: Toggle **DVR Rec switch UP**
3. **Result**: Race should start!
4. Toggle **DVR Rec switch DOWN**
5. **Result**: Race should stop!

---

## Troubleshooting

### Not Connected?
- Wrong COM port? Check Device Manager (Windows) or `ls /dev/ttyUSB*` (Linux)
- USB cable bad? Try different cable (must support data, not charge-only)
- Linux permissions? Run: `sudo usermod -a -G dialout $USER` then logout/login

### No Version Detected?
- Wrong firmware? Must flash **RotorHazard** target (not TX or VRx backpack)
- Baud rate? Must be `420000` on both sides

### Commands Not Working?
- Bind phrase mismatch? Reflash ESP32 with correct transmitter bind phrase
- DVR Rec not configured? Set it up in ELRS Lua script → Backpack menu
- Detection not started? Click "Start Detection" in Trackside first

---

## Next Steps

✅ **Working?** Great! Read [VRXC_INTEGRATION.md](VRXC_INTEGRATION.md) for advanced features

❌ **Still stuck?** Check full troubleshooting in main documentation

---

## Summary

```
Transmitter DVR Rec Switch
         ↓
   (ESPNow wireless)
         ↓
ESP32 Backpack (USB-connected)
         ↓
   (MSP over Serial)
         ↓
   FPV Trackside
         ↓
   Race Start/Stop!
```

**Key Points:**
- DVR Rec switch = race control
- Bind phrases MUST match
- Must "Start Detection" first
- USB wired = reliable timing
