using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Compose
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            Composer composer = new Composer();

            Music music = composer.CreateMusic();

            using (BinaryWriter bw
                = new BinaryWriter(File.Open("C:\\output.mid", FileMode.Create))) {
                OutputMidi(music, bw);
            }

            Application.Exit();
        }

        private MidiFileInfo MusicToMidiFile(Music music, int interval, int volume)
        {
            MidiFileInfo r = new MidiFileInfo(4);
            MidiTrackInfo s = r.GetTrack(0);
            MidiTrackInfo a = r.GetTrack(1);
            MidiTrackInfo t = r.GetTrack(2);
            MidiTrackInfo b = r.GetTrack(3);

            int now =0;
            for (int i = 0; i < music.GetNumOfChords(); ++i) {
                now += interval;
                s.AddNote(new Note(now, interval - 1, music.GetChord(i).GetPitch(0), volume));
                a.AddNote(new Note(now, interval - 1, music.GetChord(i).GetPitch(1), volume));
                t.AddNote(new Note(now, interval - 1, music.GetChord(i).GetPitch(2), volume));
                b.AddNote(new Note(now, interval - 1, music.GetChord(i).GetPitch(3), volume));
            }
            return r;
        }

        private void OutputMidi(Music music, BinaryWriter bw)
        {
            MidiFileInfo mfi = MusicToMidiFile(music, 128, 0x60);
            mfi.Write(bw);
        }
    }
}
