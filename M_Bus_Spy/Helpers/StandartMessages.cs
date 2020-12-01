using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace M_Bus_Spy
{
    static public class StandartMessages
    {
        public static string EchoIgnored(SerialPort serialPort, byte[] receivedBytes)
        {
            return $"[{serialPort.PortNameShort()}]Echo ignored: {Convertor.ByteToHex(receivedBytes)}";
        }

        public static string UnrecognizedTelegram(SerialPort serialPort, List<byte> serialBuffer)
        {
            return $"[{serialPort.PortNameShort()}]Unrecognized telegram[{serialBuffer.Count}]: {Convertor.ByteToHex(serialBuffer.ToArray())}";
        }

        public static string CurrentBytes(SerialPort serialPort, List<byte> serialBuffer)
        {
            return $"[{serialPort.PortNameShort()}]Current bytes[{serialBuffer.Count}]: {Convertor.ByteToHex(serialBuffer.ToArray())}";
        }

        public static string RespondingToMaster(SerialPort serialPort, Telegram telegram)
        {
            return $"[{serialPort.PortNameShort()}]Responding to {Convertor.ByteToHex(telegram.request)}, bytes[{telegram.response.Length}]: {Convertor.ByteToHex(telegram.response)}";
        }

        public static string NoResponse(SerialPort serialPort, Telegram telegram)
        {
            return $"[{serialPort.PortNameShort()}]Master request: {Convertor.ByteToHex(telegram.request)} had no response from the slave";
        }

        public static string TelegramSkiped(Telegram telegram)
        {
            return $"Telegram {Convertor.ByteToHex(telegram.request)} skiped (responseLength = 0)";
        }

        public static string SlaveResponded(SerialPort serialPort, byte[] result, byte[] request)
        {
            return $"[{serialPort.PortNameShort()}]Slave responded to telegram[{Convertor.ByteToHex(request)}], saving telegram to table, bytes[{result.Length}]: {Convertor.ByteToHex(result)}";
        }

        public static string RequestTimeout(SerialPort serialPort, Telegram telegram)
        {
            return $"[{serialPort.PortNameShort()}]Request timeout, request: {Convertor.ByteToHex(telegram.request)}";
        }

        public static string UnexpectedResponse(SerialPort serialPort, Telegram telegram, List<byte> buffer)
        {
            return $"[{serialPort.PortNameShort()}]Unexpected response length ({telegram.responseLength}) to telegram: {Convertor.ByteToHex(telegram.request)}, received[{buffer.Count}]: {Convertor.ByteToHex(buffer.ToArray())}";
        }

        public static string MissingTable(string name)
        {
            return $"'{name}' not found in settings.xml, proceeding in sniff mode...";
        }

        public static string AppStarted()
        {
            return $"App started: {DateTime.Now.ToString()}";
        }

        public static string AppClosed()
        {
            return $"App closed: {DateTime.Now.ToString()}";
        }
    }
}
