// このファイルは、
// 行き当たりばったりで機能を追加していったため
// PlayPcmWinの中で特にイマイチなコードである

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wasapi;
using WavRWLib2;
using System.IO;
using System.ComponentModel;
using System.Threading.Tasks;
using PcmDataLib;
using WasapiPcmUtil;
using System.Collections.ObjectModel;
using System.IO.IsolatedStorage;

namespace PlayPcmWin
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 再生の進捗状況を取りに行き表示を更新する時間間隔
        /// </summary>
        const int PROGRESS_REPORT_INTERVAL_MS = 500;

        private WasapiCS wasapi;

        private Wasapi.WasapiCS.StateChangedCallback m_wasapiStateChangedDelegate;

        private Preference m_preference = new Preference();

        /// <summary>
        /// PcmDataのリスト。
        /// </summary>
        private List<PcmDataLib.PcmData> m_pcmDataList = new List<PcmDataLib.PcmData>();

        /// <summary>
        /// プレイリスト1項目の情報。
        /// dataGridPlayList.Itemsの項目と一対一に対応する。
        /// </summary>
        class PlayListItemInfo : INotifyPropertyChanged {
            private static int m_nextRowId = 1;
            public static void SetNextRowId(int id) {
                m_nextRowId = id;
            }

            private int m_rowId;
            public int RowId {
                get {
                    return m_rowId;
                }
                set {
                    m_rowId = value;
                }
            }

            public string Id {
                get {
                    return m_pcmData.Id.ToString();
                }
                set {
                }
            }

            public string Title {
                get {
                    return m_pcmData.DisplayName;
                }
                set {
                    m_pcmData.DisplayName = value;
                }
            }

            public string ArtistName {
                get {
                    return m_pcmData.ArtistName;
                }
                set {
                    m_pcmData.ArtistName = value;
                }
            }

            public string AlbumTitle {
                get {
                    return m_pcmData.AlbumTitle;
                }
                set {
                    m_pcmData.AlbumTitle = value;
                }
            }

            /// <summary>
            /// 長さ表示用文字列
            /// </summary>
            public string Duration {
                get {
                    int seconds = m_pcmData.DurationSeconds;
                    return SecondToHMSString(seconds);
                }
                set {
                }
            }

            /// <summary>
            /// GAPの場合true
            /// </summary>
            public string IsIndex00 {
                get {
                    return (m_pcmData.CueSheetIndex == 0) ? "Yes" : "No";
                }
                set {
                }
            }

            public string SampleRate {
                get {
                    return string.Format("{0}kHz", m_pcmData.SampleRate * 0.001);
                }
                set {
                }
            }

            public string QuantizationBitRate {
                get {
                    if (m_pcmData.SampleValueRepresentationType == PcmDataLib.PcmData.ValueRepresentationType.SFloat) {
                        return m_pcmData.BitsPerSample.ToString() + " bit (浮動小数点数)";
                    }
                    return m_pcmData.BitsPerSample.ToString() + " bit";
                }
                set {
                }
            }

            public string BitRate {
                get {
                    return ((long)m_pcmData.BitsPerSample * m_pcmData.SampleRate * m_pcmData.NumChannels / 1000).ToString() + " kbps";
                }
                set {
                }
            }

            public enum ItemType {
                Unused,
                AudioData
            }

            private ItemType m_type;

            private PcmDataLib.PcmData m_pcmData;
            public PcmDataLib.PcmData PcmData() { return m_pcmData; }

            private bool m_readSeparatorAfter;
            public bool ReadSeparaterAfter {
                get { return m_readSeparatorAfter; }
                set {
                    m_readSeparatorAfter = value;
                    OnPropertyChanged("ReadSeparaterAfter");
                }
            }

            public PlayListItemInfo() {
                m_type = ItemType.Unused;
                m_pcmData = null;
                m_rowId = m_nextRowId++;
            }

            public PlayListItemInfo(ItemType type, PcmDataLib.PcmData pcmData) {
                m_type = type;
                m_pcmData = pcmData;
                m_rowId = m_nextRowId++;
            }

            #region INotifyPropertyChangedのメンバー

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            #endregion
        }

        /// <summary>
        /// プレイリスト項目情報。
        /// </summary>
        private ObservableCollection<PlayListItemInfo> m_playListItems = new ObservableCollection<PlayListItemInfo>();

        private BackgroundWorker m_playWorker;
        private BackgroundWorker m_readFileWorker;
        private System.Diagnostics.Stopwatch m_sw = new System.Diagnostics.Stopwatch();
        private bool m_playListMouseDown = false;

        // 次にプレイリストにAddしたファイルに振られるGroupId。
        private int m_groupIdNextAdd = 0;

        /// <summary>
        /// メモリ上に読み込まれているGroupId。
        /// </summary>
        private int m_loadedGroupId = -1;
        
        /// <summary>
        /// PCMデータ読み込み中グループIDまたは読み込み完了したグループID
        /// </summary>
        private int m_loadingGroupId = -1;


        /// <summary>
        /// デバイスのセットアップ情報
        /// </summary>
        struct DeviceSetupInfo {
            bool setuped;
            int samplingRate;
            WasapiCS.SampleFormatType sampleFormat;
            int latencyMillisec;
            WasapiDataFeedMode dfm;
            WasapiSharedOrExclusive shareMode;
            RenderThreadTaskType threadTaskType;

            public int SampleRate { get { return samplingRate; } }
            public WasapiCS.SampleFormatType SampleFormat { get { return sampleFormat; } }
            public int LatencyMillisec { get { return latencyMillisec; } }
            public WasapiDataFeedMode DataFeedMode { get { return dfm; } }
            public WasapiSharedOrExclusive SharedOrExclusive { get { return shareMode; } }
            public RenderThreadTaskType ThreadTaskType { get { return threadTaskType; } }

            public bool Is(
                    int samplingRate,
                    WasapiCS.SampleFormatType fmt,
                    int latencyMillisec,
                    WasapiDataFeedMode dfm,
                    WasapiSharedOrExclusive shareMode,
                    RenderThreadTaskType threadTaskType) {
                return (this.setuped
                    && this.samplingRate == samplingRate
                    && this.sampleFormat == fmt
                    && this.latencyMillisec == latencyMillisec
                    && this.dfm == dfm
                    && this.shareMode == shareMode
                    && this.threadTaskType == threadTaskType);
            }

            public bool CompatibleTo(
                    int samplingRate,
                    WasapiCS.SampleFormatType fmt,
                    int latencyMillisec,
                    WasapiDataFeedMode dfm,
                    WasapiSharedOrExclusive shareMode,
                    RenderThreadTaskType threadTaskType) {
                return (this.setuped
                    && this.samplingRate == samplingRate
                    && SampleFormatIsCompatible(this.sampleFormat,fmt)
                    && this.latencyMillisec == latencyMillisec
                    && this.dfm == dfm
                    && this.shareMode == shareMode
                    && this.threadTaskType == threadTaskType);
            }

            private bool SampleFormatIsCompatible(
                    WasapiCS.SampleFormatType lhs,
                    WasapiCS.SampleFormatType rhs) {
                switch (lhs) {
                case WasapiCS.SampleFormatType.Sint24:
                case WasapiCS.SampleFormatType.Sint32V24:
                    return rhs == WasapiCS.SampleFormatType.Sint24 ||
                        rhs == WasapiCS.SampleFormatType.Sint32V24;
                default:
                    return lhs == rhs;
                }
            }

            public void Set(int samplingRate,
                WasapiCS.SampleFormatType fmt,
                int latencyMillisec,
                WasapiDataFeedMode dfm,
                WasapiSharedOrExclusive shareMode,
                RenderThreadTaskType threadTaskType) {
                    this.setuped = true;
                this.samplingRate = samplingRate;
                this.sampleFormat = fmt;
                this.latencyMillisec = latencyMillisec;
                this.dfm = dfm;
                this.shareMode = shareMode;
                this.threadTaskType = threadTaskType;
            }

            /// <summary>
            /// wasapi.Unsetup()された場合に呼ぶ。
            /// </summary>
            public void Unsetuped() {
                setuped = false;
            }

            /// <summary>
            /// Setup状態か？
            /// </summary>
            /// <returns>true: Setup状態。false: Setupされていない。</returns>
            public bool IsSetuped() {
                return setuped;
            }
        }

        /// <summary>
        /// デバイスSetup情報。サンプリングレート、量子化ビット数…。
        /// </summary>
        DeviceSetupInfo m_deviceSetupInfo = new DeviceSetupInfo();

        // 再生停止完了後に行うタスク。
        enum TaskType {
            /// <summary>
            /// 停止する。
            /// </summary>
            None,

            /// <summary>
            /// 指定されたグループをメモリに読み込み、グループの先頭の項目を再生開始する。
            /// </summary>
            PlaySpecifiedGroup,
        }

        class Task {
            public Task() {
                Type = TaskType.None;
                GroupId = -1;
                WavDataId = -1;
            }

            public Task(TaskType type) {
                Set(type);
            }

            public Task(TaskType type, int groupId, int wavDataId) {
                Set(type, groupId, wavDataId);
            }

            public void Set(TaskType type) {
                // 現時点で、このSet()のtypeはNoneしかありえない。
                System.Diagnostics.Debug.Assert(type == TaskType.None);
                Type = type;
            }

            public void Set(TaskType type, int groupId, int wavDataId) {
                Type = type;
                GroupId = groupId;
                WavDataId = wavDataId;
            }

            public TaskType Type { get; set; }
            public int GroupId { get; set; }
            public int WavDataId { get; set; }
        };

        Task m_task = new Task();

        enum State {
            未初期化,
            初期化完了,
            プレイリストあり,

            // これ以降の状態にいる場合、再生リストに新しいファイルを追加できない。
            デバイスSetup完了,
            ファイル読み込み完了,
            再生中,
            再生一時停止中,
            再生停止開始,
            再生グループ切り替え中,
        }

        /// <summary>
        /// UIの状態。
        /// </summary>
        private State m_state = State.未初期化;

        private void ChangeState(State nowState) {
            m_state = nowState;
        }

        /// <summary>
        /// 再生グループId==groupIdの先頭のファイルのWavDataIdを取得。O(n)
        /// </summary>
        /// <param name="groupId">再生グループId</param>
        /// <returns>再生グループId==groupIdの先頭のファイルのWavDataId。見つからないときは-1</returns>
        private int GetFirstWavDataIdOnGroup(int groupId) {
            for (int i = 0; i < m_pcmDataList.Count(); ++i) {
                if (m_pcmDataList[i].GroupId == groupId) {
                    return m_pcmDataList[i].Id;
                }
            }

            return -1;
        }

        /// <summary>
        /// 指定された再生グループIdに属するWavDataの数を数える。O(n)
        /// </summary>
        /// <param name="groupId">指定された再生グループId</param>
        /// <returns>WavDataの数。1つもないときは0</returns>
        private int CountWaveDataOnPlayGroup(int groupId) {
            int count = 0;
            for (int i = 0; i < m_pcmDataList.Count(); ++i) {
                if (m_pcmDataList[i].GroupId == groupId) {
                    ++count;
                }
            }

            return count;
        }

        private void RenumberPcmDataId() {
            for (int i = 0; i < m_pcmDataList.Count(); ++i) {
                m_pcmDataList[i].Id = i;
            }
        }

        /// <summary>
        /// 指定されたWavDataIdの、プレイリスト位置番号(プレイリスト内のindex)を戻す。
        /// </summary>
        /// <param name="wavDataId">プレイリスト位置番号を知りたいWaveDataのId</param>
        /// <returns>プレイリスト位置番号(プレイリスト内のindex)。見つからないときは-1</returns>
        private int GetPlayListIndexOfWaveDataId(int wavDataId) {
            for (int i = 0; i < m_playListItems.Count(); ++i) {
                if (m_playListItems[i].PcmData() != null
                    && m_playListItems[i].PcmData().Id == wavDataId) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 保存してあった再生リストを読んでm_pcmDataListとm_playListItemsに足す。
        /// UpdateUIは行わない。
        /// </summary>
        private int LoadPlaylist(string path) {
            int count = 0;

            // エラーメッセージを貯めて出す。作りがいまいちだが。
            m_loadErrorMessages = new StringBuilder();

            PlaylistSave pl;
            if (path.Length == 0) {
                pl = PlaylistRW.Load();
            } else {
                pl = PlaylistRW.LoadFrom(path);
            }
            foreach (var p in pl.Items) {
                int rv = ReadFileHeader(p.PathName, ReadHeaderMode.OnlyConcreteFile, null, null);
                if (1 == rv) {
                    // 読み込み成功。読み込んだPcmDataの曲名、アーティスト名、アルバム名、startTick等を上書きする。

                    // pcmDataのメンバ。
                    var pcmData = m_pcmDataList.Last();
                    pcmData.DisplayName = p.Title;
                    pcmData.AlbumTitle = p.AlbumName;
                    pcmData.ArtistName = p.ArtistName;
                    pcmData.StartTick = p.StartTick;
                    pcmData.EndTick = p.EndTick;
                    pcmData.CueSheetIndex = p.CueSheetIndex;

                    // playList表のメンバ。
                    var playListItem = m_playListItems[count];
                    playListItem.ReadSeparaterAfter = p.ReadSeparaterAfter;
                }
                count += rv;
            }

            if (0 < m_loadErrorMessages.Length) {
                MessageBox.Show(m_loadErrorMessages.ToString(), "読み込めなかったファイル", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            m_loadErrorMessages = null;

            return count;
        }

        private bool SavePlaylist(string path) {
            var s = new PlaylistSave();

            for (int i=0; i<m_pcmDataList.Count; ++i) {
                var p = m_pcmDataList[i];
                var playListItem = m_playListItems[i];

                s.Items.Add(new PlaylistItemSave().Set(
                    p.DisplayName, p.AlbumTitle, p.ArtistName, p.FullPath,
                    p.CueSheetIndex, p.StartTick, p.EndTick, playListItem.ReadSeparaterAfter));
            }

            if (path.Length == 0) {
                return PlaylistRW.Save(s);
            } else {
                return PlaylistRW.SaveAs(s, path);
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // InitializeComponent()によって、チェックボックスのチェックイベントが発生し
            // m_preferenceの内容が変わるので、InitializeComponent()の後にロードする。

            m_preference = PreferenceStore.Load();

            if (m_preference.ManuallySetMainWindowDimension) {
                if (0 <= m_preference.MainWindowLeft) {
                    Left = m_preference.MainWindowLeft;
                }
                if (0 <= m_preference.MainWindowTop) {
                    Top = m_preference.MainWindowTop;
                }
                if (100 <= m_preference.MainWindowWidth) {
                    Width = m_preference.MainWindowWidth;
                }
                if (100 <= m_preference.MainWindowHeight) {
                    Height = m_preference.MainWindowHeight;
                }
            }

            if (!m_preference.SettingsIsExpanded) {
                expanderSettings.IsExpanded = false;
            }

            if (m_preference.StorePlaylistContent) {
                LoadPlaylist(string.Empty);
            }

            UpdateWindowSettings();

            AddLogText(string.Format("PlayPcmWin {0} {1}\r\n",
                    AssemblyVersion,
                    IntPtr.Size == 8 ? "64bit" : "32bit"));

            PlayListItemInfo.SetNextRowId(1);
            dataGridPlayList.ItemsSource = m_playListItems;

            m_groupIdNextAdd = 0;

            int hr = 0;
            wasapi = new WasapiCS();
            hr = wasapi.Init();
            AddLogText(string.Format("wasapi.Init() {0:X8}\r\n", hr));

            m_wasapiStateChangedDelegate = new Wasapi.WasapiCS.StateChangedCallback(WasapiStatusChanged);
            wasapi.RegisterCallback(m_wasapiStateChangedDelegate);

            textBoxLatency.Text = string.Format("{0}", m_preference.LatencyMillisec);

            switch (m_preference.wasapiSharedOrExclusive) {
            case WasapiSharedOrExclusive.Exclusive:
                radioButtonExclusive.IsChecked = true;
                break;
            case WasapiSharedOrExclusive.Shared:
                radioButtonShared.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            switch (m_preference.wasapiDataFeedMode) {
            case WasapiDataFeedMode.EventDriven:
                radioButtonEventDriven.IsChecked = true;
                break;
            case WasapiDataFeedMode.TimerDriven:
                radioButtonTimerDriven.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            switch (m_preference.renderThreadTaskType) {
            case RenderThreadTaskType.None:
                radioButtonTaskNone.IsChecked = true;
                break;
            case RenderThreadTaskType.Audio:
                radioButtonTaskAudio.IsChecked = true;
                break;
            case RenderThreadTaskType.ProAudio:
                radioButtonTaskProAudio.IsChecked = true;
                break;
            case RenderThreadTaskType.Playback:
                radioButtonTaskPlayback.IsChecked = true;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            checkBoxContinuous.IsChecked = m_preference.PlayRepeat;

            SetupBackgroundWorkers();

            CreateDeviceList();
        }

        private void SetupBackgroundWorkers() {
            m_readFileWorker = new BackgroundWorker();
            m_readFileWorker.DoWork += new DoWorkEventHandler(ReadFileDoWork);
            m_readFileWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(ReadFileRunWorkerCompleted);
            m_readFileWorker.WorkerReportsProgress = true;
            m_readFileWorker.ProgressChanged += new ProgressChangedEventHandler(ReadFileWorkerProgressChanged);
            m_readFileWorker.WorkerSupportsCancellation = true;

            m_playWorker = new BackgroundWorker();
            m_playWorker.WorkerReportsProgress = true;
            m_playWorker.DoWork += new DoWorkEventHandler(PlayDoWork);
            m_playWorker.ProgressChanged += new ProgressChangedEventHandler(PlayProgressChanged);
            m_playWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlayRunWorkerCompleted);
            m_playWorker.WorkerSupportsCancellation = true;
        }

        private void Window_Closed(object sender, EventArgs e) {
            Term();
        }

        private void MenuItemFileExit_Click(object sender, RoutedEventArgs e) {
            Exit();
        }

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private string SampleFormatTypeToStr(WasapiCS.SampleFormatType t) {
            switch (t) {
            case WasapiCS.SampleFormatType.Sfloat:
                return "32bit浮動小数点数";
            case WasapiCS.SampleFormatType.Sint16:
                return "16bit";
            case WasapiCS.SampleFormatType.Sint24:
                return "24bit";
            case WasapiCS.SampleFormatType.Sint32:
                return "32bit";
            case WasapiCS.SampleFormatType.Sint32V24:
                return "32bit(有効bit数24)";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        private void UpdateUIStatus() {
            dataGridPlayList.UpdateLayout();

            switch (m_state) {
            case State.初期化完了:
                menuItemFileNew.IsEnabled        = false;
                menuItemFileOpen.IsEnabled       = true;
                menuItemFileSaveAs.IsEnabled     = false;
                menuItemFileSaveCueAs.IsEnabled  = false;
                buttonPlay.IsEnabled             = false;
                buttonStop.IsEnabled             = false;
                buttonPause.IsEnabled            = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                groupBoxWasapiSettings.IsEnabled = true;

                buttonClearPlayList.IsEnabled    = false;
                buttonRemovePlayList.IsEnabled = false;
                buttonInspectDevice.IsEnabled    = true;

                buttonSettings.IsEnabled = true;
                menuToolSettings.IsEnabled = true;
                statusBarText.Content = "再生リストを作って下さい。";
                break;
            case State.プレイリストあり:
                // dataGridの選択単位をセル単位にする(停止中は再生リスト編集を主に行うであろう)
                if (0 <= dataGridPlayList.SelectedIndex) {
                    var si = dataGridPlayList.SelectedIndex;
                    dataGridPlayList.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
                    dataGridPlayList.SelectedIndex = si;
                } else {
                    dataGridPlayList.SelectionUnit = DataGridSelectionUnit.CellOrRowHeader;
                }
                menuItemFileNew.IsEnabled        = true;
                menuItemFileOpen.IsEnabled       = true;
                menuItemFileSaveAs.IsEnabled     = true;
                menuItemFileSaveCueAs.IsEnabled  = true;
                buttonPlay.IsEnabled             = true;
                buttonStop.IsEnabled             = false;
                buttonPause.IsEnabled            = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                groupBoxWasapiSettings.IsEnabled = true;

                buttonClearPlayList.IsEnabled = true;
                buttonRemovePlayList.IsEnabled = (dataGridPlayList.SelectedIndex >= 0);
                buttonInspectDevice.IsEnabled    = false;

                buttonSettings.IsEnabled = true;
                menuToolSettings.IsEnabled = true;
                statusBarText.Content = "再生リストを作り、再生ボタンを押して下さい。";
                break;
            case State.デバイスSetup完了:
                // dataGridの選択単位を行単位にする(再生中は再生リスト編集よりは、曲選択をしたい)
                dataGridPlayList.SelectionUnit = DataGridSelectionUnit.FullRow;

                // 一覧のクリアーとデバイスの選択、再生リストの作成関連を押せなくする。
                menuItemFileNew.IsEnabled        = false;
                menuItemFileOpen.IsEnabled       = false;
                menuItemFileSaveAs.IsEnabled     = true;
                menuItemFileSaveCueAs.IsEnabled  = true;
                buttonPlay.IsEnabled             = false;
                buttonStop.IsEnabled             = false;
                buttonPause.IsEnabled            = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonClearPlayList.IsEnabled    = false;
                buttonRemovePlayList.IsEnabled = false;
                buttonInspectDevice.IsEnabled = false;

                buttonSettings.IsEnabled = false;
                menuToolSettings.IsEnabled = false;
                statusBarText.Content = "デバイス選択完了。ファイル読み込み中……";
                break;
            case State.ファイル読み込み完了:
                menuItemFileNew.IsEnabled        = false;
                menuItemFileOpen.IsEnabled       = false;
                menuItemFileSaveAs.IsEnabled     = true;
                menuItemFileSaveCueAs.IsEnabled  = true;
                buttonPlay.IsEnabled             = true;
                buttonStop.IsEnabled             = false;
                buttonPause.IsEnabled            = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonClearPlayList.IsEnabled    = false;
                buttonRemovePlayList.IsEnabled = false;
                buttonInspectDevice.IsEnabled = false;

                buttonSettings.IsEnabled = false;
                menuToolSettings.IsEnabled = false;
                statusBarText.Content = "ファイル読み込み完了。再生できます。";

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                slider1.Value = 0;
                labelPlayingTime.Content = "--:--:--/--:--:--";
                break;
            case State.再生中:
                menuItemFileNew.IsEnabled        = false;
                menuItemFileOpen.IsEnabled       = false;
                menuItemFileSaveAs.IsEnabled     = false;
                menuItemFileSaveCueAs.IsEnabled  = false;
                buttonPlay.IsEnabled             = false;
                buttonStop.IsEnabled             = true;
                buttonPause.IsEnabled            = true;
                buttonPause.Content = "一時停止(_U)";

                buttonNext.IsEnabled             = true;
                buttonPrev.IsEnabled             = true;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonClearPlayList.IsEnabled    = false;
                buttonRemovePlayList.IsEnabled = false;
                buttonInspectDevice.IsEnabled = false;

                buttonSettings.IsEnabled = false;
                menuToolSettings.IsEnabled = false;
                statusBarText.Content =
                    string.Format("再生中。WASAPI{0} {1}kHz {2} {3}ch",
                        radioButtonShared.IsChecked == true ? "共有" : "排他",
                        wasapi.GetBufferFormatSampleRate()*0.001,
                        SampleFormatTypeToStr(wasapi.GetBufferFormatType()),
                        wasapi.GetNumOfChannels());

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case State.再生一時停止中:
                menuItemFileNew.IsEnabled    = false;
                menuItemFileOpen.IsEnabled   = false;
                menuItemFileSaveAs.IsEnabled = false;
                menuItemFileSaveCueAs.IsEnabled  = false;
                buttonPlay.IsEnabled         = false;
                buttonStop.IsEnabled         = true;
                buttonPause.IsEnabled        = true;
                buttonPause.Content = "再生再開(_U)";

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonClearPlayList.IsEnabled    = false;
                buttonRemovePlayList.IsEnabled = false;
                buttonInspectDevice.IsEnabled = false;

                buttonSettings.IsEnabled   = false;
                menuToolSettings.IsEnabled = false;
                statusBarText.Content = "再生一時停止中";

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case State.再生停止開始:
                menuItemFileNew.IsEnabled        = false;
                menuItemFileOpen.IsEnabled       = false;
                menuItemFileSaveAs.IsEnabled     = false;
                menuItemFileSaveCueAs.IsEnabled  = false;
                buttonPlay.IsEnabled = false;
                buttonStop.IsEnabled = false;
                buttonPause.IsEnabled = false;

                buttonNext.IsEnabled = false;
                buttonPrev.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonClearPlayList.IsEnabled    = false;
                buttonRemovePlayList.IsEnabled = false;
                buttonInspectDevice.IsEnabled = false;

                buttonSettings.IsEnabled = false;
                menuToolSettings.IsEnabled = false;
                statusBarText.Content = "再生停止開始";
                break;
            case State.再生グループ切り替え中:
                menuItemFileNew.IsEnabled        = false;
                menuItemFileOpen.IsEnabled       = false;
                menuItemFileSaveAs.IsEnabled     = false;
                menuItemFileSaveCueAs.IsEnabled  = false;
                buttonPlay.IsEnabled = false;
                buttonStop.IsEnabled = false;
                buttonPause.IsEnabled = false;

                buttonNext.IsEnabled = false;
                buttonPrev.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonClearPlayList.IsEnabled    = false;
                buttonRemovePlayList.IsEnabled = false;
                buttonInspectDevice.IsEnabled = false;

                buttonSettings.IsEnabled = false;
                menuToolSettings.IsEnabled = false;
                statusBarText.Content = "読み込み中……";
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        /// <summary>
        /// デバイス一覧を取得し、デバイス一覧リストを更新する。
        /// 同一デバイスのデバイス番号がずれるので注意。
        /// </summary>
        private void CreateDeviceList() {
            int hr;

            int selectedIndex = -1;

            listBoxDevices.Items.Clear();

            hr = wasapi.DoDeviceEnumeration(WasapiCS.DeviceType.Play);
            AddLogText(string.Format("wasapi.DoDeviceEnumeration(Play) {0:X8}\r\n", hr));

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                string deviceName = wasapi.GetDeviceName(i);
                listBoxDevices.Items.Add(deviceName);
                if (0 < m_preference.PreferredDeviceName.Length
                    && 0 == m_preference.PreferredDeviceName.CompareTo(deviceName)) {
                    // お気に入りデバイスを選択状態にする。
                    selectedIndex = i;
                }
            }

            if (0 < nDevices) {
                if (0 <= selectedIndex && selectedIndex < listBoxDevices.Items.Count) {
                    listBoxDevices.SelectedIndex = selectedIndex;
                } else {
                    listBoxDevices.SelectedIndex = 0;
                }

                buttonInspectDevice.IsEnabled = true;
            }

            if (0 < m_pcmDataList.Count) {
                ChangeState(State.プレイリストあり);
            } else {
                ChangeState(State.初期化完了);
            }

            UpdateUIStatus();
        }

        /// <summary>
        /// 再生中の場合は、停止を開始する。
        /// (ブロックしないのでこの関数から抜けたときに停止完了していないことがある)
        /// 
        /// 再生中でない場合は、再生停止後イベントtaskAfterStopをここで実行する。
        /// 再生中の場合は、停止完了後にtaskAfterStopを実行する。
        /// </summary>
        /// <param name="taskAfterStop"></param>
        void Stop(Task taskAfterStop) {
            m_task = taskAfterStop;

            if (m_playWorker.IsBusy) {
                m_playWorker.CancelAsync();
                // 再生停止したらPlayRunWorkerCompletedでイベントを開始する。
            } else {
                // 再生停止後イベントをここで、いますぐ開始。
                PerformPlayCompletedTask();
            }
        }

        /// <summary>
        /// デバイス選択を解除する。再生停止中に呼ぶ必要あり。
        /// この関数を呼ぶと、デバイスリストが消えるため要注意。
        /// ふたたびCreateDeviceList()する必要あり。
        /// </summary>
        private void DeviceDeselect() {
            System.Diagnostics.Debug.Assert(!m_playWorker.IsBusy);

            UnsetupDevice();

            wasapi.UnchooseDevice();
            AddLogText("wasapi.UnchooseDevice()\r\n");

            m_loadedGroupId = -1;
            m_loadingGroupId = -1;
        }

        private void Term() {
            if (wasapi != null) {
                Stop(new Task(TaskType.None));
                m_readFileWorker.CancelAsync();

                // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせする。
                // そうしないと、バックグラウンドスレッドによって使用中のオブジェクトが
                // この後のUnsetupの呼出によって開放されてしまい問題が起きる。

                while (m_playWorker.IsBusy) {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));

                    System.Threading.Thread.Sleep(100);
                }

                while (m_readFileWorker.IsBusy) {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));
                    System.Threading.Thread.Sleep(100);
                }

                UnsetupDevice();
                wasapi.Term();
                wasapi = null;

                // ウィンドウの位置とサイズを保存
                m_preference.SetMainWindowLeftTopWidthHeight(
                    Left,
                    Top,
                    Width,
                    Height);

                // 再生リピート設定を保存
                m_preference.PlayRepeat = checkBoxContinuous.IsChecked == true;

                // 設定画面の表示状態を保存
                m_preference.SettingsIsExpanded = expanderSettings.IsExpanded;

                // 設定ファイルを書き出す。
                PreferenceStore.Save(m_preference);

                // 再生リストをIsolatedStorageに保存。
                SavePlaylist(string.Empty);
            }
        }

        private void Exit() {
            Term();
            // Application.Current.Shutdown();
            Close();
        }

        /// <summary>
        /// wasapi.Unsetupを行う。
        /// 既にUnsetup状態の場合は、空振りする。
        /// </summary>
        private void UnsetupDevice() {
            if (!m_deviceSetupInfo.IsSetuped()) {
                return;
            }

            wasapi.Unsetup();
            AddLogText("wasapi.Unsetup()\r\n");
            m_deviceSetupInfo.Unsetuped();
        }

        /// <summary>
        /// m_deviceSetupInfoにしたがってWasapiをSetupする。
        /// </summary>
        /// <returns>WasapiのSetup HRESULT。</returns>
        private int WasapiSetup1() {
            wasapi.SetShareMode(
                PreferenceShareModeToWasapiCSShareMode(
                    m_deviceSetupInfo.SharedOrExclusive));
            AddLogText(string.Format("wasapi.SetShareMode({0})\r\n",
                m_deviceSetupInfo.SharedOrExclusive));

            wasapi.SetSchedulerTaskType(
                PreferenceSchedulerTaskTypeToWasapiCSSchedulerTaskType(
                    m_deviceSetupInfo.ThreadTaskType));
            AddLogText(string.Format("wasapi.SetSchedulerTaskType({0})\r\n",
                m_deviceSetupInfo.ThreadTaskType));

            wasapi.SetDataFeedMode(PreferenceDataFeedModeToWasapiCS(m_deviceSetupInfo.DataFeedMode));
            AddLogText(string.Format("wasapi.SetDataFeedMode({0})\r\n",
                PreferenceDataFeedModeToWasapiCS(m_deviceSetupInfo.DataFeedMode)));

            wasapi.SetLatencyMillisec(m_deviceSetupInfo.LatencyMillisec);
            AddLogText(string.Format("wasapi.SetLatencyMillisec({0})\r\n",
                m_deviceSetupInfo.LatencyMillisec));

            int hr = wasapi.Setup(
                m_deviceSetupInfo.SampleRate,
                m_deviceSetupInfo.SampleFormat,
                2);
            AddLogText(string.Format("wasapi.Setup({0}, {1}, {2}) {3:X8}\r\n",
                m_deviceSetupInfo.SampleRate, m_deviceSetupInfo.SampleFormat,
                ShareModeToStr(m_preference.wasapiSharedOrExclusive), hr));
            if (hr < 0) {
                UnsetupDevice();

                string s = string.Format("wasapi.Setup() {0} {1} {2} {3} {4} failed {5:X8}\r\n",
                    m_deviceSetupInfo.SampleRate, m_deviceSetupInfo.SampleFormat,
                    m_deviceSetupInfo.LatencyMillisec,
                    m_deviceSetupInfo.DataFeedMode,
                    ShareModeToStr(m_preference.wasapiSharedOrExclusive), hr);
                AddLogText(s);
                return hr;
            }
            return hr;
        }

        /// <summary>
        /// デバイスSetupを行う。
        /// すでに同一フォーマットのSetupがなされている場合は空振りする。
        /// </summary>
        /// <param name="loadGroupId">再生するグループ番号。この番号のWAVファイルのフォーマットでSetupする。</param>
        /// <returns>false: デバイスSetup失敗。よく起こる。</returns>
        private bool SetupDevice(int loadGroupId) {
            int latencyMillisec = Int32.Parse(textBoxLatency.Text);
            if (latencyMillisec <= 0) {
                latencyMillisec = Preference.DefaultLatencyMilliseconds;
                textBoxLatency.Text = string.Format("{0}", latencyMillisec);
            }
            m_preference.LatencyMillisec = latencyMillisec;

            int startWavDataId = GetFirstWavDataIdOnGroup(loadGroupId);
            System.Diagnostics.Debug.Assert(0 <= startWavDataId);

            PcmDataLib.PcmData startPcmData = m_pcmDataList[startWavDataId];

            // 1つのフォーマットに対して複数のデバイス設定選択肢がありうる。

            int candidateNum = SampleFormatInfo.GetDeviceSampleFormatCandidateNum(
                m_preference.wasapiSharedOrExclusive,
                m_preference.bitsPerSampleFixType,
                startPcmData.ValidBitsPerSample,
                startPcmData.SampleValueRepresentationType);
            for (int i = 0; i < candidateNum; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.GetDeviceSampleFormat(
                    m_preference.wasapiSharedOrExclusive,
                    m_preference.bitsPerSampleFixType,
                    startPcmData.ValidBitsPerSample,
                    startPcmData.SampleValueRepresentationType,
                    i);

                if (m_deviceSetupInfo.Is(
                    startPcmData.SampleRate,
                    sf.GetSampleFormatType(),
                    latencyMillisec,
                    m_preference.wasapiDataFeedMode,
                    m_preference.wasapiSharedOrExclusive,
                    m_preference.renderThreadTaskType)) {
                    // すでにこのフォーマットでSetup完了している。
                    return true;
                }
            }

            for (int i = 0; i < candidateNum; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.GetDeviceSampleFormat(
                    m_preference.wasapiSharedOrExclusive,
                    m_preference.bitsPerSampleFixType,
                    startPcmData.ValidBitsPerSample,
                    startPcmData.SampleValueRepresentationType, i);

                m_deviceSetupInfo.Set(
                    startPcmData.SampleRate,
                    sf.GetSampleFormatType(),
                    latencyMillisec,
                    m_preference.wasapiDataFeedMode,
                    m_preference.wasapiSharedOrExclusive,
                    m_preference.renderThreadTaskType);

                int hr = WasapiSetup1();
                if (0 <= hr) {
                    // 成功
                    break;
                }

                // 失敗
                if (i == (candidateNum-1)) {
                    string s = string.Format("エラー: wasapi.Setup({0}Hz {1} レイテンシー{2}ms {3} {4}) 失敗 {5:X8}\n" +
                        "\n" +
                        "・再生リストの[一覧をクリア]し[対応フォーマット]ボタンを押すと再生可能なフォーマット一覧が出ます。\n" +
                        "・RME Fireface400やM-AUDIO ProFire2626等は、機器に設定されているマスターサンプリングレートをWASAPIから変更できないため手動で合わせる必要があります。\n" +
                        "・Creative X-Fi Titanium HDの44.1kHzと88.2kHzはタイマー駆動モードならば動作します。\n" +
                        "・Creative USB Sound Blaster HDはイベント駆動モードのほうが安定動作します。\n" +
                        "・Lynx AES16eのWaveRTドライバは、出力レイテンシーを20msなどの小さな値に設定すると再生できます。\n" +
                        "・無音時に省電力のためS/PDIF出力を停止するデバイス等では、出力レイテンシーを50msなどの小さな値に設定すると曲の頭が途切れることがあるそうです。\n" +
                        "・E-MU 0404 USBは、PlayPcmWinのバグが原因で量子化ビット数24bitのファイルを再生できません。[詳細設定][Sint16に固定する]を選択すると16bitにダウンサンプルされてしまいますが音は鳴ります。\n" +
                        "・Halide Bridgeは[詳細設定][Sint24に固定する]を選択すると再生できたそうです。"
                        ,
                        startPcmData.SampleRate, sf.GetSampleFormatType(), latencyMillisec,
                        DfmToStr(m_preference.wasapiDataFeedMode),
                        ShareModeToStr(m_preference.wasapiSharedOrExclusive), hr);
                    MessageBox.Show(s);
                    return false;
                }
            }

            ChangeState(State.デバイスSetup完了);
            UpdateUIStatus();
            return true;
        }

        enum PlayListClearMode {
            // プレイリストをクリアーし、UI状態も更新する。(通常はこちらを使用。)
            ClearWithUpdateUI,

            // ワーカースレッドから呼ぶためUIを操作しない。UIは内部状態とは矛盾した状態になるため
            // この後UIスレッドであらためてClearPlayList(ClearWithUpdateUI)する必要あり。
            ClearWithoutUpdateUI,
        }

        private StringBuilder m_loadErrorMessages;

        private void LoadErrorMessageAdd(string s) {
            s = "■" + s.TrimEnd('\n').TrimEnd('\r') + "。 ";
            m_loadErrorMessages.Append(s);
        }

        private void ClearPlayList(PlayListClearMode mode) {
            m_pcmDataList.Clear();
            m_playListItems.Clear();
            PlayListItemInfo.SetNextRowId(1);

            wasapi.ClearPlayList();

            m_groupIdNextAdd = 0;
            m_loadedGroupId = -1;
            m_loadingGroupId = -1;

            GC.Collect();

            ChangeState(State.初期化完了);

            if (mode == PlayListClearMode.ClearWithUpdateUI) {
                //m_playListView.RefreshCollection();
                progressBar1.Value = 0;
                UpdateUIStatus();
            }
        }

        /// <summary>
        /// サブルーチン
        /// PcmData読み込み成功後に行う処理。
        /// FLACとWAVとAIFFで共通。
        /// </summary>
        private bool CheckAddPcmData(CueSheetReader csr, CueSheetTrackInfo csti, string path, PcmDataLib.PcmData pcmData) {
            if (pcmData.NumChannels != 1 &&
                pcmData.NumChannels != 2) {
                string s = string.Format("1chモノラルと2chステレオ以外のPCMファイルの再生には対応していません: {0} {1}ch\r\n",
                    path, pcmData.NumChannels);
                AddLogText(s);
                LoadErrorMessageAdd(s);
                return false;
            }
            if (pcmData.BitsPerSample != 16
             && pcmData.BitsPerSample != 24
             && pcmData.BitsPerSample != 32) {
                string s = string.Format("量子化ビット数が16でも24でも32でもないPCMファイルの再生には対応していません: {0} {1}bit\r\n",
                    path, pcmData.BitsPerSample);
                AddLogText(s);
                LoadErrorMessageAdd(s);
                return false;
            }

            if (0 < m_pcmDataList.Count
                && !m_pcmDataList[m_pcmDataList.Count - 1].IsSameFormat(pcmData)) {
                /* データフォーマットが変わった。
                 * Setupのやり直しになるのでファイルグループ番号を変える。
                 */
                ++m_groupIdNextAdd;
            }

            pcmData.FullPath = path;
            pcmData.FileName = System.IO.Path.GetFileName(path);
            pcmData.Id = m_pcmDataList.Count();
            pcmData.GroupId = m_groupIdNextAdd;

            // CUEシートの情報をセットする。
            if (null == csti) {
                if (pcmData.DisplayName == null || pcmData.DisplayName.Length == 0) {
                    pcmData.DisplayName = pcmData.FileName;
                }
                pcmData.StartTick = 0;
                pcmData.EndTick = -1;
                pcmData.CueSheetIndex = 1;
            } else {
                if (0 < csti.title.Length) {
                    pcmData.DisplayName = csti.title;
                    /* if (csti.indexId == 0) {
                        pcmData.DisplayName = csti.title + " (gap)";
                    } */
                } else {
                    pcmData.DisplayName = pcmData.FileName;
                }
                pcmData.StartTick = csti.startTick;
                pcmData.EndTick = csti.endTick;

                pcmData.ArtistName = csti.performer;
                pcmData.CueSheetIndex = csti.indexId;
            }

            if (null != csr) {
                pcmData.AlbumTitle     = csr.GetAlbumTitle();
                pcmData.ArtistName = csr.GetAlbumPerformer();
            }

            var pli = new PlayListItemInfo(
                PlayListItemInfo.ItemType.AudioData,
                pcmData);
            
            if (csti != null) {
                pli.ReadSeparaterAfter = csti.readSeparatorAfter;
            }
            pli.PropertyChanged += new PropertyChangedEventHandler(PlayListItemInfoPropertyChanged);
            m_pcmDataList.Add(pcmData);
            m_playListItems.Add(pli);

            //m_playListView.RefreshCollection();

            // 状態の更新。再生リストにファイル有り。
            ChangeState(State.プレイリストあり);
            return true;
        }

        /// <summary>
        /// WAVファイルのヘッダ部分を読み込む。
        /// </summary>
        /// <returns>読めたらtrue</returns>
        private bool ReadWavFileHeader(string path, CueSheetReader csr, CueSheetTrackInfo csti)
        {
            bool result = false;

            WavData wavData = new WavData();
            try {
                using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open))) {
                    if (wavData.ReadHeader(br)) {
                        // WAVヘッダ読み込み成功。PcmDataを作って再生リストに足す。

                        PcmDataLib.PcmData pd = new PcmDataLib.PcmData();
                        pd.SetFormat(wavData.NumChannels, wavData.BitsPerFrame, wavData.BitsPerFrame,
                            wavData.SampleRate, wavData.SampleValueRepresentationType, wavData.NumFrames);
                        if (wavData.Title != null) {
                            pd.DisplayName = wavData.Title;
                        }
                        if (wavData.AlbumName != null) {
                            pd.AlbumTitle = wavData.AlbumName;
                        }
                        if (wavData.ArtistName != null) {
                            pd.ArtistName = wavData.ArtistName;
                        }

                        result = CheckAddPcmData(csr, csti, path, pd);
                    } else {
                        string s = string.Format("WAVファイル読み込み失敗: {0}\r\n", path);
                        AddLogText(s);
                        LoadErrorMessageAdd(s);
                    }
                }
            } catch (Exception ex) {
                string s = string.Format("WAVファイル読み込み失敗\r\n{0}\r\n\r\n{1}", path, ex);
                AddLogText(s);
                LoadErrorMessageAdd(string.Format("WAVファイル読み込み失敗: {0}", path));
            }

            return result;
        }

        /// <summary>
        /// AIFFファイルのヘッダ部分を読み込む。
        /// </summary>
        /// <returns>読めたらtrue</returns>
        private bool ReadAiffFileHeader(string path, CueSheetReader csr, CueSheetTrackInfo csti) {
            bool result = false;

            AiffReader ar = new AiffReader();
            try {
                using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open))) {
                    PcmDataLib.PcmData pd;
                    AiffReader.ResultType aiffResult = ar.ReadHeader(br, out pd);
                    if (aiffResult == AiffReader.ResultType.Success) {
                        if (CheckAddPcmData(csr, csti, path, pd)) {
                            result = true;
                        }
                    } else {
                        string s = string.Format("AIFFファイル読み込み失敗、{0}: {1}\r\n", aiffResult, path);
                        AddLogText(s);
                        LoadErrorMessageAdd(s);
                    }
                }
            } catch (Exception ex) {
                string s = string.Format("AIFFファイル読み込み失敗\r\n{0}\r\n\r\n{1}", path, ex);
                AddLogText(s);
                LoadErrorMessageAdd(string.Format("AIFFファイル読み込み失敗: {0}", path));
            }

            return result;
        }

        /// <summary>
        /// FLACファイルのヘッダ部分を読み込む。
        /// </summary>
        /// <returns>読めたらtrue</returns>
        private bool ReadFlacFileHeader(string path, CueSheetReader csr, CueSheetTrackInfo csti) {
            bool readSuccess = false;
            PcmDataLib.PcmData pcmData;
            FlacDecodeIF fdif = new FlacDecodeIF();
            int flacErcd = 0;
            flacErcd = fdif.ReadHeader(path, out pcmData);
            if (flacErcd == 0) {
                CheckAddPcmData(csr, csti, path, pcmData);
                readSuccess = true;
            } else {
                string s = string.Format("FLACファイル読み込み失敗、{1}: {0}\r\n",
                        path, FlacDecodeIF.ErrorCodeToStr(flacErcd));
                AddLogText(s);
                LoadErrorMessageAdd(string.Format("FLACファイル読み込み失敗、{1}: {0}",
                    path, FlacDecodeIF.ErrorCodeToStr(flacErcd)));
            }

            return readSuccess;
        }

        /// <summary>
        /// CUEシートを読み込む。
        /// </summary>
        /// <returns>読めたファイルの数を戻す</returns>
        private int ReadCueSheet(string path) {
            CueSheetReader csr = new CueSheetReader();
            bool result = csr.ReadFromFile(path);
            if (!result) {
                string s = string.Format("CUEファイル読み込み失敗: {0}\r\n",
                        path);
                AddLogText(s);
                LoadErrorMessageAdd(s);
                return 0;
            }

            int readCount = 0;
            for (int i = 0; i < csr.GetTrackInfoCount(); ++i) {
                CueSheetTrackInfo csti = csr.GetTrackInfo(i);
                readCount += ReadFileHeader1(csti.path, ReadHeaderMode.OnlyConcreteFile, csr, csti);

                if ((csti.indexId == 0 &&
                    m_preference.ReplaceGapWithKokomade) ||
                    csti.readSeparatorAfter) {
                    // INDEX 00 == gap しかも gapのかわりに[ここまで読みこみ]を追加するの場合
                    // または、CUEシートにREM KOKOMADEが書いてある場合。
                    AddKokomade();
                }
            }
            return readCount;
        }

        enum ReadHeaderMode {
            ReadAll,
            OnlyConcreteFile,
            OnlyMetaFile,
        }

        /// <summary>
        /// N.B. PcmReader.StreamBeginも参照(へぼい)。
        /// MenuItemFileOpen_Clickも参照。
        /// </summary>
        /// <returns>読めたファイルの数を戻す</returns>
        private int ReadFileHeader1(string path, ReadHeaderMode mode, CueSheetReader csr, CueSheetTrackInfo csti) {
            int result = 0;
            string ext = System.IO.Path.GetExtension(path);

            switch (ext.ToLower()) {
            case ".cue":
                if (mode != ReadHeaderMode.OnlyConcreteFile) {
                    result += ReadCueSheet(path);
                }
                break;
            case ".flac":
                if (mode != ReadHeaderMode.OnlyMetaFile) {
                    result += ReadFlacFileHeader(path, csr, csti) ? 1 : 0;
                }
                break;
            case ".aif":
            case ".aiff":
            case ".aifc":
            case ".aiffc":
                if (mode != ReadHeaderMode.OnlyMetaFile) {
                    result += ReadAiffFileHeader(path, csr, csti) ? 1 : 0;
                }
                break;
            case ".wav":
            case ".wave":
                if (mode != ReadHeaderMode.OnlyMetaFile) {
                    result += ReadWavFileHeader(path, csr, csti) ? 1 : 0;
                }
                break;
            default: {
                    string s = string.Format("未対応ファイル: {0}\r\n",
                            path);
                    AddLogText(s);
                    LoadErrorMessageAdd(s);
                }
                break;
            }
            return result;
        }

        private int ReadFileHeader(string path, ReadHeaderMode mode, CueSheetReader csr, CueSheetTrackInfo csti) {
            int result = 0;

            if (System.IO.Directory.Exists(path)) {
                // pathはディレクトリである。直下のファイル一覧を作って足す。再帰的にはたぐらない。
                var files = System.IO.Directory.GetFiles(path);
                foreach (var file in files) {
                    result += ReadFileHeader1(file, mode, csr, csti);
                }
            } else {
                // pathはファイル。
                result += ReadFileHeader1(path, mode, csr, csti);
            }

            return result;
        }

        //////////////////////////////////////////////////////////////////////////

        private void MainWindowDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effects = DragDropEffects.Copy;
            } else {
                e.Effects = DragDropEffects.None;
            }
        }

        private void MainWindowDragDrop(object sender, DragEventArgs e)
        {
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            Console.WriteLine("D: Form1_DragDrop() {0}", paths.Length);
            for (int i = 0; i < paths.Length; ++i) {
                Console.WriteLine("   {0}", paths[i]);
            }

            if (State.デバイスSetup完了 <= m_state) {
                // 追加不可。
                MessageBox.Show("都合により、出力デバイス選択後は再生リストに追加できない作りになっております。いったん再生を停止してから追加していただけますようお願い致します。");
                return;
            }

            // エラーメッセージを貯めて出す。作りがいまいちだが。
            m_loadErrorMessages = new StringBuilder();

            for (int i = 0; i < paths.Length; ++i) {
                ReadFileHeader(paths[i], ReadHeaderMode.ReadAll, null, null);
            }

            if (0 < m_loadErrorMessages.Length) {
                MessageBox.Show(m_loadErrorMessages.ToString(), "読み込めなかったファイル", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            m_loadErrorMessages = null;

            UpdateUIStatus();
        }

        private void MenuItemFileSaveCueAs_Click(object sender, RoutedEventArgs e) {
            if (m_pcmDataList.Count() == 0) {
                MessageBox.Show("保存するデータはありません");
                return;
            }

            System.Diagnostics.Debug.Assert(0 < m_pcmDataList.Count());
            var pcmData0 = m_pcmDataList[0];

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(pcmData0.FullPath);
            dlg.Filter = "CUEファイル|*.cue";
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                CueSheetWriter csw = new CueSheetWriter();

                csw.SetAlbumTitle(m_playListItems[0].AlbumTitle);
                csw.SetAlbumPerformer(m_playListItems[0].PcmData().ArtistName);

                int i = 0;
                foreach (var pli in m_playListItems) {
                    var pcmData = m_pcmDataList[i];

                    CueSheetTrackInfo cst = new CueSheetTrackInfo();
                    cst.title = pli.Title;
                    cst.albumTitle = pli.AlbumTitle;
                    cst.indexId = pcmData.CueSheetIndex;
                    cst.performer = pli.ArtistName;
                    cst.readSeparatorAfter = pli.ReadSeparaterAfter;
                    cst.startTick = pcmData.StartTick;
                    cst.endTick = pcmData.EndTick;
                    cst.path = pcmData.FullPath;
                    csw.AddTrackInfo(cst);
                    ++i;
                }

                if (!csw.WriteToFile(dlg.FileName)) {
                    MessageBox.Show(
                        string.Format("ファイル保存失敗: {0}", dlg.FileName));
                }
            }
        }

        private void MenuItemFileSaveAs_Click(object sender, RoutedEventArgs e) {
            if (m_pcmDataList.Count() == 0) {
                MessageBox.Show("保存するデータはありません");
                return;
            }

            System.Diagnostics.Debug.Assert(0 < m_pcmDataList.Count());
            var pcmData0 = m_pcmDataList[0];

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.InitialDirectory = System.IO.Path.GetDirectoryName(pcmData0.FullPath);
            dlg.Filter = "PPWPLファイル|*.ppwpl";
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                if (!SavePlaylist(dlg.FileName)) {
                    MessageBox.Show(
                        string.Format("ファイル保存失敗: {0}", dlg.FileName));
                }
            }
        }
        
        private void MenuItemFileNew_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
        }

        private void MenuItemFileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (State.デバイスSetup完了 <= m_state) {
                // 追加不可。
                MessageBox.Show("都合により、出力デバイス選択後は再生リストに追加できない作りになっております。いったん再生を停止してから追加していただけますようお願い致します。");
                return;
            }

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter =
                "対応しているファイル|*.wav;*.wave;*.flac;*.aif;*.aiff;*.cue;*.ppwpl|" +
                "WAVEファイル|*.wav;*.wave|" +
                "FLACファイル|*.flac|" +
                "AIFFファイル|*.aif;*.aiff|" +
                "CUEファイル|*.cue|" +
                "PPWPLファイル|*.ppwpl|" +
                "全てのファイル|*.*";
            dlg.Multiselect = true;

            Nullable<bool> result = dlg.ShowDialog();


            if (result == true) {
                if (0 == System.IO.Path.GetExtension(dlg.FileName).CompareTo(".ppwpl")) {
                    // PPWプレイリストを読み込み
                    LoadPlaylist(dlg.FileName);
                } else {
                    // エラーメッセージを貯めて出す。
                    m_loadErrorMessages = new StringBuilder();

                    for (int i = 0; i < dlg.FileNames.Length; ++i) {
                        ReadFileHeader(dlg.FileNames[i], ReadHeaderMode.ReadAll, null, null);
                    }

                    if (0 < m_loadErrorMessages.Length) {
                        MessageBox.Show(m_loadErrorMessages.ToString(), "読み込めなかったファイル", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    m_loadErrorMessages = null;
                }
                UpdateUIStatus();
            }

        }

        private void MenuItemHelpAbout_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(
                string.Format("PlayPcmWin バージョン {0}\r\n\r\n" +
                    "PlayPcmWinは libFLACを使用しています。\r\n" +
                    "libFLACのライセンスは、New BSD Licenseです。" +
                    "libFlacLicense.txtをご覧ください。",
                    AssemblyVersion));
        }

        private void MenuItemHelpWeb_Click(object sender, RoutedEventArgs e) {
            try {
                System.Diagnostics.Process.Start("http://code.google.com/p/bitspersampleconv2/wiki/PlayPcmWin");
            } catch (System.ComponentModel.Win32Exception) {
            }
        }

        private static string DfmToStr(WasapiDataFeedMode dfm) {
            switch (dfm) {
            case WasapiDataFeedMode.EventDriven:
                return "イベント駆動";
            case WasapiDataFeedMode.TimerDriven:
                return "タイマー駆動";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        private static string ShareModeToStr(WasapiSharedOrExclusive t) {
            switch (t) {
            case WasapiSharedOrExclusive.Exclusive:
                return "WASAPI排他モード";
            case WasapiSharedOrExclusive.Shared:
                return "WASAPI共有モード";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        struct ReadFileRunWorkerCompletedArgs {
            public string message;
            public int hr;
        }

        private void ReadFileDoWork(object o, DoWorkEventArgs args) {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            ReadFileSingleThreadDoWork(o, args);

            sw.Stop();
            Console.WriteLine("ReadFile elapsed time {0}", sw.Elapsed);
        }

        /// <summary>
        ///  バックグラウンド読み込み(1スレッドバージョン)。
        ///  m_readFileWorker.RunWorkerAsync(読み込むgroupId)で開始する。
        /// </summary>
        private void ReadFileSingleThreadDoWork(object o, DoWorkEventArgs args) {
            BackgroundWorker bw = (BackgroundWorker)o;
            int readGroupId = (int)args.Argument;
            Console.WriteLine("D: ReadFileSingleDoWork({0}) started", readGroupId);

            ReadFileRunWorkerCompletedArgs r = new ReadFileRunWorkerCompletedArgs();
            try {
                r.hr = -1;

                wasapi.ClearPlayList();
                wasapi.AddPlayPcmDataStart();
                for (int i = 0; i < m_pcmDataList.Count; ++i) {
                    PcmDataLib.PcmData pd = m_pcmDataList[i];
                    if (pd.GroupId != readGroupId) {
                        continue;
                    }

                    // どーなのよ、という感じがするが。
                    // 効果絶大である。
                    GC.Collect();

                    long startFrame = (long)(pd.StartTick) * pd.SampleRate / 75;

                    PcmReader pr = new PcmReader();
                    int ercd = pr.StreamBegin(pd.FullPath, startFrame);
                    if (0 != ercd) {
                        r.message = string.Format("読み込みエラー。{0}\r\nエラーコード {1}(0x{1:X8})。{2}",
                            pd.FullPath, ercd, FlacDecodeIF.ErrorCodeToStr(ercd));
                        args.Result = r;
                        Console.WriteLine("D: ReadFileSingleDoWork() !readSuccess");
                        return;
                    }

                    // endFrameの計算。
                    long endFrame   = (long)(pd.EndTick)   * pd.SampleRate / 75;
                    if (endFrame < 0 || pr.NumFrames < endFrame) {
                        endFrame = pr.NumFrames;
                    }

                    int posFrame = 0;
                    do {
                        // 4Mフレームずつ読み出す。
                        int readFrames = 4 * 1024 * 1024;
                        if (endFrame < startFrame + posFrame + readFrames) {
                            readFrames = (int)(endFrame - (startFrame + posFrame));
                        }
                        byte[] part = pr.StreamReadOne(readFrames);

                        readFrames = part.Length / (pd.BitsPerFrame / 8);

                        // フレーム数は本来の数endFrame - startFrameを入れる
                        pd.SetSampleArray(endFrame - startFrame, part);

                        // 必要に応じて量子化ビット数の変更を行う。
                        PcmData pdAfter = PcmUtil.BitsPerSampleConvAsNeeded(pd, m_deviceSetupInfo.SampleFormat);

                        if (pdAfter.GetSampleArray() == null ||
                            0 == pdAfter.GetSampleArray().Length) {
                            // サンプルが存在しないんでWasapiにAddしない。
                            break;
                        }

                        if (pdAfter.NumChannels == 1) {
                            // モノラル→ステレオ変換。
                            pdAfter = pdAfter.MonoToStereo();
                        }

                        if (0 == posFrame) {
                            // 最初のフレームが来たとき領域確保。
                            int allocBytes = (int)(pdAfter.NumFrames * pdAfter.BitsPerFrame / 8);
                            
                            if (!wasapi.AddPlayPcmDataAllocateMemory(pd.Id, allocBytes)) {
                                //ClearPlayList(PlayListClearMode.ClearWithoutUpdateUI); //< メモリを空ける：効果があるか怪しいが
                                r.message = string.Format("メモリ不足です。再生リストのファイル数を減らすか、PCのメモリを増設して下さい。");
                                args.Result = r;
                                Console.WriteLine("D: ReadFileSingleDoWork() lowmemory");
                                pr.StreamEnd();
                                return;
                            }
                            m_readFileWorker.ReportProgress((int)(100 * i / m_pcmDataList.Count),
                                string.Format("wasapi.AddPlayPcmData(id={0}, frames={1})\r\n", pd.Id, pd.NumFrames));
                        }

                        int posBytes = (int)((long)posFrame * pdAfter.BitsPerFrame / 8);

                        bool rv = wasapi.AddPlayPcmDataSetPcmFragment(pd.Id, posBytes, pdAfter.GetSampleArray());
                        System.Diagnostics.Debug.Assert(rv);

                        pd.ForgetDataPart();
                        part = null;

                        pdAfter.ForgetDataPart();

                        if (bw.CancellationPending) {
                            Console.WriteLine("D: ReadFileSingleDoWork() Cancelled");
                            args.Cancel = true;
                            pr.StreamEnd();
                            return;
                        }

                        // posFrameを進める

                        posFrame += readFrames;

                        float progressPos = (float)posFrame / (endFrame - startFrame);
                        m_readFileWorker.ReportProgress((int)(100 * (i + progressPos) / m_pcmDataList.Count), "");
                    } while (posFrame < endFrame - startFrame);

                    pr.StreamEnd();
                }

                // ダメ押し。
                GC.Collect();
                wasapi.AddPlayPcmDataEnd();

                // 成功。
                r.message = string.Format("再生グループ{0}番読み込み完了。\r\n", readGroupId);
                r.hr = 0;
                args.Result = r;

                m_loadedGroupId = readGroupId;

                Console.WriteLine("D: ReadFileSingleDoWork({0}) done", readGroupId);
            } catch (Exception ex) {
                r.message = ex.ToString();
                args.Result = r;
                Console.WriteLine("D: ReadFileSingleDoWork() {0}", ex.ToString());
            }
        }

        private void ReadFileWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
            string s = (string)e.UserState;
            if (0 < s.Length) {
                AddLogText(s);
            }
            progressBar1.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// リピート設定。
        /// </summary>
        private void UpdatePlayRepeat() {
            bool repeat = false;
            // GroupIdが0しかない場合、リピート設定が可能。
            if (0 == CountWaveDataOnPlayGroup(1)) {
                if (checkBoxContinuous.IsChecked == true) {
                    repeat = true;
                }
            }
            wasapi.SetPlayRepeat(repeat);
        }

        /// <summary>
        /// バックグラウンドファイル読み込みが完了した。
        /// </summary>
        private void ReadFileRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            ReadFileRunWorkerCompletedArgs r = (ReadFileRunWorkerCompletedArgs)args.Result;
            AddLogText(r.message);

            if (r.hr < 0) {
                MessageBox.Show(r.message);
                Exit();
                return;
            }

            // WasapiCSのリピート設定。
            UpdatePlayRepeat();

            if (m_task.Type == TaskType.PlaySpecifiedGroup) {
                // ファイル読み込み完了後、再生を開始する。
                // 再生するファイルは、タスクで指定されたファイル。
                // このwavDataIdは、再生開始ボタンが押された時点で選択されていたファイル。
                int wavDataId = m_task.WavDataId;

                if (null != m_pliUpdatedByUserSelectWhileLoading) {
                    // (Issue 6)再生リストで選択されている曲が違う曲の場合、
                    // 選択されている曲を再生する。
                    wavDataId = m_pliUpdatedByUserSelectWhileLoading.PcmData().Id;

                    // 使い終わったのでクリアーする。
                    m_pliUpdatedByUserSelectWhileLoading = null;
                }

                ReadStartPlayByWavDataId(wavDataId);
                return;
            }

            // ファイル読み込み完了後、何もすることはない。
            ChangeState(State.ファイル読み込み完了);
            UpdateUIStatus();
        }

        /// <summary>
        /// 使用デバイスを指定する(デバイスIdと名前指定)
        /// 既に使用中の場合、空振りする。
        /// 別のデバイスを使用中の場合、そのデバイスを未使用にして、新しいデバイスを使用状態にする。
        /// </summary>
        /// <param name="id">デバイスId</param>
        /// <param name="deviceName">デバイス名</param>
        private bool UseDevice(int id, string deviceName) {
            int chosenDeviceId      = wasapi.GetUseDeviceId();
            string chosenDeviceName = wasapi.GetUseDeviceName();

            if (id == chosenDeviceId &&
                0 == deviceName.CompareTo(chosenDeviceName)) {
                // このデバイスが既に指定されている場合は、空振りする。
                return true;
            }

            if (0 <= chosenDeviceId) {
                // 別のデバイスが選択されている場合、Unchooseする。
                wasapi.UnchooseDevice();
                AddLogText(string.Format("wasapi.UnchooseDevice()\r\n"));
            }

            // このデバイスを選択。
            int hr = wasapi.ChooseDevice(listBoxDevices.SelectedIndex);
            AddLogText(string.Format("wasapi.ChooseDevice({0}) {1:X8}\r\n",
                deviceName, hr));
            if (hr < 0) {
                return false;
            }

            // 通常使用するデバイスとする。
            string selectedItemName = (string)listBoxDevices.SelectedItem;
            m_preference.PreferredDeviceName = selectedItemName;

            int loadGroupId = 0;
            if (0 <= dataGridPlayList.SelectedIndex) {
                PcmDataLib.PcmData w = m_playListItems[dataGridPlayList.SelectedIndex].PcmData();
                if (null != w) {
                    loadGroupId = w.GroupId;
                }
            }
            return true;
        }

        /// <summary>
        /// loadGroupIdのファイル読み込みを開始する。
        /// 読み込みが完了したらReadFileRunWorkerCompletedが呼ばれる。
        /// </summary>
        private void StartReadFiles(int loadGroupId) {
            progressBar1.Visibility = System.Windows.Visibility.Visible;
            progressBar1.Value = 0;

            m_loadingGroupId = loadGroupId;
            
            m_readFileWorker.RunWorkerAsync(loadGroupId);
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e) {
            if (!UseDevice(listBoxDevices.SelectedIndex, (string)listBoxDevices.SelectedItem)) {
                return;
            }

            // 再生する曲のwavDataIdをdataGridの選択セルから取得する
            int wavDataId = 0;
            var selectedCells = dataGridPlayList.SelectedCells;
            if (0 < selectedCells.Count) {
                var cell = selectedCells[0];
                System.Diagnostics.Debug.Assert(cell != null);
                PlayListItemInfo pli = cell.Item as PlayListItemInfo;
                System.Diagnostics.Debug.Assert(pli != null);
                var pcmData = pli.PcmData();

                if (null != pcmData) {
                    wavDataId = pcmData.Id;
                } else {
                    // ココまで読んだ的な行は、pcmDataを持っていない
                }
            }

            ReadStartPlayByWavDataId(wavDataId);
        }

        private void buttonPause_Click(object sender, RoutedEventArgs e) {
            int hr = 0;

            switch (m_state) {
            case State.再生中:
                hr = wasapi.Pause();
                AddLogText(string.Format("wasapi.Pause() {0:X8}\r\n", hr));
                if (0 <= hr) {
                    ChangeState(State.再生一時停止中);
                    UpdateUIStatus();
                } else {
                    // Pause失敗＝すでに再生していない または再生一時停止ができない状況。ここで状態遷移する必要はない。
                }
                break;
            case State.再生一時停止中:
                hr = wasapi.Unpause();
                AddLogText(string.Format("wasapi.Unpause() {0:X8}\r\n", hr));
                if (0 <= hr) {
                    ChangeState(State.再生中);
                    UpdateUIStatus();
                } else {
                    // Unpause失敗＝すでに再生していない。ここで状態遷移する必要はない。
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }


        /// <summary>
        /// wavDataIdのGroupがロードされていたら直ちに再生開始する。
        /// 読み込まれていない場合、直ちに再生を開始できないので、ロードしてから再生する。
        /// </summary>
        private bool ReadStartPlayByWavDataId(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);

            PcmDataLib.PcmData pcmData = m_pcmDataList[wavDataId];

            if (pcmData.GroupId != m_loadedGroupId) {
                // m_LoadedGroupIdと、wavData.GroupIdが異なる場合。
                // 再生するためには、ロードする必要がある。
                UnsetupDevice();

                if (!SetupDevice(pcmData.GroupId)) {
                    //dataGridPlayList.SelectedIndex = 0;
                    ChangeState(State.ファイル読み込み完了);

                    DeviceDeselect();
                    CreateDeviceList();
                    return false;
                }

                m_task.Set(TaskType.PlaySpecifiedGroup, pcmData.GroupId, pcmData.Id);
                StartReadPlayGroupOnTask();
                return true;
            }

            // wavDataIdのグループがm_LoadedGroupIdである。ロードされている。
            // 連続再生フラグの設定と、現在のグループが最後のグループかどうかによって
            // m_LoadedGroupIdの再生が自然に完了したら、行うタスクを決定する。
            UpdateNextTask();

            if (!SetupDevice(pcmData.GroupId)) {
                //dataGridPlayList.SelectedIndex = 0;
                ChangeState(State.ファイル読み込み完了);

                DeviceDeselect();
                CreateDeviceList();
                return false;
            }
            StartPlay(wavDataId);
            return true;
        }

        /// <summary>
        /// 現在のグループの最後のファイルの再生が終わった後に行うタスクを判定し、
        /// m_taskにセットする。
        /// </summary>
        private void UpdateNextTask() {
            if (0 == CountWaveDataOnPlayGroup(1)) {
                // ファイルグループが1個しかない場合、
                // wasapiUserの中で自発的にループ再生する。
                // ファイルの再生が終わった=停止。
                m_task.Set(TaskType.None);
                return;
            }

            // 順当に行ったら次に再生するグループ番号は(m_loadedGroupId+1)。
            // ①(m_loadedGroupId+1)の再生グループが存在する場合
            //     (m_loadedGroupId+1)の再生グループを再生開始する。
            // ②(m_loadedGroupId+1)の再生グループが存在しない場合
            //     ②-①連続再生(checkBoxContinuous.IsChecked==true)の場合
            //         GroupId==0、wavDataId=0を再生開始する。
            //     ②-②連続再生ではない場合
            //         停止する。先頭の曲を選択状態にする。
            int nextGroupId = m_loadedGroupId + 1;

            if (0 < CountWaveDataOnPlayGroup(nextGroupId)) {
                m_task.Set(TaskType.PlaySpecifiedGroup, 
                    nextGroupId,
                    GetFirstWavDataIdOnGroup(nextGroupId));
                return;
            }

            if (checkBoxContinuous.IsChecked == true) {
                m_task.Set(TaskType.PlaySpecifiedGroup, 0, 0);
                return;
            }

            m_task.Set(TaskType.None);
        }

        /// <summary>
        /// ただちに再生を開始する。
        /// wavDataIdのGroupが、ロードされている必要がある。
        /// </summary>
        /// <returns>false: 再生開始できなかった。</returns>
        private bool StartPlay(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);
            if (m_pcmDataList[wavDataId].GroupId != m_loadedGroupId) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            slider1.Maximum = wasapi.GetTotalFrameNum();

            ChangeState(State.再生中);
            UpdateUIStatus();

            m_sw.Reset();
            m_sw.Start();

            int hr = wasapi.Start(wavDataId);
            AddLogText(string.Format("wasapi.Start({0}) {1:X8}\r\n",
                wavDataId, hr));
            if (hr < 0) {
                MessageBox.Show(string.Format("再生開始に失敗！{0:X8}", hr));
                Exit();
                return false;
            }

            // 再生バックグラウンドタスク開始。PlayDoWorkが実行される。
            // 再生バックグラウンドタスクを止めるには、Stop()を呼ぶ。
            // 再生バックグラウンドタスクが止まったらPlayRunWorkerCompletedが呼ばれる。
            m_playWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// 再生中。バックグラウンドスレッド。
        /// </summary>
        private void PlayDoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("PlayDoWork started");
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!wasapi.Run(PROGRESS_REPORT_INTERVAL_MS)) {
                m_playWorker.ReportProgress(0);
                System.Threading.Thread.Sleep(1);
                if (bw.CancellationPending) {
                    Console.WriteLine("PlayDoWork() CANCELED");
                    wasapi.Stop();
                    args.Cancel = true;
                }
            }

            // 正常に最後まで再生が終わった場合、ここでStopを呼んで、後始末する。
            // キャンセルの場合は、2回Stopが呼ばれることになるが、問題ない!!!
            wasapi.Stop();

            // 停止完了後タスクの処理は、ここではなく、PlayRunWorkerCompletedで行う。

            //Console.WriteLine("PlayDoWork end");
        }

        private static string SecondToHMSString(int seconds) {
            int h = seconds / 3600;
            int m = seconds / 60 - h * 60;
            int s = seconds - h * 3600 - m * 60;
            return string.Format(
                "{0:D2}:{1:D2}:{2:D2}", h, m, s);
        }

        /// <summary>
        /// 再生の進行状況をUIに反映する。
        /// </summary>
        private void PlayProgressChanged(object o, ProgressChangedEventArgs args) {
            BackgroundWorker bw = (BackgroundWorker)o;

            if (null == wasapi) {
                return;
            }

            if (bw.CancellationPending) {
                // ワーカースレッドがキャンセルされているので、何もしない。
                return;
            }

            int playingPcmDataId = wasapi.GetNowPlayingPcmDataId();

            if (playingPcmDataId < 0) {
                labelPlayingTime.Content = "--:--:--/--:--:--";
            } else {
                dataGridPlayList.SelectedIndex
                    = GetPlayListIndexOfWaveDataId(playingPcmDataId);

                slider1.Value =wasapi.GetPosFrame();
                PcmDataLib.PcmData pcmData = m_pcmDataList[playingPcmDataId];
                // textBoxFileName.Text = pcmData.FileName;

                slider1.Maximum = pcmData.NumFrames;


                labelPlayingTime.Content = string.Format("{0}/{1}",
                    SecondToHMSString((int)(slider1.Value / pcmData.SampleRate)),
                    SecondToHMSString((int)(pcmData.NumFrames / pcmData.SampleRate)));
            }
        }

        /// <summary>
        /// m_taskに指定されているグループをロードし、ロード完了したら指定ファイルを再生開始する。
        /// ファイル読み込み完了状態にいるときに呼ぶ。
        /// </summary>
        private void StartReadPlayGroupOnTask() {
            m_loadedGroupId = -1;

            System.Diagnostics.Debug.Assert(m_task.Type == TaskType.PlaySpecifiedGroup);

            // 再生状態→再生グループ切り替え中状態に遷移。
            ChangeState(State.再生グループ切り替え中);
            UpdateUIStatus();

            StartReadFiles(m_task.GroupId);
        }

        /// <summary>
        /// 再生終了後タスクを実行する。
        /// </summary>
        private void PerformPlayCompletedTask() {
            // 再生終了後に行うタスクがある場合、ここで実行する。
            if (m_task.Type == TaskType.PlaySpecifiedGroup) {
                UnsetupDevice();

                if (SetupDevice(m_task.GroupId)) {
                    StartReadPlayGroupOnTask();
                    return;
                }

                // デバイスの設定を試みたら、失敗した。
                // FALL_THROUGHする。
            }

            // 再生終了後に行うタスクがない。停止する。
            // 再生状態→ファイル読み込み完了状態。

            // 先頭の曲を選択状態にする。
            //dataGridPlayList.SelectedIndex = 0;
            
            ChangeState(State.ファイル読み込み完了);

            // さらに、デバイスを選択解除し、デバイス一覧を更新する。
            // 停止後に再生リストの追加ができて便利。
            DeviceDeselect();
            CreateDeviceList();
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            m_sw.Stop();
            AddLogText(string.Format("再生終了. 所要時間 {0}\r\n", m_sw.Elapsed));

            PerformPlayCompletedTask();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            ChangeState(State.再生停止開始);
            UpdateUIStatus();

            // 停止ボタンで停止した場合は、停止後何もしない。
            Stop(new Task(TaskType.None));
            AddLogText(string.Format("wasapi.Stop()\r\n"));
        }

        private void slider1_MouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                Console.WriteLine("slider1_MouseMove {0}", slider1.Value);
                if (!buttonPlay.IsEnabled) {
                    wasapi.SetPosFrame((int)slider1.Value);
                }
            }
        }

        private void buttonInspectDevice_Click(object sender, RoutedEventArgs e) {
            string dn = wasapi.GetDeviceName(listBoxDevices.SelectedIndex);
            string s = wasapi.InspectDevice(listBoxDevices.SelectedIndex);
            AddLogText(string.Format("wasapi.InspectDevice()\r\n{0}\r\n{1}\r\n", dn, s));
        }

        /// <summary>
        /// SettingsWindowによって変更された表示情報をUIに反映する。
        /// </summary>
        void UpdateWindowSettings() {
            FontFamilyConverter ffc = new FontFamilyConverter();
            FontFamily ff = ffc.ConvertFromString(m_preference.PlayingTimeFontName) as FontFamily;
            if (null != ff) {
                labelPlayingTime.FontFamily = ff;
            }
            labelPlayingTime.FontSize = m_preference.PlayingTimeSize;
            labelPlayingTime.FontWeight = m_preference.PlayingTimeFontBold
                ? FontWeights.Bold
                : FontWeights.Normal;

            sliderWindowScaling.Value = m_preference.WindowScale;
        }

        List<string> m_logList = new List<string>();
        int m_logLineNum = 100;

        /// <summary>
        /// ログを追加する。
        /// </summary>
        /// <param name="s">追加するログ。行末に\r\nを入れる必要あり。</param>
        private void AddLogText(string s) {
            System.Console.Write(s);

            // ログを適当なエントリ数で流れるようにする。
            // sは複数行の文字列が入っていたり、改行が入っていなかったりするので、行数制限にはなっていない。
            m_logList.Add(s);
            while (m_logLineNum < m_logList.Count) {
                m_logList.RemoveAt(0);
            }

            StringBuilder sb = new StringBuilder();
            foreach (var item in m_logList) {
                sb.Append(item);
            }

            textBoxLog.Text = sb.ToString();
            textBoxLog.ScrollToEnd();
        }

        /// <summary>
        /// ロード中に選択曲が変更された場合、ロード後に再生曲変更処理を行う。
        /// ChangePlayWavDataById()でセットし
        /// ReadFileRunWorkerCompleted()で参照する。
        /// </summary>
        private PlayListItemInfo m_pliUpdatedByUserSelectWhileLoading = null;

        /// <summary>
        /// 再生中に、再生曲をwavDataIdの曲に切り替える。
        /// wavDataIdの曲がロードされていたら、直ちに再生曲切り替え。
        /// ロードされていなければ、グループをロードしてから再生。
        /// 
        /// 再生中でない場合は、最初に再生する曲をwavDataIdの曲に変更する。
        /// </summary>
        /// <param name="wavDataId">再生曲</param>
        private void ChangePlayWavDataById(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);

            int groupId = m_pcmDataList[wavDataId].GroupId;

            int playingId = wasapi.GetNowPlayingPcmDataId();
            if (playingId < 0 && 0 <= m_loadingGroupId) {
                // 再生中でなく、ロード中の場合。
                // ロード完了後ReadFileRunWorkerCompleted()で再生する曲を切り替えるための
                // 情報をセットする。
                m_pliUpdatedByUserSelectWhileLoading
                    = m_playListItems[dataGridPlayList.SelectedIndex];
                return;
            }

            if (playingId < 0) {
                // 再生中でなく、ロード中でもない場合。
                wasapi.UpdatePlayPcmDataById(wavDataId);
                return;
            }

            if (m_pcmDataList[playingId].GroupId == groupId) {
                // 同一ファイルグループのファイルの場合、すぐにこの曲が再生可能。
                wasapi.UpdatePlayPcmDataById(wavDataId);
                AddLogText(string.Format("wasapi.UpdatePlayPcmDataById({0})\r\n",
                    wavDataId));
            } else {
                // ファイルグループが違う場合、再生を停止し、グループを読み直し、再生を再開する。
                Stop(new Task(TaskType.PlaySpecifiedGroup, groupId, wavDataId));
            }
        }

        /// <summary>
        /// [ここまで一括読み込み]を追加できたら追加する。
        /// 
        /// [ここまで一括読み込み]ボタン押下以外にも、
        /// ギャップ→ここまで変換からも呼び出される。
        /// </summary>
        private void AddKokomade() {
            if (0 == m_playListItems.Count) {
                return;
            }

            m_playListItems[m_playListItems.Count() - 1].ReadSeparaterAfter = true;

            //m_playListView.RefreshCollection();
            ++m_groupIdNextAdd;
        }

        // しょーもない関数群 ////////////////////////////////////////////////////////////////////////

        private WasapiCS.SchedulerTaskType
        PreferenceSchedulerTaskTypeToWasapiCSSchedulerTaskType(
            RenderThreadTaskType t) {
            switch (t) {
            case RenderThreadTaskType.None:
                return WasapiCS.SchedulerTaskType.None;
            case RenderThreadTaskType.Audio:
                return WasapiCS.SchedulerTaskType.Audio;
            case RenderThreadTaskType.ProAudio:
                return WasapiCS.SchedulerTaskType.ProAudio;
            case RenderThreadTaskType.Playback:
                return WasapiCS.SchedulerTaskType.Playback;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.SchedulerTaskType.None; ;
            }
        }

        private WasapiCS.ShareMode
        PreferenceShareModeToWasapiCSShareMode(WasapiSharedOrExclusive t) {
            switch (t) {
            case WasapiSharedOrExclusive.Shared:
                return WasapiCS.ShareMode.Shared;
            case WasapiSharedOrExclusive.Exclusive:
                return WasapiCS.ShareMode.Exclusive;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.ShareMode.Exclusive;
            }
        }

        private WasapiCS.DataFeedMode
        PreferenceDataFeedModeToWasapiCS(WasapiDataFeedMode t) {
            switch (t) {
            case WasapiDataFeedMode.EventDriven:
                return WasapiCS.DataFeedMode.EventDriven;
            case WasapiDataFeedMode.TimerDriven:
                return WasapiCS.DataFeedMode.TimerDriven;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.DataFeedMode.EventDriven;
            }
        }

        private WasapiCS.BitFormatType
        VrtToBft(PcmDataLib.PcmData.ValueRepresentationType vrt) {
            switch (vrt) {
            case PcmDataLib.PcmData.ValueRepresentationType.SInt:
                return WasapiCS.BitFormatType.SInt;
            case PcmDataLib.PcmData.ValueRepresentationType.SFloat:
                return WasapiCS.BitFormatType.SFloat;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.BitFormatType.SInt;
            }
        }

        // イベント処理 /////////////////////////////////////////////////////

        private void buttonSettings_Click(object sender, RoutedEventArgs e) {
            SettingsWindow sw = new SettingsWindow();
            sw.SetPreference(m_preference);
            sw.ShowDialog();

            UpdateWindowSettings();
        }

        private void radioButtonTaskAudio_Checked(object sender, RoutedEventArgs e) {
            m_preference.renderThreadTaskType = RenderThreadTaskType.Audio;
        }

        private void radioButtonTaskProAudio_Checked(object sender, RoutedEventArgs e) {
            m_preference.renderThreadTaskType = RenderThreadTaskType.ProAudio;
        }

        private void radioButtonTaskPlayback_Checked(object sender, RoutedEventArgs e) {
            m_preference.renderThreadTaskType = RenderThreadTaskType.Playback;
        }

        private void radioButtonTaskNone_Checked(object sender, RoutedEventArgs e) {
            m_preference.renderThreadTaskType = RenderThreadTaskType.None;
        }

        private void radioButtonExclusive_Checked(object sender, RoutedEventArgs e) {
            m_preference.wasapiSharedOrExclusive = WasapiSharedOrExclusive.Exclusive;
        }

        private void radioButtonShared_Checked(object sender, RoutedEventArgs e) {
            m_preference.wasapiSharedOrExclusive = WasapiSharedOrExclusive.Shared;
        }

        private void radioButtonEventDriven_Checked(object sender, RoutedEventArgs e) {
            m_preference.wasapiDataFeedMode = WasapiDataFeedMode.EventDriven;
        }

        private void radioButtonTimerDriven_Checked(object sender, RoutedEventArgs e) {
            m_preference.wasapiDataFeedMode = WasapiDataFeedMode.TimerDriven;
        }

        private void buttonRemovePlayList_Click(object sender, RoutedEventArgs e) {
            var selectedCells = dataGridPlayList.SelectedCells;
            if (0 == selectedCells.Count) {
                return;
            }

            if (selectedCells.Count == m_playListItems.Count) {
                // すべて消える。再生開始などが出来なくなるので別処理。
                ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
            } else {
                // 再生リストの一部項目が消える。
                int idx;
                while (0 <= (idx = dataGridPlayList.SelectedIndex)) {
                    m_pcmDataList.RemoveAt(idx);
                    m_playListItems.RemoveAt(idx);
                    dataGridPlayList.UpdateLayout();
                }
                GC.Collect();

                RenumberPcmDataId();

                progressBar1.Value = 0;
                UpdateUIStatus();
            }
        }

        private void buttonPrev_Click(object sender, RoutedEventArgs e) {
            int wavDataId = wasapi.GetNowPlayingPcmDataId();
            --wavDataId;
            if (wavDataId < 0) {
                wavDataId = 0;
            }

            ChangePlayWavDataById(wavDataId);
        }

        private void buttonNext_Click(object sender, RoutedEventArgs e) {
            int wavDataId = wasapi.GetNowPlayingPcmDataId();
            ++wavDataId;
            if (wavDataId < 0) {
                wavDataId = 0;
            }
            if (m_pcmDataList.Count <= wavDataId) {
                wavDataId = 0;
            }
            
            ChangePlayWavDataById(wavDataId);
        }

        private void checkBoxContinuous_CheckedChanged(object sender, RoutedEventArgs e) {
            if (buttonStop.IsEnabled) {
                // 再生中に連続再生かどうかが変更された。
                UpdatePlayRepeat();
            }
        }

        private void dataGridPlayList_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            m_playListMouseDown = true;

        }

        private void dataGridPlayList_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            m_playListMouseDown = false;
        }

        private void dataGridPlayList_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e) {
            /*
                if (m_state == State.プレイリストあり && 0 <= dataGridPlayList.SelectedIndex) {
                    buttonRemovePlayList.IsEnabled = true;
                } else {
                    buttonRemovePlayList.IsEnabled = false;
                }
            */
        }

        private void dataGridPlayList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (m_state == State.プレイリストあり && 0 <= dataGridPlayList.SelectedIndex) {
                buttonRemovePlayList.IsEnabled = true;
            } else {
                buttonRemovePlayList.IsEnabled = false;
            }

            if (null == wasapi) {
                return;
            }

            if (!m_playListMouseDown ||
                dataGridPlayList.SelectedIndex < 0 ||
                m_playListItems.Count() <= dataGridPlayList.SelectedIndex) {
                return;
            }

            PlayListItemInfo pli = m_playListItems[dataGridPlayList.SelectedIndex];
            if (pli.PcmData() == null) {
                // 曲じゃない部分を選択したら無視。
                return;
            }

            if (m_state != State.再生中) {
                ChangePlayWavDataById(pli.PcmData().Id);
                return;
            }

            // 再生中の場合。

            int playingId = wasapi.GetNowPlayingPcmDataId();
            if (playingId < 0) {
                return;
            }

            // 再生中で、しかも、マウス押下中にこのイベントが来た場合で、
            // しかも、この曲を再生していない場合、この曲を再生する。
            if (null != pli.PcmData() &&
                playingId != pli.PcmData().Id) {
                ChangePlayWavDataById(pli.PcmData().Id);
            }
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e) {
            Exit();
        }

        private void buttonMinimize_Click(object sender, RoutedEventArgs e) {
            WindowState = System.Windows.WindowState.Minimized;
        }

        Point m_prevPos;

        private bool IsWindowMoveMode(MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) {
                return false;
            }

            foreach (MenuItem mi in menu1.Items) {
                if (mi.IsMouseOver) {
                    return false;
                }
            }
            return true;
        }

        private void menu1_MouseMove(object sender, MouseEventArgs e) {
            if (IsWindowMoveMode(e)) {
                Point pos = e.GetPosition(this);
                Point delta = new Point(pos.X - m_prevPos.X, pos.Y - m_prevPos.Y);

                Left += delta.X;
                Top += delta.Y;
            } else {
                m_prevPos = e.GetPosition(this);
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl)) {
                // CTRL + マウスホイールで画面のスケーリング

                double scaling = sliderWindowScaling.Value;
                if (e.Delta < 0) {
                    // 1.25の128乗根 = 1.001744829441175331741294013303
                    scaling /= 1.001744829441175331741294013303;
                } else {
                    scaling *= 1.001744829441175331741294013303;
                }
                sliderWindowScaling.Value = scaling;
                m_preference.WindowScale = scaling;
            }
        }

        /// <summary>
        /// デバイスが突然消えたとか、突然増えたとかのイベント。
        /// </summary>
        private void WasapiStatusChanged() {
            Console.WriteLine("WasapiStatusChanged");
            Dispatcher.BeginInvoke(new Action(delegate() {

                // お気に入りデバイス設定。
                object selectedItem = listBoxDevices.SelectedItem;
                if (null != selectedItem) {
                    string selectedItemName = (string)selectedItem;
                    m_preference.PreferredDeviceName = selectedItemName;
                }

                DeviceDeselect();
                CreateDeviceList();
            }));
        }
        
        #region ドラッグアンドドロップ

        private void dataGridPlayList_CheckDropTarget(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                // ファイルのドラッグアンドドロップ。
                // ここでハンドルせず、MainWindowのMainWindowDragDropに任せる。
                e.Handled = false;
                return;
            }

            e.Handled = true;
            DataGridRow row = FindVisualParent<DataGridRow>(e.OriginalSource as UIElement);
            if (row == null || !(row.Item is PlayListItemInfo)) {
                // 行がドラッグされていない。
                e.Effects = DragDropEffects.None;
            } else {
                // 行がドラッグされている。
                // Id列を選択している場合のみドラッグアンドドロップ可能。
                //if (0 != "Id".CompareTo(dataGridPlayList.CurrentCell.Column.Header)) {
                //    e.Effects = DragDropEffects.None;
                //}
                // e.Effects = DragDropEffects.Move;
            }
        }

        private void dataGridPlayList_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                // ファイルのドラッグアンドドロップ。
                // ここでハンドルせず、MainWindowのMainWindowDragDropに任せる。
                e.Handled = false;
                return;
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
            DataGridRow row = FindVisualParent<DataGridRow>(e.OriginalSource as UIElement);
            if (row == null || !(row.Item is PlayListItemInfo)) {
                // 行がドラッグされていない。(セルがドラッグされている)
            } else {
                // 再生リスト項目のドロップ。
                m_dropTargetPlayListItem = row.Item as PlayListItemInfo;
                if (m_dropTargetPlayListItem != null) {
                    e.Effects = DragDropEffects.Move;
                }
            }
        }

        private void dataGridPlayList_MouseMove(object sender, MouseEventArgs e) {
            if (m_state == State.再生中 ||
                m_state == State.再生一時停止中) {
                // 再生中は再生リスト項目入れ替え不可能。
                return;
            }

            if (e.LeftButton != MouseButtonState.Pressed) {
                // 左マウスボタンが押されていない。
                return;
            }

            DataGridRow row = FindVisualParent<DataGridRow>(e.OriginalSource as FrameworkElement);
            if ((row == null) || !row.IsSelected) {
                Console.WriteLine("MouseMove row==null || !row.IsSelected");
                return;
            }

            PlayListItemInfo pli = row.Item as PlayListItemInfo;

            // MainWindow.Drop()イベントを発生させる(ブロック)。
            DragDropEffects finalDropEffect = DragDrop.DoDragDrop(row, pli, DragDropEffects.Move);
            if (finalDropEffect == DragDropEffects.Move && m_dropTargetPlayListItem != null) {
                // ドロップ操作実行。
                Console.WriteLine("MouseMove do move");

                int oldIndex = m_playListItems.IndexOf(pli);
                int newIndex = m_playListItems.IndexOf(m_dropTargetPlayListItem);
                if (oldIndex != newIndex) {
                    // 項目が挿入された。PcmDataも挿入処理する。
                    m_playListItems.Move(oldIndex, newIndex);
                    PcmDataListItemsMove(oldIndex, newIndex);
                    // m_playListView.RefreshCollection();
                    dataGridPlayList.UpdateLayout();
                }
                m_dropTargetPlayListItem = null;
            }
        }

        private static T FindVisualParent<T>(UIElement element) where T : UIElement {
            UIElement parent = element;
            while (parent != null) {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null) {
                    return correctlyTyped;
                }

                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private PlayListItemInfo m_dropTargetPlayListItem = null;

        #endregion

        /// <summary>
        /// m_playListItemsのIdとGroupIdをリナンバーする。
        /// </summary>
        private void PcmDataListItemsRenumber() {
            m_groupIdNextAdd = 0;
            for (int i = 0; i < m_pcmDataList.Count(); ++i) {
                PcmData pcmData = m_pcmDataList[i];
                PlayListItemInfo pli = m_playListItems[i];

                if (0 < i) {
                    PcmData prevPcmData = m_pcmDataList[i - 1];
                    PlayListItemInfo prevPli = m_playListItems[i - 1];

                    if (prevPli.ReadSeparaterAfter || !pcmData.IsSameFormat(prevPcmData)) {
                        /* 1つ前の項目にReadSeparatorAfterフラグが立っている、または
                         * 1つ前の項目とPCMフォーマットが異なる。
                         * ファイルグループ番号を更新する。
                         */
                        ++m_groupIdNextAdd;
                    }
                }

                pcmData.Id = i;
                pcmData.GroupId = m_groupIdNextAdd;
            }
        }

        /// <summary>
        /// oldIdxの項目をnewIdxの項目の後に挿入する。
        /// </summary>
        private void PcmDataListItemsMove(int oldIdx, int newIdx) {
            System.Diagnostics.Debug.Assert(oldIdx != newIdx);

            /* oldIdx==0, newIdx==1, Count==2の場合
             * remove(0)
             * insert(1)
             * 
             * oldIdx==1, newIdx==0, Count==2の場合
             * remove(1)
             * insert(0)
             */

            PcmData old = m_pcmDataList[oldIdx];
            m_pcmDataList.RemoveAt(oldIdx);
            m_pcmDataList.Insert(newIdx, old);

            // Idをリナンバーする。
            PcmDataListItemsRenumber();
        }

        void PlayListItemInfoPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "ReadSeparaterAfter") {
                // グループ番号をリナンバーする。
                PcmDataListItemsRenumber();
            }
        }

        private void buttonClearPlayList_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
        }

    }
}
