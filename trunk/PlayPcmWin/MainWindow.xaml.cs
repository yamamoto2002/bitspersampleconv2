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

namespace PlayPcmWin
{
    /// <summary>
    /// プレイリスト1項目の情報。区切り線を1個と数える。
    /// ListBoxPlayList.Itemsの項目と一対一に対応する。
    /// </summary>
    class PlayListItemInfo {
        public enum ItemType {
            Item,
            Separator
        }

        public ItemType Type { get; set; }
        public WavData WavData { get; set; }

        public PlayListItemInfo() {
            Type = ItemType.Item;
            WavData = null;
        }

        public PlayListItemInfo(ItemType type, WavData wavData) {
            Type = type;
            WavData = wavData;
        }
    }

    public partial class MainWindow : Window
    {
        const int PROGRESS_REPORT_INTERVAL_MS = 500;

        private WasapiCS wasapi;

        private Preference m_preference = new Preference();

        /// <summary>
        /// WavDataのリスト。
        /// </summary>
        private List<WavData> m_wavDataList = new List<WavData>();

        /// <summary>
        /// プレイリスト項目情報。
        /// </summary>
        private List<PlayListItemInfo> m_playListItems = new List<PlayListItemInfo>();

        private BackgroundWorker m_playWorker;
        private BackgroundWorker m_readFileWorker;
        private System.Diagnostics.Stopwatch m_sw = new System.Diagnostics.Stopwatch();
        private bool m_playListMouseDown = false;

        // プレイリストにAddしたファイルに振られるGroupId。
        private int m_readGroupId = 0;

        // メモリ上に読み込まれているGroupId。
        private int m_loadedGroupId = -1;

        /// <summary>
        /// デバイスのセットアップ情報
        /// </summary>
        struct DeviceSetupInfo {
            bool setuped;
            int samplingRate;
            int bitsPerSample;
            int latencyMillisec;
            WasapiDataFeedMode dfm;
            WasapiSharedOrExclusive shareMode;
            RenderThreadTaskType threadTaskType;

            public bool Is(int samplingRate,
                int bitsPerSample,
                int latencyMillisec,
                WasapiDataFeedMode dfm,
                WasapiSharedOrExclusive shareMode,
                RenderThreadTaskType threadTaskType) {
                return (this.setuped
                    && this.samplingRate == samplingRate
                    && this.bitsPerSample == bitsPerSample
                    && this.latencyMillisec == latencyMillisec
                    && this.dfm == dfm
                    && this.shareMode == shareMode
                    && this.threadTaskType == threadTaskType);
            }

