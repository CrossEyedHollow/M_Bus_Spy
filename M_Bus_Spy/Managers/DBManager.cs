using ConnectionTools;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace M_Bus_Spy
{
    /// <summary>
    /// Manages the database, call Init() after any parameter change to apply changes
    /// </summary>
    class DBManager : DBBase
    {
        protected DBManager() { }

        public static bool Enabled { get; set; } = false;

        private static Regex regexAZ = new Regex("^[A-z]+");

        private static string GenerateQuery(byte[] responseMessage, DataRow[] dataInfo, string table)
        {
            string result = $"INSERT INTO `{DBName}`.`{table}` (";
            List<string> values = new List<string>();
            foreach (DataRow dr in dataInfo)
            {
                if ((string)dr["fldTable"] == table)
                {
                    //TODO apply IColumn interface to avoid the list of string
                    int startAdress = Convert.ToInt32(dr["fldStartAdress"]);
                    string type = (string)dr["fldType"];
                    string fieldName = (string)dr["fldColumn"];
                    float k = Convert.ToSingle(dr["fldK"]);
                    bool signed = Convertor.StringToBool((string)dr["fldSigned"]);
                    Endian endian = Convertor.StringToEndian((string)dr["fldEndian"]);
                    string value = GetValueFromMessage(startAdress, type, responseMessage, k, signed, endian);
                    value = value.Replace(',', '.');
                    values.Add(value);
                    result += $"{fieldName}, ";

                }
            }
            // remove the ', ' at the end
            result = result.Remove(result.Length - 2, 2);
            result += ") VALUES (";
            foreach (string str in values)
            {
                result += $"{str}, ";
            }
            // remove the ', ' at the end
            result = result.Remove(result.Length - 2, 2);
            result += "); ";
            return result;
        }

        private static string GetValueFromMessage(int startAdress, string type, byte[] responseMessage, float k, bool signed, Endian endian)
        {
            type = type.ToLower();
            Match matchAZ = regexAZ.Match(type);
            switch (matchAZ.Value)
            {
                case "int":
                    {
                        return DataExtractor.FromInt(startAdress, type, responseMessage, k, endian);
                    }
                case "real":
                    {
                        return DataExtractor.FromReal(startAdress, responseMessage, k, endian);
                    }
                case "bcd":
                    {
                        return DataExtractor.FromBCD(startAdress, type, responseMessage, k, signed, endian);
                    }
                case "hex":
                    {
                        return DataExtractor.FromHex(startAdress, type, responseMessage, k, endian);
                    }
                default:
                    throw new NotImplementedException($"'{type}' not recognized, insert a valid type in the settings.xml");
            }
        }

        public static void UpdateDB(int portID)
        {
            foreach (Telegram telegram in Program.telegrams)
            {
                //Update the db only for the current port
                if (telegram.PortID != portID) continue;
                //Skip any partial telegrams
                if (telegram.teleType == TeleType.Partial) continue;

                //If the telegram is not of the combined type
                if (telegram.teleType != TeleType.Combined)
                {
                    //If the response is null, skip
                    if (telegram.response == null) continue;

                    //If the telegram length doesnt match the expected length, skip
                    if (telegram.response.Length != telegram.responseLength) continue;
                    try
                    {
                        WriteToDatabase(telegram);
                    }
                    catch (Exception ex)
                    {
                        Messenger.Enqueue(ex.Message);
                    }
                }
                else //Combined type logic
                {
                    try
                    {
                        WriteToDatabase(Program.telegrams.FindAll(x => x.teleType == TeleType.Partial), telegram);
                    }
                    catch (Exception ex)
                    {
                        Messenger.Enqueue(ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the data from the responseMessage and writes to the DB
        /// </summary>
        /// <param name="telegram"></param>
        /// <param name="responseMessage"></param>
        /// <param name="teleInfo"></param>
        private static void WriteToDatabase(Telegram telegram)
        {
            if (telegram.responseLength < 1) return;
            if (Program.mySetting.Tables["tblData"] == null)
            {
                Messenger.Enqueue($"'tblData' not found in Settings.xml for telegram id: {telegram.id}");
                return;
            }

            //Get the rows containings the data information 
            DataRow[] dataInfo = Program.mySetting.Tables["tblData"].Select($"fldID = '{telegram.id}'");
            if (dataInfo.Length < 1)
            {
                if (telegram.teleType == TeleType.Default)
                {
                    Messenger.Enqueue($"No rows found in tblData for telegram id: {telegram.id}");
                }
                return;
            }

            //TODO Check for bugs
            //Get a DISTINCT list of the tables we are going to send the data to
            DataView view = Program.mySetting.Tables["tblData"].DefaultView;
            view.RowFilter = $"fldID = '{telegram.id}'";
            List<string> distinctTables = view
                .ToTable(true, "fldTable")
                .Rows
                .OfType<DataRow>().
                Select(dr => dr.Field<string>("fldTable")) //return each row of that column as string
                .ToList();

            //Create the insert query
            string query = "";
            foreach (string tbl in distinctTables)
            {
                query += GenerateQuery(telegram.response, dataInfo, tbl);
            }

            //Execute query            
            cmd.CommandText = query;
            DBBase.Connect();

            try
            {
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBBase.Disconnect();
            }
            Messenger.Enqueue(query);



            //Program.telegrams.Find(x => x.id == 1);
        }
        private static void WriteToDatabase(List<Telegram> telegrams, Telegram combined)
        {
            //Existence check
            DataRow[] combinedInfo = Program.mySetting.Tables["tblData"].Select($"fldID = '{combined.id}'");
            if (combinedInfo.Length < 1)
            {
                Messenger.Enqueue($"No rows found in tblData for telegram id: {combined.id}");
                return;
            }

            //Get the table name
            string tableName = (string)combinedInfo[0]["fldTable"];

            //Create the insert query
            string query = $"INSERT INTO `{DBName}`.`{tableName}` (";

            //Create a list of all columns
            List<IColumn> columns = new List<IColumn>();

            //Cycle trought the partial telegrams and add each found column to the list
            foreach (Telegram telegram in telegrams)
            {
                //TODO Encapsulate as Verification
                if (telegram.responseLength < 1) continue;
                if (telegram.response == null) continue;
                if (telegram.response.Length != telegram.responseLength) continue;

                //Get the rows containings the data information 
                DataRow[] dataInfo = Program.mySetting.Tables["tblData"].Select($"fldID = '{telegram.id}'");
                if (dataInfo.Length < 1)
                {
                    Messenger.Enqueue($"No rows found in tblData for telegram id: {telegram.id}");
                    continue;
                }

                foreach (DataRow dr in dataInfo)
                {
                    int startAdress = Convert.ToInt32(dr["fldStartAdress"]);
                    string type = (string)dr["fldType"];
                    string fieldName = (string)dr["fldColumn"];
                    float k = Convert.ToSingle(dr["fldK"]);
                    bool signed = Convertor.StringToBool((string)dr["fldSigned"]);
                    Endian endian = Convertor.StringToEndian((string)dr["fldEndian"]);
                    string value = GetValueFromMessage(startAdress, type, telegram.response, k, signed, endian);
                    value = value.Replace(',', '.');

                    IColumn temp = new Column() { Name = fieldName, Value = value };
                    columns.Add(temp);
                }
            }

            //Add the fields to the query
            foreach (IColumn column in columns)
            {
                query += $"{column.Name}, ";
            }

            // remove the ', ' at the end
            query = query.Remove(query.Length - 2, 2);
            query += ") VALUES (";

            //Add the values to the query
            foreach (IColumn column in columns)
            {
                query += $"{column.Value}, ";
            }
            // remove the ', ' at the end
            query = query.Remove(query.Length - 2, 2);
            query += "); ";

            //Execute query            
            cmd.CommandText = query;
            DBBase.Connect();
            try
            {
                cmd.ExecuteNonQuery();
            }
            finally
            {
                DBBase.Disconnect();
            }
            Messenger.Enqueue(query);
        }
    }
}
