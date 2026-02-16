using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;

namespace Timing.ELRS
{
    public class ELRSSettings : TimingSystemSettings
    {
        [Category("ELRS Connection")]
        [Description("Serial port name (e.g., COM3, /dev/ttyUSB0)")]
        public string SerialPort { get; set; }
        
        [Category("ELRS Connection")]
        [Description("Serial baud rate (default: 420000 for ELRS/CRSF)")]
        public int BaudRate { get; set; }
        
        [Category("Trigger Configuration")]
        [Description("RC channel to use for race start/stop trigger (5-16, where 5=AUX1, 6=AUX2, etc.)")]
        public int TriggerChannel { get; set; }
        
        [Category("Trigger Configuration")]
        [Description("Channel value threshold for trigger (1000-2000 microseconds, default: 1500)")]
        public int ThresholdValue { get; set; }
        
        [Category("Trigger Configuration")]
        [Description("Trigger on high (>threshold) or low (<threshold)")]
        public bool TriggerOnHigh { get; set; }
        
        [Category("Advanced")]
        [Description("Minimum time between triggers in milliseconds (debounce)")]
        public int DebounceMs { get; set; }
        
        [Category("Advanced")]
        [Description("Number of virtual receivers to simulate (for testing)")]
        public int VirtualReceivers { get; set; }
        
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
            BaudRate = 420000; // Standard CRSF/ELRS baud rate
            TriggerChannel = 6; // AUX2 (channel 6)
            ThresholdValue = 1500; // Midpoint
            TriggerOnHigh = true; // Trigger when channel goes above threshold
            DebounceMs = 500; // Half second debounce
            VirtualReceivers = 8; // Default to 8 slots
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
            return $"ELRS ({SerialPort})";
        }
    }
}
