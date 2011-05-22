// 日本語UTF-8

using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PcmDataLib;
using WWDirectComputeCS;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        //////////////////////////////////////////////////////////////////////////////////////////
        // FIR

        private const int m_firTapN = 8192;

        // 20Hz～20kHzの範囲の、周波数が対数軸で等間隔になるようにサンプルしたデータが並んでいるテーブル
        private double [] m_freqGainTable;

        Point m_prevFreqGainPress = new Point(-1, -1);

        private void InitializeFirTable() {
            m_freqGainTable = new double[512];
            for (int i=0; i < m_freqGainTable.Length; ++i) {
                m_freqGainTable[i] = 1.0;
            }
        }

        /// <summary>
        /// freqTableの最新状態を画面表示する。
        /// </summary>
        private void DrawFreqResponse() {
            double width  = rectangleFreqGain.Width;
            double height = rectangleFreqGain.Height;

            freqGainLine.Points.Clear();
            for (int i=0; i < m_freqGainTable.Length; ++i) {
                // 周波数軸は対数なので、そのまま等間隔にプロットする

                double x = width * i / m_freqGainTable.Length;

                {   // 利得。
                    double v = m_freqGainTable[i];
                    /* 約±12dBぐらいの範囲にする */
                    if (v < 0.25) {
                        v = 0.25;
                    }
                    if (v > 4) {
                        v = 4;
                    }
                    double y = height / 2 - height / 4 * Math.Log(v, 2);

                    var pointG = new Point(x, y);
                    freqGainLine.Points.Add(pointG);
                }
            }
        }

        /// <summary>
        /// グラフから読み取る。周波数→振幅
        /// </summary>
        private double GetValueOnFreq(double[] freqValueTable, double freq) {
            int width = freqValueTable.Length;
            double lowestFreq = 20;
            double highestFreq = 20000;
            double octave = 3;
            if (freq < lowestFreq) {
                return freqValueTable[0];
            }
            if (highestFreq <= freq) {
                return freqValueTable[freqValueTable.Length - 1];
            }

            int x = (int)((Math.Log10(freq) - Math.Log10(lowestFreq)) * width / octave);
            if (x < 0) {
                System.Diagnostics.Debug.Assert(0 <= x);
                return freqValueTable[0];
            }
            if (width <= x) {
                // 起こらないのではないだろうか？
                return freqValueTable[freqValueTable.Length - 1];
            }

            return freqValueTable[x];
        }

        double GetRealPartOnFreq(double freq) {
            var amp = GetValueOnFreq(m_freqGainTable, freq);
            var phase = 0;
            return amp * Math.Cos(phase);
        }

        double GetImaginaryPartOnFreq(double freq) {
            var amp = GetValueOnFreq(m_freqGainTable, freq);
            var phase = 0;
            return amp * Math.Sin(phase);
        }

        /// <summary>
        /// 周波数グラフからIDFT入力パラメータ配列を作成。
        /// IDFT入力パラメータは複素数。r0, i0, r1, i1,…の順で並ぶ。
        /// </summary>
        /// <returns></returns>
        private double[] FreqGraphToIdftInput(int sampleRate) {
            System.Diagnostics.Debug.Assert((m_firTapN & 1) == 0);

            var result = new double[m_firTapN * 2];
            result[0] = GetRealPartOnFreq(0);
            result[1] = GetImaginaryPartOnFreq(0);
            for (int i=1; i <= m_firTapN / 2; ++i) {
                // 周波数は、リニアスケール
                // i==result.Length/2のとき freq = sampleRate/2 これが最大周波数。
                // 左右対称な感じで折り返す。
                var freq = (double)i * sampleRate / 2 / (m_firTapN / 2);

                var re = GetRealPartOnFreq(freq);
                var im = GetImaginaryPartOnFreq(freq);
                result[i * 2 + 0] = re;
                result[i * 2 + 1] = im;
                result[result.Length - i * 2 + 0] = re;
                result[result.Length - i * 2 + 1] = im;
            }
            return result;
        }

        private delegate double MouseYToValue(double mouseY);

        // マウスy座標→増幅率(倍)
        private double MouseYToMag(double y) {
            // yが最大 = 12dB  = 4x    = 2^2
            // yが中央 = -12dB = 1x    = 2^(0)
            // yが最小 = -12dB = 0.25x = 2^(-2)
            double vPowMag = 2;

            double yReg = (rectangleFreqGain.Height * 0.5 - y) / (rectangleFreqGain.Height * 0.5);
            double v = Math.Pow(2.0, vPowMag * yReg);
            if (v < Math.Pow(2.0, -vPowMag)) {
                v = Math.Pow(2.0, -vPowMag);
            }
            if (Math.Pow(2.0, vPowMag) < v) {
                v = Math.Pow(2.0, vPowMag);
            }

            return v;
        }

        /// <summary>
        /// 周波数特性テーブルfreqTableに(px0, y0)-(px1, y1)の線を引く。
        /// </summary>
        private void UpdateFreqResponse(System.Windows.Shapes.Rectangle rectGraph, double[] freqTable, double px0, double y0, double px1, double y1) {
            int x0 = (int)(px0 * freqTable.Length / rectGraph.Width);
            int x1 = (int)(px1 * freqTable.Length / rectGraph.Width);
            if (x0 != x1) {
                double dy = (y1 - y0) / ((double)x1 - x0);
                if (x0 < x1) {
                    for (int x=x0; x < x1; ++x) {
                        freqTable[x] = y0 + dy * (x - x0);
                    }
                }
                if (x1 < x0) {
                    for (int x=x0; x > x1; --x) {
                        freqTable[x] = y0 + dy * (x - x0);
                    }
                }
            }
            freqTable[x1] = y1;
        }

        private void FreqResponseMouseUpdate(System.Windows.Shapes.Rectangle rectGraph, double[] freqTable, ref Point prevPress, MouseYToValue YtoV, int mx, int my) {
            var pos = new Point(mx - Canvas.GetLeft(rectGraph), my - Canvas.GetTop(rectGraph));

            if (0 <= pos.X && pos.X < rectGraph.Width &&
                0 <= prevPress.X && prevPress.X < rectGraph.Width &&
                0 <= pos.Y && pos.Y < rectGraph.Height &&
                0 <= prevPress.Y && prevPress.Y < rectGraph.Height) {

                double prevY = YtoV(prevPress.Y);
                double nowY  = YtoV(pos.Y);

                UpdateFreqResponse(rectGraph, freqTable, prevPress.X, prevY, pos.X, nowY);
                DrawFreqResponse();
            }
            prevPress = pos;
        }

        private void FirCanvasMouseUpdate(int mx, int my) {
            System.Diagnostics.Debug.Assert(rectangleFreqGain.Width == m_freqGainTable.Length);

            FreqResponseMouseUpdate(rectangleFreqGain, m_freqGainTable, ref m_prevFreqGainPress, MouseYToMag, mx, my);
        }

        private void buttonFirFlat_Click(object sender, RoutedEventArgs e) {
            InitializeFirTable();
            DrawFreqResponse();
        }


        private void canvas1_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            System.Diagnostics.Debug.Assert(rectangleFreqGain.Width == m_freqGainTable.Length);

            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) {
                return;
            }

            var pos = e.GetPosition(canvas1);
            m_prevFreqGainPress = new Point(pos.X - Canvas.GetLeft(rectangleFreqGain), pos.Y - Canvas.GetTop(rectangleFreqGain));
            FirCanvasMouseUpdate((int)pos.X, (int)pos.Y);
        }

        private void canvas1_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) {
                return;
            }

            var pos = e.GetPosition(canvas1);
            FirCanvasMouseUpdate((int)pos.X, (int)pos.Y);
        }

        private void canvas1_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            m_prevFreqGainPress.X = -1;
            m_prevFreqGainPress.Y = -1;
        }

        private void buttonFirDo_Click(object sender, RoutedEventArgs e) {
            var args = new FirWorkerArgs();
            args.inputPath = textBoxFirInputPath.Text;
            args.outputPath = textBoxFirOutputPath.Text;

            textBoxFirLog.Text += string.Format("開始。{0} → {1}\r\n", args.inputPath, args.outputPath);
            buttonFirDo.IsEnabled = false;
            m_FirWorker.RunWorkerAsync(args);
        }

        void m_FirWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            string s = (string)e.Result;
            textBoxFirLog.Text += s + "\r\n";
            progressBarFir.Value = 0;
            buttonFirDo.IsEnabled = true;
        }

        void m_FirWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBarFir.Value = e.ProgressPercentage;
        }

        struct FirWorkerArgs {
            public string inputPath;
            public string outputPath;
        }

        void m_FirWorker_DoWork(object sender, DoWorkEventArgs e) {
            FirWorkerArgs args = (FirWorkerArgs)e.Argument;

            var dft = new WWDirectComputeCS.WWDftCpu();

            // pcmファイルを読み込んでサンプル配列pcm1chを作成。
            PcmData pcmDataIn = null;
            try {
                pcmDataIn = ReadWavFile(args.inputPath);
            } catch (IOException ex) {
                e.Result = string.Format("WAVファイル {0} 読み込み失敗\r\n{1}", args.inputPath, ex);
                return;
            }
            if (null == pcmDataIn) {
                e.Result = string.Format("WAVファイル {0} 読み込み失敗", args.inputPath);
                return;
            }
            pcmDataIn = pcmDataIn.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SFloat);

            var from = FreqGraphToIdftInput(pcmDataIn.SampleRate);

            double [] idftResult;
            dft.Idft1d(from, out idftResult);

            // 窓関数の要素数は、IDFT結果の複素数の個数 -1個。
            double [] window;
            WWWindowFunc.BlackmanWindow((idftResult.Length / 2) - 1, out window);

            // FIR coeffの個数は、window.Length個。
            double [] coeff;
            dft.IdftToFirCoeff(idftResult, window, out coeff);

            /*
            for (int i=0; i < coeff.Length; ++i) {
                System.Console.WriteLine("coeff {0:D2} {1}", i, coeff[i]);
            }
            System.Console.WriteLine("");
            */

            PcmData pcmDataOut = new PcmData();
            pcmDataOut.CopyFrom(pcmDataIn);

            for (int ch=0; ch < pcmDataOut.NumChannels; ++ch) {
                // 全てのチャンネルでループ。

                var pcm1ch = new double[pcmDataOut.NumFrames];
                for (long i=0; i < pcm1ch.Length; ++i) {
                    pcm1ch[i] = pcmDataOut.GetSampleValueInFloat(ch, i);
                }

                // 少しずつFIRする。
                var fir = new WWFirCpu();
                fir.Setup(coeff, pcm1ch);

                const int FIR_SAMPLE = 65536;
                for (int offs=0; offs < pcm1ch.Length; offs += FIR_SAMPLE) {
                    int nSample = FIR_SAMPLE;
                    if (pcm1ch.Length < offs + nSample) {
                        nSample = pcm1ch.Length - offs;
                    }

                    var pcmFir = new double[nSample];
                    fir.Do(offs - window.Length / 2, nSample, pcmFir);

                    // 結果を出力に書き込む。
                    for (long i=0; i < pcmFir.Length; ++i) {
                        var re = pcmFir[i];
                        pcmDataOut.SetSampleValueInFloat(ch, i + offs,
                            (float)(re));
                    }

                    // 進捗Update。
                    int percentage = (int)(
                        (100L * ch / pcmDataOut.NumChannels) +
                        (100L * (offs + 1) / pcm1ch.Length / pcmDataOut.NumChannels));
                    m_FirWorker.ReportProgress(percentage);
                }
                fir.Unsetup();
            }

            // 音量制限処理。
            pcmDataOut.LimitLevelOnFloatRange();

            bool writeResult = false;
            try {
                writeResult = WriteWavFile(pcmDataOut, args.outputPath);
            } catch (IOException ex) {
                e.Result = string.Format("WAVファイル書き込み失敗: {0}\r\n{1}", args.outputPath, ex);
                return;
            }
            if (!writeResult) {
                e.Result = string.Format("WAVファイル書き込み失敗: {0}", args.outputPath);
                return;
            }

            e.Result = string.Format("WAVファイル書き込み成功: {0}", args.outputPath);
            return;
        }

        private void buttonFirInputBrowse_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile();
            if (0 < fileName.Length) {
                textBoxFirInputPath.Text = fileName;
            }
        }

        private void buttonFirOutputBrowse_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseSaveFile();
            if (0 < fileName.Length) {
                textBoxFirOutputPath.Text = fileName;
            }
        }

        private void buttonFirSmooth_Click(object sender, RoutedEventArgs e) {

            for (int j=0; j < 10; ++j) {
                double prev = m_freqGainTable[0];
                for (int i=1; i < m_freqGainTable.Length - 1; ++i) {
                    double t = m_freqGainTable[i];
                    m_freqGainTable[i] =
                        (prev +
                         m_freqGainTable[i] +
                         m_freqGainTable[i + 1]) / 3.0;
                    prev = t;
                }
            }
            DrawFreqResponse();
        }

        private void buttonEqSave_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonEqLoad_Click(object sender, RoutedEventArgs e) {

        }

    }
}
