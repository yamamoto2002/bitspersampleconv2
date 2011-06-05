using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;
using PcmDataLib;
using System.IO;
using WWDirectComputeCS;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        //////////////////////////////////////////////////////////////////////////////////////////
        // ヒルベルト変換

        /// <summary>
        /// FIRフィルタの長さ。奇数。
        /// </summary> 
        private int m_hilbFirLength = 8191;

        private BackgroundWorker m_HilbWorker;

        private void InitHilbTab() {
            m_HilbWorker = new BackgroundWorker();
            m_HilbWorker.WorkerReportsProgress = true;
            m_HilbWorker.DoWork += new DoWorkEventHandler(m_HilbWorker_DoWork);
            m_HilbWorker.ProgressChanged += new ProgressChangedEventHandler(m_HilbWorker_ProgressChanged);
            m_HilbWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(m_HilbWorker_RunWorkerCompleted);
            m_HilbWorker.WorkerSupportsCancellation = true;
        }
        
        private void AddHilbLog(string s) {
            textBoxHilbLog.Text += s;
            textBoxHilbLog.ScrollToEnd();
        }

        private void buttonHilbInputBrowse_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseOpenFile(m_filterWav);
            if (0 < fileName.Length) {
                textBoxHilbInputPath.Text = fileName;
            }
        }

        private void buttonHilbOutputBrowse_Click(object sender, RoutedEventArgs e) {
            string fileName = BrowseSaveFile(m_filterSaveWav);
            if (0 < fileName.Length) {
                textBoxHilbOutputPath.Text = fileName;
            }
        }

        private void buttonHilbOutputStart_Click(object sender, RoutedEventArgs e) {
            int filterLength;
            if (!Int32.TryParse(textBoxHilbFilterN.Text, out filterLength) ||
                (filterLength & 1) == 0 || filterLength <= 0) {
                MessageBox.Show("FIRフィルタ長は正の奇数の数値を半角数字で入力してください。処理中断。");
                return;
            }
            m_hilbFirLength = filterLength;

            var args = new FirWorkerArgs();
            args.inputPath = textBoxHilbInputPath.Text;
            args.outputPath = textBoxHilbOutputPath.Text;
            if (radioButtonHilbSint16.IsChecked == true) {
                args.outputBitsPerSample = 16;
                args.outputSampleIsFloat = false;
            }
            if (radioButtonHilbSint24.IsChecked == true) {
                args.outputBitsPerSample = 24;
                args.outputSampleIsFloat = false;
            }
            if (radioButtonHilbFloat32.IsChecked == true) {
                args.outputBitsPerSample = 32;
                args.outputSampleIsFloat = true;
            }

            if (!Double.TryParse(textBoxHilbKaiserAlpha.Text, out args.kaiserAlpha) ||
                args.kaiserAlpha <= 4.0 || 9.0 <= args.kaiserAlpha) {
                MessageBox.Show("カイザー窓α値は4.0<α<9.0の範囲の数値を半角数字で入力してください。処理中断。");
                return;
            }

            if (radioButtonHilbBlackman.IsChecked == true) {
                args.windowFunc = WindowFuncType.Blackman;
            } else {
                args.windowFunc = WindowFuncType.Kaiser;
            }

            AddHilbLog(string.Format("開始。{0} → {1}\r\n", args.inputPath, args.outputPath));
            buttonHilbOutputStart.IsEnabled = false;
            groupBoxHilbInput.IsEnabled = false;
            groupBoxHilbSettings.IsEnabled = false;
            groupBoxHilbOutput.IsEnabled = false;
            m_HilbWorker.RunWorkerAsync(args);
        }

        void m_HilbWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            string s = (string)e.Result;
            AddHilbLog(s + "\r\n");
            progressBarHilb.Value = 0;

            buttonHilbOutputStart.IsEnabled = true;
            groupBoxHilbInput.IsEnabled = true;
            groupBoxHilbSettings.IsEnabled = true;
            groupBoxHilbOutput.IsEnabled = true;
        }

        void m_HilbWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progressBarFir.Value = e.ProgressPercentage;
        }

        private bool HilbertDo(FirWorkerArgs args, PcmData pcmDataIn, out PcmData pcmDataOutput) {
            var dft = new WWDirectComputeCS.WWDftCpu();

            var hilb = WWHilbert.HilbertFirCoeff(m_hilbFirLength);
            System.Diagnostics.Debug.Assert(hilb.Length == m_hilbFirLength);

            // 窓関数
            double [] window;
            if (args.windowFunc == WindowFuncType.Blackman) {
                WWWindowFunc.BlackmanWindow(m_hilbFirLength, out window);
            } else {
                WWWindowFunc.KaiserWindow(m_hilbFirLength, args.kaiserAlpha, out window);
            }

            // FIR coeffの個数は、window.Length個。
            double [] coeff = new double[m_hilbFirLength];
            for (int i=0; i<coeff.Length; ++i) {
                coeff[i] = hilb[i] * window[i];
            }

            /*
            for (int i=0; i < coeff.Length; ++i) {
                System.Console.WriteLine("coeff {0:D2} {1}", i, coeff[i]);
            }
            System.Console.WriteLine("");
            */

            PcmData pcmDataEq = new PcmData();
            pcmDataEq.CopyFrom(pcmDataIn);

            for (int ch=0; ch < pcmDataEq.NumChannels; ++ch) {
                // 全てのチャンネルでループ。

                var pcm1ch = new double[pcmDataEq.NumFrames];
                for (long i=0; i < pcm1ch.Length; ++i) {
                    pcm1ch[i] = pcmDataEq.GetSampleValueInDouble(ch, i);
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
                        pcmDataEq.SetSampleValueInDouble(ch, i + offs,
                            (float)(re));
                    }

                    // 進捗Update。
                    int percentage = (int)(
                        (100L * ch / pcmDataEq.NumChannels) +
                        (100L * (offs + 1) / pcm1ch.Length / pcmDataEq.NumChannels));
                    m_HilbWorker.ReportProgress(percentage);
                }
                fir.Unsetup();
            }

            // 音量制限処理。
            pcmDataEq.LimitLevelOnDoubleRange();

            pcmDataOutput
                = pcmDataEq.BitsPerSampleConvertTo(
                    args.outputBitsPerSample,
                    args.outputSampleIsFloat ? PcmData.ValueRepresentationType.SFloat : PcmData.ValueRepresentationType.SInt);
            return true;
        }

        void m_HilbWorker_DoWork(object sender, DoWorkEventArgs e) {
            FirWorkerArgs args = (FirWorkerArgs)e.Argument;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            string result;
            if (!FirDoCommon(args, HilbertDo, out result)) {
                e.Result = result;
                return;
            }

            sw.Stop();
            e.Result = string.Format("WAVファイル書き込み成功: {0}\r\n所要時間 {1}秒",
                args.outputPath, sw.ElapsedMilliseconds / 1000);
        }
    }
}
