using ConnectionTools;
using MySql.Data.MySqlClient;

namespace M_Bus_Spy
{
    /// <summary>
    /// Manages the database, call Init() after any parameter change to apply changes
    /// </summary>
    public class DBBase
    {
        protected DBBase() { }

        public static string DBName { get; set; }

        public static string DBIP { get; set; }

        public static string DBUser { get; set; }

        public static string DBPass { get; set; }

        protected static MySqlConnection conn;
        protected static MySqlCommand cmd;

        /// <summary>
        /// Call this function to Initialize the MySql objects or apply new settings
        /// </summary>
        public static void Init()
        {
            string connString = DataBaseTools.GenerateConnectionString(DBIP, DBUser, DBPass);
            conn = new MySqlConnection(connString);
            cmd = new MySqlCommand("", conn);
        }

        public static void Connect()
        {
            try
            {
                conn.Open();
            }
            catch (System.Exception ex)
            {
                Messenger.Enqueue($"Failed to connect to database: {ex.Message}");
            }
        }
        public static void Disconnect()
        {
            try
            {
                conn.Close();
            }
            catch (System.Exception ex)
            {
                Messenger.Enqueue($"Failed to disconnect from database: {ex.Message}");
            }
        }
    }
}
