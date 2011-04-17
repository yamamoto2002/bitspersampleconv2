using System;
using System.Text;
using System.IO.IsolatedStorage;
using System.Xml.Serialization;
using PcmDataLib;
using WasapiPcmUtil;

namespace PlayPcmWin {
    public class Preference {
        public const int DefaultLatencyMilliseconds = 170;
        public const int CurrentVersion = 3;

        public int Version { get; set; }

        public int LatencyMillisec { get; set; }
        public WasapiSharedOrExclusive wasapiSharedOrExclusive { get; set; }
        public WasapiDataFeedMode wasapiDataFeedMode { get; set; }
        public RenderThreadTaskType renderThreadTaskType { get; set; }
        public BitsPerSampleFixType bitsPerSampleFixType { get; set; }

        public bool ReplaceGapWithKokomade { get; set; }

        public string PreferredDeviceName { get; set; }

        public double MainWindowLeft { get; set; }
        public double MainWindowTop { get; set; }
        public double MainWindowWidth { get; set; }
        public double MainWindowHeight { get; set; }

        public bool ManuallySetMainWindowDimension { get; set; }

        public bool ParallelRead { get; set; }

        public bool PlayRepeat { get; set; }

        public int PlayingTimeSize { get; set; }

        public bool PlayingTimeFontBold { get; set; }

        public string PlayingTimeFontName { get; set; }

        public double WindowScale { get; set; }

        public bool SettingsIsExpanded { get; set; }

        public bool StorePlaylistContent { get; set; }

        public Preference() {
            Reset();
        }

        /// <summary>
        /// デフォルト設定値。
        /// </summary>
        public void Reset() {
            Version = CurrentVersion;
            LatencyMillisec = DefaultLatencyMilliseconds;
            wasapiSharedOrExclusive = WasapiSharedOrExclusive.Exclusive;
            wasapiDataFeedMode = WasapiDataFeedMode.EventDriven;
            renderThreadTaskType = RenderThreadTaskType.ProAudio;
            bitsPerSampleFixType = BitsPerSampleFixType.AutoSelect;
            PreferredDeviceName = "";
            ReplaceGapWithKokomade = false;
            ManuallySetMainWindowDimension = true;
            ParallelRead = false;
            PlayRepeat = true;
            PlayingTimeSize = 16;
            PlayingTimeFontBold = true;
            PlayingTimeFontName = "Courier New";
            WindowScale = 1.0f;
            SettingsIsExpanded = true;
            StorePlaylistContent = true;

            MainWindowLeft = -1;
            MainWindowTop = -1;
            MainWindowWidth = 850;
            MainWindowHeight = 640;
        }

        /// <summary>
        /// ウィンドウサイズセット。
        /// </summary>
        public void SetMainWindowLeftTopWidthHeight(
                double left, double top,
                double width, double height) {
            MainWindowLeft   = left;
            MainWindowTop    = top;
            MainWindowWidth  = width;
            MainWindowHeight = height;
        }
    }

    sealed class PreferenceStore {
        private static readonly string m_fileName = "PlayPcmWinPreference.xml";

        private PreferenceStore() {
        }

        public static Preference Load() {
            Preference p = new Preference();

            try {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(
                        m_fileName, System.IO.FileMode.Open,
                        IsolatedStorageFile.GetUserStoreForDomain())) {
                    byte[] buffer = new byte[isfs.Length];
                    isfs.Read(buffer, 0, (int)isfs.Length);
                    System.IO.MemoryStream stream = new System.IO.MemoryStream(buffer);

                    XmlSerializer formatter = new XmlSerializer(typeof(Preference));
                    p = formatter.Deserialize(stream) as Preference;
                }
            } catch (System.Exception ex) {
                Console.WriteLine(ex);
                p = new Preference();
            }

            if (Preference.CurrentVersion != p.Version) {
                Console.WriteLine("Preference Version mismatch {0} != {1}", Preference.CurrentVersion, p.Version);
                p = new Preference();
            }

            p.ParallelRead = false;
            return p;
        }

        public static bool Save(Preference p) {
            bool result = false;

            try {
                using (IsolatedStorageFileStream isfs = new IsolatedStorageFileStream(
                        m_fileName, System.IO.FileMode.Create,
                        IsolatedStorageFile.GetUserStoreForDomain())) {
                    XmlSerializer s = new XmlSerializer(typeof(Preference));
                    p.Version = Preference.CurrentVersion;
                    s.Serialize(isfs, p);
                    result = true;
                }
            } catch (System.Exception ex) {
                Console.WriteLine(ex.ToString());
            }

            return result;
        }
    }
}
