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
        }

        public void FinalizeAll()
        {
            AsioFromCS.DriverUnload();
        }

        private void buttonLoadDriver_Click(object sender, EventArgs e)
        {
            buttonLoadDriver.Enabled = false;
            bool bRv = AsioFromCS.DriverLoad(listBoxDrivers.SelectedIndex);
            if (!bRv) {
                return;
            }


        }
    }
}
