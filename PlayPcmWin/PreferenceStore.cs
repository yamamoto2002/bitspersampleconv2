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
            ManuallySetMainWindowDimension = false;
            ParallelRead = false;
            PlayRepeat = true;
            PlayingTimeSize = 16;
            PlayingTimeFontBold = true;
            PlayingTimeFontName = "Courier New";
            WindowScale = 1.0f;
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
        public static string fileName = "PlayPcmWinPreference.xml";
        private PreferenceStore() {
        }

        public static Preference Load() {
            XmlSerializer formatter = new XmlSerializer(typeof(Preference));
            IsolatedStorageFileStream settingsFile;

            Preference p = new Preference();
            try {
                settingsFile = new IsolatedStorageFileStream(
                    fileName, System.IO.FileMode.Open,
                    IsolatedStorageFile.GetUserStoreForDomain());

                byte[] buffer = new byte[settingsFile.Length];
                settingsFile.Read(buffer, 0, (int)settingsFile.Length);
                System.IO.MemoryStream stream = new System.IO.MemoryStream(buffer);
                p = formatter.Deserialize(stream) as Preference;
                settingsFile.Close();
            } catch (System.Exception ex) {
                Console.WriteLine(ex);
                return p;
            }

            if (Preference.CurrentVersion != p.Version) {
                Console.WriteLine("Version mismatch {0} != {1}", Preference.CurrentVersion, p.Version);
                return new Preference();
            }

            p.ParallelRead = false;
            return p;
        }

        public static void Save(Preference p) {
            IsolatedStorageFileStream isfs = null;
            bool bOpen = false;
            try {
                XmlSerializer s
                    = new XmlSerializer(typeof(Preference));
                isfs = new IsolatedStorageFileStream(fileName,
                        System.IO.FileMode.Create,
                        IsolatedStorageFile.GetUserStoreForDomain());
                bOpen = true;
                p.Version = Preference.CurrentVersion;
                s.Serialize(isfs, p);
            } catch (System.Exception ex) {
                Console.WriteLine(ex.ToString());
            } finally {
                if (bOpen) {
                    isfs.Close();
                }
            }
        }
    }
}
