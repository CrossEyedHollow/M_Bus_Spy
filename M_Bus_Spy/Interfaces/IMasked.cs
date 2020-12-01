namespace M_Bus_Spy
{
    public interface IMasked : ITelegram
    {
        byte[] Mask { get; set; }
    }
}
