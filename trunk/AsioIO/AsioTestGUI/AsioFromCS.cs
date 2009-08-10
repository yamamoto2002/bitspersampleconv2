/*
    AsioIO
    Copyright (C) 2009 Yamamoto DIY Software Lab.

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/

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
