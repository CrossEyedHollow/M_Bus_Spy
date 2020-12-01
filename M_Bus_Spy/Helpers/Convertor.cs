using System;

namespace M_Bus_Spy
{
    /// <summary>
    /// Contains funcions for data conversion
    /// </summary>
    static class Convertor
    {
        /// <summary>
        /// Converts a Bit-coded Decimal into a string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string BCD2String(byte[] bytes, bool signed)
        {
            string result = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                //If the value might have a sign check the first byte
                if (signed && (i == 0))
                {
                    //If the first byte is == 1111 xxxx
                    if ((bytes[i] & 0xF0) == 0xF0)
                    {
                        //Remove the first semibyte F and add make the result negative
                        bytes[i] = (byte)((bytes[i]) & 0x0F);
                        result += "-";
                    }
                }
                int digit = (bytes[i] >> 4) & 0xF;
                result += digit.ToString();
                digit = bytes[i] & 0xF;
                result += digit.ToString();
            }
            return Convert.ToInt64(result).ToString(); //the Convert removes the zeros in front, BCD12 can be bigger than 32 bit int
        }

        /// <summary>
        /// Converts Byte[] into a hex string with white space separator
        /// </summary>
        /// <param name="receivedMessage"></param>
        /// <returns></returns>
        public static string ByteToHex(byte[] receivedMessage)
        {
            if (receivedMessage == null) return "null message";
            return BitConverter.ToString(receivedMessage).Replace("-", " ");
        }

        /// <summary>
        /// Converts a hex string, separated by space into a Byte[]
        /// </summary>
        /// <param name="telegram"></param>
        /// <returns></returns>
        public static byte[] HexToByte(string telegram)
        {
            string[] rx = telegram.Split(' ');
            byte[] output = new byte[rx.Length];
            for (int i = 0; i < output.Length; i++) output[i] = Convert.ToByte(rx[i], 16);
            return output;
        }

        public static bool StringToBool(string v)
        {
            return Convert.ToBoolean(v);
        }

        public static Endian StringToEndian(string input)
        {
            switch (input.ToLower())
            {
                case "big":
                    return Endian.Big;
                case "little":
                    return Endian.Little;
                default:
                    throw new NotImplementedException($"No such endian type: '{input}'");
            }
        }
    }
}
