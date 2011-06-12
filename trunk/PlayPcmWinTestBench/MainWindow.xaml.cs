// 日本語UTF-8

using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows;
using Wasapi;
using System.Runtime.InteropServices;

namespace PlayPcmWinTestBench {
    public partial class MainWindow : Window {
        Wasapi.WasapiCS wasapi;

        public MainWindow() {
            InitializeComponent();

            wasapi = new WasapiCS();
            wasapi.Init();

            Prepare();
        }

        private void Prepare() {
            InitUpsampleTab();
            InitAbxTab();
            InitFirTab();
            InitHilbTab();
            InitAnalyticSignalTab();
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


        //////////////////////////////////////////////////////////////////////////////////////////
        // 互換性実験

        [DllImport("WWDirectDrawTest.dll")]
        private extern static int
        WWDirectDrawTest_Test();

        private void buttonCompatibility_Click(object sender, RoutedEventArgs e) {
            int hr = WWDirectDrawTest_Test();
            textBoxCompatibility.Text += string.Format("DirectDraw PrimarySurface Lock {0:X8} {1}\r\n",
                hr, hr==0 ? "成功" : "失敗");
        }

    }
}
