using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Wasapiex {
    public class WasapiCS {
        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Init();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Term();

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_DoDeviceEnumeration();

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_GetDeviceCount();

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Auto)]
        private extern static bool 
        WasapiIO_GetDeviceName(int id, System.Text.StringBuilder name, int nameBytes);

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Auto)]
        private extern static bool
        WasapiIO_InspectDevice(int id, System.Text.StringBuilder result, int resultBytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_ChooseDevice(int id);

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_Setup(int sampleRate, int bitsPerSample, int latencyMillisec);

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_Unsetup();

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_SetOutputData(byte[] data, int bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_ClearOutputData();

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_Start();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool 
        WasapiIO_Run(int millisec);

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_Stop();

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_GetPosFrame();

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_GetTotalFrameNum();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_SetPosFrame(int v);

        public int Init() {
            return WasapiIO_Init();
        }

        public void Term() {
            WasapiIO_Term();
        }

        public int DoDeviceEnumeration() {
            return WasapiIO_DoDeviceEnumeration();
        }

        public int GetDeviceCount() {
            return WasapiIO_GetDeviceCount();
        }

        public string GetDeviceName(int id) {
            StringBuilder buf = new StringBuilder(64);
            WasapiIO_GetDeviceName(id, buf, buf.Capacity*2);
            return buf.ToString();
        }

        public string InspectDevice(int id) {
            StringBuilder buf = new StringBuilder(65536);
            WasapiIO_InspectDevice(id, buf, buf.Capacity * 2);
            return buf.ToString();
        }

        public int ChooseDevice(int id) {
            return WasapiIO_ChooseDevice(id);
        }

        public int Setup(int sampleRate, int bitsPerSample, int latencyMillisec) {
            return WasapiIO_Setup(sampleRate, bitsPerSample, latencyMillisec);
        }

        public void Unsetup() {
            WasapiIO_Unsetup();
        }

        public void SetOutputData(byte[] data) {
            WasapiIO_SetOutputData(data, data.Length);
        }

        public void ClearOutputData() {
            WasapiIO_ClearOutputData();
        }

        public int Start() {
            return WasapiIO_Start();
        }

        public bool Run(int millisec) {
            return WasapiIO_Run(millisec);
        }

        public void Stop() {
            WasapiIO_Stop();
        }

        public int GetPosFrame() {
            return WasapiIO_GetPosFrame();
        }

        public int GetTotalFrameNum() {
            return WasapiIO_GetTotalFrameNum();
        }

        public bool SetPosFrame(int v) {
            return WasapiIO_SetPosFrame(v);
        }

    }
}
