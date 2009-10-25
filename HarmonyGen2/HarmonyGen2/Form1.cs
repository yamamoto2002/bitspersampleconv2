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
         * 10      G_O4C_Y
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
         * 22      F_O2C_Y
         * 23
         * 24 BOTTOM_Y
         * */
        
        const int   BAR_INTERVAL_PIXEL = 10;
        const int   NOTE_INTERVAL_Y_PIXEL = BAR_INTERVAL_PIXEL/2;
        const int   G_START_BAR_Y = 5;
        const int   F_START_BAR_Y = 16;
        const float G_CLEF_Y_PIXEL = BAR_INTERVAL_PIXEL * 3.3f;
        const float F_CLEF_Y_PIXEL = BAR_INTERVAL_PIXEL * 14.3f;
        const int G_O4C_Y_PIXEL = BAR_INTERVAL_PIXEL * 10;
        const int F_O2C_Y_PIXEL = BAR_INTERVAL_PIXEL * 22;
        const int MARGIN_W_PIXEL = BAR_INTERVAL_PIXEL * 1;
        const int   CHORD_SPACE_PIXEL = 64;
        const int CHORD_START_PIXEL_X = MARGIN_W_PIXEL + 6 * BAR_INTERVAL_PIXEL;
        const int BOTTOM_Y_PIXEL = 24 * BAR_INTERVAL_PIXEL;

        const float NOTE_W_PIXEL = 19.0f * BAR_INTERVAL_PIXEL / 16;
        const float NOTE_H_PIXEL = 15.0f * BAR_INTERVAL_PIXEL / 16;

        private void PaintCursor(Graphics g)
        {
            Point top = new Point(
                CHORD_START_PIXEL_X + CHORD_SPACE_PIXEL * cursorPos, 0);
            Point bottom = new Point(top.X, BOTTOM_Y_PIXEL);

            Pen pen = new Pen(Color.Black, 2.0f);
            pen.StartCap = LineCap.Square;
            pen.EndCap = LineCap.Square;
            pen.DashStyle = DashStyle.Dash;

            g.DrawLine(pen, top, bottom);
        }

        private int MusicalNoteToNoteDistanceFromC(MN musicalNote)
        {
            switch (musicalNote){
                case MN.C:
                case MN.CIS:
                    return 0;
                case MN.D:
                case MN.DIS:
                    return 1;
                case MN.E:
                    return 2;
                case MN.F:
                case MN.FIS:
                    return 3;
                case MN.G:
                case MN.GIS:
                    return 4;
                case MN.A:
                case MN.AIS:
                    return 5;
                case MN.B:
                    return 6;
                default:
                    return -1;
            }
        }

        private int PitchToGClefY(Pitch p)
        {
            if (p.octave < 3) {
                return -1;
            }
            return (p.octave - 4) * 7 + MusicalNoteToNoteDistanceFromC(p.musicalNote);
        }

        private int PitchToFClefY(Pitch p)
        {
            if (p.octave < 1)
            {
                return -1;
            }
            return (p.octave - 2) * 7 + MusicalNoteToNoteDistanceFromC(p.musicalNote);
        }

        private int PitchAndPartToY(Pitch pitch, Part part)
        {
            switch (part)
            {
            case Part.Soprano:
            case Part.Alto:
                return G_O4C_Y_PIXEL - NOTE_INTERVAL_Y_PIXEL * PitchToGClefY(pitch);
            case Part.Tenor:
            case Part.Bass:
                return F_O2C_Y_PIXEL - NOTE_INTERVAL_Y_PIXEL * PitchToFClefY(pitch);
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
            return -1;
        }

        private void PaintNote(Graphics g, Pitch pitch, Part part)
        {
            SolidBrush brush = new SolidBrush(Color.Black);

            Point pos = new Point(
                CHORD_START_PIXEL_X + CHORD_SPACE_PIXEL * cursorPos,
                PitchAndPartToY(pitch,part));

            g.FillEllipse(brush,
                pos.X - NOTE_W_PIXEL / 2.0f,
                pos.Y - NOTE_H_PIXEL / 2.0f,
                NOTE_W_PIXEL,
                NOTE_H_PIXEL);

            // 棒
            Pen pen = new Pen(Color.Black, 1.0f);
            switch (part) {
                case Part.Soprano:
                case Part.Tenor:
                    g.DrawLine(pen,
                        pos.X + NOTE_W_PIXEL / 2, pos.Y,
                        pos.X + NOTE_W_PIXEL / 2, pos.Y - NOTE_INTERVAL_Y_PIXEL * 7);
                    break;
                case Part.Alto:
                case Part.Bass:
                    g.DrawLine(pen,
                        pos.X - NOTE_W_PIXEL / 2, pos.Y,
                        pos.X - NOTE_W_PIXEL / 2, pos.Y + NOTE_INTERVAL_Y_PIXEL * 7);
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
                Point left = new Point(xy.X + MARGIN_W_PIXEL, xy.Y);
                Point right = new Point(upperRight.X, upperRight.Y);

                left.Y += (i + G_START_BAR_Y) * BAR_INTERVAL_PIXEL;
                right.Y = left.Y;
                g.DrawLine(pen, left, right);
            }
            for (int i=0; i < 5; ++i) {
                Point left = new Point(xy.X + MARGIN_W_PIXEL, xy.Y);
                Point right = new Point(upperRight.X, upperRight.Y);

                left.Y += (i + F_START_BAR_Y) * BAR_INTERVAL_PIXEL;
                right.Y = left.Y;
                g.DrawLine(pen, left, right);
            }

            PointF gClefPos = new PointF(MARGIN_W_PIXEL, G_CLEF_Y_PIXEL);
            PointF fClefPos = new PointF(MARGIN_W_PIXEL, F_CLEF_Y_PIXEL);
            SolidBrush brush = new SolidBrush(Color.Black);
            Font font = new Font("MusiSync", 56.0f *BAR_INTERVAL_PIXEL/16);
            g.DrawString("G", font, brush, gClefPos);
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