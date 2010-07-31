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
using Wasapiex;
using WavRWLib2;
using System.IO;
using System.ComponentModel;

namespace RecPcmWin {
    public partial class MainWindow : Window {
        private WasapiCS wasapi;

        WavData m_wavData = null; 

        const int DEFAULT_OUTPUT_LATENCY_MS = 200;
        int m_samplingFrequency     = 44100;
        int m_samplingBitsPerSample = 16;

        public MainWindow() {
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

            hr = wasapi.DoDeviceEnumeration(WasapiCS.DeviceType.Rec);
            textBoxLog.Text += string.Format("wasapi.DoDeviceEnumeration(Rec) {0:X8}\r\n", hr);

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                listBoxDevices.Items.Add(wasapi.GetDeviceName(i));
            }

            buttonDeviceSelect.IsEnabled = true;
            buttonDeselect.IsEnabled = false;
            buttonRec.IsEnabled = false;
            buttonStop.IsEnabled = false;
            groupBoxWasapiSettings.IsEnabled = true;
            buttonInspectDevice.IsEnabled = false;

            if (0 < nDevices) {
                if (0 <= selectedIndex && selectedIndex < listBoxDevices.Items.Count) {
                    listBoxDevices.SelectedIndex = selectedIndex;
                } else {
                    listBoxDevices.SelectedIndex = 0;
                }

                if (m_wavData != null) {
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

        private void buttonDeviceSelect_Click(object sender, RoutedEventArgs e) {
            int latencyMillisec = -1;
            try {
                latencyMillisec = Int32.Parse(textBoxLatency.Text);
            } catch (Exception ex) {
                textBoxLog.Text += string.Format("{0}\r\n", ex);
            }
            if (latencyMillisec <= 0) {
                latencyMillisec = DEFAULT_OUTPUT_LATENCY_MS;
                textBoxLatency.Text = string.Format("{0}", DEFAULT_OUTPUT_LATENCY_MS);
            }

            int hr = wasapi.ChooseDevice(listBoxDevices.SelectedIndex);
            textBoxLog.Text += string.Format("wasapi.ChooseDevice({0}) {1:X8}\r\n",
                listBoxDevices.SelectedItem.ToString(), hr);
            if (hr < 0) {
                return;
            }

            WasapiCS.DataFeedMode dfm = WasapiCS.DataFeedMode.EventDriven;
            if (true == radioButtonTimerDriven.IsChecked) {
                dfm = WasapiCS.DataFeedMode.TimerDriven;
            }

            hr = wasapi.Setup(dfm, m_samplingFrequency, m_samplingBitsPerSample, latencyMillisec);
            textBoxLog.Text += string.Format("wasapi.Setup({0}, {1}, {2}, {3}) {4:X8}\r\n",
                m_samplingFrequency, m_samplingBitsPerSample, latencyMillisec, dfm, hr);
            if (hr < 0) {
                wasapi.Unsetup();
                textBoxLog.Text += string.Format("wasapi.Unsetup()\r\n");
                CreateDeviceList();
                string sDfm = DfmToStr(dfm);
                string s = string.Format("エラー: wasapi.Setup({0}, {1}, {2}, {3})失敗。{4:X8}\nこのプログラムのバグか、オーディオデバイスが{0}Hz {1}bit レイテンシー{2}ms {3}に対応していないのか、どちらかです。\r\n",
                    m_samplingFrequency, m_samplingBitsPerSample,
                    latencyMillisec, sDfm, hr);
                textBoxLog.Text += s;
                MessageBox.Show(s);
                return;
            }

            buttonDeviceSelect.IsEnabled     = false;
            buttonDeselect.IsEnabled         = true;
            buttonRec.IsEnabled              = true;
            buttonInspectDevice.IsEnabled    = false;
            groupBoxWasapiSettings.IsEnabled = false;
        }

        private void buttonDeviceDeselect_Click(object sender, RoutedEventArgs e) {
            wasapi.Stop();
            wasapi.Unsetup();
            CreateDeviceList();
        }

        private void buttonInspectDevice_Click(object sender, RoutedEventArgs e) {
            string dn = wasapi.GetDeviceName(listBoxDevices.SelectedIndex);
            string s = wasapi.InspectDevice(listBoxDevices.SelectedIndex);
            textBoxLog.Text += string.Format("wasapi.InspectDevice()\r\n{0}\r\n{1}\r\n", dn, s);
        }

        BackgroundWorker bw;

        private int m_bufferBytes = -1;

        private void buttonRec_Click(object sender, RoutedEventArgs e) {
            try {
                m_bufferBytes = Int32.Parse(textBoxRecMaxMB.Text) * 1024 * 1024;
            } catch (Exception ex) {
                textBoxLog.Text += string.Format("{0}\r\n", ex);
            }
            if (m_bufferBytes < 0) {
                m_bufferBytes = 1024*1024*1024;
                textBoxRecMaxMB.Text = "1024";
            }
            wasapi.SetupCaptureBuffer(m_bufferBytes);
            textBoxLog.Text += string.Format("wasapi.SetupCaptureBuffer() {0:X8}\r\n", m_bufferBytes);

            int hr = wasapi.Start();
            textBoxLog.Text += string.Format("wasapi.Start() {0:X8}\r\n", hr);
            if (hr < 0) {
                return;
            }

            slider1.Value = 0;
            slider1.Maximum = wasapi.GetTotalFrameNum();
            buttonStop.IsEnabled     = true;
            buttonRec.IsEnabled      = false;
            buttonDeselect.IsEnabled = false;

            bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.DoWork += new DoWorkEventHandler(DoWork);
            bw.ProgressChanged += new ProgressChangedEventHandler(ProgressChanged);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RunWorkerCompleted);
            bw.RunWorkerAsync();
        }

        private void ProgressChanged(object o, ProgressChangedEventArgs args) {
            if (null == wasapi) {
                return;
            }
            slider1.Value = wasapi.GetPosFrame();
            label1.Content = string.Format("{0:F1}/{1:F1}",
                slider1.Value / m_samplingFrequency,
                slider1.Maximum / m_samplingFrequency);
        }

        private void RunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            textBoxLog.Text += string.Format("Rec completed.\r\n");

            SaveRecordedData();

            buttonRec.IsEnabled = true;
            buttonStop.IsEnabled = false;
            buttonDeselect.IsEnabled = true;
        }

