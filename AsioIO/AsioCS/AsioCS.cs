using System;
using System.Text;
using System.Runtime.InteropServices;

namespace AsioCS
{
    /** simple wrapper class of ASIO API
     */
    public class AsioWrap
    {
        [DllImport("AsioIODLL.dll")]
        private extern static int AsioWrap_getDriverNum();

        [DllImport("AsioIODLL.dll", CharSet = CharSet.Ansi)]
        private extern static bool AsioWrap_getDriverName(int n, System.Text.StringBuilder name, int size);

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

        [DllImport("AsioIODLL.dll", CharSet = CharSet.Ansi)]
        private extern static bool AsioWrap_getInputChannelName(int n, System.Text.StringBuilder name_return, int size);

        [DllImport("AsioIODLL.dll", CharSet = CharSet.Ansi)]
        private extern static bool AsioWrap_getOutputChannelName(int n, System.Text.StringBuilder name_return, int size);

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_setOutput(int channel, int[] data, int samples);

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_setInput(int inputChannel, int samples);

        [DllImport("AsioIODLL.dll")]
        private extern static int AsioWrap_start();

        [DllImport("AsioIODLL.dll")]
        private extern static bool AsioWrap_run();

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_stop();

        [DllImport("AsioIODLL.dll")]
        private extern static void AsioWrap_getRecordedData(int inputChannel, int [] recordedData_return, int samples);

        private bool driverLoaded = false;

        /////////////////////////////////////////////////////////////////////////

        /** returns num of Asio driver */
        public int DriverNumGet() {
            return AsioWrap_getDriverNum();
        }

        /** @param n asio driver index (0 ... DriverNumGet()-1)
         **/
        public string DriverNameGet(int n) {
            StringBuilder buf = new StringBuilder(64);
            AsioWrap_getDriverName(n, buf, 64);
            return buf.ToString();
        }

        /** load ASIO driver
         * @param n asio driver index (0 ... DriverNumGet()-1)
         */
        public bool DriverLoad(int n) {
            driverLoaded = AsioWrap_loadDriver(n);
            Console.WriteLine("AsioWrap_loadDriver({0}) rv={1}", n, driverLoaded);
            return driverLoaded;
        }

        /** unload ASIO driver 
         * @param n asio driver index (0 ... DriverNumGet()-1)
         */
        public void DriverUnload() {
            if (driverLoaded) {
                AsioWrap_unloadDriver();
                Console.WriteLine("AsioWrap_unloadDriver()");
                driverLoaded = false;
            }
        }

        /** set samplerate
         * @return 0: success. other: ASIOError
         */
        public int Setup(int sampleRate) {
            System.Diagnostics.Debug.Assert(driverLoaded);
            return AsioWrap_setup(sampleRate);
        }

        /** unset samplerate
         */
        public void Unsetup() {
            AsioWrap_unsetup();
        }

        /** returns num of input channels
         */
        public int InputChannelsNumGet() {
            return AsioWrap_getInputChannelsNum();
        }

        /** returns input channel name
         * @param n input channel index (0 ... InputChannelsNumGet()-1)
         */
        public string InputChannelNameGet(int n) {
            StringBuilder buf = new StringBuilder(64);
            AsioWrap_getInputChannelName(n, buf, buf.Capacity);
            return buf.ToString();
        }

        /** returns num of output channels
         */
        public int OutputChannelsNumGet() {
            return AsioWrap_getOutputChannelsNum();
        }

        /** returns output channel name
         * @param n output channel index (0 ... OutputChannelsNumGet()-1)
         */
        public string OutputChannelNameGet(int n) {
            StringBuilder buf = new StringBuilder(64);
            AsioWrap_getOutputChannelName(n, buf, buf.Capacity);
            return buf.ToString();
        }

        /** set num of receive input data samples
         * @param channel input channel (0 ... InputChannelsNumGet()-1)
         * @param samples num of samples to retrieve
         */
        public void InputSet(int channel, int samples) {
            AsioWrap_setInput(channel, samples);
        }

        /** send output sample data
         * @param channel output channel (0 ... OutputChannelsNumGet()-1)
         * @param outputData output sample data
         */
        public void OutputSet(int channel, int[] outputData) {
            AsioWrap_setOutput(channel, outputData, outputData.Length);
        }

        /** receive input data
         * @param channel input channel (0 ... InputChannelsNumGet()-1)
         * @param samples num of samples to retrieve
         */
        public int[] RecordedDataGet(int inputChannel, int samples) {
            int [] recordedData = new int[samples];
            AsioWrap_getRecordedData(inputChannel, recordedData, samples);
            return recordedData;
        }

        /** start input/output tasks
         * @return 0: success. other: ASIOError
         */
        public int Start() {
            return AsioWrap_start();
        }

        /** stop input/output tasks
         */
        public void Stop() {
            AsioWrap_stop();
        }

        /** run looper (this is a blocking function. call from dedicated thread)
         */
        public bool Run() {
            return AsioWrap_run();
        }

        public string AsioTrademarkStringGet() {
            return "ASIO is a trademark and software of Steinberg Media Technologies GmbH";
        }
    }
}
