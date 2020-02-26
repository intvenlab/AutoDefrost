using System;
using System.IO;
using System.IO.Ports;

namespace MeSoft.MeCom.PhyWrapper
{
    /// <summary>
    /// Implements the IMeComPhy interface for the Serial Port interface. 
    /// </summary>
    public class MeComPhySerialPort : SerialPort, IMeComPhy
    {
        /// <summary>
        /// Opens the SerialPort with the standard settings used to communicate with ME devices.
        /// </summary>
        /// <param name="portName">Name of the Port to open. Ex. COM1</param>
        /// <param name="baudRate">Baud rate for the serial interface.</param>
        public void OpenWithDefaultSettings(string portName, int baudRate)
        {
            PortName = portName;
            BaudRate = baudRate;
            Parity = Parity.None;
            StopBits = StopBits;
            DataBits = 8;
            ReadTimeout = 300;
            Open();
        }

        void IMeComPhy.ChangeSpeed(int NewBaudrate)
        {
            BaudRate = NewBaudrate;
        }

        void IMeComPhy.SetTimeout(int Miliseconds)
        {
            ReadTimeout = Miliseconds;
        }

        void IMeComPhy.SendString(MemoryStream Stream)
        {
            try
            {
                byte[] txData = Stream.ToArray();
                base.Write(txData, 0, txData.Length);
            }
            catch (Exception ex)
            {
                throw new MeComPhyIntefaceException("Failure during sending: " + ex.Message, ex);
            }
        }

        void IMeComPhy.GetDataOrTimeout(MemoryStream Stream)
        {
            try
            {
                byte[] readBuffer = new byte[512];
                int nrOfReadBytes = 0;
                nrOfReadBytes = base.Read(readBuffer, 0, readBuffer.Length);
                Stream.Write(readBuffer, 0, nrOfReadBytes);
            }
            catch (TimeoutException)
            {
                throw new MeComPhyTimeoutException();
            }
            catch (Exception ex)
            {
                throw new MeComPhyIntefaceException("Failure during receiving: " + ex.Message, ex);
            }
        }
    }
}
