using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using ReportTools;

namespace M_Bus_Spy
{
    class Program
    {
        public static DataSet mySetting;
        static bool echoCancellation = false;
        static int sleepTime;
        static int cycleTime;

        static List<Thread> threads = new List<Thread>();
        static List<SerialPortPlus> ports = new List<SerialPortPlus>();

        public static List<Telegram> telegrams;
        public static Thread MainThread { get; set; } = Thread.CurrentThread;

        private static void Initialize()
        {
            mySetting = new DataSet();
            telegrams = new List<Telegram>();

            try
            {
                //Read and save the settings
                mySetting.ReadXml($"{AppDomain.CurrentDomain.BaseDirectory}Settings.xml");

                //Initializa the ports
                InitializePorts();

                //Get General setting
                GetGeneralSettings();

                //Set database settings
                SetDBManager();

                Output.ConsoleTimeStamp = true;

                //Fill the telegrams table
                if (mySetting.Tables["tblTelegrams"] != null) FillTelegramsList();
                else Messenger.Enqueue(StandartMessages.MissingTable("tblTelegrams"));

            }
            catch (Exception ex)
            {
                Messenger.Enqueue(ex.Message);
            }
        }

        private static void Main(string[] args)
        {
            Initialize();
            Start();
            Output.Report(StandartMessages.AppStarted());
            //Console.WriteLine(Environment.NewLine + "Press ESC to Exit" + Environment.NewLine);
            //while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            while (true)
            {
                try
                {
                    if (Messenger.MessageCount > 0)
                    {
                        string message = Messenger.Dequeue();
                        Output.Report(message);
                    }
                }
                finally
                {
                    Thread.Sleep(250);
                }
            }
            //Stop();
            //Output.Report(StandartMessages.AppClosed());
        }

        private static void Start()
        {
            foreach (Thread tr in threads)
            {
                try
                {
                    tr.Start();
                    Messenger.Enqueue($"{tr.Name} started");
                }
                catch (Exception ex)
                {
                    Messenger.Enqueue($"{tr.Name} error: {ex.Message}");
                }
                Thread.Sleep(50);
            }
        }
        private static void Stop()
        {
            //Abort Threads
            foreach (Thread tr in threads)
            {
                try
                {
                    tr.Abort();
                    Console.WriteLine($"{tr.Name} aborted.");
                }
                catch { }
            }

            //Close ports
            foreach (SerialPort port in ports)
            {
                try
                {
                    port.Close();
                    Messenger.Enqueue($"{port.PortName} closed");
                }
                catch (Exception ex)
                {
                    Messenger.Enqueue(ex.Message);
                }
            }
        }

        #region Threads
        private static void SlaveThread(SerialPortPlus serialPort)
        {
            List<byte> serialBuffer = new List<byte>();
            byte[] lastResponse = new byte[0];
            //Try to open the port
            OpenPort(serialPort);
            //If the port didnt open return
            if (!serialPort.IsOpen) return;
            //clear the buffer
            serialPort.DiscardInBuffer();

            //Messenger.Enqueue($"{Thread.CurrentThread.Name} status: {Thread.CurrentThread.IsAlive.ToString()}");
            while (MainThread.IsAlive)
            {
                try
                {
                    int bytesToRead = serialPort.BytesToRead;
                    //Wait for a full telegram
                    if (serialPort.BytesToRead > 0)
                    {
                        Thread.Sleep(100);
                        if (serialPort.BytesToRead > bytesToRead) continue;
                        else bytesToRead = serialPort.BytesToRead;
                    }
                    else
                    {
                        Thread.Sleep(250);
                        continue;
                    }

                    //Read and save everything from the buffer
                    byte[] receivedBytes = new byte[bytesToRead];
                    //Console.WriteLine($"Bytes to read: {receivedBytes.Length}");
                    serialPort.Read(receivedBytes, 0, bytesToRead);

                    //If the received message is just an echo, ignore it
                    if (echoCancellation && IsEcho(lastResponse, receivedBytes))
                    {
                        Messenger.Enqueue(StandartMessages.EchoIgnored(serialPort, receivedBytes));
                        continue;
                    }
                    //Save the read bytes into the buffer
                    serialBuffer.InsertRange(serialBuffer.Count, receivedBytes);

                    //If there are known telegrams to listen for, do so
                    if (telegrams.Count > 0)
                    {
                        //For each telegram in the list 
                        for (int i = 0; i < telegrams.Count; i++)
                        {
                            Telegram telegram = telegrams[i];

                            //If a telegram is recognized
                            if (FindInBuffer(telegram.request, serialBuffer))
                            {
                                Respond(serialPort, telegram, ref lastResponse);

                                //If the telegram is of the default type clear the buffer,
                                //else keep the bytes and wait for next telegram
                                if (telegram.teleType != TeleType.Complex) serialBuffer.Clear();
                                break;
                            }
                            else if (i >= telegrams.Count - 1)
                            {
                                //Check if any of the master ports have TransferCommand enabled
                                SerialPortPlus sp;
                                if ((sp = GetPlcPort()) != null)
                                {
                                    lock (sp)
                                    {
                                        Messenger.Enqueue(StandartMessages.UnrecognizedTelegram(serialPort, serialBuffer) + $"...sending to {sp.PortName}");

                                        //Send the bytes to the port and get response
                                        List<byte> response = GetRawResponse(sp, serialBuffer.ToArray());
                                        byte[] arrResponse = response.ToArray();

                                        Messenger.Enqueue($"[{sp.PortName}]response is: [{Convertor.ByteToHex(arrResponse)}], transfering response to master...");

                                        //Send the responce back to the port that requested it
                                        Respond(serialPort, arrResponse);
                                        lastResponse = arrResponse;
                                        serialBuffer.Clear();
                                    }
                                }
                                else
                                {
                                    Messenger.Enqueue(StandartMessages.UnrecognizedTelegram(serialPort, serialBuffer));
                                    serialBuffer.Clear();
                                }
                            }
                        }
                    }
                    else //Sniff the port
                    {
                        Messenger.Enqueue(StandartMessages.CurrentBytes(serialPort, serialBuffer));
                        serialBuffer.Clear();
                    }

                    //Check connection
                    if (!serialPort.IsOpen) Reconnect(serialPort);
                    //Sleep between readings             
                    Thread.Sleep(250);
                }
                catch (Exception ex)
                {
                    Messenger.Enqueue(ex.Message);
                    serialBuffer.Clear();
                    serialPort.DiscardInBuffer();
                    Thread.Sleep(250);
                }
            }
            serialPort.Close();
        }

