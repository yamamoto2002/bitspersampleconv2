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
using System.Windows.Shapes;
using Wasapi;
using System.Security.Cryptography;
using System.IO;
using WavRWLib2;
using PcmDataLib;

namespace PlayPcmWin {
    /// <summary>
    /// ABX.xaml の相互作用ロジック
    /// </summary>
    public partial class ABX : Window {
        Wasapi.WasapiCS wasapi;
        RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();

        enum AB {
            Unknown = -1,
            A,
            B
        };

        struct TestInfo {
            public AB x;
            public AB answer;

            public TestInfo(AB x) {
                this.x      = x;
                answer = AB.Unknown;
            }
        }

        List<TestInfo> m_testInfoList = new List<TestInfo>();

        public ABX() {
            InitializeComponent();
        }

        private void ComboBoxDeviceInit(ComboBox comboBox) {
            comboBox.Items.Clear();

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                string deviceName = wasapi.GetDeviceName(i);
                comboBox.Items.Add(deviceName);
            }
            comboBox.SelectedIndex = 0;
        }

        private void ListDevices() {
            int hr = wasapi.DoDeviceEnumeration(WasapiCS.DeviceType.Play);

            ComboBoxDeviceInit(comboBoxDeviceA);
            ComboBoxDeviceInit(comboBoxDeviceB);
        }

        public void Prepare(Wasapi.WasapiCS wasapi) {
            this.wasapi = wasapi;
            ListDevices();

            byte [] r = new byte[1];
            gen.GetBytes(r);

            AB x = ((r[0] & 1) == 0) ? AB.A : AB.B;
            TestInfo ti = new TestInfo(x);

            m_testInfoList.Clear();
            m_testInfoList.Add(ti);

            UpdateUIStatus();
        }

        enum Status {
            Stop,
            Play,
        }

        private Status m_status = Status.Stop;

        public void UpdateUIStatus() {
            labelGuide.Content = string.Format("テスト {0}回目", m_testInfoList.Count);

            if (m_status == Status.Stop) {
                // Stop
                buttonPlayA.IsEnabled = false;
                buttonPlayB.IsEnabled = false;
                buttonPlayX.IsEnabled = false;
                buttonPlayY.IsEnabled = false;
                buttonConfirm.IsEnabled = false;
                buttonStop.IsEnabled = false;

                if (0 < textBoxPathA.Text.Length && System.IO.File.Exists(textBoxPathA.Text)) {
                    buttonPlayA.IsEnabled = true;
                }
                if (0 < textBoxPathA.Text.Length && System.IO.File.Exists(textBoxB.Text)) {
                    buttonPlayB.IsEnabled = true;
                }
                if (buttonPlayA.IsEnabled && buttonPlayB.IsEnabled) {
                    buttonPlayX.IsEnabled = true;
                    buttonPlayY.IsEnabled = true;
                    buttonConfirm.IsEnabled = true;
                }
            } else {
                // Play
                buttonPlayA.IsEnabled = true;
                buttonPlayB.IsEnabled = true;
                buttonPlayX.IsEnabled = true;
                buttonPlayY.IsEnabled = true;
                buttonStop.IsEnabled = true;
            }
        }

        private void buttonFinish_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private string BrowseFile() {
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

        private void buttonBrowseA_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseFile();
            if (0 < fileName.Length) {
                textBoxPathA.Text = fileName;
                UpdateUIStatus();
            }
        }

        private void buttonBrowseB_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseFile();
            if (0 < fileName.Length) {
                textBoxB.Text = fileName;
                UpdateUIStatus();
            }
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            if (m_status == Status.Play) {
                m_status = Status.Stop;
                buttonStop.IsEnabled = false;
                wasapi.Stop();
            }
        }
        
        private int WasapiSetup(
                bool isExclusive,
                bool isEventDriven,
                int sampleRate,
                WasapiCS.SampleFormatType sampleFormat,
                int latencyMillisec) {
            wasapi.SetShareMode(
                isExclusive ? WasapiCS.ShareMode.Exclusive : WasapiCS.ShareMode.Shared);
            wasapi.SetSchedulerTaskType(
                WasapiCS.SchedulerTaskType.ProAudio);

            int hr = wasapi.Setup(
                isEventDriven ? WasapiCS.DataFeedMode.EventDriven : WasapiCS.DataFeedMode.TimerDriven,
                sampleRate, sampleFormat, latencyMillisec, 2);
            if (hr < 0) {
                wasapi.Unsetup();
                return hr;
            }
            return hr;
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

        private void buttonPlayA_Click(object sender, RoutedEventArgs e) {
            PcmData pcmData = ReadWavFile(textBoxPathA.Text);
            if (null == pcmData) {
                MessageBox.Show(
                    string.Format("WAVファイル A 読み込み失敗: {0}", textBoxPathA.Text));
                return;
            }
            /*
            WasapiSetup(radioButtonExclusiveA.IsChecked == true,
                radioButtonEventDrivenA.IsChecked == true,
                pcmData.SampleRate, pcmData.BitsPerSample, Int32.Parse(textBoxLatencyA.Text));
             */
        }

        private void buttonPlayB_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonPlayX_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonPlayY_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonConfirm_Click(object sender, RoutedEventArgs e) {

        }
    }
}
