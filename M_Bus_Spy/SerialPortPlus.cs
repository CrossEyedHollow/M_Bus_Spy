using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace M_Bus_Spy
{
    class SerialPortPlus : SerialPort
    {
        public int ID { get; set; }
        public bool TransferCommands { get; set; } = false;

        public SerialPortPlus(string portName, int id, int baudRate, Parity parity, int dataBits, StopBits stopbits)
        {
            PortName = portName;
            ID = id;
            BaudRate = baudRate;
            Parity = parity;
            DataBits = dataBits;
            StopBits = stopbits;
        }
    }
}
