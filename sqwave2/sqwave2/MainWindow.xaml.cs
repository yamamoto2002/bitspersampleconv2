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
using WavRWLib2;
using System.IO;
using System.ComponentModel;
using System.Threading.Tasks;
using Asio;
using System.Reflection;

namespace sqwave2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private AsioCS asio;

        const double ASIO_FREQ_MIN = 10.0;

        private bool m_uiInitialized = false;

        enum OutputMode
        {
            WavFile,
            Asio,
            NUM
        }

        OutputMode m_outputMode = OutputMode.WavFile;

        enum AsioStatus
        {
            NotReady,
            Ready,
        }

        AsioStatus m_asioStatus = AsioStatus.NotReady;

        private bool m_restart = false;

        struct OutputFormat
        {
            public int sampleRate;
            public int bitsPerSample;

            public void Set(int sampleRate, int bitsPerSample) {
                this.sampleRate = sampleRate;
                this.bitsPerSample = bitsPerSample;
            }
        }

        int m_selectedAsioDeviceIdx = -1;

        public MainWindow() {
            InitializeComponent();

            asio = new AsioCS();
            asio.Init();

            backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.WorkerSupportsCancellation = true;
            backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(backgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);
            backgroundWorker1.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker1_ProgressChanged);

            textBoxOutputFilePath.Text = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "\\output.wav";
            m_uiInitialized = true;
            UpdateUIStatus();

            buttonStop.IsEnabled = false;
            buttonStart.IsEnabled = true;
        }

        private void Window_Closed(object sender, EventArgs e) {
            asio.Term();
            asio = null;
        }


        private void UpdateUIStatus() {
            if (!m_uiInitialized) {
                return;
            }
            int driverNum = asio.DriverNumGet();
            if (0 == driverNum) {
                // ASIOデバイスがないのでファイル出力モードしか選べない
                m_outputMode = OutputMode.WavFile;
                radioButtonOutAsio.IsEnabled = false;
            } else {
                radioButtonOutAsio.IsEnabled = true;
            }

            switch (m_outputMode) {
            case OutputMode.WavFile:
                textBoxOutputFilePath.IsEnabled = true;
                listBoxAsioDevices.IsEnabled = false;
                listBoxAsioChannels.IsEnabled = false;
                listBoxAsioClockSource.IsEnabled = false;
                buttonAsioControlPanel.IsEnabled = false;
                buttonRef.IsEnabled = true;
                textBoxSeconds.IsEnabled = true;
                listBoxSampleFreq.IsEnabled = true;
                listBoxBits.IsEnabled = true;
                break;
            case OutputMode.Asio:
                textBoxOutputFilePath.IsEnabled = false;
                listBoxAsioDevices.IsEnabled = true;
                listBoxAsioChannels.IsEnabled = true;
                listBoxAsioClockSource.IsEnabled = true;
                buttonAsioControlPanel.IsEnabled = true;
                buttonRef.IsEnabled = false;
                textBoxSeconds.IsEnabled = false;
                listBoxSampleFreq.IsEnabled = false;
                listBoxBits.IsEnabled = false;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private string SaveDialogAndAskPath() {
            string ret = string.Empty;

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "output";
            dlg.DefaultExt = ".wav";
            dlg.Filter = "WAV files (.wav)|*.wav";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                ret = dlg.FileName;
            }
            return ret;
        }

        private void buttonRef_Click(object sender, RoutedEventArgs e) {
            string path = SaveDialogAndAskPath();

            textBoxOutputFilePath.Text = path;
        }

        struct SignalGeneraterWorkerArg
        {
            public SignalGenerateParams sgParams;
            public string outputPath;
            public OutputMode outputMode;
            public List<int> outputChannels;
        }

        private bool TextBoxFreqToFreq(out double freq) {
            freq = 0;
            try {
                freq = System.Convert.ToDouble(textBoxFreq.Text);
            } catch (System.Exception ex) {
                return false;
            }
            if (freq < 0.0001) {
                return false;
            }
            return true;
        }

        private bool TextBoxDbToDb(out double dB) {
            dB = 0;
            try {
                dB = System.Convert.ToDouble(textBoxLevel.Text);
            } catch (System.Exception ex) {
                return false;
            }
            return true;
        }

        private bool TextBoxSecondsToSeconds(out int seconds) {
            seconds = 1;
            try {
                seconds = System.Convert.ToInt32(textBoxSeconds.Text);
            } catch (System.Exception ex) {
                return false;
            }
            if (seconds <= 0) {
                return false;
            }
            return true;
        }

        private bool TextBoxTruncToTrunc(out double trunc) {
            trunc = 99;
            try {
                trunc = System.Convert.ToDouble(textBoxTrunc.Text);
            } catch (System.Exception ex) {
                return false;
            }
            if (trunc < 0.0) {
                return false;
            }
            return true;
        }

        private void Start() {
            int sampleRate = System.Convert.ToInt32(((ListBoxItem)listBoxSampleFreq.SelectedItem).Content);
            int bitsPerSample = System.Convert.ToInt32(((ListBoxItem)listBoxBits.SelectedItem).Content);

            double dB;
            if (!TextBoxDbToDb(out dB)) {
                MessageBox.Show("エラー: 出力レベルには 数値を半角で入力してください");
                return;
            }

            double freq;
            if (!TextBoxFreqToFreq(out freq)) {
                MessageBox.Show("エラー: 信号周波数には0.0001以上の数値を半角で入力してください");
                return;
            }
            if (freq < ASIO_FREQ_MIN) {
                MessageBox.Show(
                    string.Format("ASIO出力モードでは、信号周波数には{0:0.#}以上の数値を半角で入力してください", ASIO_FREQ_MIN));
                return;
            }

            int seconds = 0;
            if (m_outputMode == OutputMode.WavFile) {
                if (!TextBoxSecondsToSeconds(out seconds)) {
                    MessageBox.Show("エラー: 長さには 0よりも大きい整数を半角で入力してください");
                    return;
                }
            } else {
                {
                    double freq01 = (double)((int)(freq * 10)) * 0.1;

                    double diff = Math.Abs(freq01 - freq);
                    if (0.00001 < diff) {
                        MessageBox.Show(
                            string.Format("ASIO出力モードでは、都合により0.1Hz以下の周波数を切り捨てます。{0}Hzの波形を出力します。", freq01));
                        freq = freq01;
                    }
                }

                seconds = 1;
                if (((int)(freq * 10) % 10) != 0) {
                    seconds = 10;
                }
            }

            SignalShape ss = (SignalShape)listBoxShape.SelectedIndex;

            double trunc = 0;
            if (!TextBoxTruncToTrunc(out trunc)) {
                MessageBox.Show("エラー: 級数加算打ち切り％には0.0以上の数値を半角で入力してください");
                return;
            }

            SignalGeneraterWorkerArg sgwa;
            sgwa.sgParams.seconds = seconds;
            sgwa.sgParams.sampleRate = sampleRate;
            sgwa.sgParams.bitsPerSample = bitsPerSample;
            sgwa.sgParams.dB = dB;
            sgwa.sgParams.freq = freq;
            sgwa.sgParams.ss = ss;
            sgwa.outputPath = textBoxOutputFilePath.Text;
            sgwa.sgParams.truncationRatio = trunc * 0.01;
            sgwa.sgParams.amplitude = (int)(((2 << (sgwa.sgParams.bitsPerSample - 2)) - 1) * Math.Pow(10, sgwa.sgParams.dB / 20.0));
            sgwa.outputMode = m_outputMode;
            sgwa.outputChannels = new List<int>();
            for (int i = 0; i < listBoxAsioChannels.Items.Count; ++i) {
                ListBoxItem item = listBoxAsioChannels.Items.GetItemAt(i) as ListBoxItem;
                if (item.IsSelected) {
                    sgwa.outputChannels.Add(i);
                }
            }

        /*
            if (sampleRate <= freq * 2) {
                MessageBox.Show("エラー: 信号周波数をサンプリング周波数の半分未満にしてください");
                return;
            }
        */

            switch (bitsPerSample) {
            case 16:
                if (dB < -96.0) {
                    MessageBox.Show("エラー: 出力レベルには -96.0以上の数値を入力してください");
                    return;
                }
                break;
            case 24:
                if (dB < -144.0) {
                    MessageBox.Show("エラー: 出力レベルには -144.0以上の数値を入力してください");
                    return;
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            textBoxLog.Text = "";

            switch (ss) {
            case SignalShape.SineWave:
                break;
            case SignalShape.TriangleWave: {
                    double harmonics = 0;
                    for (int i = 1; ; ++i) {
                        harmonics = 2 * i - 1;
                        double level = dB + 20 * Math.Log10(1.0 / harmonics / harmonics);
                        if (harmonics * freq < sampleRate / 2 &&
                            -96.0 < level) {
                        } else {
                            break;
                        }
                    }
                    if (harmonics <= 5) {
                        textBoxLog.Text += string.Format("高調波成分が少ないためあまり三角波っぽい形にはなりません\r\n");
                    }
                }
                break;
            case SignalShape.SawToothWaveDesc:
            case SignalShape.SawToothWaveAsc: {
                    double harmonics = 0;
                    for (int i = 1; ; ++i) {
                        harmonics = i;
                        double level = dB + 20 * Math.Log10(1.0 / harmonics);
                        if (harmonics * freq < sampleRate / 2 &&
                            -96.0 < level) {
                        } else {
                            break;
                        }
                    }
                    if (harmonics <= 5) {
                        textBoxLog.Text += string.Format("高調波成分が少ないためあまりのこぎり波っぽい形にはなりません\r\n");
                    }
                }
                break;
            case SignalShape.SquareWave: {
                    double harmonics = 0;
                    for (int i = 1; ; ++i) {
                        harmonics = 2 * i - 1;
                        double level = dB + 20 * Math.Log10(1.0 / harmonics);
                        if (harmonics * freq < sampleRate / 2 &&
                            -96.0 < level) {
                            /*
                            if (harmonics == 1) {
                                textBoxLog.Text += string.Format("基本周波数: {1}Hz {2:0.0}dB\r\n", harmonics, harmonics * freq, level);
                            } else {
                                textBoxLog.Text += string.Format("第{0}次高調波: {1}Hz {2:0.0}dB\r\n", harmonics, harmonics * freq, level);
                            }*/
                        } else {
                            break;
                        }
                    }
                    if (harmonics <= 5) {
                        textBoxLog.Text += string.Format("高調波成分が少ないためあまり矩形波っぽい形にはなりません\r\n");
                    }
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            buttonStart.IsEnabled = false;
            if (m_outputMode == OutputMode.Asio) {
                buttonStop.IsEnabled = true;
            }
            Console.WriteLine("D: MainWindow Start()");
            backgroundWorker1.RunWorkerAsync(sgwa);
        }

        private bool Stop(bool bRestart) {
            Console.WriteLine("D: MainWindow Stop()");
            if (buttonStop.IsEnabled == true && asio.GetStatus() == AsioCS.AsioStatus.Running) {
                backgroundWorker1.CancelAsync();
                m_restart = bRestart;
                asio.Stop();
                buttonStop.IsEnabled = false;
                return true;
            }

            Console.WriteLine("D: MainWindow Stop() 空振り");
            return false;
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            Stop(false);
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            radioButtonOutFile.IsEnabled = false;
            Start();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e) {
            SignalGeneraterWorkerArg sgwa = (SignalGeneraterWorkerArg)e.Argument;
            SignalGenerator sg = new SignalGenerator();

            backgroundWorker1.ReportProgress(1, "出力データの準備開始。\r\n");

            WavData wavData;
            SignalGeneratorResult cwdr
                = sg.GenerateSignal(sgwa.sgParams, out wavData);

            switch (cwdr) {
            case SignalGeneratorResult.Success:
                backgroundWorker1.ReportProgress(2, "出力データの準備完了。\r\n");
                break;
            case SignalGeneratorResult.LevelOver:
                backgroundWorker1.ReportProgress(2, "レベルオーバー。出力レベルを下げて下さい。\r\n");
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            switch (sgwa.outputMode) {
            case OutputMode.WavFile:
                if (!WriteWavFile(wavData, sgwa.outputPath)) {
                    backgroundWorker1.ReportProgress(3, string.Format("書き込み失敗: {0}\r\n", sgwa.outputPath));
                    return;
                }
                backgroundWorker1.ReportProgress(3, string.Format("書き込み成功: {0}\r\n", sgwa.outputPath));
                break;
            case OutputMode.Asio:
                {
                    // short → int に拡張
                    int[] asioData = new int[wavData.NumSamples];
                    for (int i = 0; i < wavData.NumSamples; ++i) {
                        asioData[i] = wavData.Sample16Get(0, i) << 16;
                    }
                    // 出力デバイスに出力データをセット
                    foreach (int ch in sgwa.outputChannels) {
                        asio.OutputSet(ch, asioData, true);
                    }
                    // 開始
                    asio.Start();
                    /* backgroundWorker1.ReportProgress(3, 
                        string.Format("出力開始。{0}Hz {1} {2}dB\r\n", sgwa.sgParams.freq, sgwa.sgParams.ss, sgwa.sgParams.dB)); */
                    while (!asio.Run()) {
                    }
                    backgroundWorker1.ReportProgress(3, "出力終了。\r\n");
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            Console.WriteLine("D: MainWindow RunWorkerCompleted()");
            if (m_restart) {
                m_restart = false;
                Start();
            } else {
                buttonStart.IsEnabled = true;
                buttonStop.IsEnabled = false;
                radioButtonOutFile.IsEnabled = true;
            }
        }

        void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            string s = (string)e.UserState;
            textBoxLog.Text += s;
        }

        private bool WriteWavFile(WavData wavData, string path) {

            bool rv = true;
            try {
                using (BinaryWriter bw1 = new BinaryWriter(File.Open(path, FileMode.Create))) {
                    wavData.Write(bw1);
                }
            } catch (System.Exception ex) {
                Console.WriteLine(ex.ToString());
                rv = false;
            }
            return rv;
        }

        private void MenuItemFileExit_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        private void MenuItemHelpAbout_Click(object sender, RoutedEventArgs e) {
            AboutBox dlg = new AboutBox();

            dlg.Owner = this;
            dlg.SetText(string.Format("{0} version {1}\n\n{2}",
            AssemblyProduct, AssemblyVersion, asio.AsioTrademarkStringGet()));
            dlg.ShowDialog();
        }

        #region アセンブリ情報
        public string AssemblyVersion {
            get {
                return string.Format("{0}.{1}.{2}",
                    Assembly.GetExecutingAssembly().GetName().Version.Major,
                    Assembly.GetExecutingAssembly().GetName().Version.Minor,
                    Assembly.GetExecutingAssembly().GetName().Version.Build);
            }
        }

        public string AssemblyProduct {
            get {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0) {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }
        #endregion

        private void MenuItemHelpWeb_Click(object sender, RoutedEventArgs e) {
            try {
                System.Diagnostics.Process.Start("http://code.google.com/p/bitspersampleconv2/wiki/SqWave2");
            } catch (System.ComponentModel.Win32Exception) {
            }
        }

        private void radioButtonOutFile_Checked(object sender, RoutedEventArgs e) {
            if (m_asioStatus != AsioStatus.NotReady) {
                asio.Unsetup();
                m_asioStatus = AsioStatus.NotReady;
            }
            if (0 <= m_selectedAsioDeviceIdx) {
                asio.DriverUnload();
                m_selectedAsioDeviceIdx = -1;
            }
            m_outputMode = OutputMode.WavFile;
            UpdateUIStatus();
        }

        private void radioButtonOutAsio_Checked(object sender, RoutedEventArgs e) {
            m_outputMode = OutputMode.Asio;

            int driverNum = asio.DriverNumGet();
            listBoxAsioDevices.Items.Clear();
            for (int i = 0; i < driverNum; ++i) {
                listBoxAsioDevices.Items.Add(asio.DriverNameGet(i));
            }
            listBoxAsioDevices.SelectedIndex = 0;

            UpdateUIStatus();
        }

        private void listBoxAsioDevices_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (listBoxAsioDevices.SelectedIndex != m_selectedAsioDeviceIdx) {
                if (m_asioStatus == AsioStatus.Ready) {
                    asio.Unsetup();
                    m_asioStatus = AsioStatus.NotReady;
                }

                if (0 <= m_selectedAsioDeviceIdx) {
                    asio.DriverUnload();
                    m_selectedAsioDeviceIdx = -1;
                }
                m_selectedAsioDeviceIdx = listBoxAsioDevices.SelectedIndex;
                bool bRv = asio.DriverLoad(m_selectedAsioDeviceIdx);
                if (!bRv) {
                    MessageBox.Show(string.Format("エラー: ASIOドライバー{0}のロードに失敗",
                        asio.DriverNameGet(m_selectedAsioDeviceIdx)));
                    return;
                }

                int sampleRate = System.Convert.ToInt32(((ListBoxItem)listBoxSampleFreq.SelectedItem).Content);


                int rv = asio.Setup(sampleRate);
                if (rv != 0) {
                    MessageBox.Show(string.Format("エラー: サンプルレートの設定に失敗 {0}\n AsioError {1}",
                        sampleRate, rv));
                    return;
                }
                m_asioStatus = AsioStatus.Ready;

                {
                    int nCh = asio.OutputChannelsNumGet();
                    if (nCh <= 0) {
                        MessageBox.Show("エラー: 出力チャンネル情報取得に失敗");
                        return;
                    }
                    listBoxAsioChannels.Items.Clear();
                    for (int i = 0; i < nCh; ++i) {
                        ListBoxItem item = new ListBoxItem();
                        item.Content = asio.OutputChannelNameGet(i);
                        listBoxAsioChannels.Items.Add(item);
                    }
                    listBoxAsioChannels.SelectedIndex = 0;
                }

                {
                    int nCS = asio.ClockSourceNumGet();
                    if (nCS <= 0) {
                        MessageBox.Show("エラー: クロックソース情報取得に失敗");
                        return;
                    }
                    listBoxAsioClockSource.Items.Clear();
                    for (int i = 0; i < nCS; ++i) {
                        ListBoxItem item = new ListBoxItem();
                        item.Content = asio.ClockSourceNameGet(i);
                        listBoxAsioClockSource.Items.Add(item);
                    }
                    listBoxAsioClockSource.SelectedIndex = 0;
                }
            }
        }

        private void buttonAsioControlPanel_Click(object sender, RoutedEventArgs e) {
            int rv = asio.ControlPanel();
            if (rv == -1000) {
                MessageBox.Show("このASIOデバイスには、コントロールパネルはありません");
            }
        }

        private void textBoxFreq_TextChanged(object sender, TextChangedEventArgs e) {
            if (m_outputMode != OutputMode.Asio || buttonStart.IsEnabled != false) {
                return;
            }

            double freq;
            if (!TextBoxFreqToFreq(out freq)) {
                return;
            }
            if (freq < ASIO_FREQ_MIN) {
                return;
            }

            Stop(true);
        }

        private void listBoxShape_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (m_outputMode != OutputMode.Asio || buttonStart.IsEnabled != false) {
                return;
            }

            Stop(true);
        }

        private void textBoxTrunc_TextChanged(object sender, TextChangedEventArgs e) {
            if (m_outputMode != OutputMode.Asio || buttonStart.IsEnabled != false) {
                return;
            }

            double trunc;
            if (TextBoxTruncToTrunc(out trunc)) {
                return;
            }

            Stop(true);
        }

        private void textBoxLevel_TextChanged(object sender, TextChangedEventArgs e) {
            if (m_outputMode != OutputMode.Asio || buttonStart.IsEnabled != false) {
                return;
            }

            double dB;
            if (TextBoxDbToDb(out dB)) {
                return;
            }

            Stop(true);
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            Stop(false);
            while (asio.GetStatus() == AsioCS.AsioStatus.Running) {
                System.Threading.Thread.Sleep(10);
            }
        }

    }
}
