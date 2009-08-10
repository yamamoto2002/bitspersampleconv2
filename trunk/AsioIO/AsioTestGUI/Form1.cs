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
        public Form1()
        {
            InitializeComponent();

            System.Console.WriteLine("driverNum=" +AsioFromCS.DriverNumGet());
            for (int i = 0; i < AsioFromCS.DriverNumGet(); ++i) {
                listBoxDrivers.Items.Add(AsioFromCS.DriverNameGet(i));
            }
            if (0 < AsioFromCS.DriverNumGet()) {
                listBoxDrivers.SelectedIndex = 0;
                buttonLoadDriver.Enabled = true;
            }

            if (1 == AsioFromCS.DriverNumGet()) {
                buttonLoadDriver_Click(null, null);
            }
        }

        public void FinalizeAll()
        {
            AsioFromCS.Unsetup();
            AsioFromCS.DriverUnload();
        }

        private void buttonLoadDriver_Click(object sender, EventArgs e)
        {
            buttonLoadDriver.Enabled = false;
            bool bRv = AsioFromCS.DriverLoad(listBoxDrivers.SelectedIndex);
            if (!bRv) {
                return;
            }

            int rv = AsioFromCS.Setup(96000);
            if (0 != rv) {
                MessageBox.Show(string.Format("AsioFromCS.Setup(96000) failed {0:X8}", rv));
                return;
            }

            for (int i = 0; i < AsioFromCS.InputChannelsNumGet(); ++i) {
                listBoxInput.Items.Add(AsioFromCS.InputChannelNameGet(i));
            }
            if (0 < listBoxInput.Items.Count) {
                listBoxInput.SelectedIndex = 0;
            }
            for (int i = 0; i < AsioFromCS.OutputChannelsNumGet(); ++i) {
                listBoxOutput.Items.Add(AsioFromCS.OutputChannelNameGet(i));
            }
            if (0 < listBoxOutput.Items.Count) {
                listBoxOutput.SelectedIndex = 0;
            }

            if (0 < listBoxInput.Items.Count &&
                0 < listBoxOutput.Items.Count) {
                buttonStart.Enabled = true;
            }
        }
    }
}
