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
            AsioWrap_getDriverNum();

        [DllImport("AsioIODLL.dll")]
        private extern static bool
            AsioWrap_getDriverName(int n, System.Text.StringBuilder name, int size);

        [DllImport("AsioIODLL.dll")]
        private extern static bool AsioWrap_loadDriver(int n);

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_unloadDriver();

        [DllImport("AsioIODLL.dll")]
        private extern static int AsioWrap_setup(int sampleRate);

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_unsetup();

        [DllImport("AsioIODLL.dll")]
        private extern static int AsioWrap_getInputChannelsNum();

        [DllImport("AsioIODLL.dll")]
        private extern static int AsioWrap_getOutputChannelsNum();

        [DllImport("AsioIODLL.dll")]
        private extern static bool AsioWrap_getInputChannelName(int n, System.Text.StringBuilder name_return, int size);

        [DllImport("AsioIODLL.dll")]
        private extern static bool AsioWrap_getOutputChannelName(int n, System.Text.StringBuilder name_return, int size);

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_setOutputData(int channel, int[] data, int length);

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_run();

        /////////////////////////////////////////////////////////////////////////

        public string OutputChannelNameGet(int n) {
            StringBuilder buf = new StringBuilder(64);
            AsioWrap_getOutputChannelName(n, buf, buf.Capacity);
            return buf.ToString();
        }

        public int DriverNumGet() {
            return AsioWrap_getDriverNum();
        }

        public string DriverNameGet(int n) {
            StringBuilder buf = new StringBuilder(64);
            AsioWrap_getDriverName(n, buf, buf.Capacity);
            return buf.ToString();
        }

        bool driverLoaded = false;

        public bool DriverLoad(int n) {
            driverLoaded = AsioWrap_loadDriver(n);
            System.Console.WriteLine("AsioWrap_loadDriver({0}) rv={1}", n, driverLoaded);
            return driverLoaded;
        }

        public void DriverUnload() {
            if (driverLoaded) {
                AsioWrap_unloadDriver();
                System.Console.WriteLine("AsioWrap_unloadDriver()");
                driverLoaded = false;
            }
        }

        public int Setup(int sampleRate) {
            System.Diagnostics.Debug.Assert(driverLoaded);

            return AsioWrap_setup(sampleRate);
        }

        public void Unsetup() {
            AsioWrap_unsetup();
        }

        public int InputChannelsNumGet() {
            return AsioWrap_getInputChannelsNum();
        }

        public int OutputChannelsNumGet() {
            return AsioWrap_getOutputChannelsNum();
        }

        public string InputChannelNameGet(int n) {
            StringBuilder buf = new StringBuilder(64);
            AsioWrap_getInputChannelName(n, buf, buf.Capacity);
            return buf.ToString();
        }

        public void OutputDataSet(int channel, int [] outputData) {
            AsioWrap_setOutputData(channel, outputData, outputData.Length);
        }

        public void Run() {
            AsioWrap_run();
        }
    }
}
