using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace M_Bus_Spy
{
    static class Messenger
    {
        private static Queue<string> messages = new Queue<string>();

        public static int MessageCount
        {
            get { return messages.Count; }
        }
        public static void Enqueue(string message)
        {
            messages.Enqueue(message);
        }
        public static string Dequeue()
        {
            return messages.Dequeue();
        }
    }
}
