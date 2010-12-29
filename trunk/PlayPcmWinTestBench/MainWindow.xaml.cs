// 日本語UTF-8

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using PcmDataLib;
using Wasapi;
using WasapiPcmUtil;
using WavRWLib2;
using System.Threading.Tasks;
using WWDirectComputeCS;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        Wasapi.WasapiCS wasapi;
        RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();
        private BackgroundWorker m_playWorker;
        private BackgroundWorker m_AQworker;
        private BackgroundWorker m_USworker;

        enum AB {
            Unknown = -1,
            A,
            B
        };

        class TestInfo {
            public AB x;
            public AB answer;

            public TestInfo(AB x) {
                this.x      = x;
                answer = AB.Unknown;
            }

            public void SetAnswer(AB answer) {
                this.answer = answer;
            }
        }

        List<TestInfo> m_testInfoList = new List<TestInfo>();

        private AB GetX() {
            return m_testInfoList[m_testInfoList.Count - 1].x;
        }

        /// <summary>
        /// 回答する。
        /// </summary>
        /// <param name="answer"></param>
        /// <returns>true:まだ続きがある。false:終わり</returns>
        private bool Answer(AB answer) {
            m_testInfoList[m_testInfoList.Count - 1].answer = answer;
            if (10 == m_testInfoList.Count) {
                return false;
            }
            return true;
        }

        private void PrepareNextTest() {
            byte[] r = new byte[1];
            gen.GetBytes(r);

            AB x = ((r[0] & 1) == 0) ? AB.A : AB.B;
            TestInfo ti = new TestInfo(x);
            m_testInfoList.Add(ti);
        }


        public MainWindow() {
            InitializeComponent();

            wasapi = new WasapiCS();
            wasapi.Init();

            Prepare();
        }

        private void Prepare() {
            ListDevices();

            m_testInfoList.Clear();
            PrepareNextTest();

            UpdateUIStatus();

            m_playWorker = new BackgroundWorker();
            m_playWorker.WorkerReportsProgress = true;
            m_playWorker.DoWork += new DoWorkEventHandler(PlayDoWork);
            m_playWorker.ProgressChanged += new ProgressChangedEventHandler(PlayProgressChanged);
            m_playWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlayRunWorkerCompleted);
            m_playWorker.WorkerSupportsCancellation = true;

            m_AQworker = new BackgroundWorker();
            m_AQworker.WorkerReportsProgress = true;
            m_AQworker.DoWork += new DoWorkEventHandler(m_AQworker_DoWork);
            m_AQworker.ProgressChanged += new ProgressChangedEventHandler(m_AQworker_ProgressChanged);
            m_AQworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(m_AQworker_RunWorkerCompleted);
            m_AQworker.WorkerSupportsCancellation = false;

            m_USworker = new BackgroundWorker();
            m_USworker.WorkerReportsProgress = true;
            m_USworker.DoWork += new DoWorkEventHandler(m_USworker_DoWork);
            m_USworker.ProgressChanged += new ProgressChangedEventHandler(m_USworker_ProgressChanged);
            m_USworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(m_USworker_RunWorkerCompleted);
            m_USworker.WorkerSupportsCancellation = true;

            textBoxUSResult.Text += "※GPU計算機能を使用するためには以下の3つの準備が要ります:\r\n"
                + "・GPUはGeForce GTX 570以上を用意して下さい。"
                + "・最新のNVIDIAディスプレイドライバ をインストールして下さい(バージョン260以降が必要)。\r\n"
                + "・最新のDirectXエンドユーザーランタイムをインストールする必要があります(August 2009以降が必要)。"
                + " http://www.microsoft.com/downloads/details.aspx?FamilyID=2da43d38-db71-4c1b-bc6a-9b6652cd92a3&displayLang=ja\r\n";
        }

        
        private void Window_Closed(object sender, EventArgs e) {
            if (wasapi != null) {
                Stop();

                // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせする。
                // そうしないと、バックグラウンドスレッドによって使用中のオブジェクトが
                // この後のTermの呼出によって開放されてしまい問題が起きる。

                while (m_playWorker.IsBusy) {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));

                    System.Threading.Thread.Sleep(100);
                }

                wasapi.UnchooseDevice();
                wasapi.Term();
                wasapi = null;
            }

        }

        private void ComboBoxDeviceInit(ComboBox comboBox) {
            int selectedIdx = comboBox.SelectedIndex;

            comboBox.Items.Clear();

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                string deviceName = wasapi.GetDeviceName(i);
                comboBox.Items.Add(deviceName);
            }

            if (0 <= selectedIdx && selectedIdx < nDevices) {
                comboBox.SelectedIndex = selectedIdx;
            } else if (0 < nDevices) {
                comboBox.SelectedIndex = 0;
            }
        }

        private void ListDevices() {
            int hr = wasapi.DoDeviceEnumeration(WasapiCS.DeviceType.Play);
            
            ComboBoxDeviceInit(comboBoxDeviceA);
            ComboBoxDeviceInit(comboBoxDeviceB);
        }

        /// <summary>
        /// 再生中。バックグラウンドスレッド。
        /// </summary>
        private void PlayDoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("PlayDoWork started");
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!wasapi.Run(100)) {
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
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            m_status = Status.Stop;

            wasapi.Unsetup();
            UnchooseRecreateDeviceList();
        }

        enum Status {
            Stop,
            Play,
        }

        private Status m_status = Status.Stop;

        public void UpdateUIStatus() {
            labelGuide.Content = string.Format("テスト{0}回目", m_testInfoList.Count);

            if (m_status != Status.Play) {
                // Stop
                buttonPlayA.IsEnabled = false;
                buttonPlayB.IsEnabled = false;
                buttonPlayX.IsEnabled = false;
                buttonPlayY.IsEnabled = false;
                buttonConfirm.IsEnabled = false;
                buttonStopA.IsEnabled = false;
                buttonStopB.IsEnabled = false;
                buttonStopX.IsEnabled = false;
                buttonStopY.IsEnabled = false;

                if (0 < textBoxPathA.Text.Length && System.IO.File.Exists(textBoxPathA.Text)) {
                    buttonPlayA.IsEnabled = true;
                }
                if (0 < textBoxPathA.Text.Length && System.IO.File.Exists(textBoxPathB.Text)) {
                    buttonPlayB.IsEnabled = true;
                }
                if (buttonPlayA.IsEnabled && buttonPlayB.IsEnabled) {
                    buttonPlayX.IsEnabled = true;
                    buttonPlayY.IsEnabled = true;
                    buttonConfirm.IsEnabled = true;
                }
            } else {
                // Play
                buttonPlayA.IsEnabled = false;
                buttonPlayB.IsEnabled = false;
                buttonPlayX.IsEnabled = false;
                buttonPlayY.IsEnabled = false;
                buttonStopA.IsEnabled = true;
                buttonStopB.IsEnabled = true;
                buttonStopX.IsEnabled = true;
                buttonStopY.IsEnabled = true;
            }
        }

        private void buttonFinish_Click(object sender, RoutedEventArgs e) {
            DispResult();
            DialogResult = true;
            Close();
        }

        private string BrowseOpenFile() {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter =
                "WAVEファイル|*.wav";
            dlg.Multiselect = false;

            Nullable<bool> result = dlg.ShowDialog();
            if (result != true) {
                return "";
            }
            return dlg.FileName;
        }

        private string BrowseSaveFile() {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter =
                "WAVEファイル|*.wav";

            Nullable<bool> result = dlg.ShowDialog();
            if (result != true) {
                return "";
            }
            return dlg.FileName;
        }

        private void buttonBrowseA_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile();
            if (0 < fileName.Length) {
                textBoxPathA.Text = fileName;
                UpdateUIStatus();
            }
        }

        private void buttonBrowseB_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile();
            if (0 < fileName.Length) {
                textBoxPathB.Text = fileName;
                UpdateUIStatus();
            }
        }

        private void Stop() {
            if (m_status == Status.Play) {
                buttonStopA.IsEnabled = false;
                buttonStopB.IsEnabled = false;
                buttonStopX.IsEnabled = false;
                buttonStopY.IsEnabled = false;
                wasapi.Stop();
            }
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            Stop();
        }
        
        private PcmData ReadWavFile(string path) {
            PcmData pcmData = new PcmData();

            using (BinaryReader br = new BinaryReader(
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))) {
                WavData wavData = new WavData();
                bool readSuccess = wavData.ReadAll(br, 0, -1);
                if (!readSuccess) {
                    return null;
                }
                pcmData.SetFormat(wavData.NumChannels, wavData.BitsPerFrame, wavData.BitsPerFrame,
                    wavData.SampleRate, wavData.SampleValueRepresentationType, wavData.NumFrames);
                pcmData.SetSampleArray(wavData.NumFrames, wavData.GetSampleArray());
            }

            return pcmData;
        }

        private bool WriteWavFile(PcmData pcmData, string path) {
            using (BinaryWriter bw = new BinaryWriter(
                    File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Write))) {
                WavData wavData = new WavData();
                wavData.Set(pcmData.NumChannels, pcmData.BitsPerSample, pcmData.ValidBitsPerSample, pcmData.SampleRate,
                    pcmData.SampleValueRepresentationType, pcmData.NumFrames, pcmData.GetSampleArray());
                wavData.Write(bw);
            }

            return true;
        }

        private SampleFormatInfo m_sampleFormat;

        private int WasapiSetup(
                bool isExclusive,
                bool isEventDriven,
                int sampleRate,
                int pcmDataValidBitsPerSample,
                PcmDataLib.PcmData.ValueRepresentationType vrt,
                int latencyMillisec) {
            int num = SampleFormatInfo.GetDeviceSampleFormatCandidateNum(
                isExclusive ? WasapiSharedOrExclusive.Exclusive : WasapiSharedOrExclusive.Shared,
                BitsPerSampleFixType.AutoSelect,
                pcmDataValidBitsPerSample, vrt);

            int hr = -1;
            for (int i = 0; i < num; ++i) {
                SampleFormatInfo sf = SampleFormatInfo.GetDeviceSampleFormat(
                    isExclusive ? WasapiSharedOrExclusive.Exclusive : WasapiSharedOrExclusive.Shared,
                    BitsPerSampleFixType.AutoSelect,
                    pcmDataValidBitsPerSample, vrt, i);

                wasapi.SetDataFeedMode(
                    isEventDriven ?
                        WasapiCS.DataFeedMode.EventDriven :
                        WasapiCS.DataFeedMode.TimerDriven);

                wasapi.SetLatencyMillisec(latencyMillisec);

                hr = wasapi.Setup(
                    sampleRate, sf.GetSampleFormatType(), 2);
                if (0 <= hr) {
                    m_sampleFormat = sf;
                    return hr;
                }
            }
            wasapi.Unsetup();
            return hr;
        }


        private void UnchooseRecreateDeviceList() {
            wasapi.UnchooseDevice();
            ListDevices();
            UpdateUIStatus();
        }

        private void buttonPlayA_Click(object sender, RoutedEventArgs e) {
            PcmData pcmData = ReadWavFile(textBoxPathA.Text);
            if (null == pcmData) {
                MessageBox.Show(
                    string.Format("WAVファイル A 読み込み失敗: {0}", textBoxPathA.Text));
                return;
            }

            int hr = wasapi.ChooseDevice(comboBoxDeviceA.SelectedIndex);
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.ChooseDevice()失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            hr = WasapiSetup(
                radioButtonExclusiveA.IsChecked == true,
                radioButtonEventDrivenA.IsChecked == true,
                pcmData.SampleRate,
                pcmData.ValidBitsPerSample,
                pcmData.SampleValueRepresentationType,
                Int32.Parse(textBoxLatencyA.Text));
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.Setup失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            pcmData = PcmUtil.BitsPerSampleConvAsNeeded(pcmData, m_sampleFormat.GetSampleFormatType());

            wasapi.ClearPlayList();
            wasapi.AddPlayPcmDataStart();
            wasapi.AddPlayPcmData(0, pcmData.GetSampleArray());
            wasapi.AddPlayPcmDataEnd();

            hr = wasapi.Start(0);
            m_playWorker.RunWorkerAsync();
            m_status = Status.Play;
            UpdateUIStatus();
        }

        private void buttonPlayB_Click(object sender, RoutedEventArgs e) {
            PcmData pcmData = ReadWavFile(textBoxPathB.Text);
            if (null == pcmData) {
                MessageBox.Show(
                    string.Format("WAVファイル B 読み込み失敗: {0}", textBoxPathB.Text));
                return;
            }

            int hr = wasapi.ChooseDevice(comboBoxDeviceB.SelectedIndex);
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.ChooseDevice()失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            hr = WasapiSetup(
                radioButtonExclusiveB.IsChecked == true,
                radioButtonEventDrivenB.IsChecked == true,
                pcmData.SampleRate,
                pcmData.ValidBitsPerSample,
                pcmData.SampleValueRepresentationType,
                Int32.Parse(textBoxLatencyB.Text));
            if (hr < 0) {
                MessageBox.Show(string.Format("Wasapi.Setup失敗 {0:X8}", hr));
                UnchooseRecreateDeviceList();
                return;
            }

            pcmData = PcmUtil.BitsPerSampleConvAsNeeded(pcmData, m_sampleFormat.GetSampleFormatType());

            wasapi.ClearPlayList();
            wasapi.AddPlayPcmDataStart();
            wasapi.AddPlayPcmData(0, pcmData.GetSampleArray());
            wasapi.AddPlayPcmDataEnd();

            hr = wasapi.Start(0);
            m_playWorker.RunWorkerAsync();
            m_status = Status.Play;
            UpdateUIStatus();
        }

        private void buttonPlayX_Click(object sender, RoutedEventArgs e) {
            AB x = GetX();
            if (x == AB.A) {
                buttonPlayA_Click(sender, e);
            } else {
                buttonPlayB_Click(sender, e);
            }
        }

        private void buttonPlayY_Click(object sender, RoutedEventArgs e) {
            AB x = GetX();
            if (x == AB.A) {
                buttonPlayB_Click(sender, e);
            } else {
                buttonPlayA_Click(sender, e);
            }
        }

        private void buttonConfirm_Click(object sender, RoutedEventArgs e) {
            if (!Answer(radioButtonAXBY.IsChecked == true ? AB.A : AB.B)) {
                // 終わり
                DispResult();
                DialogResult = true;
                Close();
                return;
            }
            PrepareNextTest();
            UpdateUIStatus();
        }

        private void DispResult() {
            if (m_testInfoList.Count == 1 && m_testInfoList[0].answer == AB.Unknown) {
                return;
            }
            string s = "結果発表\r\n\r\n";

            int answered = 0;
            int correct = 0;
            for (int i=0; i<m_testInfoList.Count; ++i) {
                TestInfo ti = m_testInfoList[i];
                if (ti.answer == AB.Unknown) {
                    break;
                }

                ++answered;
                s += string.Format("テスト{0}回目 {1} 正解は X={2},Y={3}\r\n",
                    i, ti.x == ti.answer ? "○" : "×",
                    ti.x == AB.A ? "A" : "B",
                    ti.x == AB.A ? "B" : "A");
                if (ti.x == ti.answer) {
                    ++correct;
                }
            }
            s += string.Format("\r\n{0}回中{1}回正解", answered, correct);
            MessageBox.Show(s);
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        /// 音質劣化

        /// <summary>
        ///  仮数部が32bitぐらいまで値が埋まっているランダムの0～1
        /// </summary>
        /// <returns></returns>
        private static double GenRandom0to1(RNGCryptoServiceProvider gen) {
            byte[] bytes = new byte[4];
            gen.GetBytes(bytes);
            uint u = BitConverter.ToUInt32(bytes, 0);
            double d = (double)u / uint.MaxValue;
            return d;
        }

        // GPUでジッター付加。
        private int GpuJitterAdd(AQWorkerArgs args, PcmData pcmDataIn, PcmData pcmDataOut) {
            RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();

            long sampleN = pcmDataIn.NumFrames;
            if (0x7fffffff < sampleN) {
                return -1;
            }

            int hr = -1;

            WWDirectComputeCS.WWDirectComputeCS dc = new WWDirectComputeCS.WWDirectComputeCS();
            hr = dc.Init(
                WWDirectComputeCS.WWDirectComputeCS.GpuPrecisionType.PDouble,
                args.convolutionN);
            if (hr < 0) {
                return hr;
            }

            for (int ch = 0; ch < pcmDataIn.NumChannels; ++ch) {
                float[] sampleDataBuffer = new float[sampleN];
                float[] jitterXBuffer = new float[sampleN];
                float[] outBuffer = new float[sampleN];

                int offs = 0;
                for (long i = 0; i < pcmDataIn.NumFrames; ++i) {
                    sampleDataBuffer[offs] = pcmDataIn.GetSampleValueInFloat(ch, i);

                    // generate jitter
                    double seqJitter = args.ampSeqJitter
                        * Math.Sin((args.thetaCoefficientSeqJitter * i) % (2.0 * Math.PI));
                    double tpdfJitter = 0.0;
                    double rpdfJitter = 0.0;
                    if (0.0 < args.tpdfJitterPicoseconds) {
                        double r = GenRandom0to1(gen) + GenRandom0to1(gen) - 1.0;
                        tpdfJitter = args.ampTpdfJitter * r;
                    }
                    if (0.0 < args.rpdfJitterPicoseconds) {
                        rpdfJitter = args.ampRpdfJitter * (GenRandom0to1(gen) * 2.0 - 1.0);
                    }
                    double jitter = seqJitter + tpdfJitter + rpdfJitter;
                    jitterXBuffer[offs] = (float)jitter;

                    outBuffer[offs] = 0.0f;

                    ++offs;
                }

                int sampleRemain = (int)sampleN;
                offs = 0;
                while (0 < sampleRemain) {
                    int sample1 = args.gpuSampleSlice;
                    if (sampleRemain < sample1) {
                        sample1 = sampleRemain;
                    }
                    hr = dc.JitterAddPortion(
                        WWDirectComputeCS.WWDirectComputeCS.GpuPrecisionType.PDouble,
                        (int)sampleN, args.convolutionN,
                        sampleDataBuffer, jitterXBuffer, ref outBuffer, offs, sample1);
                    if (hr < 0) {
                        break;
                    }

                    sampleRemain -= sample1;
                    offs += sample1;

                    // 10%～99%
                    m_AQworker.ReportProgress(
                        10 + (int)(89L * offs / sampleN + 89L * ch) / pcmDataIn.NumChannels);
                }

                if (hr < 0) {
                    break;
                }

                // 成功。結果をpcmDataOutに詰める。
                for (long i = 0; i < pcmDataIn.NumFrames; ++i) {
                    pcmDataOut.SetSampleValueInFloat(ch, i, outBuffer[i]);
                }
            }

            dc.Term();

            return hr;
        }

        // CPUでジッター付加。
        // @todo ジッターの量に、πをかけるのを忘れているような気がするので、あとで調べる。
        private int CpuJitterAdd(AQWorkerArgs args, PcmData pcmDataIn, PcmData pcmDataOut) {
            int hr = 0;

            RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();

            float maxValue = 0.0f;
            float minValue = 0.0f;

            long count = 0;
            Parallel.For(0, pcmDataIn.NumFrames, delegate(long i) {
                //for (long i=0; i<pcmDataIn.NumFrames; ++i) {
                for (int ch = 0; ch < pcmDataIn.NumChannels; ++ch) {
                    double acc = 0.0;

                    // generate jitter
                    double seqJitter = args.ampSeqJitter
                        * Math.Sin((args.thetaCoefficientSeqJitter * i) % (2.0 * Math.PI));
                    double tpdfJitter = 0.0;
                    double rpdfJitter = 0.0;
                    if (0.0 < args.tpdfJitterPicoseconds) {
                        double r = GenRandom0to1(gen) + GenRandom0to1(gen) - 1.0;
                        tpdfJitter = args.ampTpdfJitter * r;
                    }
                    if (0.0 < args.rpdfJitterPicoseconds) {
                        rpdfJitter = args.ampRpdfJitter * (GenRandom0to1(gen) * 2.0 - 1.0);
                    }
                    double jitter = seqJitter + tpdfJitter + rpdfJitter;

                    double sinTheta = Math.Sin(jitter % (2.0 * Math.PI));

                    for (int offset = -args.convolutionN; offset < args.convolutionN; ++offset) {
                        double pos = Math.PI * offset + jitter;
                        double v = pcmDataIn.GetSampleValueInFloat(ch, i + offset);

                        if (pos < -double.Epsilon || double.Epsilon < pos) {
                            v *= sinTheta / pos;
                        }

                        acc += v;
                    }
                    pcmDataOut.SetSampleValueInFloat(ch, i, (float)acc);
                    if (maxValue < acc) {
                        maxValue = (float)acc;
                    }
                    if (acc < minValue) {
                        minValue = (float)acc;
                    }
                    /*
                    if (ch == 0 && i < 1024) {
                        System.Console.WriteLine("{0}, {1}, {2}",
                            i, pcmDataIn.GetSampleValueInFloat(ch, i),
                            acc);
                    }
                    */
                }

                ++count;
                if (0 == (count % pcmDataIn.SampleRate)) {
                    m_AQworker.ReportProgress((int)(1 + 98 * count / pcmDataIn.NumFrames));
                }
            });

            // 音量制限
            args.scale = 1.0;
            if (0.99999988079071044921875f < maxValue) {
                // 最大値は 1.0 - (1/8388608)
                args.scale = 0.99999988079071044921875 / maxValue;
            }
            if (minValue < -1.0f) {
                // 最小値は-1.0
                args.scale = -1.0 / minValue;
            }
            if (args.scale != 1.0) {
                pcmDataOut.Scale((float)args.scale);
            }

            return hr;
        }

        // -----------------------------------------------------------------

        private void buttonAQBrowseOpen_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile();
            if (0 < fileName.Length) {
                textBoxAQInputFilePath.Text = fileName;
            }
        }

        private void buttonAQBrowseSaveAs_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseSaveFile();
            if (0 < fileName.Length) {
                textBoxAQOutputFilePath.Text = fileName;
            }
        }

        enum ProcessDevice {
            Cpu,
            Gpu
        };

        struct AQWorkerArgs {
            public string inputPath;
            public string outputPath;
            public double sequentialJitterFrequency;
            public double sequentialJitterPicoseconds;
            public double tpdfJitterPicoseconds;
            public double rpdfJitterPicoseconds;
            public int convolutionN;
            public ProcessDevice device;
            public int gpuSampleSlice;

            // --------------------------------------------------------
            // 以降、物置(RunWorkerAsync()でセットする必要はない)
            public double thetaCoefficientSeqJitter;
            public double ampSeqJitter;

            public double ampTpdfJitter;
            public double ampRpdfJitter;

            // 音量制限
            public double scale;
        };

        private void m_AQworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            progressBar1.Value = 0;
            buttonAQOutputStart.IsEnabled  = true;
            buttonAQBrowseOpen.IsEnabled   = true;
            buttonAQBrowseSaveAs.IsEnabled = true;

            string result = (string)e.Result;

            textBoxAQResult.Text += string.Format("結果: {0}\r\n", result);
            textBoxAQResult.ScrollToEnd();
        }

        private void m_AQworker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void buttonAQOutputStart_Click(object sender, RoutedEventArgs e) {
            AQWorkerArgs args = new AQWorkerArgs();
            args.inputPath = textBoxAQInputFilePath.Text;
            args.outputPath = textBoxAQOutputFilePath.Text;
            if (!System.IO.File.Exists(args.inputPath)) {
                MessageBox.Show("エラー。入力ファイルが存在しません");
                return;
            }
            if (!Double.TryParse(textBoxSequentialJitterFrequency.Text, out args.sequentialJitterFrequency) ||
                    args.sequentialJitterFrequency < 0.0) {
                MessageBox.Show("エラー。周期ジッター周波数に0以上の数値を入力してください");
                return;
            }
            if (!Double.TryParse(textBoxSequentialJitterPicoseconds.Text, out args.sequentialJitterPicoseconds) ||
                    args.sequentialJitterPicoseconds < 0.0) {
                MessageBox.Show("エラー。周期ジッター最大ずれ量に0以上の数値を入力してください");
                return;
            }
            // sequential jitter RMS⇒peak
            args.sequentialJitterPicoseconds *= Math.Sqrt(2.0);

            if (!Double.TryParse(textBoxTpdfJitterPicoseconds.Text, out args.tpdfJitterPicoseconds) ||
                    args.tpdfJitterPicoseconds < 0.0) {
                MessageBox.Show("エラー。三角分布ジッター最大ずれ量に0以上の数値を入力してください");
                return;
            }
            if (!Double.TryParse(textBoxRpdfJitterPicoseconds.Text, out args.rpdfJitterPicoseconds) ||
                    args.rpdfJitterPicoseconds < 0.0) {
                MessageBox.Show("エラー。一様分布ジッター最大ずれ量に0以上の数値を入力してください");
                return;
            }

            args.convolutionN = 256;
            args.device = ProcessDevice.Cpu;
            args.gpuSampleSlice = 32768;
            if (radioButtonConvolution65536.IsChecked == true) {
                args.convolutionN = 65536;
            }
            if (radioButtonConvolution65536GPU.IsChecked == true) {
                args.convolutionN = 65536;
                args.device = ProcessDevice.Gpu;
            }
            if (radioButtonConvolution1048576GPU.IsChecked == true) {
                args.convolutionN = 1048576;
                args.device = ProcessDevice.Gpu;
            }
            if (radioButtonConvolution16777216GPU.IsChecked == true) {
                args.convolutionN = 16777216;
                args.device = ProcessDevice.Gpu;

                /// この条件では一度に32768個処理できないので半分に減らす。
                args.gpuSampleSlice = 16384;
            }

            buttonAQOutputStart.IsEnabled  = false;
            buttonAQBrowseOpen.IsEnabled   = false;
            buttonAQBrowseSaveAs.IsEnabled = false;
            progressBar1.Value = 0;

            textBoxAQResult.Text += string.Format("処理中 {0}⇒{1}……処理中はPCの動作が重くなります!\r\n",
                args.inputPath, args.outputPath);
            textBoxAQResult.ScrollToEnd();

            m_AQworker.RunWorkerAsync(args);
        }

        private void m_AQworker_DoWork(object sender, DoWorkEventArgs e) {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Lowest;

            AQWorkerArgs args = (AQWorkerArgs)e.Argument;

            PcmData pcmDataIn = ReadWavFile(args.inputPath);
            if (null == pcmDataIn) {
                e.Result = string.Format("WAVファイル 読み込み失敗: {0}", args.inputPath);
                return;
            }

            m_AQworker.ReportProgress(1);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            pcmDataIn = pcmDataIn.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SFloat);
            PcmData pcmDataOut = new PcmData();
            pcmDataOut.CopyFrom(pcmDataIn);

            /*
             sampleRate        == 96000 Hz
             jitterFrequency   == 50 Hz
             jitterPicoseconds == 1 ps の場合

             サンプル位置posのθ= 2 * PI * pos * 50 / 96000 (ラジアン)

             サンプル間隔= 1/96000秒 = 10.4 μs
             
             1ms = 10^-3秒
             1μs= 10^-6秒
             1ns = 10^-9秒
             1ps = 10^-12秒

              1psのずれ           x サンプルのずれ
             ──────── ＝ ─────────
              10.4 μsのずれ      1 サンプルのずれ

             1psのサンプルずれA ＝ 10^-12 ÷ 1/96000 (サンプルのずれ)
             
             サンプルを採取する位置= pos + Asin(θ)
             
             */

            args.thetaCoefficientSeqJitter = 2.0 * Math.PI * args.sequentialJitterFrequency / pcmDataIn.SampleRate;
            args.ampSeqJitter = 1.0e-12 * pcmDataIn.SampleRate * args.sequentialJitterPicoseconds;
            args.ampTpdfJitter = 1.0e-12 * pcmDataIn.SampleRate * args.tpdfJitterPicoseconds;
            args.ampRpdfJitter = 1.0e-12 * pcmDataIn.SampleRate * args.rpdfJitterPicoseconds;
            args.scale = 1.0;

            int hr = 0;
            if (args.device == ProcessDevice.Gpu) {
                hr = GpuJitterAdd(args, pcmDataIn, pcmDataOut);
            } else {
                hr = CpuJitterAdd(args, pcmDataIn, pcmDataOut);
            }

            if (hr < 0) {
                e.Result = string.Format("JitterAdd エラー 0x{0:X8}", hr);
                return;
            }

            sw.Stop();

            WriteWavFile(pcmDataOut, args.outputPath);

            e.Result = string.Format("書き込み成功。処理時間 {0}秒",
                sw.ElapsedMilliseconds * 0.001);
            if (args.scale < 1.0) {
                e.Result = string.Format("書き込み成功。処理時間 {0}秒" +
                    "レベルオーバーのため音量調整{1}dB({2}倍)しました。",
                    sw.ElapsedMilliseconds * 0.001,
                    20.0 * Math.Log10(args.scale), args.scale);
            }
            m_AQworker.ReportProgress(100);
        }

        /// /////////////////////////////////////////////////////////////////
        /// アップサンプル
        /// 

        struct USWorkerArgs {
            public string inputPath;
            public string outputPath;
            public int resampleFrequency;
            public int convolutionN;
            public ProcessDevice device;
            public int sampleSlice;
        };

        private void buttonUSBrowseOpen_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile();
            if (0 < fileName.Length) {
                textBoxUSInputFilePath.Text = fileName;
            }
        }

        private void buttonUSBrowseSaveAs_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseSaveFile();
            if (0 < fileName.Length) {
                textBoxUSOutputFilePath.Text = fileName;
            }
        }

        private void m_USworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            progressBarUS.Value = 0;
            buttonUSCancel.IsEnabled = false;
            buttonUSOutputStart.IsEnabled = true;
            buttonUSBrowseOpen.IsEnabled = true;
            buttonUSBrowseSaveAs.IsEnabled = true;

            if (e.Cancelled) {
                textBoxUSResult.Text += string.Format("キャンセル完了。\r\n");
            } else {
                string result = (string)e.Result;
                textBoxUSResult.Text += string.Format("結果: {0}\r\n", result);
            }
            textBoxUSResult.ScrollToEnd();
        }

        private void m_USworker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBarUS.Value = e.ProgressPercentage;
        }

        private void buttonUSOutputStart_Click(object sender, RoutedEventArgs e) {
            USWorkerArgs args = new USWorkerArgs();
            args.inputPath = textBoxUSInputFilePath.Text;
            args.outputPath = textBoxUSOutputFilePath.Text;
            if (!System.IO.File.Exists(args.inputPath)) {
                MessageBox.Show("エラー。入力ファイルが存在しません");
                return;
            }
            if (!Int32.TryParse(textBoxUSFrequency.Text, out args.resampleFrequency) ||
                    args.resampleFrequency < 0.0) {
                MessageBox.Show("エラー。リサンプル周波数に0以上の数値を入力してください");
                return;
            }

            args.convolutionN = 256;
            args.device = ProcessDevice.Cpu;
            args.sampleSlice = 256;
            if (radioButtonUSCpu16.IsChecked == true) {
                args.convolutionN = 65536;
            }
            if (radioButtonUSGpu16.IsChecked == true) {
                args.convolutionN = 65536;
                args.device = ProcessDevice.Gpu;
                args.sampleSlice = 32768;
            }
            if (radioButtonUSGpu20.IsChecked == true) {
                args.convolutionN = 1048576;
                args.device = ProcessDevice.Gpu;

                // 重いので減らす。
                args.sampleSlice = 256;
            }
            if (radioButtonUSGpu24.IsChecked == true) {
                args.convolutionN = 16777216;
                args.device = ProcessDevice.Gpu;

                // この条件では一度に32768個処理できない。
                // 16384以下の値をセットする。
                args.sampleSlice = 16;
            }

            buttonUSCancel.IsEnabled = true;
            buttonUSOutputStart.IsEnabled = false;
            buttonUSBrowseOpen.IsEnabled = false;
            buttonUSBrowseSaveAs.IsEnabled = false;
            progressBarUS.Value = 0;

            textBoxUSResult.Text += string.Format("処理中 {0}⇒{1}……処理中はPCの動作が重くなります!\r\n",
                args.inputPath, args.outputPath);
            textBoxUSResult.ScrollToEnd();

            m_USworker.RunWorkerAsync(args);
        }

        private int CpuUpsample(USWorkerArgs args, PcmData pcmDataIn, PcmData pcmDataOut) {
            int hr = 0;

            int sampleTotalTo = (int)(args.resampleFrequency * pcmDataIn.NumFrames / pcmDataIn.SampleRate);
            {   // PcmDataOutのサンプルレートとサンプル数を更新する。
                byte[] outSampleArray = new byte[(long)sampleTotalTo * pcmDataOut.NumChannels * 4];
                pcmDataOut.SetSampleArray(sampleTotalTo, outSampleArray);
                pcmDataOut.SampleRate = args.resampleFrequency;
                outSampleArray = null;
            }
            WWUpsampleGpu us = new WWUpsampleGpu();

            float[] sampleData = new float[pcmDataIn.NumFrames];
            for (int ch = 0; ch < pcmDataIn.NumChannels; ++ch) {
                for (int i = 0; i < pcmDataIn.NumFrames; ++i) {
                    sampleData[i] = pcmDataIn.GetSampleValueInFloat(ch, i);
                }

                hr = us.UpsampleCpuSetup(args.convolutionN, sampleData, sampleData.Length,
                    pcmDataIn.SampleRate, pcmDataOut.SampleRate, sampleTotalTo);
                if (hr < 0) {
                    break;
                }

                for (int offs=0; offs < sampleTotalTo; offs += args.sampleSlice) {
                    int sample1 = args.sampleSlice;
                    if (sampleTotalTo - offs < sample1) {
                        sample1 = sampleTotalTo - offs;
                    }
                    if (sample1 < 1) {
                        break;
                    }

                    float[] outFragment = new float[sample1];
                    hr = us.UpsampleCpuDo(offs, sample1, ref outFragment);
                    if (hr < 0) {
                        // ここからreturnしても外のfor文までしか行かない
                        us.UpsampleCpuUnsetup();
                        sampleData = null;
                        break;
                    }
                    if (m_USworker.CancellationPending) {
                        // ここからreturnしても外のfor文までしか行かない
                        us.UpsampleCpuUnsetup();
                        sampleData = null;
                        hr = -1;
                        break;
                    }
                    if (0 <= hr) {
                        // 成功。出てきたデータをpcmDataOutに詰める。
                        for (int j = 0; j < sample1; ++j) {
                            pcmDataOut.SetSampleValueInFloat(ch, offs + j, outFragment[j]);
                        }
                    }
                    outFragment = null;

                    // 10%～99%
                    m_USworker.ReportProgress(
                        10 + (int)(89L * offs / sampleTotalTo + 89L * ch) / pcmDataIn.NumChannels);
                }

                if (m_USworker.CancellationPending) {
                    break;
                }
                if (hr < 0) {
                    break;
                }

                us.UpsampleCpuUnsetup();
            }

            sampleData = null;

            return hr;
        }

        private int GpuUpsample(USWorkerArgs args, PcmData pcmDataIn, PcmData pcmDataOut) {
            int hr = 0;

            int sampleTotalTo = (int)(args.resampleFrequency * pcmDataIn.NumFrames / pcmDataIn.SampleRate);
            {   // PcmDataOutのサンプルレートとサンプル数を更新する。
                byte[] outSampleArray = new byte[(long)sampleTotalTo * pcmDataOut.NumChannels * 4];
                pcmDataOut.SetSampleArray(sampleTotalTo, outSampleArray);
                pcmDataOut.SampleRate = args.resampleFrequency;
                outSampleArray = null;
            }

            float[] sampleData = new float[pcmDataIn.NumFrames];
            for (int ch = 0; ch < pcmDataIn.NumChannels; ++ch) {
                for (int i = 0; i < pcmDataIn.NumFrames; ++i) {
                    sampleData[i] = pcmDataIn.GetSampleValueInFloat(ch, i);
                }

                WWUpsampleGpu us = new WWUpsampleGpu();
                hr = us.Init(args.convolutionN, sampleData, (int)pcmDataIn.NumFrames, pcmDataIn.SampleRate,
                    args.resampleFrequency, sampleTotalTo);
                if (hr < 0) {
                    us.Term();
                    return hr;
                }

                int sampleRemain = sampleTotalTo;
                int offs = 0;
                while (0 < sampleRemain) {
                    int sample1 = args.sampleSlice;
                    if (sampleRemain < sample1) {
                        sample1 = sampleRemain;
                    }
                    hr = us.ProcessPortion(offs, sample1);
                    if (hr < 0) {
                        break;
                    }
                    if (m_USworker.CancellationPending) {
                        us.Term();
                        return -1;
                    }

                    sampleRemain -= sample1;
                    offs += sample1;

                    // 10%～99%
                    m_USworker.ReportProgress(
                        10 + (int)(89L * offs / sampleTotalTo + 89L * ch) / pcmDataIn.NumChannels);
                }

                if (0 <= hr) {
                    float[] output = new float[sampleTotalTo];
                    hr = us.GetResultFromGpuMemory(ref output, sampleTotalTo);
                    if (0 <= hr) {
                        // すべて成功。
                        for (int i = 0; i < pcmDataOut.NumFrames; ++i) {
                            pcmDataOut.SetSampleValueInFloat(ch, i, output[i]);
                        }
                    }
                    output = null;
                }
                us.Term();

                if (hr < 0) {
                    break;
                }
            }
            sampleData = null;

            return hr;
        }

        private void m_USworker_DoWork(object sender, DoWorkEventArgs e) {
            // System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Lowest;

            USWorkerArgs args = (USWorkerArgs)e.Argument;

            PcmData pcmDataIn = ReadWavFile(args.inputPath);
            if (null == pcmDataIn) {
                e.Result = string.Format("WAVファイル 読み込み失敗: {0}", args.inputPath);
                return;
            }

            if (args.resampleFrequency < pcmDataIn.SampleRate) {
                e.Result = string.Format("エラー: ダウンサンプルは対応していません {0} from={1} to={2}",
                    args.inputPath, pcmDataIn.SampleRate, args.resampleFrequency);
                return;
            }
            if (0x7fff0000L < pcmDataIn.NumFrames * 4 * pcmDataIn.NumChannels * args.resampleFrequency / pcmDataIn.SampleRate) {
                e.Result = string.Format("エラー: リサンプル後のファイルサイズが2GBを超えそうなので中断しました {0}",
                    args.inputPath);
                return;
            }

            m_USworker.ReportProgress(1);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            pcmDataIn = pcmDataIn.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SFloat);
            PcmData pcmDataOut = new PcmData();
            pcmDataOut.CopyFrom(pcmDataIn);

            int hr = 0;
            if (args.device == ProcessDevice.Gpu) {
                hr = GpuUpsample(args, pcmDataIn, pcmDataOut);
            } else {
                hr = CpuUpsample(args, pcmDataIn, pcmDataOut);
            }

            if (m_USworker.CancellationPending) {
                e.Result = string.Format("キャンセル完了。");
                e.Cancel = true;
                return;
            }
            if (hr < 0) {
                e.Result = string.Format("Upsample エラー 0x{0:X8}", hr);
                return;
            }
            sw.Stop();

            // 成功した。レベル制限する。
            float scale = pcmDataOut.LimitLevelOnFloatRange();

            try {
                WriteWavFile(pcmDataOut, args.outputPath);
            } catch (IOException ex) {
                // 書き込みエラー。
                e.Result = ex.ToString();
                return;
            }

            e.Result = string.Format("書き込み成功。処理時間 {0}秒",
                sw.ElapsedMilliseconds * 0.001);
            if (scale < 1.0f) {
                e.Result = string.Format("書き込み成功。処理時間 {0}秒" +
                    "レベルオーバーのため音量調整{1}dB({2}倍)しました。",
                    sw.ElapsedMilliseconds * 0.001,
                    20.0 * Math.Log10(scale), scale);
            }
            m_USworker.ReportProgress(100);
        }

        private void buttonUSCancel_Click(object sender, RoutedEventArgs e) {
            m_USworker.CancelAsync();
            buttonUSCancel.IsEnabled = false;
        }
    }
}
