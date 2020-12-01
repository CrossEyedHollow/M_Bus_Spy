namespace M_Bus_Spy
{
    public class Telegram
    {
        public int id;
        public byte[] request;
        public byte[] response;
        public byte[] dictionary;
        public int responseLength;
        public TeleType teleType;
        public int PortID { get; set; }
    }
}
