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

        string m_wavFilePath;
        WavData m_wavData = null; 

        const int DEFAULT_OUTPUT_LATENCY_MS = 200;

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
            textBoxLog.Text += string.Format("wasapi.DoDeviceEnumeration() {0:X8}\r\n", hr);

            int nDevices = wasapi.GetDeviceCount();
            for (int i = 0; i < nDevices; ++i) {
                listBoxDevices.Items.Add(wasapi.GetDeviceName(i));
            }

            buttonDeviceSelect.IsEnabled = false;
            buttonDeselect.IsEnabled = false;
            buttonRec.IsEnabled = false;
            buttonStop.IsEnabled = false;
            buttonRefer.IsEnabled = true;
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

        private void buttonRefer_Click(object sender, RoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "WAVEファイル (.wav)|*.wav";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                m_wavFilePath = dlg.FileName;
                m_wavData = new WavData();

                textBoxRecFile.Text = m_wavFilePath;

                buttonDeviceSelect.IsEnabled = true;
            }
        }

        private void buttonDeviceSelect_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonDeviceDeselect_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonInspectDevice_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonRec_Click(object sender, RoutedEventArgs e) {

        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {

        }
    }
}
