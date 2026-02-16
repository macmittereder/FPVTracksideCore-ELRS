using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tools;

namespace Timing.ELRS
{
    /// <summary>
    /// ELRS (ExpressLRS) Timing System with VRXC Protocol Support
    /// Monitors VRXC/ELRS Backpack commands for race start/stop control
    /// </summary>
    public class ELRSTimingSystem : ITimingSystem
    {
        public TimingSystemType Type => TimingSystemType.Other;
        public string Name => "ELRS/VRXC";
        public bool Connected { get; private set; }
        public int MaxPilots => 1; // VRXC is race director control, not lap timing
        
        private ELRSSettings elrsSettings;
        private VRXCProtocol vrxcProtocol;
        private bool detectionRunning;
        private DateTime lastTriggerTime;
        private List<ListeningFrequency> frequencies;
        private string backpackVersion;
        
        public TimingSystemSettings Settings
        {
            get => elrsSettings;
            set => elrsSettings = value as ELRSSettings;
        }
        
        public event DetectionEventDelegate OnDetectionEvent;
        public event MarshallEventDelegate OnMarshallEvent;
        
        public IEnumerable<StatusItem> Status
        {
            get
            {
                yield return new StatusItem
                {
                    StatusOK = Connected,
                    Value = Connected ? $"Connected ({elrsSettings.SerialPort})" : "Disconnected"
                };
                
                if (!string.IsNullOrEmpty(backpackVersion))
                {
                    yield return new StatusItem
                    {
                        StatusOK = true,
                        Value = $"Backpack: {backpackVersion}"
                    };
                }
                
                yield return new StatusItem
                {
                    StatusOK = true,
                    Value = "VRXC Protocol (MSP)"
                };
                
                if (detectionRunning)
                {
                    yield return new StatusItem
                    {
                        StatusOK = true,
                        Value = "Listening for race commands"
                    };
                }
            }
        }
        
        public ELRSTimingSystem()
        {
            elrsSettings = new ELRSSettings();
            vrxcProtocol = new VRXCProtocol();
            frequencies = new List<ListeningFrequency>();
            lastTriggerTime = DateTime.MinValue;
            
            vrxcProtocol.OnStartRaceCommand += HandleStartRaceCommand;
            vrxcProtocol.OnStopRaceCommand += HandleStopRaceCommand;
            vrxcProtocol.OnBackpackVersion += HandleBackpackVersion;
            vrxcProtocol.OnError += HandleError;
        }
        
        public bool Connect()
        {
            try
            {
                Logger.TimingLog.Log(this, "Connecting", elrsSettings.SerialPort, Logger.LogType.Notice);
                
                if (string.IsNullOrEmpty(elrsSettings.SerialPort))
                {
                    Logger.TimingLog.Log(this, "Error", "No serial port configured", Logger.LogType.Error);
                    return false;
                }
                
                bool success = vrxcProtocol.Connect(elrsSettings.SerialPort, elrsSettings.BaudRate);
                Connected = success;
                
                if (success)
                {
                    Logger.TimingLog.Log(this, "Connected", $"{elrsSettings.SerialPort} @ {elrsSettings.BaudRate}bps (VRXC/MSP)", Logger.LogType.Notice);
                }
                else
                {
                    Logger.TimingLog.Log(this, "Connection Failed", elrsSettings.SerialPort, Logger.LogType.Error);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
                Connected = false;
                return false;
            }
        }
        
        public bool Disconnect()
        {
            try
            {
                Logger.TimingLog.Log(this, "Disconnecting", Logger.LogType.Notice);
                vrxcProtocol.Disconnect();
                Connected = false;
                return true;
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
                return false;
            }
        }
        
        public bool SetListeningFrequencies(IEnumerable<ListeningFrequency> newFrequencies)
        {
            try
            {
                frequencies.Clear();
                frequencies.AddRange(newFrequencies);
                
                Logger.TimingLog.Log(this, "SetFrequencies", $"{frequencies.Count} frequencies", Logger.LogType.Notice);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
                return false;
            }
        }
        
        public bool StartDetection(ref DateTime time, StartMetaData raceMetaData)
        {
            try
            {
                Logger.TimingLog.Log(this, "StartDetection", "Listening for VRXC race commands", Logger.LogType.Notice);
                
                if (!Connected)
                {
                    Logger.TimingLog.Log(this, "Error", "Not connected", Logger.LogType.Error);
                    return false;
                }
                
                detectionRunning = true;
                lastTriggerTime = DateTime.MinValue;
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
                return false;
            }
        }
        
        public bool EndDetection(EndDetectionType type)
        {
            try
            {
                Logger.TimingLog.Log(this, "EndDetection", type.ToString(), Logger.LogType.Notice);
                detectionRunning = false;
                return true;
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
                return false;
            }
        }
        
        private void HandleStartRaceCommand()
        {
            if (!detectionRunning)
            {
                return;
            }
            
            try
            {
                DateTime now = DateTime.Now;
                
                // Apply debounce
                if ((now - lastTriggerTime).TotalMilliseconds < elrsSettings.DebounceMs)
                {
                    Logger.TimingLog.Log(this, "Debounce", "Start command ignored (too soon)", Logger.LogType.Notice);
                    return;
                }
                
                lastTriggerTime = now;
                
                Logger.TimingLog.Log(this, "VRXC Command", "START RACE received from transmitter", Logger.LogType.Notice);
                
                // Fire detection event for all configured frequencies
                // This triggers race start for all pilots simultaneously
                foreach (var freq in frequencies)
                {
                    OnDetectionEvent?.Invoke(this, freq.Frequency, now, 1);
                }
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }
        
        private void HandleStopRaceCommand()
        {
            if (!detectionRunning)
            {
                return;
            }
            
            try
            {
                DateTime now = DateTime.Now;
                
                // Apply debounce
                if ((now - lastTriggerTime).TotalMilliseconds < elrsSettings.DebounceMs)
                {
                    Logger.TimingLog.Log(this, "Debounce", "Stop command ignored (too soon)", Logger.LogType.Notice);
                    return;
                }
                
                lastTriggerTime = now;
                
                Logger.TimingLog.Log(this, "VRXC Command", "STOP RACE received from transmitter", Logger.LogType.Notice);
                
                // Fire detection event with stop indicator (value = 0)
                foreach (var freq in frequencies)
                {
                    OnDetectionEvent?.Invoke(this, freq.Frequency, now, 0);
                }
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }
        
        private void HandleBackpackVersion(string version)
        {
            backpackVersion = version;
            Logger.TimingLog.Log(this, "Backpack Version", version, Logger.LogType.Notice);
        }
        
        private void HandleError(string error)
        {
            Logger.TimingLog.Log(this, "VRXC Error", error, Logger.LogType.Error);
        }
        
        public void Dispose()
        {
            if (detectionRunning)
            {
                EndDetection(EndDetectionType.Normal);
            }
            
            Disconnect();
            vrxcProtocol?.Dispose();
        }
    }
}
