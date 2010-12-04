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
        private extern static void
        WasapiIO_SetDataFeedMode(int dfm);

        [DllImport("WasapiIODLL.dll")]
        private extern static void
        WasapiIO_SetLatencyMillisec(int ms);

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
        private extern static void
        WasapiIO_UnchooseDevice();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetUseDeviceId();

        [DllImport("WasapiIODLL.dll", CharSet = CharSet.Auto)]
        private extern static bool
        WasapiIO_GetUseDeviceName(System.Text.StringBuilder name, int nameBytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static int 
        WasapiIO_Setup(int sampleRate, int format, int numChannels);

        [DllImport("WasapiIODLL.dll")]
        private extern static void 
        WasapiIO_Unsetup();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool 
        WasapiIO_AddPlayPcmDataStart();

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmData(int id, byte[] data, int bytes);

        [DllImport("WasapiIODLL.dll")]
        private extern static bool
        WasapiIO_AddPlayPcmDataEnd();

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
        WasapiIO_Start(int wavDataId);

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
        
        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetMixFormatSampleRate();

        [DllImport("WasapiIODLL.dll")]
        private extern static int
        WasapiIO_GetMixFormatType();

        public int Init() {
            return WasapiIO_Init();
        }

        public void Term() {
            WasapiIO_Term();
        }

        public enum SchedulerTaskType {
            None,
            Audio,
            ProAudio,
            Playback
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

        public enum DataFeedMode {
            EventDriven,
            TimerDriven,
        };

        public void SetDataFeedMode(DataFeedMode t) {
            WasapiIO_SetDataFeedMode((int)t);
        }

        public void SetLatencyMillisec(int ms) {
            WasapiIO_SetLatencyMillisec(ms);
        }

        public enum DeviceType {
            Play,
            Rec
        };

        public enum BitFormatType {
            SInt,
            SFloat
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

        public void UnchooseDevice() {
            WasapiIO_UnchooseDevice();
        }

        public int GetUseDeviceId() {
            return WasapiIO_GetUseDeviceId();
        }

        public string GetUseDeviceName() {
            StringBuilder buf = new StringBuilder(64);
            WasapiIO_GetUseDeviceName(buf, buf.Capacity * 2);
            return buf.ToString();
        }

        public enum SampleFormatType {
            Unknown = -1,
            Sint16,
            Sint24,
            Sint32V24,
            Sint32,
            Sfloat,
        };

        public int Setup(int sampleRate, SampleFormatType format, int numChannels) {
            return WasapiIO_Setup(sampleRate, (int)format, numChannels);
        }

        public void Unsetup() {
            WasapiIO_Unsetup();
        }

        public bool AddPlayPcmDataStart() {
            return WasapiIO_AddPlayPcmDataStart();
        }

        public bool AddPlayPcmData(int id, byte[] data) {
            return WasapiIO_AddPlayPcmData(id, data, data.Length);
        }

        public bool AddPlayPcmDataEnd() {
            return WasapiIO_AddPlayPcmDataEnd();
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

        /// <summary>
        /// 再生中の曲変更。
        /// idのグループが読み込まれている必要がある。
        /// 再生中に呼ぶ必要がある。再生中でない場合、空振りする。
        /// </summary>
        /// <param name="id">曲番号</param>
        public void UpdatePlayPcmDataById(int id) {
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

        public int Start(int wavDataId) {
            return WasapiIO_Start(wavDataId);
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

        public int GetBufferFormatSampleRate() {
            return WasapiIO_GetMixFormatSampleRate();
        }

        public SampleFormatType GetBufferFormatType() {
            return (SampleFormatType)WasapiIO_GetMixFormatType();
        }
    }
}
