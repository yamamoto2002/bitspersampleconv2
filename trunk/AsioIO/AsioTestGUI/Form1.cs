using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace AsioTestGUI
{
    public partial class Form1 : Form
    {
        private AsioFromCS afc;

        public Form1()
        {
            InitializeComponent();

            afc = new AsioFromCS();

            Console.WriteLine("driverNum=" +afc.DriverNumGet());
            for (int i = 0; i < afc.DriverNumGet(); ++i) {
                listBoxDrivers.Items.Add(afc.DriverNameGet(i));
            }
            if (0 < afc.DriverNumGet()) {
                listBoxDrivers.SelectedIndex = 0;
                buttonLoadDriver.Enabled = true;
            }

            if (1 == afc.DriverNumGet()) {
                buttonLoadDriver_Click(null, null);
            }
        }

        public void FinalizeAll()
        {
            afc.Unsetup();
            afc.DriverUnload();
        }

        private void buttonLoadDriver_Click(object sender, EventArgs e)
        {
            buttonLoadDriver.Enabled = false;
            bool bRv = afc.DriverLoad(listBoxDrivers.SelectedIndex);
            if (!bRv) {
                return;
            }

            int rv = afc.Setup(96000);
            if (0 != rv) {
                MessageBox.Show(string.Format("afc.Setup(96000) failed {0:X8}", rv));
                return;
            }

            for (int i = 0; i < afc.InputChannelsNumGet(); ++i) {
                listBoxInput.Items.Add(afc.InputChannelNameGet(i));
            }
            if (0 < listBoxInput.Items.Count) {
                listBoxInput.SelectedIndex = 0;
            }
            for (int i = 0; i < afc.OutputChannelsNumGet(); ++i) {
                listBoxOutput.Items.Add(afc.OutputChannelNameGet(i));
            }
            if (0 < listBoxOutput.Items.Count) {
                listBoxOutput.SelectedIndex = 0;
            }

            if (0 < listBoxInput.Items.Count &&
                0 < listBoxOutput.Items.Count) {
                buttonStart.Enabled = true;
            }
        }

        BackgroundWorker bw;
        int inputChannelNum;
        int seconds;
        string writeFilePath;

        private void DoWork(object o, DoWorkEventArgs args) {
            Console.WriteLine("DoWork started\n");

            int count = 0;
            while (!afc.Run()) {
                ++count;
                bw.ReportProgress(100 * count / seconds);
            }
            int [] recordedData = afc.RecordedDataGet(inputChannelNum, seconds * 96000);
            PcmSamples1Channel ch0 = new PcmSamples1Channel(seconds * 96000, 16);
            int max = 0;
            int min = 0;
            for (int i = 0; i < recordedData.Length; ++i) {
                if (max < recordedData[i]) {
                    max = recordedData[i];
                }
                if (recordedData[i] < min) {
                    min = recordedData[i];
                }
            }
            Console.WriteLine("max={0} min={1}", max, min);

            if (max < -min) {
                max = -min;
            }
            double mag = 32767.0 / max;
            Console.WriteLine("mag={0}", mag);

            for (int i = 0; i < recordedData.Length; ++i) {
                ch0.Set16(i, (short)(recordedData[i] * mag));
            }

            List<PcmSamples1Channel> chList = new List<PcmSamples1Channel>();
            chList.Add(ch0);

            WavData wd = new WavData();
            wd.Create(96000, 16, chList);
            using (BinaryWriter bw = new BinaryWriter(File.Open(writeFilePath, FileMode.Create))) {
                wd.Write(bw);
            }

            args.Result = 0;
            Console.WriteLine("DoWork end\n");
        }

        private void ProgressChanged(object o, ProgressChangedEventArgs args) {
            progressBar1.Value = args.ProgressPercentage;
        }

        private void RunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            progressBar1.Visible = false;
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
        }

        // 1 oct 22.5Hz to approx. 20000Hz ... 10 variations

        public bool Start() {
            inputChannelNum = listBoxInput.SelectedIndex;

            seconds = 0;
            for (double f = 27.5; f < 20000.0; f *= Math.Pow(2, 1.0 / 3.0)) {
                ++seconds;
            }

            int [] outputData = new int[seconds * 96000];
            int pos = 0;
            for (double f = 22.5; f < 20000.0; f *= Math.Pow(2, 1.0 / 3.0)) {
                for (int i = 0; i < 96000; ++i) {
                    outputData[pos + i] = 0;
                }

                for (int i = 0; i < 96000 * (int)numericUpDownPulseCount.Value / f; ++i) {
                    outputData[pos + i] = (int)(System.Int32.MaxValue * Math.Sin(2.0 * Math.PI * (i *f / 96000)));
                }
                pos += 96000;
            }
            afc.OutputSet(listBoxOutput.SelectedIndex, outputData);
            afc.InputSet(listBoxInput.SelectedIndex, outputData.Length);
            afc.Start();

            progressBar1.Value = 0;
            progressBar1.Visible = true;

            bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.DoWork             += new DoWorkEventHandler(DoWork);
            bw.ProgressChanged    += new ProgressChangedEventHandler(ProgressChanged);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RunWorkerCompleted);
            bw.RunWorkerAsync();
            buttonStop.Enabled = true;
            return true;
        }

        private void buttonStart_Click(object sender, EventArgs e) {
            writeFilePath = textBoxFilePath.Text;
            buttonStart.Enabled = false;
            Start();
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = textBoxFilePath.Text;
            ofd.CheckPathExists = true;
            ofd.CheckFileExists = false;
            if (DialogResult.OK == ofd.ShowDialog()) {
                textBoxFilePath.Text = ofd.FileName;
            }
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            buttonStop.Enabled = false;
            afc.Stop();
        }

        private void buttonAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Pulse5 by Yamamoto Software Lab.\nASIO Technology by Steinberg Media Technology GmbH.");
        }
    }
}
