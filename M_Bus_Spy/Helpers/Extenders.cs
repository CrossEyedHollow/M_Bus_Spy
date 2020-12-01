using System.IO.Ports;

namespace M_Bus_Spy
{
    /// <summary>
    /// Containst extending methods for various classes
    /// </summary>
    public static class Extenders
    {
        public static string PortNameShort(this SerialPort port)
        {
            return port.PortName.Substring(port.PortName.Length - 4); //4 works for windows and unix platforms
        }
    }
}
