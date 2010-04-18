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

namespace sqwave2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.ComponentModel.BackgroundWorker backgroundWorker1;

        public MainWindow() {
            InitializeComponent();
            backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(backgroundWorker1_DoWork);
            backgroundWorker1.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.backgroundWorker1_RunWorkerCompleted);

            textBoxOutput.Text = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal) + "\\output.wav";
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

            textBoxOutput.Text = path;
        }

        struct Settings
        {
            public int seconds;
            public int sampleRate;
            public int bitsPerSample;
            public double dB;
            public double freq;
            public SignalShape ss;
            public string path;
        };

        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            int seconds = 0;
            try {
                seconds = System.Convert.ToInt32(textBoxSeconds.Text);
            } catch (System.InvalidOperationException ex) {
                MessageBox.Show("エラー: 長さには 0よりも大きい整数を入力してください");
                return;
            }
            int sampleRate = System.Convert.ToInt32(((ListBoxItem)listBoxSampleFreq.SelectedItem).Content);
            int bitsPerSample = System.Convert.ToInt32(((ListBoxItem)listBoxBits.SelectedItem).Content);
            double dB = System.Convert.ToDouble(textBoxLevel.Text);
            double freq = System.Convert.ToDouble(textBoxFreq.Text);
            SignalShape ss = (SignalShape)listBoxShape.SelectedIndex;

            Settings s;
            s.seconds = seconds;
            s.sampleRate = sampleRate;
            s.bitsPerSample = bitsPerSample;
            s.dB = dB;
            s.freq = freq;
            s.ss = ss;
            s.path = textBoxOutput.Text;

            if (sampleRate <= freq * 2) {
                MessageBox.Show("エラー: 信号周波数をサンプリング周波数の半分未満にしてください");
                return;
            }

            switch (bitsPerSample) {
            case 16:
                if (dB < -96.0 || 0.0 < dB) {
                    MessageBox.Show("エラー: 出力レベルには -96～0の範囲の数値を入力してください");
                    return;
                }
                break;
            case 24:
                if (dB < -120.0 || 0.0 < dB) {
                    MessageBox.Show("エラー: 出力レベルには -120～0の範囲の数値を入力してください");
                    return;
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            textBoxLog.Text = "";

            if (ss == SignalShape.SquareWave) {

                double harmonics = 0;
                for (int i = 1; ; ++i) {
                    harmonics = 2 * i - 1;
                    double level = dB + 20 * Math.Log10(1.0 / harmonics);
                    if (harmonics * freq < sampleRate/2 &&
                        -96.0 < level) {
                        textBoxLog.Text += string.Format("第{0}次高調波: {1}Hz {2:0.0}dB\r\n", harmonics, harmonics * freq, level);
                    } else {
                        break;
                    }
                }
                if (harmonics <= 5) {
                    textBoxLog.Text += string.Format("あまり矩形波っぽい形にはなりません\r\n");
                }
            }

            textBoxLog.Text += string.Format("書き込み開始: {0}\r\n", s.path);
            buttonStart.IsEnabled = false;
            backgroundWorker1.RunWorkerAsync(s);
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e) {
            Settings s = (Settings)e.Argument;
            int amplitude
                = (int)(((2 << (s.bitsPerSample - 2)) - 1) * Math.Pow(10, s.dB / 20.0));

            WavData wavData;
            CreateWavDataResult cwdr
                = CreateWavData(s.sampleRate, s.bitsPerSample, s.ss, s.freq, amplitude, s.seconds, out wavData);

            string resultString = "書き込むデータの準備: ";
            switch (cwdr) {
            case CreateWavDataResult.Success:
                resultString += "成功\r\n";
                break;
            case CreateWavDataResult.LevelOver:
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

        ////////////////////////////////////////////////////////

        enum CreateWavDataResult
        {
            Success,
            LevelOver
        }

        enum SignalShape {
            SineWave,
            SquareWave,
        };

        private CreateWavDataResult CreateWavData(int sampleRate, int bitsPerSample, SignalShape ss, double freq, int amplitude, int seconds, out WavData wavData) {
            List<PcmSamples1Channel> samples = new List<PcmSamples1Channel>();

            int nSample = seconds * sampleRate;
            PcmSamples1Channel ch = new PcmSamples1Channel(nSample, bitsPerSample);
            samples.Add(ch);

            CreateWavDataResult result = CreateWavDataResult.Success;

            switch (ss) {
            case SignalShape.SineWave:
                result = CreateSineWave(ch, sampleRate, freq, amplitude);
                break;
            case SignalShape.SquareWave:
                result = CreateSquareWave(ch, sampleRate, freq, amplitude);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            wavData = new WavData();
            wavData.Create(sampleRate, bitsPerSample, samples);
            return result;
        }

        private CreateWavDataResult CreateSineWave(PcmSamples1Channel ch, int sampleRate, double freq, int amplitude) {
            Console.WriteLine("CreateSineWave sampleRate={0} freq={1} amp={2}", sampleRate, freq, amplitude);

            CreateWavDataResult result = CreateWavDataResult.Success;

            double step = 2.0 * Math.PI * (freq / sampleRate);
            for (int i = 0; i < ch.NumSamples; ++i) {
                int v = (int)(amplitude * Math.Sin(step * i));
                short sv = (short)v;
                if (v < -32768) {
                    result = CreateWavDataResult.LevelOver;
                    sv = -32768;
                }
                if (32767 < v) {
                    result = CreateWavDataResult.LevelOver;
                    sv = 32767;
                }
                ch.Set16(i, sv);
            }
            return result;
        }

        private CreateWavDataResult CreateSquareWave(PcmSamples1Channel ch, int sampleRate, double freq, int amplitude) {
            Console.WriteLine("CreateSquareWave sampleRate={0} freq={1} amp={2}", sampleRate, freq, amplitude);

            CreateWavDataResult result = CreateWavDataResult.Success;
            double step = 2.0 * Math.PI * (freq / sampleRate);
            for (int i = 0; i < ch.NumSamples; ++i) {
                double v = 0.0;
                for (int h=1; ; ++h) {
                    double harmonics = 2 * h - 1;
                    if (amplitude / harmonics < 1.0) {
                        break;
                    }
                    if (sampleRate/2 <= harmonics * freq) {
                        break;
                    }
                    double x = amplitude / harmonics * Math.Sin(step * i * harmonics);
                    v += x;
                }

                short sv = (short)v;
                if (v < -32768) {
                    result = CreateWavDataResult.LevelOver;
                    sv = -32768;
                }
                if (32767 < v) {
                    result = CreateWavDataResult.LevelOver;
                    sv = 32767;
                }
                ch.Set16(i, sv);
            }

            return result;
        }
    }
}
