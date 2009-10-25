using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Compose;

namespace HarmonyGen2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private void exit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e)
        {
            PaintMusicalNotation(e.Graphics,
                new Point(0, 0),
                new Size(splitContainer1.Panel1.Width,
                    splitContainer1.Panel1.Height));
        }

        private int cursorPos = 0;
        
        private Pitch P(MN n, int octave)
        {
            Pitch p = new Pitch();
            p.musicalNote = n;
            p.octave = octave;
            return p;
        }

        private void PaintMusicalNotation(Graphics g, Point xy, Size wh)
        {
            PaintGrandStaff(g, xy, wh);
            PaintCursor(g);
            PaintChord(g, new Chord(CT.I, P(MN.E, 4), P(MN.C, 4), P(MN.G, 3), P(MN.C, 3)));
        }

        /*
         * 0
         * 1
         * 2
         * 3
         * 4
         * 5       G_START_BAR_Y----------------------------------
         * 6       -----------------------------------------------
         * 7       -----------------------------------------------
         * 8       G_CLEF_Y---------------------------------------
         * 9       -----------------------------------------------
         * 10
         * 11
         * 12
         * 13
         * 14
         * 15
         * 16      F_START_BAR_Y ---------------------------------
         * 17      F_CLEF_Y---------------------------------------
         * 18      -----------------------------------------------
         * 19      -----------------------------------------------
         * 20      -----------------------------------------------
         * 21 MARGIN_W
         * 22
         * 23
         * 24 BOTTOM_Y
         * */

        const int   BAR_INTERVAL_PIXEL = 16;
        const int   G_START_BAR_Y = 5;
        const int   F_START_BAR_Y = 16;
        const float G_CLEF_Y = BAR_INTERVAL_PIXEL * 3.3f;
        const float F_CLEF_Y = BAR_INTERVAL_PIXEL * 14.3f;
        const int   MARGIN_W = BAR_INTERVAL_PIXEL * 1;
        const int   CHORD_SPACE_PIXEL = 64;
        const int   CHORD_START_PIXEL_X = MARGIN_W + 6 * BAR_INTERVAL_PIXEL;
        const int   BOTTOM_Y = 24 * BAR_INTERVAL_PIXEL;

        const float   NOTE_W = 19;
        const float   NOTE_H = 15;

        private void PaintCursor(Graphics g)
        {
            Point top = new Point(
                CHORD_START_PIXEL_X + CHORD_SPACE_PIXEL * cursorPos, 0);
            Point bottom = new Point(top.X, BOTTOM_Y);

            Pen pen = new Pen(Color.Black, 2.0f);
            pen.StartCap = LineCap.Square;
            pen.EndCap = LineCap.Square;
            pen.DashStyle = DashStyle.Dash;

            g.DrawLine(pen, top, bottom);
        }

        private void PaintNote(Graphics g, Pitch pitch, Part part)
        {
            Point pos = new Point(
                CHORD_START_PIXEL_X + CHORD_SPACE_PIXEL * cursorPos,
                G_START_BAR_Y * BAR_INTERVAL_PIXEL);
            SolidBrush brush = new SolidBrush(Color.Black);

            switch (part) {
            case Part.Soprano:
            case Part.Alto:
                g.FillEllipse(brush,
                    pos.X - NOTE_W / 2.0f,
                    pos.Y - NOTE_H / 2.0f,
                    NOTE_W,
                    NOTE_H);
                break;
            case Part.Tenor:
            case Part.Bass:
                break;
            }
        }

        private void PaintChord(Graphics g, Chord chord)
        {
            PaintNote(g, chord.GetPitch(0), Part.Soprano);
            PaintNote(g, chord.GetPitch(1), Part.Alto);
            PaintNote(g, chord.GetPitch(2), Part.Tenor);
            PaintNote(g, chord.GetPitch(3), Part.Bass);
        }

        private void PaintGrandStaff(Graphics g, Point xy, Size wh)
        {
            Pen pen = new Pen(Color.Black, 2.0f);
            pen.StartCap = LineCap.Square;
            pen.EndCap   = LineCap.Square;

            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Point upperRight = new Point(xy.X + wh.Width, xy.Y);

            for (int i=0; i < 5; ++i) {
                Point left = new Point(xy.X + MARGIN_W, xy.Y);
                Point right = new Point(upperRight.X, upperRight.Y);

                left.Y += (i + G_START_BAR_Y) * BAR_INTERVAL_PIXEL;
                right.Y = left.Y;
                g.DrawLine(pen, left, right);
            }
            for (int i=0; i < 5; ++i) {
                Point left = new Point(xy.X + MARGIN_W, xy.Y);
                Point right = new Point(upperRight.X, upperRight.Y);

                left.Y += (i + F_START_BAR_Y) * BAR_INTERVAL_PIXEL;
                right.Y = left.Y;
                g.DrawLine(pen, left, right);
            }

            PointF gClefPos = new PointF(MARGIN_W, G_CLEF_Y);
            PointF fClefPos = new PointF(MARGIN_W, F_CLEF_Y);
            SolidBrush brush = new SolidBrush(Color.Black);
            Font font = new Font("MusicalSymbols", 40);
            g.DrawString("&", font, brush, gClefPos);
            g.DrawString("?", font, brush, fClefPos);
        }

        // ################################################################
        // イベントハンドラ。

        private void radioType3_CheckedChanged(object sender, EventArgs e)
        {
            // 3の和音が設定された。
            if (radioBass3.Checked) {
                // 3の和音の低音位は第3展開位置には設定不可能。
                // 和音を生成しなおす。
                radioBass0.Checked = true;
            }
            radioBass3.Enabled = false;
        }

        private void radioType7_CheckedChanged(object sender, EventArgs e)
        {
            // 7の和音が設定された。
            // 7の和音の低音位は第3転回位置に設定可能。
            radioBass3.Enabled = true;
        }

        private void buttonGenerateChord_Click(object sender, EventArgs e)
        {

        }
    }
}