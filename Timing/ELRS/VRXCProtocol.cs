using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace Timing.ELRS
{
    /// <summary>
    /// VRXC (VRx Controller) MSP protocol parser
    /// Implements the MSP protocol used by ELRS Backpack for race control
    /// </summary>
    public class VRXCProtocol : IDisposable
    {
        // MSP v2 header constants
        private const byte MSP_HEADER_DOLLAR = 0x24;  // '$'
        private const byte MSP_HEADER_X = 0x58;       // 'X'
        private const byte MSP_TYPE_COMMAND = 0x3C;   // '<'
        private const byte MSP_TYPE_RESPONSE = 0x3E;  // '>'
        private const int MSP_HEADER_LENGTH = 8;
        
        // ELRS Backpack MSP function codes
        private const ushort MSP_ELRS_BACKPACK_SET_RECORDING_STATE = 0x0305;
        private const ushort MSP_ELRS_BACKPACK_GET_RECORDING_STATE = 0x0304;
        private const ushort MSP_ELRS_GET_BACKPACK_VERSION = 0x0010;
        
        // Recording state values
        private const byte RECORDING_STATE_STOP = 0x00;
        private const byte RECORDING_STATE_START = 0x01;
        
        private SerialPort serialPort;
        private Thread readThread;
        private bool running;
        private byte[] buffer = new byte[256];
        private int bufferIndex = 0;
        
        public event Action OnStartRaceCommand;
        public event Action OnStopRaceCommand;
        public event Action<string> OnBackpackVersion;
        public event Action<string> OnError;
        
        public bool IsConnected => serialPort != null && serialPort.IsOpen;
        
        public VRXCProtocol()
        {
        }
        
        public bool Connect(string portName, int baudRate = 460800)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    Disconnect();
                }
                
                serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                serialPort.Open();

                // Handshake: send version request and wait for response (matches SoloHazard plugin behavior)
                Thread.Sleep(200); // Let ESP32 settle after port open
                serialPort.DiscardInBuffer();

                byte[] versionRequest = BuildMSPPacket(MSP_TYPE_COMMAND, MSP_ELRS_GET_BACKPACK_VERSION, new byte[0]);
                serialPort.Write(versionRequest, 0, versionRequest.Length);

                Thread.Sleep(300); // Wait for response

                bool handshakeOk = false;
                int available = serialPort.BytesToRead;
                if (available > 0)
                {
                    byte[] response = new byte[available];
                    serialPort.Read(response, 0, available);
                    // Look for MSP v2 header '$X' in response
                    for (int i = 0; i < response.Length - 1; i++)
                    {
                        if (response[i] == MSP_HEADER_DOLLAR && response[i + 1] == MSP_HEADER_X)
                        {
                            handshakeOk = true;
                            break;
                        }
                    }
                }

                if (!handshakeOk)
                {
                    serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                    OnError?.Invoke($"No ELRS Backpack response on {portName} — wrong port or firmware?");
                    return false;
                }

                running = true;
                readThread = new Thread(ReadLoop);
                readThread.Name = "VRXC Protocol Reader";
                readThread.IsBackground = true;
                readThread.Start();
                
                // Request version again to populate status display
                RequestVersion();
                
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to connect: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Scans all serial ports and returns the first one that responds to an ELRS Backpack version request.
        /// </summary>
        public static string DetectPort(int baudRate = 460800)
        {
            string[] avoidedPorts = { "COM1", "/dev/ttyAMA0", "/dev/ttyAMA10" };
            string[] ports = SerialPort.GetPortNames();

            foreach (string port in ports)
            {
                if (Array.Exists(avoidedPorts, p => p.Equals(port, StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    using (var sp = new SerialPort(port, baudRate, Parity.None, 8, StopBits.One))
                    {
                        sp.ReadTimeout = 500;
                        sp.WriteTimeout = 500;
                        sp.Open();

                        Thread.Sleep(200);
                        sp.DiscardInBuffer();

                        // Build and send version request packet
                        byte[] packet = BuildVersionRequestPacket();
                        sp.Write(packet, 0, packet.Length);

                        Thread.Sleep(300);

                        int available = sp.BytesToRead;
                        if (available > 0)
                        {
                            byte[] response = new byte[available];
                            sp.Read(response, 0, available);
                            for (int i = 0; i < response.Length - 1; i++)
                            {
                                if (response[i] == MSP_HEADER_DOLLAR && response[i + 1] == MSP_HEADER_X)
                                {
                                    return port; // Found it!
                                }
                            }
                        }

                        sp.Close();
                    }
                }
                catch { }
            }

            return null; // Not found
        }

        private static byte[] BuildVersionRequestPacket()
        {
            // Static version of BuildMSPPacket for use before instance exists
            const ushort func = MSP_ELRS_GET_BACKPACK_VERSION;
            byte[] packet = new byte[MSP_HEADER_LENGTH + 1]; // no payload + 1 CRC
            packet[0] = MSP_HEADER_DOLLAR;
            packet[1] = MSP_HEADER_X;
            packet[2] = MSP_TYPE_COMMAND;
            packet[3] = 0;
            packet[4] = (byte)(func & 0xFF);
            packet[5] = (byte)((func >> 8) & 0xFF);
            packet[6] = 0;
            packet[7] = 0;
            // CRC over bytes 3..7
            byte crc = 0;
            for (int i = 3; i < MSP_HEADER_LENGTH; i++)
            {
                crc ^= packet[i];
                for (int b = 0; b < 8; b++)
                    crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0xD5) : (byte)(crc << 1);
            }
            packet[MSP_HEADER_LENGTH] = crc;
            return packet;
        }
        
        public void Disconnect()
        {
            running = false;
            
            if (readThread != null && readThread.IsAlive)
            {
                readThread.Join(1000);
            }
            
            if (serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    serialPort.Close();
                }
                catch { }
                serialPort.Dispose();
                serialPort = null;
            }
        }
        
        public void RequestVersion()
        {
            try
            {
                if (!IsConnected)
                    return;
                
                byte[] packet = BuildMSPPacket(MSP_TYPE_COMMAND, MSP_ELRS_GET_BACKPACK_VERSION, new byte[0]);
                serialPort.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to request version: {ex.Message}");
            }
        }
        
        private void ReadLoop()
        {
            while (running)
            {
                try
                {
                    if (serialPort != null && serialPort.IsOpen && serialPort.BytesToRead > 0)
                    {
                        int bytesToRead = Math.Min(serialPort.BytesToRead, buffer.Length - bufferIndex);
                        if (bytesToRead > 0)
                        {
                            int bytesRead = serialPort.Read(buffer, bufferIndex, bytesToRead);
                            bufferIndex += bytesRead;
                            ProcessBuffer();
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (TimeoutException)
                {
                    // Normal timeout, continue
                }
                catch (Exception ex)
                {
                    if (running)
                    {
                        OnError?.Invoke($"Read error: {ex.Message}");
                        Thread.Sleep(100);
                    }
                }
            }
        }
        
        private void ProcessBuffer()
        {
            while (bufferIndex >= 4)
            {
                // Find MSP v2 header: '$X'
                int headerIndex = -1;
                for (int i = 0; i < bufferIndex - 1; i++)
                {
                    if (buffer[i] == MSP_HEADER_DOLLAR && buffer[i + 1] == MSP_HEADER_X)
                    {
                        headerIndex = i;
                        break;
                    }
                }
                
                if (headerIndex == -1)
                {
                    // No header found, clear buffer
                    bufferIndex = 0;
                    return;
                }
                
                // Remove data before header
                if (headerIndex > 0)
                {
                    Array.Copy(buffer, headerIndex, buffer, 0, bufferIndex - headerIndex);
                    bufferIndex -= headerIndex;
                }
                
                // Check if we have complete header
                if (bufferIndex < MSP_HEADER_LENGTH)
                {
                    return;
                }
                
                // Parse header
                byte type = buffer[2];
                byte flags = buffer[3];
                ushort function = (ushort)(buffer[4] | (buffer[5] << 8));
                ushort payloadLength = (ushort)(buffer[6] | (buffer[7] << 8));
                
                int totalLength = MSP_HEADER_LENGTH + payloadLength + 1; // +1 for CRC
                
                // Check if we have complete packet
                if (bufferIndex < totalLength)
                {
                    return;
                }
                
                // Verify CRC
                byte receivedCRC = buffer[totalLength - 1];
                byte calculatedCRC = CalculateCRC(buffer, 3, MSP_HEADER_LENGTH - 3 + payloadLength);
                
                if (receivedCRC == calculatedCRC)
                {
                    // Extract payload
                    byte[] payload = new byte[payloadLength];
                    if (payloadLength > 0)
                    {
                        Array.Copy(buffer, MSP_HEADER_LENGTH, payload, 0, payloadLength);
                    }
                    
                    // Process packet
                    ProcessMSPPacket(type, function, payload);
                }
                else
                {
                    OnError?.Invoke($"CRC mismatch (expected: 0x{calculatedCRC:X2}, got: 0x{receivedCRC:X2})");
                }
                
                // Remove processed packet from buffer
                if (totalLength < bufferIndex)
                {
                    Array.Copy(buffer, totalLength, buffer, 0, bufferIndex - totalLength);
                }
                bufferIndex -= totalLength;
            }
        }
        
        private void ProcessMSPPacket(byte type, ushort function, byte[] payload)
        {
            try
            {
                switch (function)
                {
                    case MSP_ELRS_BACKPACK_SET_RECORDING_STATE:
                        if (type == MSP_TYPE_COMMAND && payload.Length > 0)
                        {
                            byte state = payload[0];
                            if (state == RECORDING_STATE_START)
                            {
                                OnStartRaceCommand?.Invoke();
                            }
                            else if (state == RECORDING_STATE_STOP)
                            {
                                OnStopRaceCommand?.Invoke();
                            }
                        }
                        break;
                    
                    case MSP_ELRS_GET_BACKPACK_VERSION:
                        if (type == MSP_TYPE_RESPONSE)
                        {
                            // Version is null-terminated string
                            int nullIndex = Array.IndexOf(payload, (byte)0);
                            int length = nullIndex >= 0 ? nullIndex : payload.Length;
                            string version = System.Text.Encoding.UTF8.GetString(payload, 0, length);
                            OnBackpackVersion?.Invoke(version);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Error processing MSP packet: {ex.Message}");
            }
        }
        
        private byte CalculateCRC(byte[] data, int start, int length)
        {
            byte crc = 0;
            for (int i = 0; i < length; i++)
            {
                crc = CRC8_DVB_S2(crc, data[start + i]);
            }
            return crc;
        }
        
        private byte CRC8_DVB_S2(byte crc, byte data)
        {
            crc ^= data;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x80) != 0)
                {
                    crc = (byte)((crc << 1) ^ 0xD5);
                }
                else
                {
                    crc = (byte)(crc << 1);
                }
            }
            return crc;
        }
        
        private byte[] BuildMSPPacket(byte type, ushort function, byte[] payload)
        {
            int payloadLength = payload?.Length ?? 0;
            byte[] packet = new byte[MSP_HEADER_LENGTH + payloadLength + 1];
            
            // Header
            packet[0] = MSP_HEADER_DOLLAR;  // '$'
            packet[1] = MSP_HEADER_X;       // 'X'
            packet[2] = type;
            packet[3] = 0; // flags
            packet[4] = (byte)(function & 0xFF);
            packet[5] = (byte)((function >> 8) & 0xFF);
            packet[6] = (byte)(payloadLength & 0xFF);
            packet[7] = (byte)((payloadLength >> 8) & 0xFF);
            
            // Payload
            if (payloadLength > 0)
            {
                Array.Copy(payload, 0, packet, MSP_HEADER_LENGTH, payloadLength);
            }
            
            // CRC
            packet[packet.Length - 1] = CalculateCRC(packet, 3, MSP_HEADER_LENGTH - 3 + payloadLength);
            
            return packet;
        }
        
        public void Dispose()
        {
            Disconnect();
        }
    }
}
