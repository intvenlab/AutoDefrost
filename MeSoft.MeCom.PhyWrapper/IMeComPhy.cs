using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MeSoft.MeCom.PhyWrapper
{
    /// <summary>
    /// The upper communication level uses this interface 
    /// to have standardized interface to the physical level.
    /// The physical interface which implements this interface must already be open, 
    /// before you can use functions of this interface. 
    /// </summary>
    public interface IMeComPhy
	{
        /// <summary>
        /// Sends data to the physical interface.
        /// </summary>
        /// <param name="Stream">The whole content of the Stream is sent to the physical interface.</param>
        /// <exception cref="MeComPhyIntefaceException">Thrown when the underlying physical interface is not or not all bytes were sent.</exception>
        void SendString(MemoryStream Stream);

        /// <summary>
        /// Tries to read data from the physical interface or throws an timeout exception.
        /// </summary>
        /// <remarks>
        /// Reads the available data in the physical interface buffer and returns immediately. 
        /// If the receiving buffer is empty, it tries to read at least one byte. 
        /// It will wait till the timeout occurs if nothing is received.
        /// Must probably be called several times to receive the whole frame.
        /// </remarks>
        /// <param name="Stream">Stream where data will be added to.</param>
        /// <exception cref="MeComPhyIntefaceException">Thrown when the underlying physical interface is not OK.</exception>
        /// <exception cref="MeComPhyTimeoutException">Thrown when 0 bytes were received during the specified timeout time.</exception>
        void GetDataOrTimeout(MemoryStream Stream);

        /// <summary>
        /// Used to change the Serial Speed in case of serial communication interfaces.
        /// </summary>
        /// <param name="NewBaudrate">Target Baud Rate</param>
        /// <exception cref="MeComPhyIntefaceException">Thrown when the underlying physical interface is not OK.</exception>
        void ChangeSpeed(int NewBaudrate);

        /// <summary>
        /// Used to modify the standard timeout of the physical interface.
        /// </summary>
        /// <param name="Miliseconds">Timeout in milliseconds.</param>
        /// <exception cref="MeComPhyIntefaceException">Thrown when the underlying physical interface is not OK.</exception>
        void SetTimeout(int Miliseconds);
	}

    /// <summary>
    /// Represents timeout errors occur during data reception.
    /// </summary>
    public class MeComPhyTimeoutException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the Exception class with a specified error message.
        /// </summary>
        public MeComPhyTimeoutException()
        {
        }
    }
    /// <summary>
    /// Represents general physical interface errors.
    /// </summary>
    public class MeComPhyIntefaceException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the Exception class with a specified error message.
        /// </summary>
        public MeComPhyIntefaceException(string message)
            : base(message)
        {
        }
        /// <summary>
        /// Initializes a new instance of the Exception class with a specified error message.
        /// </summary>
        public MeComPhyIntefaceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

}
