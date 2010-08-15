using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Wasapi {
    public class WasapiCS {
        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_Init();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_Term();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetSchedulerTaskType(int t);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetShareMode(int sm);

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_DoDeviceEnumeration(int deviceType);

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
        WasapiIO_Setup(int mode, int sampleRate, int bitsPerSample, int latencyMillisec);

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_Unsetup();

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_AddPlayPcmData(int id, byte[] data, int bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_ClearPlayList();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetPlayRepeat(bool repeat);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetNowPlayingPcmDataId();

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetNowPlayingPcmDataId(int id);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetupCaptureBuffer(int bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetCapturedData(byte[] data, int bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetCaptureGlitchCount();

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

        public enum SchedulerTaskType {
            Audio,
            ProAudio
        };

        public void SetSchedulerTaskType(SchedulerTaskType t) {
            WasapiIO_SetSchedulerTaskType((int)t);
        }

        public enum ShareMode {
            Shared,
            Exclusive
        };

        public void SetShareMode(ShareMode t) {
            WasapiIO_SetShareMode((int)t);
        }

        public enum DeviceType {
            Play,
            Rec
        };

        public int DoDeviceEnumeration(DeviceType t) {
            return WasapiIO_DoDeviceEnumeration((int)t);
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

        public enum DataFeedMode {
            EventDriven,
            TimerDriven,
        };

        public int Setup(DataFeedMode mode, int sampleRate, int bitsPerSample, int latencyMillisec) {
            return WasapiIO_Setup((int)mode, sampleRate, bitsPerSample, latencyMillisec);
        }

        public void Unsetup() {
            WasapiIO_Unsetup();
        }

        public void AddPlayPcmData(int id, byte[] data) {
            WasapiIO_AddPlayPcmData(id, data, data.Length);
        }

        public void ClearPlayList() {
            WasapiIO_ClearPlayList();
        }

        public void SetPlayRepeat(bool repeat) {
            WasapiIO_SetPlayRepeat(repeat);
        }

        public int GetNowPlayingPcmDataId() {
            return WasapiIO_GetNowPlayingPcmDataId();
        }

        public void SetNowPlayingPcmDataId(int id) {
            WasapiIO_SetNowPlayingPcmDataId(id);
        }

        public void SetupCaptureBuffer(int bytes) {
            WasapiIO_SetupCaptureBuffer(bytes);
        }

        public int GetCapturedData(byte[] data) {
            return WasapiIO_GetCapturedData(data, data.Length);
        }

        public int GetCaptureGlitchCount() {
            return WasapiIO_GetCaptureGlitchCount();
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
