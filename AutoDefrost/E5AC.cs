﻿using System;
using System.IO.Ports;
using NLog;
using NModbus;
using NModbus.Serial;

namespace AutoDefrost
{
    public class E5EC : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private SerialPort _serialPort;
        private IModbusSerialMaster _modbusMaster;
        private readonly byte _slaveAddress;

        public E5EC(byte slaveAddress = 1)
        {
            _slaveAddress = slaveAddress;
        }

        public void Dispose()
        {
            _modbusMaster?.Dispose();
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
        }

        public bool Connect()
        {
            foreach (var portName in SerialPort.GetPortNames())
            {
                try
                {
                    Logger.Info($"E5EC: Trying port: {portName}");
                    _serialPort = new SerialPort(portName, 9600, Parity.Even, 8, StopBits.One)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    _serialPort.Open();

                    // Create a transport and master for Modbus
                    var factory = new ModbusFactory();
                    var transport = factory.CreateRtuTransport(_serialPort);
                    _modbusMaster = factory.CreateMaster(transport);

                    // Test the connection by reading a known register (e.g., process value register)
                    _modbusMaster.ReadHoldingRegisters(_slaveAddress, 0x2000, 1);

                    Logger.Info($"E5EC: Connected to controller on {portName}");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Info($"E5EC: Failed to connect on {portName}: {ex.Message}");
                    _serialPort?.Close();
                }
            }

            Logger.Info("E5EC: No controller found on available COM ports.");
            return false;
        }

        public double GetCurrentTemperature()
        {
            ushort[] registers = _modbusMaster.ReadHoldingRegisters(_slaveAddress, 0x2000, 1);
            short signedValue = (short)registers[0];
            return signedValue / 10.0; // Convert to Celsius
        }

        public double GetSetpoint()
        {
            ushort[] registers = _modbusMaster.ReadHoldingRegisters(_slaveAddress, 0x2601, 1);
            short signedValue = (short)registers[0];
            return signedValue / 10.0; // Convert to Celsius
        }

        public void SetSetpoint(double setpoint)
        {
            ushort value = (ushort)(setpoint * 10);
            _modbusMaster.WriteSingleRegister(_slaveAddress, 0x2601, value);
        }
    }
}