        private static void MasterThread(SerialPortPlus serialPort)
        {
            while (MainThread.IsAlive)
            {
                try
                {
                    //For each telegram
                    for (int i = 0; i < telegrams.Count; i++)
                    {
                        Telegram telegram = telegrams[i];

                        //If the telegram does not belong to this port, skip
                        if (telegram.PortID != serialPort.ID) continue;

                        //Get response                      
                        byte[] response = null;
                        ResponseResult arResponse = ResponseResult.Unindentified;

                        //Retry until response is correct
                        for (int k = 0; k < 8; k++)
                        {
                            try
                            {
                                lock (serialPort)
                                {
                                    arResponse = GetResponseMessage(serialPort, telegram, out response);
                                }

                                //If a correct response is received break the cycle
                                if (arResponse == ResponseResult.Correct) break;
                                //Sleep between tries
                                Thread.Sleep(TimeSpan.FromSeconds(sleepTime));
                            }
                            catch (Exception ex)
                            {
                                Messenger.Enqueue(ex.Message);
                            }
                        }

                        telegram.response = response;

                        //Sleep before sending next request
                        if (i < telegrams.Count - 1) Thread.Sleep(TimeSpan.FromSeconds(sleepTime));
                    }

                    //Update the database
                    DBManager.UpdateDB(serialPort.ID);

                    //Sleep
                    Thread.Sleep(TimeSpan.FromSeconds(cycleTime));
                }
                catch (Exception ex)
                {
                    Messenger.Enqueue(ex.Message);
                }
            }
            ClosePort(serialPort);
        }
        #endregion

        #region Methods  
        /// <summary>
        /// Finds and returns the first serialPort with TransferCommand field enabled
        /// </summary>
        /// <returns></returns>
        private static SerialPortPlus GetPlcPort()
        {
            SerialPortPlus output = null;
            foreach (SerialPortPlus port in ports)
            {
                if (port.TransferCommands == true)
                {
                    output = port;
                    break;
                }
            }
            return output;
        }

        private static void GetGeneralSettings()
        {
            DataRow dr = mySetting.Tables["tblGeneral"].Rows[0];
            sleepTime = Convert.ToInt32(dr["fldSleepTime"]);
            cycleTime = Convert.ToInt32(dr["fldCycleSleepTime"]);
            echoCancellation = Convertor.StringToBool((string)dr["fldEchoCancellation"]);
        }

        private static void SetDBManager()
        {
            DataRow dr = mySetting.Tables["tblDBSettings"].Rows[0];
            DBManager.Enabled = Convertor.StringToBool((string)dr["fldEnabled"]);
            DBManager.DBName = (string)dr["fldDBName"];
            DBManager.DBIP = (string)dr["fldServer"];
            DBManager.DBUser = (string)dr["fldAccount"];
            DBManager.DBPass = (string)dr["fldPassword"];
            DBManager.Init();
        }

