using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FTD2XX_NET;
using System.IO;

namespace MeSoft.MeCom.PhyWrapper
{
    /// <summary>
    /// Implements the IMeComPhy interface for the FTDI chip drivers. 
    /// </summary>
    public class MeComPhyFTDI : FTDI, IMeComPhy
    {
        /// <summary>
        /// Initializes the FTDI default settings, so that is it usually running with meerstetter products.
        /// This function is not part of the IMeComPhy interface.
        /// </summary>
        /// <param name="Baudrate">Baud Rate for the Serial Interface.</param>
        public void MeComSetDefaultSettings(int Baudrate)
        {
            FTDI.FT_STATUS ftStatus;

            ftStatus = base.SetBaudRate(Convert.ToUInt32(Baudrate));
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());

            ftStatus = base.SetDataCharacteristics(FT_DATA_BITS.FT_BITS_8, FT_STOP_BITS.FT_STOP_BITS_1, FT_PARITY.FT_PARITY_NONE);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());

            ftStatus = base.SetFlowControl(FT_FLOW_CONTROL.FT_FLOW_NONE, 0, 0);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());

            ftStatus = base.SetTimeouts(300, 300);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());

            ftStatus = base.SetLatency(3);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());

            ftStatus = base.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());
        }

        void IMeComPhy.SendString(MemoryStream Stream)
        {
            byte[] TxData = Stream.ToArray();

            FTDI.FT_STATUS ftStatus;
            uint WrittenBytes = 0;
            ftStatus = base.Write(TxData, TxData.Length, ref WrittenBytes);

            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());
            if (TxData.Length != WrittenBytes) throw new MeComPhyIntefaceException("FTDI has not sent all bytes");
        }


        void IMeComPhy.GetDataOrTimeout(MemoryStream Stream)
        {
            FTDI.FT_STATUS ftStatus;
            uint ToRead;
            byte[] outData = new byte[512];

            uint numBytesAvailable = 0;
            ftStatus = base.GetRxBytesAvailable(ref numBytesAvailable);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());
            if (numBytesAvailable == 0) ToRead = 1;
            else if (numBytesAvailable > outData.Length) ToRead = Convert.ToUInt32(outData.Length);
            else ToRead = numBytesAvailable;

            uint ReadBytes = 0;
            ftStatus = base.Read(outData, ToRead, ref ReadBytes);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());

            Stream.Write(outData, 0, Convert.ToInt32(ReadBytes));

            if (ReadBytes == 0) throw new MeComPhyTimeoutException();
        }

        void IMeComPhy.ChangeSpeed(int NewBaudrate)
        {
            FTDI.FT_STATUS ftStatus;
            ftStatus = base.SetBaudRate(Convert.ToUInt32(NewBaudrate));
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());
        }


        void IMeComPhy.SetTimeout(int Miliseconds)
        {
            FTDI.FT_STATUS ftStatus;

            ftStatus = base.SetTimeouts(Convert.ToUInt32(Miliseconds), 300);
            if (ftStatus != FT_STATUS.FT_OK) throw new MeComPhyIntefaceException(ftStatus.ToString());
        }
    }
}
