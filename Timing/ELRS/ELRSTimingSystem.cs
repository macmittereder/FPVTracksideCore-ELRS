using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tools;

namespace Timing.ELRS
{
    /// <summary>
    /// ELRS (ExpressLRS) Timing System
    /// Monitors ELRS/CRSF RC channels for race start/stop triggers
    /// </summary>
    public class ELRSTimingSystem : ITimingSystem
    {
        public TimingSystemType Type => TimingSystemType.Other;
        public string Name => "ELRS";
        public bool Connected { get; private set; }
        public int MaxPilots => elrsSettings.VirtualReceivers;
        
        private ELRSSettings elrsSettings;
        private CRSFProtocol crsfProtocol;
        private bool detectionRunning;
        private DateTime lastTriggerTime;
        private bool lastTriggerState;
        private List<ListeningFrequency> frequencies;
        
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
                
                yield return new StatusItem
                {
                    StatusOK = true,
                    Value = $"Ch{elrsSettings.TriggerChannel} @ {elrsSettings.ThresholdValue}µs"
                };
                
                if (detectionRunning)
                {
                    yield return new StatusItem
                    {
                        StatusOK = true,
                        Value = "Detecting"
                    };
                }
            }
        }
        
        public ELRSTimingSystem()
        {
            elrsSettings = new ELRSSettings();
            crsfProtocol = new CRSFProtocol();
            frequencies = new List<ListeningFrequency>();
            lastTriggerTime = DateTime.MinValue;
            lastTriggerState = false;
            
            crsfProtocol.OnChannelsReceived += HandleChannelsReceived;
            crsfProtocol.OnError += HandleError;
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
                
                bool success = crsfProtocol.Connect(elrsSettings.SerialPort, elrsSettings.BaudRate);
                Connected = success;
                
                if (success)
                {
                    Logger.TimingLog.Log(this, "Connected", $"{elrsSettings.SerialPort} @ {elrsSettings.BaudRate}bps", Logger.LogType.Notice);
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
                crsfProtocol.Disconnect();
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
                Logger.TimingLog.Log(this, "StartDetection", Logger.LogType.Notice);
                
                if (!Connected)
                {
                    Logger.TimingLog.Log(this, "Error", "Not connected", Logger.LogType.Error);
                    return false;
                }
                
                detectionRunning = true;
                lastTriggerTime = DateTime.MinValue;
                lastTriggerState = false;
                
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
        
        private void HandleChannelsReceived(int[] channels)
        {
            if (!detectionRunning)
            {
                return;
            }
            
            try
            {
                // Check trigger channel (convert 1-based to 0-based index)
                int channelIndex = elrsSettings.TriggerChannel - 1;
                
                if (channelIndex < 0 || channelIndex >= channels.Length)
                {
                    return;
                }
                
                int channelValue = channels[channelIndex];
                bool triggerState = elrsSettings.TriggerOnHigh 
                    ? (channelValue > elrsSettings.ThresholdValue)
                    : (channelValue < elrsSettings.ThresholdValue);
                
                // Check for state change (edge detection)
                if (triggerState != lastTriggerState)
                {
                    lastTriggerState = triggerState;
                    
                    // Only trigger on the configured edge
                    if (triggerState)
                    {
                        DateTime now = DateTime.Now;
                        
                        // Apply debounce
                        if ((now - lastTriggerTime).TotalMilliseconds < elrsSettings.DebounceMs)
                        {
                            return;
                        }
                        
                        lastTriggerTime = now;
                        
                        // Fire detection event for all configured frequencies
                        // This simulates lap triggers for all pilots simultaneously
                        foreach (var freq in frequencies)
                        {
                            Logger.TimingLog.Log(this, "Trigger", $"Ch{elrsSettings.TriggerChannel} = {channelValue}µs → {freq.Frequency}MHz", Logger.LogType.Notice);
                            OnDetectionEvent?.Invoke(this, freq.Frequency, now, channelValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }
        
        private void HandleError(string error)
        {
            Logger.TimingLog.Log(this, "CRSF Error", error, Logger.LogType.Error);
        }
        
        public void Dispose()
        {
            if (detectionRunning)
            {
                EndDetection(EndDetectionType.Normal);
            }
            
            Disconnect();
            crsfProtocol?.Dispose();
        }
    }
}