        private void DoWork(object o, DoWorkEventArgs args) {
            Console.WriteLine("DoWork started");

            while (!wasapi.Run(200)) {
                bw.ReportProgress(0);
                System.Threading.Thread.Sleep(1);
            }

            wasapi.Stop();

            Console.WriteLine("DoWork end");
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            buttonStop.IsEnabled = false;

            wasapi.Stop();
            textBoxLog.Text += string.Format("wasapi.Stop()\r\n");

        }

        private void SaveRecordedData() {
            byte[] capturedPcmData = new byte[m_bufferBytes];
            int bytes = wasapi.GetCapturedData(capturedPcmData);
            int nFrames = bytes / (m_samplingBitsPerSample / 8) / 2; // 2==2ch

            if (nFrames == 0) {
                return;
            }

            textBoxLog.Text += string.Format("captured frames={0} glichCount={1}\r\n",
                nFrames, wasapi.GetCaptureGlitchCount());

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "WAVEファイル|*.wav";

            Nullable<bool> result = dlg.ShowDialog();

            if (result != true) {
                return;
            }
            
            m_wavData = new WavData();
            List<PcmSamples1Channel> samples = new List<PcmSamples1Channel>();
            PcmSamples1Channel sL = new PcmSamples1Channel(nFrames, m_samplingBitsPerSample);
            PcmSamples1Channel sR = new PcmSamples1Channel(nFrames, m_samplingBitsPerSample);

            switch (m_samplingBitsPerSample) {
            case 16: {
                    int pos = 0;
                    short v;
                    for (int i = 0; i < nFrames; ++i) {
                        v = (short)(capturedPcmData[pos] + capturedPcmData[pos+1] * 256);
                        pos += 2;
                        sL.Set16(i, v);
                        v = (short)(capturedPcmData[pos] + capturedPcmData[pos + 1] * 256);
                        pos += 2;
                        sR.Set16(i, v);
                    }
                }
                break;
            case 32: {
                    int pos = 0;
                    int v;
                    for (int i = 0; i < nFrames; ++i) {
                        v = capturedPcmData[pos]
                            + capturedPcmData[pos + 1] * (1 << 8)
                            + capturedPcmData[pos + 2] * (1 << 16)
                            + capturedPcmData[pos + 3] * (1 << 24);
                        pos += 4;
                        sL.Set32(i, v);
                        v = capturedPcmData[pos]
                            + capturedPcmData[pos + 1] * (1 << 8)
                            + capturedPcmData[pos + 2] * (1 << 16)
                            + capturedPcmData[pos + 3] * (1 << 24);
                        pos += 4;
                        sR.Set32(i, v);
                    }
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
            samples.Add(sL);
            samples.Add(sR);

            try {
                m_wavData.Create(m_samplingFrequency, m_samplingBitsPerSample, samples);
                using (BinaryWriter w = new BinaryWriter(File.Open(dlg.FileName, FileMode.Create))) {
                    m_wavData.Write(w);
                }
            } catch (Exception ex) {
                string s = string.Format("ファイル保存失敗: {0}\r\n{1}\r\n", dlg.FileName, ex);
                textBoxLog.Text +=s;
                MessageBox.Show(s);
            }

            slider1.Value = 0;
            label1.Content = "0/0";
        }

        private void radioButton44100_Checked(object sender, RoutedEventArgs e) {
            m_samplingFrequency = 44100;
        }

        private void radioButton48000_Checked(object sender, RoutedEventArgs e) {
            m_samplingFrequency = 48000;
        }

        private void radioButton88200_Checked(object sender, RoutedEventArgs e) {
            m_samplingFrequency = 88200;
        }

        private void radioButton96000_Checked(object sender, RoutedEventArgs e) {
            m_samplingFrequency = 96000;
        }

        private void radioButton176400_Checked(object sender, RoutedEventArgs e) {
            m_samplingFrequency = 176400;
        }

        private void radioButton192000_Checked(object sender, RoutedEventArgs e) {
            m_samplingFrequency = 192000;
        }

        private void radioButton16_Checked(object sender, RoutedEventArgs e) {
            m_samplingBitsPerSample = 16;
        }

        private void radioButton32_Checked(object sender, RoutedEventArgs e) {
            m_samplingBitsPerSample = 32;
        }
    }
}
