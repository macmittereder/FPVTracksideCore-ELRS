using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;

namespace Timing.ELRS
{
    public class ELRSSettings : TimingSystemSettings
    {
        [Category("VRXC Connection")]
        [Description("Serial port name (e.g., COM3, /dev/ttyUSB0) connected to ESP32 running VRXC/ELRS Backpack firmware")]
        public string SerialPort { get; set; }
        
        [Category("VRXC Connection")]
        [Description("Serial baud rate (default: 460800 for ELRS/MSP)")]
        public int BaudRate { get; set; }
        
        [Category("Race Control")]
        [Description("Minimum time between race start/stop commands in milliseconds (debounce)")]
        public int DebounceMs { get; set; }
        
        [Browsable(false)]
        public string[] AvailablePorts
        {
            get
            {
                try
                {
                    return System.IO.Ports.SerialPort.GetPortNames();
                }
                catch
                {
                    return new string[0];
                }
            }
        }
        
        public ELRSSettings()
        {
            // Default values
            SerialPort = GetDefaultPort();
            BaudRate = 460800; // Standard ELRS Backpack baud rate (matches SoloHazard plugin)
            DebounceMs = 500; // Half second debounce to prevent double triggers
        }
        
        private string GetDefaultPort()
        {
            try
            {
                var ports = System.IO.Ports.SerialPort.GetPortNames();
                if (ports != null && ports.Length > 0)
                {
                    // On Linux, prefer /dev/ttyUSB0 or /dev/ttyACM0
                    var usbPort = ports.FirstOrDefault(p => p.Contains("ttyUSB") || p.Contains("ttyACM"));
                    if (usbPort != null)
                    {
                        return usbPort;
                    }
                    
                    // On Windows, prefer COM3 or higher (COM1/COM2 are often builtin)
                    var comPort = ports.Where(p => p.StartsWith("COM")).OrderBy(p => p).Skip(2).FirstOrDefault();
                    if (comPort != null)
                    {
                        return comPort;
                    }
                    
                    // Fallback to first available port
                    return ports[0];
                }
            }
            catch
            {
            }
            
            // Default fallback
            return Environment.OSVersion.Platform == PlatformID.Unix ? "/dev/ttyUSB0" : "COM3";
        }
        
        public override string ToString()
        {
            return $"VRXC/ELRS ({SerialPort})";
        }
    }
}
