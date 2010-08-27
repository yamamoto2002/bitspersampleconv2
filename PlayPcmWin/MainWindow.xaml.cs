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
        const int DEFAULT_OUTPUT_LATENCY_MS   = 200;
        const int PROGRESS_REPORT_INTERVAL_MS = 500;

        private WasapiCS wasapi;

        /// <summary>
        /// WavDataのリスト。
        /// </summary>
        private List<WavData> m_wavDataList = new List<WavData>();

        /// <summary>
        /// プレイリスト項目情報。
        /// </summary>
        private List<PlayListItemInfo> m_playListItems = new List<PlayListItemInfo>();

        private WasapiCS.SchedulerTaskType m_schedulerTaskType = WasapiCS.SchedulerTaskType.ProAudio;
        private WasapiCS.ShareMode m_shareMode = WasapiCS.ShareMode.Exclusive;
        private BackgroundWorker m_playWorker;
        private BackgroundWorker m_readFileWorker;
        private System.Diagnostics.Stopwatch m_sw = new System.Diagnostics.Stopwatch();
        private bool m_playListMouseDown = false;

        // プレイリストにAddしたファイルに振られるGroupId。
        private int m_readGroupId = 0;

        // メモリ上に読み込まれている、または、読み込む予定のGroupId。
        private int m_loadGroupId = -1;

        enum State {
            未初期化,
            初期化完了,
            プレイリストあり,

            // これ以降の状態にいる場合、再生リストに新しいファイルを追加できない。
            デバイス選択完了,
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

        // 再生停止完了後に行うタスク。
        enum TaskAfterStop {
            /// <summary>
            /// 停止する。
            /// </summary>
            None,

            /// <summary>
            /// 次のグループをメモリに読み込み、グループの先頭の項目を再生開始する。
            /// </summary>
            PlayNextGroup,
        }

        TaskAfterStop m_taskAfterStop = TaskAfterStop.None;

        public MainWindow()
        {
            InitializeComponent();

            textBoxLog.Text += string.Format("PlayPcmWin {0} {1}\r\n",
                    AssemblyVersion,
                    IntPtr.Size == 8 ? "64bit" : "32bit");

            int hr = 0;
            wasapi = new WasapiCS();
            hr = wasapi.Init();
            textBoxLog.Text += string.Format("wasapi.Init() {0:X8}\r\n", hr);
            textBoxLatency.Text = string.Format("{0}", DEFAULT_OUTPUT_LATENCY_MS);

            m_readGroupId = 0;

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

            CreateDeviceList();
        }

        private void UpdateUIStatus() {
            switch (m_state) {
            case State.初期化完了:
                buttonDeviceSelect.IsEnabled = false;
                menuItemFileOpen.IsEnabled       = true;
                buttonDeselect.IsEnabled         = false;
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
                buttonDeviceSelect.IsEnabled = true;
                menuItemFileOpen.IsEnabled = true;
                buttonDeselect.IsEnabled         = false;
                buttonPlay.IsEnabled             = false;
                buttonStop.IsEnabled             = false;

                buttonNext.IsEnabled             = false;
                buttonPrev.IsEnabled             = false;
                buttonClearPlayList.IsEnabled    = true;
                buttonReadSeparator.IsEnabled    = true;
                groupBoxWasapiSettings.IsEnabled = true;

                buttonInspectDevice.IsEnabled    = false;
                statusBarText.Content = "再生リストを作り、出力デバイスを選択して下さい。";
                break;
            case State.デバイス選択完了:
                // 一覧のクリアーとデバイスの選択、再生リストの作成関連を押せなくする。
                buttonDeviceSelect.IsEnabled = false;
                menuItemFileOpen.IsEnabled = false;
                buttonDeselect.IsEnabled         = true;
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
                buttonDeviceSelect.IsEnabled = false;
                menuItemFileOpen.IsEnabled = false;
                buttonDeselect.IsEnabled = true;
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
                buttonDeviceSelect.IsEnabled = false;
                menuItemFileOpen.IsEnabled = false;
                buttonDeselect.IsEnabled = false;
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
                buttonDeviceSelect.IsEnabled = false;
                menuItemFileOpen.IsEnabled = false;
                buttonDeselect.IsEnabled = false;
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
                buttonDeviceSelect.IsEnabled = false;
                menuItemFileOpen.IsEnabled = false;
                buttonDeselect.IsEnabled = false;
                buttonPlay.IsEnabled = false;
                buttonStop.IsEnabled = false;

                buttonNext.IsEnabled = false;
                buttonPrev.IsEnabled = false;
                buttonClearPlayList.IsEnabled = false;
                buttonReadSeparator.IsEnabled = false;
                groupBoxWasapiSettings.IsEnabled = false;

                buttonInspectDevice.IsEnabled = false;
                statusBarText.Content = "再生グループ切り替え中";
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private void CreateDeviceList() {
            int hr;

            int selectedIndex = -1;
            if (0 < listBoxDevices.Items.Count) {
                selectedIndex = listBoxDevices.SelectedIndex;
            }

            listBoxDevices.Items.Clear();

            hr = wasapi.DoDeviceEnumeration(WasapiCS.DeviceType.Play);
            textBoxLog.Text += string.Format("wasapi.DoDeviceEnumeration(Play) {0:X8}\r\n", hr);

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                listBoxDevices.Items.Add(wasapi.GetDeviceName(i));
            }

            if (0 < nDevices) {
                if (0 <= selectedIndex && selectedIndex < listBoxDevices.Items.Count) {
                    listBoxDevices.SelectedIndex = selectedIndex;
                } else {
                    listBoxDevices.SelectedIndex = 0;
                }

                if (m_wavDataList.Count != 0) {
                    buttonDeviceSelect.IsEnabled = true;
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

        void Exit() {
            Stop(TaskAfterStop.None);
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

            wasapi.Unsetup();
            wasapi.Term();
            wasapi = null;

            Application.Current.Shutdown(0);
        }

        void Stop(TaskAfterStop taskAfterStop) {
            m_taskAfterStop = taskAfterStop;
            wasapi.Stop();
        }

        void MainWindow_Closed(object sender, EventArgs e) {
            Exit();
        }

        private void MenuItemFileExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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
            m_loadGroupId = -1;

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
                    textBoxLog.Text += s;
                    return false;
                }
                if (wavData.BitsPerSample != 16
                 && wavData.BitsPerSample != 24) {
                    string s = string.Format("量子化ビット数が16でも24でもないWAVファイルの再生には対応していません: {0} {1}bit\r\n",
                        path, wavData.BitsPerSample);
                    MessageBox.Show(s);
                    textBoxLog.Text += s;
                    return false;
                }

                if (0 < m_wavDataList.Count
                    && !m_wavDataList[0].IsSameFormat(wavData)) {
                    string s = string.Format("再生リストの先頭のファイルとデータフォーマットが異なるため追加できませんでした: {0}\r\n", path);
                    MessageBox.Show(s);
                    textBoxLog.Text += s;
                    return false;
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
                textBoxLog.Text += s;
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

            if (State.デバイス選択完了 <= m_state) {
                // 追加不可。
                MessageBox.Show("都合により、出力デバイス選択後は再生リストに追加できない作りになっております。いったん出力デバイス選択解除してから追加していただけますようお願い致します。");
                return;
            }

            for (int i = 0; i < paths.Length; ++i) {
                LoadWaveFileFromPath(paths[i]);
            }
            UpdateUIStatus();
        }

        private void MenuItemFileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (State.デバイス選択完了 <= m_state) {
                // 追加不可。
                MessageBox.Show("都合により、出力デバイス選択後は再生リストに追加できない作りになっております。いったん出力デバイス選択解除してから追加していただけますようお願い致します。");
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

        private static string DfmToStr(WasapiCS.DataFeedMode dfm) {
            switch (dfm) {
            case WasapiCS.DataFeedMode.EventDriven:
                return "イベント駆動モード";
            case WasapiCS.DataFeedMode.TimerDriven:
                return "タイマー駆動モード";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "unknown";
            }
        }

        private static string ShareModeToStr(WasapiCS.ShareMode sm) {
            switch (sm) {
            case WasapiCS.ShareMode.Exclusive:
                return "WASAPI排他モード";
            case WasapiCS.ShareMode.Shared:
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
            BackgroundWorker bw = (BackgroundWorker)o;

            ReadFileRunWorkerCompletedArgs r = new ReadFileRunWorkerCompletedArgs();
            try {
                r.hr = -1;

                wasapi.ClearPlayList();
                for (int i = 0; i < m_wavDataList.Count; ++i) {
                    WavData wd = m_wavDataList[i];
                    if (wd.GroupId != m_loadGroupId) {
                        continue;
                    }

                    if (bw.CancellationPending) {
                        Console.WriteLine("D: ReadFileDoWork() Cancelled");
                        args.Cancel = true;
                        return;
                    }

                    GC.Collect();

                    bool readSuccess = false;
                    using (BinaryReader br = new BinaryReader(File.Open(wd.FullPath, FileMode.Open))) {
                        readSuccess = wd.ReadRaw(br);
                    }
                    if (!readSuccess) {
                        r.message = string.Format("エラー。再生リスト追加時には存在していたファイルが、今見たら消えていました。{0}", wd.FullPath);
                        args.Result = r;
                        return;
                    }

                    int wavDataLength = wd.SampleRawGet().Length;

                    if (!wasapi.AddPlayPcmData(wd.Id, wd.SampleRawGet())) {
                        ClearPlayList(PlayListClearMode.ClearWithoutUpdateUI); //< メモリを空ける：効果があるか怪しいが
                        r.message = string.Format("メモリ不足です。再生リストのファイル数を減らすか、PCのメモリを増設して下さい。");
                        args.Result = r;
                        return;
                    }
                    wd.ForgetDataPart();

                    m_readFileWorker.ReportProgress(100 * (i + 1) / m_wavDataList.Count,
                        string.Format("wasapi.AddOutputData({0})\r\n", wavDataLength));
                }
                GC.Collect();

                // 成功。
                r.message = "全ファイル読み込み完了。\r\n";
                r.hr = 0;
                args.Result = r;

            } catch (Exception ex) {
                r.message = ex.ToString();
                args.Result = r;
            }
        }

        private void ReadFileWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
            textBoxLog.Text += (string)e.UserState;
            progressBar1.Value = e.ProgressPercentage;
        }

        // WasapiCSに、リピート設定できるかどうかの判定。
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

        private void ReadFileRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            ReadFileRunWorkerCompletedArgs r = (ReadFileRunWorkerCompletedArgs)args.Result;
            textBoxLog.Text += r.message;

            if (r.hr < 0) {
                MessageBox.Show(r.message);
                Exit();
                return;
            }

            // WasapiCSに、リピート設定できるかどうかの判定。
            UpdatePlayRepeat();

            if (m_taskAfterStop == TaskAfterStop.PlayNextGroup) {
                // ファイル読み込み完了後、再生を開始する。
                ReadStartPlaySelectedItemOnPlayList();
                return;
            }

            // ファイル読み込み完了後、何もすることはない。
            ChangeState(State.ファイル読み込み完了);
            UpdateUIStatus();
        }

        private void buttonDeviceSelect_Click(object sender, RoutedEventArgs e) {
            int loadGroupId = 0;
            if (0 < listBoxPlayFiles.SelectedIndex) {
                WavData w = m_playListItems[listBoxPlayFiles.SelectedIndex].WavData;
                if (null != w) {
                    loadGroupId = w.GroupId;
                }
            }

            int hr = wasapi.ChooseDevice(listBoxDevices.SelectedIndex);
            textBoxLog.Text += string.Format("wasapi.ChooseDevice() {0:X8}\r\n", hr);
            if (hr < 0) {
                return;
            }

            SetupAndStartReadFiles(loadGroupId);
        }

        /// <summary>
        /// デバイスをセットアップし、loadGroupのファイル読み込みを開始する。
        /// </summary>
        private void SetupAndStartReadFiles(int loadGroupId) {
            // デバイス選択。 ////////////////////////////////////////////////

            int latencyMillisec = Int32.Parse(textBoxLatency.Text);
            if (latencyMillisec <= 0) {
                latencyMillisec = DEFAULT_OUTPUT_LATENCY_MS;
                textBoxLatency.Text = string.Format("{0}", DEFAULT_OUTPUT_LATENCY_MS);
            }

            wasapi.SetShareMode(m_shareMode);
            textBoxLog.Text += string.Format("wasapi.SetShareMode({0})\r\n", m_shareMode);

            wasapi.SetSchedulerTaskType(m_schedulerTaskType);
            textBoxLog.Text += string.Format("wasapi.SetSchedulerTaskType({0})\r\n", m_schedulerTaskType);

            WasapiCS.DataFeedMode dfm;
            dfm = WasapiCS.DataFeedMode.EventDriven;
            if (true == radioButtonTimerDriven.IsChecked) {
                dfm = WasapiCS.DataFeedMode.TimerDriven;
            }

            int startWavDataId = GetFirstWavDataIdOnGroup(loadGroupId);
            System.Diagnostics.Debug.Assert(0 <= startWavDataId);

            WavData startWavData = m_wavDataList[startWavDataId];

            int hr = wasapi.Setup(dfm, startWavData.SampleRate, startWavData.BitsPerSample, latencyMillisec);
            textBoxLog.Text += string.Format("wasapi.Setup({0}, {1}, {2}, {3}) {4:X8}\r\n",
                startWavData.SampleRate, startWavData.BitsPerSample, latencyMillisec, dfm, hr);
            if (hr < 0) {
                wasapi.Unsetup();
                textBoxLog.Text += "wasapi.Unsetup()\r\n";

                CreateDeviceList();
                string s = string.Format("エラー: wasapi.Setup({0}, {1}, {2}, {3})失敗。{4:X8}\nこのプログラムのバグか、オーディオデバイスが{0}Hz {1}bit レイテンシー{2}ms {3} {5}に対応していないのか、どちらかです。\r\n",
                    startWavData.SampleRate, startWavData.BitsPerSample,
                    latencyMillisec, DfmToStr(dfm), hr,
                    ShareModeToStr(m_shareMode));
                textBoxLog.Text += s;
                MessageBox.Show(s);
                return;
            }

            ChangeState(State.デバイス選択完了);
            UpdateUIStatus();

            // ファイル読み込み開始 ////////////////////////////////////////////////

            m_loadGroupId = loadGroupId;
            progressBar1.Visibility = System.Windows.Visibility.Visible;
            progressBar1.Value = 0;

            m_readFileWorker.RunWorkerAsync();
        }

        private void buttonDeviceDeselect_Click(object sender, RoutedEventArgs e) {
            Stop(TaskAfterStop.None);
            wasapi.Unsetup();
            CreateDeviceList();
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e) {
            ReadStartPlaySelectedItemOnPlayList();
        }

        /// <summary>
        /// リストボックスの選択項目のファイルがロードされていたら直ちに再生開始する。
        /// リストボックスの選択項目のWavDataが読み込まれていない場合
        /// 直ちに再生を開始できないので、ロードしてから再生する。
        /// </summary>
        private void ReadStartPlaySelectedItemOnPlayList() {
            int wavDataId = 0;

            if (0 < listBoxPlayFiles.SelectedIndex) {
                WavData wavData = m_playListItems[listBoxPlayFiles.SelectedIndex].WavData;
                if (null != wavData) {
                    wavDataId = wavData.Id;

                    if (wavData.GroupId != m_loadGroupId) {
                        // m_LoadedGroupIdと、wavData.GroupIdが異なる場合。
                        // 再生するためには、ロードする必要がある。
                        wasapi.Unsetup();
                        SetupAndStartReadPlayGroup(wavData.GroupId, wavDataId);
                        return;
                    }
                }
            }

            // wavDataIdのグループがロードされている。
            // m_LoadedGroupIdの再生が自然に完了したら、
            // m_loadGroupId+1のグループを読み込んで再生する。
            m_taskAfterStop = TaskAfterStop.PlayNextGroup;

            StartPlay(wavDataId);
            return;
        }

        /// <summary>
        /// ただちに再生を開始する。
        /// wavDataIdのGroupが、ロードされている必要がある。
        /// </summary>
        /// <returns>false: 再生開始できなかった。</returns>
        private bool StartPlay(int wavDataId) {
            System.Diagnostics.Debug.Assert(0 <= wavDataId);
            if (m_wavDataList[wavDataId].GroupId != m_loadGroupId) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            //wasapi.SetPosFrame(0);
            int hr = wasapi.Start();
            textBoxLog.Text += string.Format("wasapi.Start() {0:X8}\r\n", hr);
            if (hr < 0) {
                return false;
            }

            wasapi.SetNowPlayingPcmDataId(wavDataId);

            slider1.Value = 0;
            slider1.Maximum = wasapi.GetTotalFrameNum();

            ChangeState(State.再生中);
            UpdateUIStatus();

            m_playWorker.RunWorkerAsync();

            m_sw.Reset();
            m_sw.Start();

            return true;
        }

        /// <summary>
        /// 再生中。バックグラウンドスレッド。
        /// </summary>
        private void PlayDoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("PlayDoWork started");

            do {
                m_playWorker.ReportProgress(0);
                System.Threading.Thread.Sleep(1);
            } while (!wasapi.Run(PROGRESS_REPORT_INTERVAL_MS));

            Console.WriteLine("PlayDoWork() wasapi.Stop() " + m_taskAfterStop);

            wasapi.Stop();

            // 停止完了後タスクの処理は、ここではなく、PlayRunWorkerCompletedで行う。

            //Console.WriteLine("PlayDoWork end");
        }

        /// <summary>
        /// 再生の進行状況をUIに反映する。
        /// </summary>
        private void PlayProgressChanged(object o, ProgressChangedEventArgs args) {
            if (null == wasapi) {
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
        /// 指定グループをロードし、ロード完了したら指定ファイルを再生開始する。
        /// ファイル読み込み完了状態にいるときに呼ぶ。
        /// </summary>
        /// <param name="groupId">ロードするグループ</param>
        /// <param name="playWavDataId">ロード後に再生するファイルId</param>
        private void SetupAndStartReadPlayGroup(int groupId, int playWavDataId) {
            // ロード完了後再生開始する必要があることはm_taskAfterStopで伝える。
            // ロードするGroupIdはm_loadGroupIdで伝える。
            // ロード後に再生するファイルはlistBoxPlayFiles.SelectedIndexで伝える。

            m_taskAfterStop = TaskAfterStop.PlayNextGroup;

            m_loadGroupId = -1;

            int idx = GetPlayListIndexOfWaveDataId(playWavDataId);
            System.Diagnostics.Debug.Assert(0 <= idx);
            listBoxPlayFiles.SelectedIndex = idx;

            // 再生状態→再生グループ切り替え中状態に遷移。
            ChangeState(State.再生グループ切り替え中);
            UpdateUIStatus();

            SetupAndStartReadFiles(groupId);
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            m_sw.Stop();
            textBoxLog.Text += string.Format("再生終了. 所要時間 {0}\r\n", m_sw.Elapsed);

            // m_loadGroupIdグループの再生終了。
            // 再生終了後に行うタスクがある場合、ここで実行する。
            // 一見意味不明であり、いまいちな出来のコードである。

            if (m_taskAfterStop == TaskAfterStop.None) {
                // 再生終了後に行うタスクがない。停止する。先頭の曲を選択状態にする。
                listBoxPlayFiles.SelectedIndex = 0;
            }

            if (m_taskAfterStop == TaskAfterStop.PlayNextGroup) {
                // 次に再生するグループ番号を(m_loadGroupId+1)と仮定する。
                // ①(m_loadGroupId+1)の再生グループが存在する場合
                //     (m_loadGroupId+1)の再生グループを再生開始する。
                // ②(m_loadGroupId+1)の再生グループが存在しない場合
                //     ②-①連続再生(checkBoxContinuous.IsChecked==true)の場合
                //         GroupId==0の再生グループを再生開始する。
                //     ②-②連続再生ではない場合
                //         停止する。先頭の曲を選択状態にする。

                if (0 < CountWaveDataOnPlayGroup(m_loadGroupId + 1)) {
                    wasapi.Unsetup();
                    SetupAndStartReadPlayGroup(
                        m_loadGroupId + 1,
                        GetFirstWavDataIdOnGroup(m_loadGroupId + 1));
                    return;
                }

                if (checkBoxContinuous.IsChecked == true) {
                    wasapi.Unsetup();
                    SetupAndStartReadPlayGroup(0, 0);
                    return;
                }

                m_taskAfterStop = TaskAfterStop.None;
                listBoxPlayFiles.SelectedIndex = 0;
            }

            // 再生終了後に行うタスクがない。
            // 再生状態→ファイル読み込み完了状態。
            ChangeState(State.ファイル読み込み完了);
            UpdateUIStatus();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            ChangeState(State.再生停止開始);
            UpdateUIStatus();

            // 停止ボタンで停止した場合は、停止後何もしない。
            Stop(TaskAfterStop.None);
            textBoxLog.Text += string.Format("wasapi.Stop()\r\n");
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
            textBoxLog.Text += string.Format("wasapi.InspectDevice()\r\n{0}\r\n{1}\r\n", dn, s);
        }

        private void radioButtonTaskAudio_Checked(object sender, RoutedEventArgs e) {
            m_schedulerTaskType = WasapiCS.SchedulerTaskType.Audio;
        }

        private void radioButtonTaskProAudio_Checked(object sender, RoutedEventArgs e) {
            m_schedulerTaskType = WasapiCS.SchedulerTaskType.ProAudio;
        }

        private void radioButtonTaskNone_Checked(object sender, RoutedEventArgs e) {
            m_schedulerTaskType = WasapiCS.SchedulerTaskType.None;
        }

        private void radioButtonExclusive_Checked(object sender, RoutedEventArgs e) {
            m_shareMode = WasapiCS.ShareMode.Exclusive;
        }

        private void radioButtonShared_Checked(object sender, RoutedEventArgs e) {
            m_shareMode = WasapiCS.ShareMode.Shared;
        }

        private void buttonClearPlayList_Click(object sender, RoutedEventArgs e) {
            ClearPlayList(PlayListClearMode.ClearWithUpdateUI);
        }

        private void buttonPrev_Click(object sender, RoutedEventArgs e) {
            int playingId = wasapi.GetNowPlayingPcmDataId();
            --playingId;
            if (playingId < 0) {
                playingId = 0;
            }
            wasapi.SetNowPlayingPcmDataId(playingId);
        }

        private void buttonNext_Click(object sender, RoutedEventArgs e) {
            int playingId = wasapi.GetNowPlayingPcmDataId();
            ++playingId;
            if (playingId < 0) {
                playingId = 0;
            }
            if (m_wavDataList.Count <= playingId) {
                playingId = 0;
            }
            wasapi.SetNowPlayingPcmDataId(playingId);
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

            if (!buttonStop.IsEnabled || !m_playListMouseDown ||
                listBoxPlayFiles.SelectedIndex < 0) {
                return;
            }
            
            int playingId = wasapi.GetNowPlayingPcmDataId();

            PlayListItemInfo pli = m_playListItems[listBoxPlayFiles.SelectedIndex];

            // 再生中で、しかも、マウス押下中にこのイベントが来た場合で、
            // しかも、この曲を再生していない場合、この曲を再生する。
            if (null != pli.WavData &&
                playingId != pli.WavData.Id) {
                // @todo ファイルグループも違う場合、再生を停止し、グループを読み直し、再生を再開する。
                // 同一ファイルグループのファイルの場合、再生WavDataIdを指定するだけで良い。

                wasapi.SetNowPlayingPcmDataId(listBoxPlayFiles.SelectedIndex);
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

    }
}
