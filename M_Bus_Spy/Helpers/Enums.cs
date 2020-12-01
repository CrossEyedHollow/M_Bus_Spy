namespace M_Bus_Spy
{
    public enum ResponseResult
    {
        Correct,
        Incorrect,
        Timeout,
        Unindentified,
        Skiped,
        Checksum_Error
    }

    public enum TeleType
    {
        Default,
        Complex,
        Partial,
        Combined
    }

    public enum Endian
    {
        Big,
        Little
    }
}
