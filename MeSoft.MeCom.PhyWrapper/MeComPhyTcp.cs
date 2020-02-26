using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace MeSoft.MeCom.PhyWrapper
{
    /// <summary>
    /// Implements the IMeComPhy interface for the TCP network interface. 
    /// </summary>
    public class MeComPhyTcp: IMeComPhy
    {
        TcpClient tcpClient;
        NetworkStream networkStream;

        /// <summary>
        /// Opens a TCP connection to the specified host.
        /// Sets the send and receive timeout to 500ms.
        /// This function may block the thread up to 10s if the host does not react.
        /// </summary>
        /// <param name="hostname">Host name to connect.</param>
        /// <param name="port">Port to connect.</param>
        public void OpenClient(string hostname, int port)
        {
            try
            {
                tcpClient = new TcpClient();
                tcpClient.SendTimeout = 500;
                tcpClient.ReceiveTimeout = 500;
                tcpClient.Connect(hostname, port);
                networkStream = tcpClient.GetStream();
            }
            catch (Exception ex)
            {
                throw new MeComPhyIntefaceException("Failure during opening: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Closes the connection. Ignores all exceptions.
        /// </summary>
        public void CloseClient()
        {
            try
            {
                tcpClient.Close();
                networkStream.Close();
            }
            catch
            {
                //Ignore problem during close
            }
        }

        void IMeComPhy.SendString(System.IO.MemoryStream Stream)
        {          
            try
            {
                Stream.WriteTo(networkStream);
            }
            catch (Exception ex)
            {
                throw new MeComPhyIntefaceException("Failure during sending: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Blocks till some data has been received. 
        /// Does not thorw any timeout exception, because resend is not necessary over TCP.
        /// </summary>
        void IMeComPhy.GetDataOrTimeout(System.IO.MemoryStream Stream)
        {
            byte[] bytes = new byte[1024];
            try
            {
                int readLength = networkStream.Read(bytes, 0, bytes.Length);
                if (readLength == 0) throw new MeComPhyIntefaceException("No data received!");
                Stream.Write(bytes, 0, readLength);
            }
            catch (Exception ex)
            {
                throw new MeComPhyIntefaceException("Failure during receiving: " + ex.Message, ex);
            }
        }

        void IMeComPhy.ChangeSpeed(int NewBaudrate)
        {
            throw new NotImplementedException("This function is not supported on a TCP interface!");
        }

        void IMeComPhy.SetTimeout(int Miliseconds)
        {
            tcpClient.SendTimeout = Miliseconds;
            tcpClient.ReceiveTimeout = Miliseconds;
        }
    }
}
