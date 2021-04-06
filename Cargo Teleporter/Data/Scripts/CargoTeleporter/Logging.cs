using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CargoTeleporter
{
    public static class Logging
    {
        private static TextWriter writer = null;
        public static void setup()
        {
            try
            {
                writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("CargoTeleport" + ".log", typeof(Logging));
            }
            catch { }
        }

        public static void WriteLine(string s)
        {
            try
            {
                if (writer == null)
                    setup();
                writer.WriteLine(DateTime.Now.ToString("[HH:mm:ss] ") + s);
                writer.Flush();
            }
            catch { }
        }

        public static void close()
        {
            try
            {
                if (writer != null)
                {
                    writer.Flush();
                    writer.Close();
                }
            }
            catch { }

        }
    }
}
