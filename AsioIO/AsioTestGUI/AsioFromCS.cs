using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace AsioTestGUI
{
    class AsioFromCS
    {
        [DllImport("AsioIODLL.dll")]
        private extern static int
            AsioDriverLoad_getDriverNum();

        [DllImport("AsioIODLL.dll")]
        private extern static bool
            AsioDriverLoad_getDriverName(int n, System.Text.StringBuilder name, int size);

        [DllImport("AsioIODLL.dll")]
        private extern static bool AsioDriverLoad_loadDriver(int n);

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioDriverLoad_unloadDriver();


        public static int DriverNumGet() {
            return AsioDriverLoad_getDriverNum();
        }

        public static string DriverNameGet(int n) {
            StringBuilder buf = new StringBuilder(64);
            AsioDriverLoad_getDriverName(n, buf, buf.Capacity);
            return buf.ToString();
        }

        static bool driverLoaded = false;

        public static bool DriverLoad(int n) {
            driverLoaded = AsioDriverLoad_loadDriver(n);
            System.Console.WriteLine("AsioDriverLoad_loadDriver({0}) rv={1}", n, driverLoaded);
            return driverLoaded;
        }

        public static void DriverUnload() {
            if (driverLoaded) {
                AsioDriverLoad_unloadDriver();
                System.Console.WriteLine("AsioDriverLoad_unloadDriver()");
                driverLoaded = false;
            }
        }
    }
}
