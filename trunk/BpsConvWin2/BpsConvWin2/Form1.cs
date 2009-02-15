using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace BpsConvWin2
{
    public partial class Form1 : Form
    {
        private string readFileName; 
        private System.Resources.ResourceManager rm;
        
        public Form1()
        {
            InitializeComponent();

            rm = BpsConvWin2.Properties.Resources.ResourceManager;
        }

        private void buttonReadFilePathBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog()) {
                ofd.ReadOnlyChecked = true;
                ofd.Multiselect = false;
                ofd.Filter = rm.GetString("WavFileFilter");
                ofd.CheckPathExists = true;
                ofd.CheckFileExists = true;
                ofd.AutoUpgradeEnabled = true;
                DialogResult dr = ofd.ShowDialog();
                if (DialogResult.OK == dr) {
                    readFileName = ofd.FileName;
                    buttonConvertStart.Enabled = true;
                    textBoxReadFilePath.Text = readFileName;
                }
            }
        }

        private void buttonConvertStart_Click(object sender, EventArgs e)
        {
            buttonConvertStart.Enabled = false;
            
            /* read from textBoxReadFilePath
             * create and fill SampledDataAllChannels
             * write to file
             */
            RiffHeader riffHeader = new RiffHeader();
            SampledData sampledData = null;

            try {
                using (BinaryReader br = new BinaryReader(new FileStream(readFileName, FileMode.Open))) {
                    sampledData = riffHeader.ReadAll(br);

                    if (null == sampledData) {
                        Console.WriteLine("RIFF read error");
                        return;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("{0}", ex);
            }
        }

    }
}