            public void Set(int samplingRate,
                int bitsPerSample,
                int latencyMillisec,
                WasapiDataFeedMode dfm,
                WasapiSharedOrExclusive shareMode,
                RenderThreadTaskType threadTaskType) {
                    this.setuped = true;
                this.samplingRate = samplingRate;
                this.bitsPerSample = bitsPerSample;
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
            for (int i = 0; i < m_wavDataList.Count(); ++i) {
                if (m_wavDataList[i].GroupId == groupId) {
                    return m_wavDataList[i].Id;
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
            for (int i = 0; i < m_wavDataList.Count(); ++i) {
                if (m_wavDataList[i].GroupId == groupId) {
                    ++count;
                }
            }

            return count;
        }

        /// <summary>
        /// 指定されたWavDataIdの、プレイリスト位置番号(プレイリスト内のindex)を戻す。
        /// </summary>
        /// <param name="wavDataId">プレイリスト位置番号を知りたいWaveDataのId</param>
        /// <returns>プレイリスト位置番号(プレイリスト内のindex)。見つからないときは-1</returns>
        private int GetPlayListIndexOfWaveDataId(int wavDataId) {
            for (int i = 0; i < m_playListItems.Count(); ++i) {
                if (m_playListItems[i].WavData != null
                    && m_playListItems[i].WavData.Id == wavDataId) {
                    return i;
                }
            }

            return -1;
        }

        public MainWindow()
        {
            InitializeComponent();

            // InitializeComponent()によって、チェックボックスのチェックイベントが発生し
            // m_preferenceの内容が変わるので、InitializeComponent()の後にロードする。

            m_preference = PreferenceStore.Load();

            AddLogText(string.Format("PlayPcmWin {0} {1}\r\n",
                    AssemblyVersion,
                    IntPtr.Size == 8 ? "64bit" : "32bit"));

            m_readGroupId = 0;

            int hr = 0;
            wasapi = new WasapiCS();
            hr = wasapi.Init();
            AddLogText(string.Format("wasapi.Init() {0:X8}\r\n", hr));

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
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            Closed += new EventHandler(MainWindow_Closed);

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

            CreateDeviceList();
        }

        private void UpdateUIStatus() {
            switch (m_state) {
            case State.初期化完了:
                menuItemFileOpen.IsEnabled       = true;
                buttonPlay.IsEnabled             = false;
                buttonStop.IsEnabled             = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                buttonClearPlayList.IsEnabled    = false;
                buttonReadSeparator.IsEnabled    = false;
                groupBoxWasapiSettings.IsEnabled = true;

                buttonInspectDevice.IsEnabled    = true;
                statusBarText.Content = "再生リストを作って下さい。";
                break;
            case State.プレイリストあり:
                menuItemFileOpen.IsEnabled = true;
                buttonPlay.IsEnabled             = true;
                buttonStop.IsEnabled             = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                buttonClearPlayList.IsEnabled    = true;
                buttonReadSeparator.IsEnabled    = true;
                groupBoxWasapiSettings.IsEnabled = true;

                buttonInspectDevice.IsEnabled    = false;
                statusBarText.Content = "再生リストを作り、再生ボタンを押して下さい。";
                break;
            case State.デバイスSetup完了:
                // 一覧のクリアーとデバイスの選択、再生リストの作成関連を押せなくする。
                menuItemFileOpen.IsEnabled = false;
                buttonPlay.IsEnabled             = false;
                buttonStop.IsEnabled             = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                buttonClearPlayList.IsEnabled = false;
                buttonReadSeparator.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonInspectDevice.IsEnabled = false;
                statusBarText.Content = "デバイス選択完了。ファイル読み込み中……";
                break;
            case State.ファイル読み込み完了:
                menuItemFileOpen.IsEnabled = false;
                buttonPlay.IsEnabled = true;
                buttonStop.IsEnabled = false;

                buttonNext.IsEnabled = false;
                buttonPrev.IsEnabled = false;
                buttonClearPlayList.IsEnabled = false;
                buttonReadSeparator.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonInspectDevice.IsEnabled = false;
                statusBarText.Content = "ファイル読み込み完了。再生できます。";

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                slider1.Value = 0;
                label1.Content = "0/0";
                break;
            case State.再生中:
                menuItemFileOpen.IsEnabled = false;
                buttonPlay.IsEnabled = false;
                buttonStop.IsEnabled = true;

                buttonNext.IsEnabled = true;
                buttonPrev.IsEnabled = true;
                buttonClearPlayList.IsEnabled = false;
                buttonReadSeparator.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonInspectDevice.IsEnabled = false;
                statusBarText.Content = "再生中";

                progressBar1.Visibility = System.Windows.Visibility.Collapsed;
                break;
            case State.再生停止開始:
                menuItemFileOpen.IsEnabled = false;
                buttonPlay.IsEnabled = false;
                buttonStop.IsEnabled = false;

                buttonNext.IsEnabled = false;
                buttonPrev.IsEnabled = false;
                buttonClearPlayList.IsEnabled = false;
                buttonReadSeparator.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonInspectDevice.IsEnabled = false;
                statusBarText.Content = "再生停止開始";
                break;
            case State.再生グループ切り替え中:
                menuItemFileOpen.IsEnabled = false;
                buttonPlay.IsEnabled = false;
                buttonStop.IsEnabled = false;

                buttonNext.IsEnabled = false;
                buttonPrev.IsEnabled = false;
                buttonClearPlayList.IsEnabled = false;
                buttonReadSeparator.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonInspectDevice.IsEnabled = false;
                statusBarText.Content = "再生グループ読み込み中";
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        /// <summary>
        /// 起動時に1回だけ呼ぶようにする。
        /// そうしないと再生中のデバイス番号がずれる。
        /// </summary>
        private void CreateDeviceList() {
            int hr;

            int selectedIndex = -1;
            if (0 < listBoxDevices.Items.Count) {
                selectedIndex = listBoxDevices.SelectedIndex;
            }

            listBoxDevices.Items.Clear();

            hr = wasapi.DoDeviceEnumeration(WasapiCS.DeviceType.Play);
            AddLogText(string.Format("wasapi.DoDeviceEnumeration(Play) {0:X8}\r\n", hr));

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                string deviceName = wasapi.GetDeviceName(i);
                listBoxDevices.Items.Add(deviceName);
                if (selectedIndex < 0
                    && 0 < m_preference.PreferredDeviceName.Length
                    && 0 == m_preference.PreferredDeviceName.CompareTo(deviceName)) {
                    // まだユーザーが選択していない場合は
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

            if (0 < m_wavDataList.Count) {
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
        /// この関数は、デバイスリストが消えるため、不便である。
        /// </summary>
        private void DeviceDeselect() {
            System.Diagnostics.Debug.Assert(!m_playWorker.IsBusy);

            UnsetupDevice();

            wasapi.UnchooseDevice();
            AddLogText("wasapi.UnchooseDevice()\r\n");

            m_loadedGroupId = -1;
        }

        private void Exit() {
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

                // 設定ファイルを書き出す。
                PreferenceStore.Save(m_preference);
            }

            Application.Current.Shutdown();
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

            WavData startWavData = m_wavDataList[startWavDataId];

            // 量子化ビット数固定設定。
            int preferedBitsPerSample = startWavData.BitsPerSample;
            switch (m_preference.bitsPerSampleFixType) {
            case BitsPerSampleFixType.Variable:
                break;
            case BitsPerSampleFixType.Sint16:
                preferedBitsPerSample = 16;
                break;
            case BitsPerSampleFixType.Sint32:
                preferedBitsPerSample = 24;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            if (m_deviceSetupInfo.Is(
                startWavData.SampleRate,
                preferedBitsPerSample,
                latencyMillisec,
                m_preference.wasapiDataFeedMode,
                m_preference.wasapiSharedOrExclusive,
                m_preference.renderThreadTaskType)) {
                // すでにこのフォーマットでSetup完了している。
                return true;
            }

            m_deviceSetupInfo.Set(
                startWavData.SampleRate,
                preferedBitsPerSample,
                latencyMillisec,
                m_preference.wasapiDataFeedMode,
                m_preference.wasapiSharedOrExclusive,
                m_preference.renderThreadTaskType);

            wasapi.SetShareMode(
                PreferenceShareModeToWasapiCSShareMode(m_preference.wasapiSharedOrExclusive));
            AddLogText(string.Format("wasapi.SetShareMode({0})\r\n",
                m_preference.wasapiSharedOrExclusive));

            wasapi.SetSchedulerTaskType(
                PreferenceSchedulerTaskTypeToWasapiCSSchedulerTaskType(m_preference.renderThreadTaskType));
            AddLogText(string.Format("wasapi.SetSchedulerTaskType({0})\r\n",
                m_preference.renderThreadTaskType));

            int hr = wasapi.Setup(
                PreferenceDataFeedModeToWasapiCS(m_preference.wasapiDataFeedMode),
                startWavData.SampleRate, preferedBitsPerSample, latencyMillisec);
            AddLogText(string.Format("wasapi.Setup({0}, {1}, {2}, {3}) {4:X8}\r\n",
                startWavData.SampleRate, preferedBitsPerSample,
                latencyMillisec, m_preference.wasapiDataFeedMode, hr));
            if (hr < 0) {
                UnsetupDevice();

                string s = string.Format("エラー: wasapi.Setup({0}, {1}, {2}, {3})失敗。{4:X8}\nこのプログラムのバグか、オーディオデバイスが{0}Hz {1}bit レイテンシー{2}ms {3} {5}に対応していないのか、どちらかです。\r\n",
                    startWavData.SampleRate, preferedBitsPerSample,
                    latencyMillisec, DfmToStr(m_preference.wasapiDataFeedMode), hr,
                    ShareModeToStr(m_preference.wasapiSharedOrExclusive));
                AddLogText(s);
                MessageBox.Show(s);
                return false;
            }

            ChangeState(State.デバイスSetup完了);
            UpdateUIStatus();
            return true;
        }

        void MainWindow_Closed(object sender, EventArgs e) {
            Exit();
        }

        private void MenuItemFileExit_Click(object sender, RoutedEventArgs e) {
            Exit();
        }

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        enum PlayListClearMode {
            // プレイリストをクリアーし、UI状態も更新する。(通常はこちらを使用。)
            ClearWithUpdateUI,

            // ワーカースレッドから呼ぶためUIを操作しない。UIは内部状態とは矛盾した状態になるため
            // この後UIスレッドであらためてClearPlayList(ClearWithUpdateUI)する必要あり。
            ClearWithoutUpdateUI,
        }

        private void ClearPlayList(PlayListClearMode mode) {
            m_wavDataList.Clear();
            m_playListItems.Clear();
            wasapi.ClearPlayList();

            m_readGroupId = 0;
            m_loadedGroupId = -1;

            GC.Collect();

            ChangeState(State.初期化完了);

            if (mode == PlayListClearMode.ClearWithUpdateUI) {
                listBoxPlayFiles.Items.Clear();
                progressBar1.Value = 0;
                UpdateUIStatus();
            }
        }

        private bool LoadWaveFileFromPath(string path)
        {
            WavData wavData = new WavData();

            bool readSuccess = false;
            using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open))) {
                readSuccess = wavData.ReadHeader(br);
            }
            if (readSuccess) {
                if (wavData.NumChannels != 2) {
                    string s = string.Format("2チャンネルステレオ以外のWAVファイルの再生には対応していません: {0} {1}ch\r\n",
                        path, wavData.NumChannels);
                    MessageBox.Show(s);
                    AddLogText(s);
                    return false;
                }
                if (wavData.BitsPerSample != 16
                 && wavData.BitsPerSample != 24) {
                    string s = string.Format("量子化ビット数が16でも24でもないWAVファイルの再生には対応していません: {0} {1}bit\r\n",
                        path, wavData.BitsPerSample);
                    MessageBox.Show(s);
                    AddLogText(s);
                    return false;
                }

                if (0 < m_wavDataList.Count
                    && !m_wavDataList[m_wavDataList.Count-1].IsSameFormat(wavData)) {
                    // データフォーマットが変わった。
                    listBoxPlayFiles.Items.Add(
                        string.Format("----------{0}Hz {1}bitに変更------------", wavData.SampleRate, wavData.BitsPerSample));
                    m_playListItems.Add(new PlayListItemInfo(PlayListItemInfo.ItemType.Separator, null));
                    ++m_readGroupId;
                }

                wavData.FullPath = path;
                wavData.FileName = System.IO.Path.GetFileName(path);
                wavData.Id = m_wavDataList.Count();
                wavData.GroupId = m_readGroupId;

                m_wavDataList.Add(wavData);
                listBoxPlayFiles.Items.Add(wavData.FileName);
                m_playListItems.Add(new PlayListItemInfo(
                    PlayListItemInfo.ItemType.Item,
                    wavData));

                // 状態の更新。再生リストにファイル有り。
                ChangeState(State.プレイリストあり);
            } else {
                string s = string.Format("読み込み失敗: {0}\r\n", path);
                AddLogText(s);
                MessageBox.Show(s);
                return false;
            }
            return true;
        }

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

            for (int i = 0; i < paths.Length; ++i) {
                LoadWaveFileFromPath(paths[i]);
            }
            UpdateUIStatus();
        }

        private void MenuItemFileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (State.デバイスSetup完了 <= m_state) {
                // 追加不可。
                MessageBox.Show("都合により、出力デバイス選択後は再生リストに追加できない作りになっております。いったん再生を停止してから追加していただけますようお願い致します。");
                return;
            }

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "WAVEファイル (.wav)|*.wav";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                LoadWaveFileFromPath(dlg.FileName);
                UpdateUIStatus();
            }
        }
        
        private void MenuItemHelpAbout_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(
                string.Format("PlayPcmWin バージョン {0}",
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
                return "イベント駆動モード";
            case WasapiDataFeedMode.TimerDriven:
                return "タイマー駆動モード";
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

        /// <summary>
        ///  バックグラウンド読み込み。
        ///  m_readFileWorker.RunWorkerAsync(読み込むgroupId)で開始する。
        /// </summary>
        private void ReadFileDoWork(object o, DoWorkEventArgs args) {
            BackgroundWorker bw = (BackgroundWorker)o;
            int readGroupId = (int)args.Argument;
            Console.WriteLine("D: ReadFileDoWork({0}) started", readGroupId);

            ReadFileRunWorkerCompletedArgs r = new ReadFileRunWorkerCompletedArgs();
            try {
                r.hr = -1;

                wasapi.ClearPlayList();
                for (int i = 0; i < m_wavDataList.Count; ++i) {
                    WavData wd = m_wavDataList[i];
                    if (wd.GroupId != readGroupId) {
                        continue;
                    }

                    if (bw.CancellationPending) {
                        Console.WriteLine("D: ReadFileDoWork() Cancelled");
                        args.Cancel = true;
                        return;
                    }

                    // どーなのよ、という感じがするが。
                    // 効果絶大である。
                    GC.Collect();

                    bool readSuccess = false;
                    using (BinaryReader br = new BinaryReader(File.Open(wd.FullPath, FileMode.Open))) {
                        readSuccess = wd.ReadRaw(br);
                    }
                    if (!readSuccess) {
                        r.message = string.Format("エラー。再生リスト追加時には存在していたファイルが、今見たら消えていました。{0}", wd.FullPath);
                        args.Result = r;
                        Console.WriteLine("D: ReadFileDoWork() !readSuccess");
                        return;
                    }

                    // 必要に応じて量子化ビット数の変更を行う。
                    wd = BitsPerSampleConvAsNeeded(wd);

                    int wavDataLength = wd.SampleRawGet().Length;

                    if (!wasapi.AddPlayPcmData(wd.Id, wd.SampleRawGet())) {
                        ClearPlayList(PlayListClearMode.ClearWithoutUpdateUI); //< メモリを空ける：効果があるか怪しいが
                        r.message = string.Format("メモリ不足です。再生リストのファイル数を減らすか、PCのメモリを増設して下さい。");
                        args.Result = r;
                        Console.WriteLine("D: ReadFileDoWork() lowmemory");
                        return;
                    }
                    wd.ForgetDataPart();

                    m_readFileWorker.ReportProgress(100 * (i + 1) / m_wavDataList.Count,
                        string.Format("wasapi.AddOutputData({0}, {0}bytes)\r\n", wd.Id, wavDataLength));
                }

                // ダメ押し。
                GC.Collect();

                // 成功。
                r.message = string.Format("再生グループ{0}番読み込み完了。\r\n", readGroupId);
                r.hr = 0;
                args.Result = r;

                m_loadedGroupId = readGroupId;

                Console.WriteLine("D: ReadFileDoWork({0}) done", readGroupId);
            } catch (Exception ex) {
                r.message = ex.ToString();
                args.Result = r;
                Console.WriteLine("D: ReadFileDoWork() {0}", ex.ToString());
            }
        }

        /// <summary>
        /// 量子化ビット数を、もし必要なら変更する。
        /// </summary>
        /// <param name="wd">入力WavData</param>
        /// <returns>変更後WavData</returns>
        private WavData BitsPerSampleConvAsNeeded(WavData wd) {
            if (wd.BitsPerSample == 16
                && m_preference.bitsPerSampleFixType == BitsPerSampleFixType.Sint32) {
                // 16→24に変換する。
                System.Console.WriteLine("Converting 16bit to 24bit...");
                wd = wd.BitsPerSampleConvertTo(24);
            }
            if (wd.BitsPerSample == 24
                && m_preference.bitsPerSampleFixType == BitsPerSampleFixType.Sint16) {
                // 24→16に変換する。
                // この場合は、切り捨てによって情報が失われる。
                System.Console.WriteLine("Converting 24bit to 16bit...");
                wd = wd.BitsPerSampleConvertTo(16);
            }

            return wd;
        }

        private void ReadFileWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
            AddLogText((string)e.UserState);
            progressBar1.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// WasapiCSに、リピート設定できるかどうかの判定。
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
                ReadStartPlayByWavDataId(m_task.WavDataId);
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
            if (0 < listBoxPlayFiles.SelectedIndex) {
                WavData w = m_playListItems[listBoxPlayFiles.SelectedIndex].WavData;
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

            m_readFileWorker.RunWorkerAsync(loadGroupId);
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e) {
            if (!UseDevice(listBoxDevices.SelectedIndex, (string)listBoxDevices.SelectedItem)) {
                return;
            }

            int wavDataId = 0;
            if (0 < listBoxPlayFiles.SelectedIndex) {
                WavData wavData = m_playListItems[listBoxPlayFiles.SelectedIndex].WavData;
                if (null != wavData) {
                    wavDataId = wavData.Id;
                }
            }

            ReadStartPlayByWavDataId(wavDataId);
        }

        /// <summary>
        /// wavDataIdのGroupがロードされていたら直ちに再生開始する。
        /// 読み込まれていない場合、直ちに再生を開始できないので、ロードしてから再生する。
        /// </summary>
        private bool ReadStartPlayByWavDataId(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);

            WavData wavData = m_wavDataList[wavDataId];

            if (wavData.GroupId != m_loadedGroupId) {
                // m_LoadedGroupIdと、wavData.GroupIdが異なる場合。
                // 再生するためには、ロードする必要がある。
                UnsetupDevice();

                if (!SetupDevice(wavData.GroupId)) {
                    return false;
                }

                m_task.Set(TaskType.PlaySpecifiedGroup, wavData.GroupId, wavData.Id);
                StartReadPlayGroupOnTask();
                return true;
            }

            // wavDataIdのグループがm_LoadedGroupIdである。ロードされている。
            // 連続再生フラグの設定と、現在のグループが最後のグループかどうかによって
            // m_LoadedGroupIdの再生が自然に完了したら、行うタスクを決定する。
            UpdateNextTask();

            if (!SetupDevice(wavData.GroupId)) {
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
            listBoxPlayFiles.SelectedIndex = 0;
        }

        /// <summary>
        /// ただちに再生を開始する。
        /// wavDataIdのGroupが、ロードされている必要がある。
        /// </summary>
        /// <returns>false: 再生開始できなかった。</returns>
        private bool StartPlay(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);
            if (m_wavDataList[wavDataId].GroupId != m_loadedGroupId) {
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

            int playingWavDataId = wasapi.GetNowPlayingPcmDataId();
            int maximum = wasapi.GetNowPlayingPcmDataId();

            if (playingWavDataId < 0) {
                textBoxFileName.Text = "";
                label1.Content = string.Format("{0, 0:f1}/{1, 0:f1}", 0, 0);
            } else {
                listBoxPlayFiles.SelectedIndex
                    = GetPlayListIndexOfWaveDataId(playingWavDataId);
                slider1.Value =wasapi.GetPosFrame();
                WavData wavData = m_wavDataList[playingWavDataId];
                textBoxFileName.Text = wavData.FileName;

                slider1.Maximum = wavData.NumSamples;

                label1.Content = string.Format("{0, 0:f1}/{1, 0:f1}",
                    slider1.Value / wavData.SampleRate, wavData.NumSamples / wavData.SampleRate);
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

            // 再生終了後に行うタスクがない。停止する。先頭の曲を選択状態にする。
            // 再生状態→ファイル読み込み完了状態。
            listBoxPlayFiles.SelectedIndex = 0;
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

        private void buttonSettings_Click(object sender, RoutedEventArgs e) {
            SettingsWindow sw = new SettingsWindow();
            sw.SetPreference(m_preference);
            sw.ShowDialog();
        }

        private void radioButtonTaskAudio_Checked(object sender, RoutedEventArgs e) {
            m_preference.renderThreadTaskType = RenderThreadTaskType.Audio;
        }

        private void radioButtonTaskProAudio_Checked(object sender, RoutedEventArgs e) {
            m_preference.renderThreadTaskType = RenderThreadTaskType.ProAudio;
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

        private void buttonClearPlayList_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
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
            if (m_wavDataList.Count <= wavDataId) {
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

        private void listBoxPlayFiles_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            // ところで、この再生リストに対するキーボード操作が全く効かないのは、
            // 何とかしたいところだ。

            if (m_state != State.再生中 || !m_playListMouseDown ||
                listBoxPlayFiles.SelectedIndex < 0) {
                return;
            }
            
            int playingId = wasapi.GetNowPlayingPcmDataId();
            if (playingId < 0) {
                return;
            }

            PlayListItemInfo pli = m_playListItems[listBoxPlayFiles.SelectedIndex];

            // 再生中で、しかも、マウス押下中にこのイベントが来た場合で、
            // しかも、この曲を再生していない場合、この曲を再生する。
            if (null != pli.WavData &&
                playingId != pli.WavData.Id) {
                ChangePlayWavDataById(pli.WavData.Id);
            }
        }

        /// <summary>
        /// 再生中に、再生曲をwavDataIdの曲に切り替える。
        /// wavDataIdの曲がロードされていたら、直ちに再生曲切り替え。
        /// ロードされていなければ、グループをロードしてから再生。
        /// 
        /// 再生中に呼ぶ。再生中でない場合は何も起きない。
        /// </summary>
        /// <param name="wavDataId">再生曲</param>
        private void ChangePlayWavDataById(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);

            int playingId = wasapi.GetNowPlayingPcmDataId();
            if (playingId < 0) {
                return;
            }

            int groupId = m_wavDataList[wavDataId].GroupId;
            if (m_wavDataList[playingId].GroupId == groupId) {
                // 再生中で、同一ファイルグループのファイルの場合、すぐにこの曲が再生可能。
                wasapi.SetNowPlayingPcmDataId(wavDataId);
                AddLogText(string.Format("wasapi.SetNowPlayingPcmDataId({0})\r\n",
                    wavDataId));
            } else {
                // ファイルグループが違う場合、再生を停止し、グループを読み直し、再生を再開する。
                Stop(new Task(TaskType.PlaySpecifiedGroup, groupId, wavDataId));
            }
        }

        private void listBoxPlayFiles_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            m_playListMouseDown = true;

        }

        private void listBoxPlayFiles_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            m_playListMouseDown = false;
        }

        private void buttonReadSeparator_Click(object sender, RoutedEventArgs e) {
            if (0 == listBoxPlayFiles.Items.Count) {
                return;
            }

            if (m_wavDataList[m_wavDataList.Count - 1].GroupId != m_readGroupId) {
                // 既にグループ区切り線が入れられている。
                return;
            }

            listBoxPlayFiles.Items.Add("----------ここまで一括読み込み------------");
            m_playListItems.Add(new PlayListItemInfo(PlayListItemInfo.ItemType.Separator, null));
            ++m_readGroupId;
        }

        /// <summary>
        /// ログを追加する。
        /// </summary>
        /// <param name="s">追加するログ。行末に\r\nを入れる必要あり。</param>
        private void AddLogText(string s) {
            System.Console.Write(s);
            textBoxLog.Text += s;
            textBoxLog.ScrollToEnd();
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
    }
}
