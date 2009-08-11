using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AsioTestGUI
{
    public partial class Form1 : Form
    {
        private AsioFromCS afc;

        public Form1()
        {
            InitializeComponent();

            afc = new AsioFromCS();

            System.Console.WriteLine("driverNum=" +afc.DriverNumGet());
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

        private void DoWork(object o, DoWorkEventArgs args) {
            System.Console.WriteLine("DoWork started\n");
            afc.Run();
            args.Result = 0;
            System.Console.WriteLine("DoWork end\n");
        }

        private void ProgressChanged(object o, ProgressChangedEventArgs args) {
            progressBar1.Value = args.ProgressPercentage;
        }

        private void RunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            progressBar1.Visible = false;
            buttonStart.Enabled = true;
        }

        // 22.5
        // 55
        // 110
        // 220
        // 440
        // 880
        // 1760
        // 3520
        // 7040
        // 14080
        public bool Start() {
            int [] outputData = new int[20 * 96000];
            int pos = 0;
            for (double f = 22.5; f < 20000.0; f *= Math.Sqrt(2)) {
                for (int i = 0; i < 96000; ++i) {
                    outputData[pos + i] = 0;
                }

                for (int i = 0; i < 96000 * 10 / f; ++i) {
                    outputData[pos + i] = (int)(System.Int32.MaxValue * Math.Sin(2.0 * Math.PI * (i *f / 96000)));
                }
                pos += 96000;
            }
            afc.OutputDataSet(listBoxOutput.SelectedIndex, outputData);

            bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.DoWork             += new DoWorkEventHandler(DoWork);
            bw.ProgressChanged    += new ProgressChangedEventHandler(ProgressChanged);
            bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RunWorkerCompleted);
            bw.RunWorkerAsync();
            return true;
        }

        private void buttonStart_Click(object sender, EventArgs e) {
            buttonStart.Enabled = false;
            Start();
        }
    }
}
