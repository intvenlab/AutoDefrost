using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System;
using NLog;

namespace AutoDefrost
{
    public class PosiTectorDPM : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private SerialPort _serialPort;
        private readonly Regex _dataRegex;

        public double AirTemperature { get; private set; }
        public double SurfaceTemperature { get; private set; }
        public double RelativeHumidity { get; private set; }
        public double DewPointTemperature { get; private set; }
        public double SurfaceToDewPointDifference { get; private set; }
        public DateTime LastUpdate { get; private set; }

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public PosiTectorDPM()
        {
            _dataRegex = new Regex(
                @"\u0002\s*Ta\s*(-?\d+\.\d+)\s*C\s*\u0004\s*[\r\n]+" +
                @"\u0002\s*Ts\s*(-?\d+\.\d+)\s*C\s*\u0004\s*[\r\n]+" +
                @"\u0002\s*RH\s*(-?\d+\.\d+)\s*%\s*\u0004\s*[\r\n]+" +
                @"\u0002\s*Td\s*(-?\d+\.\d+)\s*C\s*\u0004\s*[\r\n]+" +
                @"\u0002\s*Ts-Td\s*(-?\d+\.\d+)\s*C\s*\u0004",
                RegexOptions.Compiled | RegexOptions.Singleline);

            ResetValues();
        }

        private void ResetValues()
        {
            AirTemperature = double.NaN;
            SurfaceTemperature = double.NaN;
            RelativeHumidity = double.NaN;
            DewPointTemperature = double.NaN;
            SurfaceToDewPointDifference = double.NaN;
        }

        public void Dispose()
        {
            try
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"PosiTectorDPM: Error disposing serial port: {ex.Message}");
            }
        }

        public bool Connect()
        {
            foreach (var portName in SerialPort.GetPortNames())
            {
                try
                {
                    Logger.Info($"PosiTectorDPM: Trying port: {portName}");
                    _serialPort = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 1500,
                        WriteTimeout = 1500,
                        NewLine = "\r\n"
                    };

                    _serialPort.Open();
                    Thread.Sleep(100); // Allow port initialization

                    var startTime = DateTime.Now;
                    var buffer = new StringBuilder();

                    // Fill the buffer for 1.2 seconds
                    while ((DateTime.Now - startTime).TotalSeconds < 0.5)
                    {
                        try
                        {
                            string line = _serialPort.ReadLine();
                            buffer.AppendLine(line);
                        }
                        catch (TimeoutException)
                        {
                            // Ignore timeouts during accumulation
                        }
                    }

                    // Attempt to parse the accumulated buffer
                    if (TryParse(buffer.ToString()))
                    {
                        Logger.Info($"PosiTectorDPM: Device found on {portName}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"PosiTectorDPM: Failed to connect on {portName}: {ex.Message}");
                }
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        Logger.Info("PosiTectorDPM: Close port..");
                        _serialPort.Close();
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
            }

            Logger.Error("No Positector DPM device found.");
            return false;
        }


        public void ReadData()
        {
            if (!IsConnected)
            {
                return;
//                throw new InvalidOperationException("Device is not connected.");
            }

            var buffer = new StringBuilder();
            var startTime = DateTime.Now;

            try
            {
                while ((DateTime.Now - startTime).TotalSeconds < 1 && buffer.Length < 281) // each frame is 281 bytes
                {
                    try
                    {
                        // Attempt to read a line from the serial port
                        string line = _serialPort.ReadLine();
                        buffer.AppendLine(line);
                    }
                    catch (TimeoutException)
                    {
                        // Ignore timeouts; we continue to accumulate data within the allowed time
                    }
                }
                //Logger.Debug($"PosiTectorDPM: Buffer size: {buffer.Length}");
                // After 1.5 seconds, attempt to parse the accumulated buffer
                if (TryParse(buffer.ToString()))
                {
                    LastUpdate = DateTime.Now;
                    Logger.Info("PosiTectorDPM: Data captured successfully.");
                }
                else
                {
                    Logger.Error("PosiTectorDPM: No valid data found in buffer.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while reading data: {ex.Message}");
            }
        }


        private bool TryParse(string data)
        {
            var match = _dataRegex.Match(data);
            if (match.Success)
            {
                AirTemperature = double.Parse(match.Groups[1].Value);
                SurfaceTemperature = double.Parse(match.Groups[2].Value);
                RelativeHumidity = double.Parse(match.Groups[3].Value);
                DewPointTemperature = double.Parse(match.Groups[4].Value);
                SurfaceToDewPointDifference = double.Parse(match.Groups[5].Value);

                if (RelativeHumidity < 0 || RelativeHumidity > 100)
                {
                    Logger.Warn("Invalid Relative Humidity value received.");
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