        /// <summary>
        /// Determines if the received bytes are an echo of the last response
        /// </summary>
        /// <param name="lastResponse"></param>
        /// <param name="receivedBytes"></param>
        /// <returns></returns> 
        private static bool IsEcho(byte[] lastResponse, byte[] receivedBytes)
        {
            if (lastResponse.Length == 0) return false;
            return receivedBytes.SequenceEqual(lastResponse);
        }

        private static void OpenPort(SerialPortPlus serialPort)
        {
            try
            {
                serialPort.Open();
            }
            catch (Exception ex)
            {
                Messenger.Enqueue($"Can't open {serialPort.PortName}:" + ex.Message);
            }
        }

        private static void ClosePort(SerialPortPlus serialPort)
        {
            try
            {
                Thread.Sleep(50);
                serialPort.Close();
                //Messenger.Enqueue(serialPort.PortName + " closed.");
            }
            catch (Exception ex)
            {
                Messenger.Enqueue($"Can't close {serialPort.PortName}:" + ex.Message);
            }
        }

        private static void FillTelegramsList()
        {
            foreach (DataRow row in mySetting.Tables["tblTelegrams"].Rows)
            {
                Telegram telegram = new Telegram
                {
                    id = Convert.ToInt32(row["fldID"]),
                    request = Convertor.HexToByte((string)row["fldRX"]),
                    dictionary = Convertor.HexToByte((string)row["fldRealRX"]),
                    responseLength = Convert.ToInt32(row["fldTXLength"]),
                    teleType = GetTeleType(row["fldTeleType"]),
                    PortID = Convert.ToInt32(row["fldSerialPort"])
                };
                telegrams.Add(telegram);
                //if (telegram.request.Length < shortestTelegramLength) shortestTelegramLength = telegram.request.Length;
            }
        }

        private static void InitializePorts()
        {
            foreach (DataRow dr in mySetting.Tables["tblSettings"].Rows)
            {
                //Get serial port settings
                int id = Convert.ToInt32(dr["fldID"]);
                string threadName = (string)dr["fldThread"];
                string portName = (string)dr["fldPortName"];
                int baudRate = Convert.ToInt32(dr["fldBaudRate"]);
                Parity parity = GetParity(dr["fldParity"]);
                int dataBits = Convert.ToInt32(dr["fldDataBits"]);
                StopBits stopbits = GetStopBits(dr["fldStopBits"]);
                int readTimeout = Convert.ToInt32(dr["fldReadTimeout"]);
                bool enabled = Convertor.StringToBool((string)dr["fldEnabled"]);
                bool transferCommands = Convertor.StringToBool((string)dr["fldTransferCommands"]);

                if (!enabled) continue;
                SerialPortPlus serialPort = new SerialPortPlus(portName, id, baudRate, parity, dataBits, stopbits)
                {
                    ReadTimeout = readTimeout,
                    TransferCommands = transferCommands
                };

                ports.Add(serialPort);

                //Initialize the serial port
                switch (threadName.ToLower())
                {
                    case "master":
                        {
                            Thread tr = new Thread(() => MasterThread(serialPort))
                            {
                                Name = $"Thread{id}_{threadName.ToLower()}"
                            };
                            threads.Add(tr);
                            break;
                        }
                    case "slave":
                        {
                            Thread tr = new Thread(() => SlaveThread(serialPort))
                            {
                                Name = $"Thread{id}_{threadName.ToLower()}"
                            };
                            threads.Add(tr);
                            break;
                        }
                    default:
                        throw new NotImplementedException($"Bad input '{threadName}' for fldThread in Settings.xml");
                }
            }
        }

        private static void Respond(SerialPort serialPort, Telegram telegram, ref byte[] lastResponse)
        {

            if (telegram.response != null && telegram.responseLength > 0)
            {
                lastResponse = telegram.response;
                Respond(serialPort, telegram.response);
                Messenger.Enqueue(StandartMessages.RespondingToMaster(serialPort, telegram));
            }
            else
            {
                Messenger.Enqueue(StandartMessages.NoResponse(serialPort, telegram));
            }
        }

        private static void Respond(SerialPort serialPort, byte[] bytesToSend)
        {
            try
            {
                serialPort.Write(bytesToSend, 0, bytesToSend.Length);
            }
            catch (Exception ex)
            {
                Messenger.Enqueue(ex.Message);
            }
        }

