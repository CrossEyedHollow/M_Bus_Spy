using System;
using System.Linq;

namespace M_Bus_Spy
{
    public static class Checksum
    {
        public static bool Check(byte[] msgArr)
        {
            //If telegram starts with 10 and ends with 16, its short telegram
            if ((msgArr[0] == 0x10) && (msgArr[msgArr.Length - 1] == 0x16))
            {
                //Calculate msg summ
                byte sum = ShortTelegramSum(msgArr);
                //Compare to given sum
                return msgArr[msgArr.Length - 2] == sum;
            }
            //If Long telegram
            else if ((msgArr[0] == 0x68) && (msgArr[msgArr.Length - 1] == 0x16))
            {
                //Calculate msg summ
                byte sum = LongTelegramSum(msgArr);
                //Compare to given sum
                return msgArr[msgArr.Length - 2] == sum;
            }
            //If Single char telegram
            else if ((msgArr[0] == 0xE5) && (msgArr.Length == 1))
            {
                //Do not check
                return true;
            }
            else //Not an M_Bus msg
            {
                return true;
            }
        }

        public static byte ShortTelegramSum(byte[] msgArr)
        {
            //Starts from the data length byte ends at CS excluded
            return Calculate(msgArr, 1);
        }

        public static byte LongTelegramSum(byte[] msgArr)
        {
            //Calculated from the second start byte (excluded) to CS byte (excluded)
            return Calculate(msgArr, 4);
        }

        public static byte Calculate(byte[] msgArr, int startByte)
        {
            //Get only the bytes relevant for the checksum
            byte[] relevatBytes = new byte[msgArr.Length - (startByte + 2)];
            Array.Copy(msgArr, startByte, relevatBytes, 0, relevatBytes.Length);

            //Calculate sum
            long sum = relevatBytes.Sum(x => (long)x);
            byte[] sumBytes = BitConverter.GetBytes(sum);

            //Return only the first byte
            return sumBytes[0];
        }
    }
}
