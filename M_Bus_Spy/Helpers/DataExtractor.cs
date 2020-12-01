using System;

namespace M_Bus_Spy
{
    /// <summary>
    /// Contains functions that extract the data from a byte array at specified index, multiplied by 'k'
    /// </summary>
    static class DataExtractor
    {
        public static string FromHex(int startAdress, string type, byte[] responseMessage, float k, Endian endian)
        {
            int size = Convert.ToInt32(type.Substring(3));
            //Get the value from the buffer
            byte[] value = new byte[size];
            Array.Copy(responseMessage, startAdress, value, 0, size);
            if (endian == Endian.Big) Array.Reverse(value); //To little endian
            //Convert to usable info
            string hexValue = Convertor.ByteToHex(value);
            hexValue = RemoveWhiteSpaces(hexValue);
            double result = (Int64.Parse(hexValue, System.Globalization.NumberStyles.HexNumber) * k);
            return result.ToString();
        }

        public static string FromBCD(int startAdress, string type, byte[] responseMessage, float k, bool signed, Endian endian)
        {
            int size = Convert.ToInt32(type.Substring(3));
            byte[] value = new byte[size / 2];
            Array.Copy(responseMessage, startAdress, value, 0, size / 2);
            if (endian == Endian.Big) Array.Reverse(value); //To little endian
            double result = (Convert.ToDouble(Convertor.BCD2String(value, signed))) * k;
            return result.ToString();
        }

        public static string FromReal(int startAdress, byte[] responseMessage, float k, Endian endian)
        {
            byte[] value = new byte[4];
            Array.Copy(responseMessage, startAdress, value, 0, 4);
            if (endian == Endian.Big) Array.Reverse(value); //To little endian
            return (BitConverter.ToSingle(value, 0) * k).ToString();
        }

        public static string FromInt(int startAdress, string type, byte[] responseMessage, float k, Endian endian)
        {
            int size = Convert.ToInt32(type.Substring(3));
            byte[] value = new byte[8];
            Array.Copy(responseMessage, startAdress, value, 0, size / 8);
            if (endian == Endian.Big) Array.Reverse(value); //To little endian 
            return (BitConverter.ToDouble(value, 0) * k).ToString();
        }

        private static string RemoveWhiteSpaces(string str)
        {
            return string.Join("", str.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        }

    }
}
