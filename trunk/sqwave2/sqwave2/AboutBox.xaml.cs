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
using System.Windows.Shapes;

namespace sqwave2
{
    /// <summary>
    /// Interaction logic for AboutBox.xaml
    /// </summary>
    public partial class AboutBox : Window
    {
        public AboutBox() {
            InitializeComponent();
        }

        public void SetText(string s) {
            textBox1.Text = s;
        }

        private void button1_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
        }
    }
}
