using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace Timing.ELRS
{
    /// <summary>
    /// CRSF (Crossfire/ELRS) protocol parser
    /// Implements the protocol used by ExpressLRS receivers
    /// </summary>
    public class CRSFProtocol : IDisposable
    {
        private const byte CRSF_SYNC_BYTE = 0xC8;
        private const byte CRSF_FRAMETYPE_RC_CHANNELS_PACKED = 0x16;
        private const int CRSF_CHANNEL_VALUE_MIN = 172;
        private const int CRSF_CHANNEL_VALUE_MID = 992;
        private const int CRSF_CHANNEL_VALUE_MAX = 1811;
        private const int CRSF_FRAME_SIZE_MAX = 64;
        
        private SerialPort serialPort;
        private Thread readThread;
        private bool running;
        private byte[] buffer = new byte[CRSF_FRAME_SIZE_MAX];
        private int bufferIndex = 0;
        
        public event Action<int[]> OnChannelsReceived;
        public event Action<string> OnError;
        
        public bool IsConnected => serialPort != null && serialPort.IsOpen;
        
        public CRSFProtocol()
        {
        }
        
        public bool Connect(string portName, int baudRate = 420000)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    Disconnect();
                }
                
                serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                serialPort.Open();
                
                running = true;
                readThread = new Thread(ReadLoop);
                readThread.Name = "CRSF Protocol Reader";
                readThread.Start();
                
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to connect: {ex.Message}");
                return false;
            }
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
                serialPort.Close();
                serialPort.Dispose();
                serialPort = null;
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
                        int bytesToRead = Math.Min(serialPort.BytesToRead, CRSF_FRAME_SIZE_MAX - bufferIndex);
                        serialPort.Read(buffer, bufferIndex, bytesToRead);
                        bufferIndex += bytesToRead;
                        
                        ProcessBuffer();
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    if (running)
                    {
                        OnError?.Invoke($"Read error: {ex.Message}");
                    }
                }
            }
        }
        
        private void ProcessBuffer()
        {
            while (bufferIndex >= 4) // Minimum frame: sync + length + type + crc
            {
                // Find sync byte
                int syncIndex = -1;
                for (int i = 0; i < bufferIndex; i++)
                {
                    if (buffer[i] == CRSF_SYNC_BYTE)
                    {
                        syncIndex = i;
                        break;
                    }
                }
                
                if (syncIndex == -1)
                {
                    // No sync found, clear buffer
                    bufferIndex = 0;
                    return;
                }
                
                // Remove data before sync
                if (syncIndex > 0)
                {
                    Array.Copy(buffer, syncIndex, buffer, 0, bufferIndex - syncIndex);
                    bufferIndex -= syncIndex;
                }
                
                // Check if we have enough data for frame length
                if (bufferIndex < 2)
                {
                    return;
                }
                
                byte frameLength = buffer[1];
                int totalLength = frameLength + 2; // +2 for sync and length bytes
                
                // Check if we have the complete frame
                if (bufferIndex < totalLength)
                {
                    return;
                }
                
                // Verify CRC
                byte receivedCRC = buffer[totalLength - 1];
                byte calculatedCRC = CalculateCRC(buffer, 2, frameLength - 1);
                
                if (receivedCRC == calculatedCRC)
                {
                    byte frameType = buffer[2];
                    
                    if (frameType == CRSF_FRAMETYPE_RC_CHANNELS_PACKED)
                    {
                        int[] channels = DecodeChannels(buffer, 3);
                        OnChannelsReceived?.Invoke(channels);
                    }
                }
                else
                {
                    OnError?.Invoke("CRC mismatch");
                }
                
                // Remove processed frame from buffer
                Array.Copy(buffer, totalLength, buffer, 0, bufferIndex - totalLength);
                bufferIndex -= totalLength;
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
        
        private int[] DecodeChannels(byte[] data, int offset)
        {
            // CRSF uses 11-bit channel data packed into bytes
            // 16 channels = 16 * 11 bits = 176 bits = 22 bytes
            int[] channels = new int[16];
            
            int bitIndex = 0;
            for (int i = 0; i < 16; i++)
            {
                int byteIndex = bitIndex / 8;
                int bitOffset = bitIndex % 8;
                
                int value = 0;
                for (int bit = 0; bit < 11; bit++)
                {
                    int currentByteIndex = (bitIndex + bit) / 8;
                    int currentBitOffset = (bitIndex + bit) % 8;
                    
                    if (((data[offset + currentByteIndex] >> currentBitOffset) & 1) != 0)
                    {
                        value |= (1 << bit);
                    }
                }
                
                // Convert from CRSF range (172-1811) to microseconds (1000-2000)
                channels[i] = MapChannelValue(value);
                bitIndex += 11;
            }
            
            return channels;
        }
        
        private int MapChannelValue(int crsfValue)
        {
            // Map CRSF range (172-1811) to standard PWM range (1000-2000 microseconds)
            if (crsfValue < CRSF_CHANNEL_VALUE_MIN)
            {
                return 1000;
            }
            if (crsfValue > CRSF_CHANNEL_VALUE_MAX)
            {
                return 2000;
            }
            
            return 1000 + (int)((crsfValue - CRSF_CHANNEL_VALUE_MIN) * 1000.0 / (CRSF_CHANNEL_VALUE_MAX - CRSF_CHANNEL_VALUE_MIN));
        }
        
        public void Dispose()
        {
            Disconnect();
        }
    }
}
