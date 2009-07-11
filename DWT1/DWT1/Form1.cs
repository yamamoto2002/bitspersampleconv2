using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace DWT1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            pictureBoxSource.Image = null;
            pictureBoxDWTed.Image = null;
            if (!LoadSignal(SIGNAL_FILENAME))
            {
                PrepareSourceData();
            }
            TrackbarMagnitudeUpdated();
            TrackbarOffsetUpdated();
            UpdateGui();
        }

        private const int SRC_W = 512;
        private const int SRC_H = 257;
        private const int DWT_H = 256;
        private const int SRC_HALF_H = 128;
        private const string SIGNAL_FILENAME = "sourceSignal.txt";
        private float[] sourceSignal = null;
        private int offset = 0;

        private void PrepareSourceData()
        {
            sourceSignal = new float[SRC_W];
            for (int x = 0; x < SRC_W; ++x)
            {
                sourceSignal[x] = (float)Math.Sin(x * 0.04f);
            }
        }

        private void SaveSignal(string path)
        {
            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.WriteLine("1");
                sw.WriteLine(sourceSignal.Length);

                for (int i = 0; i < sourceSignal.Length; ++i)
                {
                    sw.WriteLine(sourceSignal[i]);
                }
            }
        }

        private bool LoadSignal(string path)
        {
            bool ret = false;

            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    string version = sr.ReadLine().Trim();
                    if (0 != version.CompareTo("1"))
                    {
                        return false;
                    }

                    sourceSignal = new float[SRC_W];
                    int length = System.Int32.Parse(sr.ReadLine().Trim());
                    if (SRC_W < length)
                    {
                        length = SRC_W;
                    }

                    for (int i = 0; i < length; ++i)
                    {
                        sourceSignal[i] = (float)System.Double.Parse(sr.ReadLine().Trim());
                    }
                }
                ret = true;
            }
            catch
            {
            }
            return ret;
        }

        private void UpdateGui()
        {
            UpdatePictureBoxSource();
            UpdateDwt();
        }

        private Point prevXY;

        private void pictureBoxSource_MouseEnter(object sender, EventArgs e)
        {
            prevXY = new Point(-1, -1);
        }

        private void pictureBoxSource_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.None)
            {
                return;
            }
            if (e.X < 0 || SRC_W <= e.X ||
                e.Y < 0 || SRC_H <= e.Y)
            {
                return;
            }

            if (prevXY.X < 0)
            {
                prevXY = new Point(e.X, e.Y);
                return;
            }

            Point from = new Point(prevXY.X, prevXY.Y);
            Point to = new Point(e.X, e.Y);
            float katamuki = 0;
            if (e.X < prevXY.X)
            {
                from = new Point(e.X, e.Y);
                to = new Point(prevXY.X, prevXY.Y);
            }

            if (from.X != to.X)
            {
                katamuki = ((float)to.Y - from.Y) / (to.X - from.X);
            }

            for (int i = 0; i <= to.X - from.X; ++i)
            {
                int x = from.X + i;
                float y = (float)from.Y + katamuki * i;
                sourceSignal[x] = (SRC_HALF_H - y) / SRC_HALF_H;
            }
            prevXY = new Point(e.X, e.Y);
            UpdateGui();
        }

        private void pictureBoxSource_MouseLeave(object sender, EventArgs e)
        {
            prevXY = new Point(-1, -1);
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            SaveSignal(SIGNAL_FILENAME);
        }

        private void buttonLoad_Click(object sender, EventArgs e)
        {
            LoadSignal(SIGNAL_FILENAME);
            UpdateGui();
        }

        private void UpdatePictureBoxSource()
        {
            Bitmap bmp = new Bitmap(SRC_W, SRC_H);
            Graphics g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff)), 0, 0, SRC_W, SRC_H);

            PointF[] points = new PointF[(sourceSignal.Length - 1) * 2];

            for (int x = 0; x < sourceSignal.Length - 1; ++x)
            {
                points[x * 2] = new PointF(x, -sourceSignal[x] * SRC_HALF_H + SRC_HALF_H);
                points[x * 2 + 1] = new PointF(x + 1, -sourceSignal[x + 1] * SRC_HALF_H + SRC_HALF_H);
            }
            g.DrawLines(new Pen(Color.Black), points);

            if (null != pictureBoxSource.Image)
            {
                pictureBoxSource.Image.Dispose();
            }
            pictureBoxSource.Image = bmp;
        }

        private int magnitude = 256;

        private void UpdateDwt()
        {
            Bitmap bmp = new Bitmap(SRC_W, DWT_H);
            Graphics g = Graphics.FromImage(bmp);
            g.FillRectangle(new SolidBrush(Color.FromArgb(0xff, 0, 0, 0)), 0, 0, SRC_W, DWT_H);

            RectangleF[] rects = new RectangleF[sourceSignal.Length * 3];
            float[] dwtData = new float[sourceSignal.Length*9];
            sourceSignal.CopyTo(dwtData, 0);

            Color[] colors = new Color[sourceSignal.Length * 3];

            int readColumn = 0;
            int writeColumn = sourceSignal.Length;
            float y = 0;
            float h = 16.0f;
            int posR = 0;

            for (int j = sourceSignal.Length / 2; 2 <= j; j /= 2)
            {
                float w = sourceSignal.Length / j;

                for (int i = 0; i < j; ++i)
                {
                    float a = (dwtData[readColumn + i * 2] + dwtData[readColumn + i * 2 + 1]) * 0.5f;
                    float d = dwtData[readColumn + i * 2] - a;
                    dwtData[writeColumn + i] = a;
                    dwtData[writeColumn + j + i] = d;

                    rects[posR] = new RectangleF(i * w, y, w, h);

                    int r = (int)((0 < d) ? d * magnitude : 0);
                    if (255 < r) { r = 255; }
                    int b = (int)((d < 0) ? -d * magnitude : 0);
                    if (255 < b) { b = 255; }
                    colors[posR++] = Color.FromArgb(b, 0, r);
                }

                for (int i = j * 2; i < sourceSignal.Length; ++i)
                {
                    dwtData[writeColumn + i] = dwtData[writeColumn - sourceSignal.Length + i];
                }

                readColumn += sourceSignal.Length;
                writeColumn += sourceSignal.Length;
                y += h;
            }

            for (int i = 0; i < posR; ++i)
            {
                g.FillRectangle(new SolidBrush(colors[i]), rects[i]);
            }

            if (null != pictureBoxDWTed.Image)
            {
                pictureBoxDWTed.Image.Dispose();
            }
            pictureBoxDWTed.Image = bmp;
        }

        private void TrackbarMagnitudeUpdated()
        {
            magnitude = (int)Math.Pow(2, trackBar1.Value + 8);
            label1.Text = string.Format("{0} x", magnitude);
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            TrackbarMagnitudeUpdated();
            UpdateGui();
        }

        private void TrackbarOffsetUpdated()
        {
            offset = (int)trackBar2.Value;
            label2.Text = string.Format("offset = {0}", offset);
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            TrackbarOffsetUpdated();
            UpdateGui();
        }
    }
}
