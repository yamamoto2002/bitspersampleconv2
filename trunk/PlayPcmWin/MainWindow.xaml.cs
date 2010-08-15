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
    public partial class MainWindow : Window
    {
        const int DEFAULT_OUTPUT_LATENCY_MS   = 200;
        const int PROGRESS_REPORT_INTERVAL_MS = 500;

        private WasapiCS wasapi;

        private List<WavData> m_wavDataList = new List<WavData>();

        private WasapiCS.SchedulerTaskType m_schedulerTaskType = WasapiCS.SchedulerTaskType.ProAudio;
        private WasapiCS.ShareMode m_shareMode = WasapiCS.ShareMode.Exclusive;
        private BackgroundWorker m_playWorker;
        private System.Diagnostics.Stopwatch m_sw = new System.Diagnostics.Stopwatch();


        public MainWindow()
        {
            InitializeComponent();

            int hr = 0;
            wasapi = new WasapiCS();
            hr = wasapi.Init();
            textBoxLog.Text += string.Format("wasapi.Init() {0:X8}\r\n", hr);
            textBoxLatency.Text = string.Format("{0}", DEFAULT_OUTPUT_LATENCY_MS);

            Closed += new EventHandler(MainWindow_Closed);

            CreateDeviceList();
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

            buttonDeviceSelect.IsEnabled     = false;
            buttonDeselect.IsEnabled         = false;
            buttonPlay.IsEnabled             = false;
            buttonStop.IsEnabled             = false;
            buttonNext.IsEnabled             = false;
            buttonPrev.IsEnabled             = false;
            buttonClearPlayList.IsEnabled    = true;
            menuItemFileOpen.IsEnabled       = true;
            groupBoxWasapiSettings.IsEnabled = true;
            buttonInspectDevice.IsEnabled    = false;

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
        }

        void MainWindow_Closed(object sender, EventArgs e) {
            wasapi.Stop();
            wasapi.Unsetup();
            wasapi.Term();
            wasapi = null;

            Application.Current.Shutdown(0);
        }

        private void MenuItemFileExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ClearPlayList() {
            m_wavDataList.Clear();
            listBoxPlayFiles.Items.Clear();
            wasapi.ClearPlayList();

            buttonDeviceSelect.IsEnabled = false;
            menuItemFileOpen.IsEnabled = true;
        }

        private bool LoadWaveFileFromPath(string path)
        {
            WavData wavData = new WavData();

            bool readSuccess = false;
            using (BinaryReader br = new BinaryReader(File.Open(path, FileMode.Open))) {
                readSuccess = wavData.ReadRaw(br);
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

                wavData.Path = System.IO.Path.GetFileName(path);
                wavData.Id = m_wavDataList.Count();
                m_wavDataList.Add(wavData);
                listBoxPlayFiles.Items.Add(wavData.Path);

                // メニュー状態の更新。デバイス選択を押せるようにする。
                buttonDeviceSelect.IsEnabled = true;
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

            if (buttonStop.IsEnabled) {
                // 再生中は追加不可。
                MessageBox.Show("再生中なのでプレイリストに追加できませんでした。");
                return;
            }

            for (int i = 0; i < paths.Length; ++i) {
                LoadWaveFileFromPath(paths[i]);
            }
        }

        private void MenuItemFileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (buttonStop.IsEnabled) {
                // 再生中は追加不可。
                MessageBox.Show("再生中なのでプレイリストに追加できませんでした。");
                return;
            }

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "WAVEファイル (.wav)|*.wav";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                LoadWaveFileFromPath(dlg.FileName);
            }
        }
        
        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private void MenuItemHelpAbout_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show(
                string.Format("PlayPcmWin バージョン {0}",
                    AssemblyVersion));
        }

        private void MenuItemHelpWeb_Click(object sender, RoutedEventArgs e) {
            try {
                System.Diagnostics.Process.Start("http://code.google.com/p/bitspersampleconv2/wiki/WasapiExclusiveMode");
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

        private void buttonDeviceSelect_Click(object sender, RoutedEventArgs e) {
            int latencyMillisec = Int32.Parse(textBoxLatency.Text);
            if (latencyMillisec <= 0) {
                latencyMillisec = DEFAULT_OUTPUT_LATENCY_MS;
                textBoxLatency.Text = string.Format("{0}", DEFAULT_OUTPUT_LATENCY_MS);
            }

            wasapi.SetShareMode(m_shareMode);
            textBoxLog.Text += string.Format("wasapi.SetShareMode({0})\r\n", m_shareMode);

            wasapi.SetSchedulerTaskType(m_schedulerTaskType);
            textBoxLog.Text += string.Format("wasapi.SetSchedulerTaskType({0})\r\n", m_schedulerTaskType);

            int hr = wasapi.ChooseDevice(listBoxDevices.SelectedIndex);
            textBoxLog.Text += string.Format("wasapi.ChooseDevice() {0:X8}\r\n", hr);
            if (hr < 0) {
                return;
            }

            WasapiCS.DataFeedMode dfm = WasapiCS.DataFeedMode.EventDriven;
            if (true == radioButtonTimerDriven.IsChecked) {
                dfm = WasapiCS.DataFeedMode.TimerDriven;
            }

            WavData wavData0 = m_wavDataList[0];

            hr = wasapi.Setup(dfm, wavData0.SampleRate, wavData0.BitsPerSample, latencyMillisec);
            textBoxLog.Text += string.Format("wasapi.Setup({0}, {1}, {2}, {3}) {4:X8}\r\n",
                wavData0.SampleRate, wavData0.BitsPerSample, latencyMillisec, dfm, hr);
            if (hr < 0) {
                wasapi.Unsetup();
                textBoxLog.Text += string.Format("wasapi.Unsetup()\r\n");
                CreateDeviceList();
                string s = string.Format("エラー: wasapi.Setup({0}, {1}, {2}, {3})失敗。{4:X8}\nこのプログラムのバグか、オーディオデバイスが{0}Hz {1}bit レイテンシー{2}ms {3} {5}に対応していないのか、どちらかです。\r\n",
                    wavData0.SampleRate, wavData0.BitsPerSample,
                    latencyMillisec, DfmToStr(dfm), hr,
                    ShareModeToStr(m_shareMode));
                textBoxLog.Text += s;
                MessageBox.Show(s);
                return;
            }

            wasapi.ClearPlayList();
            for (int i = 0; i < m_wavDataList.Count; ++i) {
                WavData wd = m_wavDataList[i];
                wasapi.AddPlayPcmData(wd.Id, wd.SampleRawGet());
                textBoxLog.Text += string.Format("wasapi.AddOutputData({0})\r\n", wd.SampleRawGet().Length);
            }
            wasapi.SetPlayRepeat(checkBoxContinuous.IsChecked == true);

            menuItemFileOpen.IsEnabled       = false;
            buttonDeviceSelect.IsEnabled     = false;
            buttonDeselect.IsEnabled         = true;
            buttonPlay.IsEnabled             = true;
            buttonClearPlayList.IsEnabled    = false;
            buttonInspectDevice.IsEnabled    = false;
            groupBoxWasapiSettings.IsEnabled = false;
        }

        private void buttonDeviceDeselect_Click(object sender, RoutedEventArgs e) {
            wasapi.Stop();
            wasapi.Unsetup();
            CreateDeviceList();
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e) {
            int hr = wasapi.Start();
            textBoxLog.Text += string.Format("wasapi.Start() {0:X8}\r\n", hr);
            if (hr < 0) {
                return;
            }

            //wasapi.SetPosFrame(0);
            slider1.Value = 0;
            slider1.Maximum = wasapi.GetTotalFrameNum();
            buttonStop.IsEnabled = true;
            buttonNext.IsEnabled = true;
            buttonPrev.IsEnabled = true;
            buttonPlay.IsEnabled = false;
            buttonDeselect.IsEnabled = false;

            m_playWorker = new BackgroundWorker();
            m_playWorker.WorkerReportsProgress = true;
            m_playWorker.DoWork += new DoWorkEventHandler(DoWork);
            m_playWorker.ProgressChanged += new ProgressChangedEventHandler(ProgressChanged);
            m_playWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RunWorkerCompleted);
            m_playWorker.RunWorkerAsync();

            m_sw.Reset();
            m_sw.Start();
        }

        private void ProgressChanged(object o, ProgressChangedEventArgs args) {
            if (null == wasapi) {
                return;
            }

            int playingId = wasapi.GetNowPlayingPcmDataId();
            int maximum = wasapi.GetNowPlayingPcmDataId();

            if (playingId < 0) {
                labelFileName.Content = "";
                label1.Content = string.Format("{0, 0:f1}/{1, 0:f1}", 0, 0);
            } else {
                listBoxPlayFiles.SelectedIndex = playingId;
                slider1.Value =wasapi.GetPosFrame();
                WavData wavData = m_wavDataList[playingId];
                labelFileName.Content = wavData.Path;

                slider1.Maximum = wavData.NumSamples;

                label1.Content = string.Format("{0, 0:f1}/{1, 0:f1}",
                    slider1.Value / wavData.SampleRate, wavData.NumSamples / wavData.SampleRate);
            }
        }
        
        private void RunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            buttonPlay.IsEnabled = true;
            buttonStop.IsEnabled = false;
            buttonNext.IsEnabled = false;
            buttonPrev.IsEnabled = false;
            buttonDeselect.IsEnabled = true;

            slider1.Value = 0;
            label1.Content = "0/0";

            m_sw.Stop();

            textBoxLog.Text += string.Format("再生終了. 所要時間 {0}\r\n", m_sw.Elapsed);
        }

        private void DoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("DoWork started");

            do {
                m_playWorker.ReportProgress(0);
                System.Threading.Thread.Sleep(1);
            } while (!wasapi.Run(PROGRESS_REPORT_INTERVAL_MS));

            wasapi.Stop();

            Console.WriteLine("DoWork end");
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            buttonStop.IsEnabled = false;
            buttonNext.IsEnabled = false;
            buttonPrev.IsEnabled = false;

            wasapi.Stop();
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

        private void radioButtonTaskAudio_Checked(object sender, RoutedEventArgs e)
        {
            m_schedulerTaskType = WasapiCS.SchedulerTaskType.Audio;
        }

        private void radioButtonTaskProAudio_Checked(object sender, RoutedEventArgs e)
        {
            m_schedulerTaskType = WasapiCS.SchedulerTaskType.ProAudio;
        }

        private void radioButtonExclusive_Checked(object sender, RoutedEventArgs e) {
            m_shareMode = WasapiCS.ShareMode.Exclusive;
        }

        private void radioButtonShared_Checked(object sender, RoutedEventArgs e) {
            m_shareMode = WasapiCS.ShareMode.Shared;
        }

        private void buttonClearPlayList_Click(object sender, RoutedEventArgs e) {
            ClearPlayList();
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


    }
}
