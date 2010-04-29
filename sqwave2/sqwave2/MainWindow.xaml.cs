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
using AsioCS;
using System.Reflection;

namespace sqwave2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private AsioWrap aw;

        private bool uiInitialized = false;

        enum OutputMode
        {
            WavFile,
            Asio,
            NUM
        }

        OutputMode mode = OutputMode.WavFile;

        enum AsioStatus
        {
            NotReady,
            Ready
        }

        AsioStatus s;

        struct OutputFormat
        {
            public int sampleRate;
            public int bitsPerSample;

            public void Set(int sampleRate, int bitsPerSample) {
                this.sampleRate = sampleRate;
                this.bitsPerSample = bitsPerSample;
            }
        }

        // 出力フォーマットのマスターデータ(ASIOとWAVで切り替わるので)
        OutputFormat [] outputFormats;

        public MainWindow() {
            InitializeComponent();

            outputFormats = new OutputFormat[(int)OutputMode.NUM];
            outputFormats[0].Set(192000, 16);
            outputFormats[1].Set(0, 0);

            aw = new AsioWrap();

            backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(backgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);

            textBoxOutputFilePath.Text = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "\\output.wav";
            uiInitialized = true;
            UpdateUIStatus();
        }

        private void UpdateUIStatus() {
            if (!uiInitialized) {
                return;
            }

            switch (mode) {
            case OutputMode.WavFile:
                textBoxOutputFilePath.IsEnabled = true;
                listBoxAsioDevices.IsEnabled = false;
                listBoxAsioChannels.IsEnabled = false;
                buttonRef.IsEnabled = true;
                textBoxSeconds.IsEnabled = true;

                break;
            case OutputMode.Asio:
                textBoxOutputFilePath.IsEnabled = false;
                listBoxAsioDevices.IsEnabled = true;
                listBoxAsioChannels.IsEnabled = true;
                buttonRef.IsEnabled = false;
                textBoxSeconds.IsEnabled = false;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            {
                // outputFormats[mode]に入っている情報を元に出力フォーマットにチェックを入れる。
                int sampleRate = outputFormats[(int)mode].sampleRate;
                for (int i = 0; i < listBoxSampleFreq.Items.Count; ++i) {
                    ListBoxItem lbi = (ListBoxItem)listBoxSampleFreq.Items[i];
                    int v = Convert.ToInt32(lbi.Content);

                    if (v == sampleRate && listBoxSampleFreq.SelectedIndex != i) {
                        listBoxSampleFreq.SelectedIndex = i;
                    }
                }
            }

            {
                // outputFormats[mode]に入っている情報を元に量子化ビット数にチェックを入れる。
                int bitsPerSample = outputFormats[(int)mode].bitsPerSample;
                for (int i = 0; i < listBoxBits.Items.Count; ++i) {
                    ListBoxItem lbi = (ListBoxItem)listBoxBits.Items[i];
                    int v = Convert.ToInt32(lbi.Content);

                    if (v == bitsPerSample && listBoxBits.SelectedIndex != i) {
                        listBoxBits.SelectedIndex = i;
                    }
                }
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

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            int seconds = 0;
            try {
                seconds = System.Convert.ToInt32(textBoxSeconds.Text);
            } catch (System.Exception ex) {
                MessageBox.Show("エラー: 長さには 0よりも大きい整数を半角で入力してください");
                return;
            }
            if (seconds <= 0) {
                MessageBox.Show("エラー: 長さには 0よりも大きい整数を半角で入力してください");
                return;
            }

            int sampleRate = System.Convert.ToInt32(((ListBoxItem)listBoxSampleFreq.SelectedItem).Content);
            int bitsPerSample = System.Convert.ToInt32(((ListBoxItem)listBoxBits.SelectedItem).Content);
            
            double dB = 0;
            try {
                dB = System.Convert.ToDouble(textBoxLevel.Text);
            } catch (System.Exception ex) {
                MessageBox.Show("エラー: 出力レベルには 数値を半角で入力してください");
                return;
            }
            double freq = 0;
            try {
                freq = System.Convert.ToDouble(textBoxFreq.Text);
            } catch (System.Exception ex) {
                MessageBox.Show("エラー: 信号周波数には0.0001以上の数値を半角で入力してください");
                return;
            }
            if (freq < 0.0001) {
                MessageBox.Show("エラー: 信号周波数には0.0001以上の数値を半角で入力してください");
                return;
            }

            SignalShape ss = (SignalShape)listBoxShape.SelectedIndex;

            double trunc = 0;
            try {
                trunc = System.Convert.ToDouble(textBoxTrunc.Text);
            } catch (System.Exception ex) {
                MessageBox.Show("エラー: 級数加算打ち切り％には0.0～100.0の範囲の数値を半角で入力してください");
                return;
            }
            if (trunc < 0.0 || 100.0 < trunc) {
                MessageBox.Show("エラー: 級数加算打ち切り％には0.0～100.0の範囲の数値を半角で入力してください");
                return;
            }

            Settings s;
            s.seconds = seconds;
            s.sampleRate = sampleRate;
            s.bitsPerSample = bitsPerSample;
            s.dB = dB;
            s.freq = freq;
            s.ss = ss;
            s.path = textBoxOutputFilePath.Text;
            s.truncationRatio = trunc * 0.01;
            s.amplitude = (int)(((2 << (s.bitsPerSample - 2)) - 1) * Math.Pow(10, s.dB / 20.0));

            if (sampleRate <= freq * 2) {
                MessageBox.Show("エラー: 信号周波数をサンプリング周波数の半分未満にしてください");
                return;
            }

            switch (bitsPerSample) {
            case 16:
                if (dB < -96.0) {
                    MessageBox.Show("エラー: 出力レベルには -96.0以上の数値を入力してください");
                    return;
                }
                break;
            case 24:
                if (dB < -120.0) {
                    MessageBox.Show("エラー: 出力レベルには -120.0以上の数値を入力してください");
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

            textBoxLog.Text += string.Format("書き込み開始: {0}\r\n", s.path);
            buttonStart.IsEnabled = false;
            backgroundWorker1.RunWorkerAsync(s);
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e) {
            Settings s = (Settings)e.Argument;
            SignalGenerator sg = new SignalGenerator();

            WavData wavData;
            SignalGeneratorResult cwdr
                = sg.GenerateSignal(s, out wavData);

            string resultString = "書き込むデータの準備: ";
            switch (cwdr) {
            case SignalGeneratorResult.Success:
                resultString += "成功\r\n";
                break;
            case SignalGeneratorResult.LevelOver:
                resultString += "レベルオーバーでクリップしました。出力レベルを下げてください\r\n";
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            if (!WriteWavFile(wavData, s.path)) {
                e.Result = resultString + string.Format("書き込み失敗: {0}\r\n", s.path);
                return;
            }
            e.Result = resultString + string.Format("書き込み成功: {0}\r\n", s.path);
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            textBoxLog.Text += (string)e.Result;
            buttonStart.IsEnabled = true;
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
            AssemblyProduct, AssemblyVersion, aw.AsioTrademarkStringGet()));
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
            mode = OutputMode.WavFile;
            UpdateUIStatus();
        }

        private void radioButtonOutAsio_Checked(object sender, RoutedEventArgs e) {
            mode = OutputMode.Asio;

            bool rv = AsioInit();

            if (rv) {
            }
            UpdateUIStatus();
        }

    }
}
