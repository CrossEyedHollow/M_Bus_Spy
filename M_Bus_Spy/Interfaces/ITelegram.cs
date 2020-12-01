namespace M_Bus_Spy
{
    public interface ITelegram
    {
        int Id { get; set; }
        byte[] Request { get; set; }
        byte[] Response { get; set; }
        int ResponseLength { get; }
    }
}