        /// <summary>
        /// Gets the response message from the buffer and clears the buffer
        /// </summary>
        /// <param name="masterLength"></param>
        /// <param name="slaveLength"></param>
        /// <returns></returns>
        private static ResponseResult GetResponseMessage(SerialPortPlus serialPort, Telegram telegram, out byte[] result)
        {
            if (telegram.responseLength == 0)
            {
                Messenger.Enqueue(StandartMessages.TelegramSkiped(telegram));
                result = null;
                return ResponseResult.Skiped;
            }

            //If the telegrams dictionary is filled, ask for it instead
            byte[] request = telegram.dictionary.Length > 1 ? telegram.dictionary : telegram.request;

            List<byte> buffer = GetRawResponse(serialPort, request);

            //If the first byte in the buffer is not 68 or E5, add 68  in front
            //if (!((buffer[0] == HexToByte("68")[0]) || (buffer[0] == HexToByte("E5")[0]))) buffer.Insert(0, HexToByte("68")[0]);

            if (buffer.Count == telegram.responseLength) //correct response
            {
                result = buffer.ToArray();
                Messenger.Enqueue(StandartMessages.SlaveResponded(serialPort, result, request));
                return ResponseResult.Correct;
            }
            else if (buffer.Count == 0) //no response
            {
                Messenger.Enqueue(StandartMessages.RequestTimeout(serialPort, telegram));
                result = null;
                return ResponseResult.Timeout;
            }
            else //unexpected response
            {
                Messenger.Enqueue(StandartMessages.UnexpectedResponse(serialPort, telegram, buffer));
                result = buffer.ToArray();
                return ResponseResult.Incorrect;
            }
        }

        private static List<byte> GetRawResponse(SerialPortPlus serialPort, byte[] request)
        {
            List<byte> output = new List<byte>();
            Stopwatch stopWatch = new Stopwatch();

            //Open port
            OpenPort(serialPort);

            //Send request and start timeout timer
            serialPort.DiscardInBuffer();
            stopWatch.Start();

            //Send request
            Respond(serialPort, request);

            //Get response
            while (stopWatch.Elapsed.TotalMilliseconds < serialPort.ReadTimeout)
            {
                if (serialPort.BytesToRead > 0)
                {
                    do
                    {
                        //Read and save everything from the buffer
                        byte[] receivedBytes = new byte[serialPort.BytesToRead];
                        serialPort.Read(receivedBytes, 0, serialPort.BytesToRead);

                        //Save the read bytes into the buffer
                        output.InsertRange(output.Count, receivedBytes);
                        Thread.Sleep(200);
                    } while ((serialPort.BytesToRead > 0) && (stopWatch.Elapsed.TotalMilliseconds < serialPort.ReadTimeout));
                    stopWatch.Stop();
                    break;
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
            stopWatch.Stop();
            stopWatch = null;

            //Close port
            ClosePort(serialPort);

            return output;
        }

        /// <summary>
        /// Looks for the telegram in the beggining of the buffer, returns true if there is a match
        /// </summary>
        /// <param name="telegram"></param>
        /// <returns></returns>
        private static bool FindInBuffer(byte[] telegram, List<byte> buffer)
        {
            //Check if there are enough bytes in the buffer
            if (buffer.Count != telegram.Length) return false;
            return telegram.SequenceEqual(buffer.GetRange(0, telegram.Length));
        }

        /// <summary>
        /// Looks for the telegram in a List of telegrams, returns true if found
        /// </summary>
        /// <param name="needle"></param>
        /// <param name="haystack"></param>
        /// <returns></returns>
        private static bool FindInBuffer(byte[] needle, List<byte[]> haystack)
        {
            return haystack.Find(x => x.SequenceEqual(needle)) != null;
        }

        private static void Reconnect(SerialPort serialPort)
        {
            try
            {
                serialPort.Close();
            }
            catch
            {
                Messenger.Enqueue($"Failed to close {serialPort.PortNameShort()} while reconnecting");
            }
            try
            {
                serialPort.Open();
            }
            catch (Exception ex)
            {
                Messenger.Enqueue(ex.Message);
            }
        }

        private static Parity GetParity(object parity)
        {
            switch (((string)parity).ToLower())
            {
                case "none":
                    return Parity.None;
                case "even":
                    return Parity.Even;
                case "odd":
                    return Parity.Odd;
                case "mark":
                    return Parity.Mark;
                case "space":
                    return Parity.Space;
                default:
                    throw new NotImplementedException($"Parity type not existant '{parity}'");
            }
        }

        private static StopBits GetStopBits(object stopbits)
        {
            switch (Convert.ToInt32(stopbits))
            {
                case 0:
                    return StopBits.None;
                case 1:
                    return StopBits.One;
                case 2:
                    return StopBits.Two;
                case 3:
                    return StopBits.OnePointFive;
                default:
                    throw new NotImplementedException($"No such number of stopbits: {stopbits.ToString()}");
            }
        }

        private static TeleType GetTeleType(object type)
        {
            switch (((string)type).ToLower())
            {
                case "default":
                    return TeleType.Default;
                case "complex":
                    return TeleType.Complex;
                case "combined":
                    return TeleType.Combined;
                case "partial":
                    return TeleType.Partial;
                default:
                    throw new NotImplementedException($"No such type of telegram: '{(string)type}'");
            }
        }
        #endregion
    }
}