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

namespace sqwave2
{
    /// <summary>
    /// PianoControl2.xaml の相互作用ロジック
    /// </summary>
    public partial class PianoControl2 : UserControl
    {
        private void DrawLine1(double x0, double y0, double x1, double y1) {
            var myLine = new Line();
            myLine.Stroke = System.Windows.Media.Brushes.Black;
            myLine.X1 = x0;
            myLine.Y1 = y0;
            myLine.X2 = x1;
            myLine.Y2 = y1;
            myLine.StrokeThickness = 1;
            grid.Children.Add(myLine);
        }

        const int OCTAVE_NUM = 3;

        private void DrawKeyboard() {
            for (int i = 1; i < 7 * OCTAVE_NUM; ++i) {
                double x = (double)this.Width * i / (7 * OCTAVE_NUM);
                DrawLine1(x, 0, x, this.Height); 
            }
            for (int i = 1; i < 7 * OCTAVE_NUM; ++i) {
                double x = (double)this.Width * (i-0.5) / (7 * OCTAVE_NUM);

            }
        }

        public PianoControl2() {
            InitializeComponent();

        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e) {
            DrawKeyboard();
        }
    }
}
