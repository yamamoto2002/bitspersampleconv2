/*
    BPSConvWin
    Copyright (C) 2009 Yamamoto DIY Software Lab.

    This program is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/
using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;
using System.Globalization;

namespace BpsConvWin
{
    public partial class Form1 : Form
    {
        private string readFileName;
        private System.Resources.ResourceManager rm;

        public Form1()
        {
            InitializeComponent();

            rm = BpsConvWin.Properties.Resources.ResourceManager;
            textBoxOutput.Text = rm.GetString("PleasePressBrowseButton") + "\r\n";
        }

        private static string ReadFileNameToWriteFileName(string readFileName, int nth)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}_{1}.wav", readFileName, nth);
        }

        private void buttonReadFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.ReadOnlyChecked    = true;
                ofd.Multiselect        = false;
                ofd.Filter             = rm.GetString("WavFileFilter");
                ofd.CheckPathExists    = true;
                ofd.CheckFileExists    = true;
                ofd.AutoUpgradeEnabled = true;
                DialogResult dr = ofd.ShowDialog();
                if (DialogResult.OK == dr) {
                    readFileName             = ofd.FileName;
                    buttonConvStart.Enabled  = true;
                    textBoxReadFilePath.Text = readFileName;

                    textBoxOutput.Text = string.Empty;
                    for (int i=1; i < 16; ++i) {
                        textBoxOutput.Text += string.Format(CultureInfo.InvariantCulture, rm.GetString("OutputFileNameGuideText"),
                            ReadFileNameToWriteFileName(readFileName, i)) + "\r\n";
                    }
                    textBoxOutput.Text += rm.GetString("PleasePressConvButton") + "\r\n";
                }
            }
        }

        struct BackgroundWorkerArgs
        {
            public string readFileName;
            public string writeFileName;
            public int    newBitsPerSample;
        };

        private int workerProgress;

        private void WorkerDoNext()
        {
            ++workerProgress;

            if (15 < workerProgress) {
                textBoxOutput.Text += rm.GetString("ConvertEnd");
                return;
            }

            string writeFileName = ReadFileNameToWriteFileName(readFileName, workerProgress);
            textBoxOutput.Text += string.Format(CultureInfo.InvariantCulture, rm.GetString("ConvertingProgressText"),
                readFileName, writeFileName, workerProgress) + "\r\n";

            BackgroundWorkerArgs a = new BackgroundWorkerArgs();
            a.readFileName     = readFileName;
            a.writeFileName    = writeFileName;
            a.newBitsPerSample = workerProgress;

            backgroundWorker1.RunWorkerAsync(a);        
        }

        private void buttonConvStart_Click(object sender, EventArgs e)
        {
            textBoxOutput.Text = rm.GetString("NowConverting") + "\r\n";

            workerProgress          = 0;
            buttonConvStart.Enabled = false;

            WorkerDoNext();
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (null != e.Result) {
                textBoxOutput.Text += (string)e.Result;
                return;
            }

            WorkerDoNext();        
        }
         
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorkerArgs a    = (BackgroundWorkerArgs)e.Argument;
            try {
                using (BinaryReader br = new BinaryReader(File.Open(a.readFileName, FileMode.Open))) {
                    using (BinaryWriter bw = new BinaryWriter(File.Open(a.writeFileName, FileMode.CreateNew))) {
                        if (!BpsConv.Convert(br, bw, a.newBitsPerSample, checkBoxAddDither.Checked)) {
                            e.Result = rm.GetString("ConvFailedByWrongFormat");
                            return;
                        }
                    }
                }
            } catch (Exception ex) {
                e.Result = rm.GetString("ConvFailedByException") + "\r\n\r\n" + string.Format(CultureInfo.InvariantCulture, "{0}", ex);
                return;
            }
            e.Result = null;
        }
    }
}
