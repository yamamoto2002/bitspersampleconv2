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
    enum NoteType
    {
        Silence,
        C,
        CIS,
        D,
        DIS,
        E,
        F,
        FIS,
        G,
        GIS,
        A,
        AIS,
        B
    };

    class Note {
        int      offset;
        int      length;
        NoteType pitch;
        int      octave;
        int      volume;

        public Note(int offset, int length, NoteType pitch, int octave, int volume) {
            this.offset = offset;
            this.length = length;
            this.pitch  = pitch;
            this.octave = octave;
            this.volume = volume;
        }
    }

    class BinaryWriterBE
    {
        public BinaryWriterBE(BinaryWriter a)
        {
            bw = a;
        }

        BinaryWriter bw;

        public void WriteByte(byte b)
        {
            bw.Write(b);
        }

        public void WriteBE4(int v)
        {
            bw.Write((byte)((v >> 24) & 0xff));
            bw.Write((byte)((v >> 16) & 0xff));
            bw.Write((byte)((v >> 8) & 0xff));
            bw.Write((byte)(v & 0xff));
        }

        public void WriteBE2(short v)
        {
            bw.Write((byte)((v >> 8) & 0xff));
            bw.Write((byte)(v & 0xff));
        }
    }

    enum PartType
    {
        Soprano,
        Alto,
        Tenor,
        Bass
    };

    class Part {
        PartType part;

        public Part(PartType t) {
            part = t;
            noteList = new System.Collections.Generic.List<Note>();
        }

        public void AddNote(Note note) {
            noteList.Add(note);
        }

        System.Collections.Generic.List<Note> noteList;
    }

    class MidiHeaderInfo {
        short tempo;
        short nTrack;

        public MidiHeaderInfo(short tempo, short nTrack)
        {
            this.tempo = tempo;
            this.nTrack = nTrack;
        }

        public void WriteMidiHeader(BinaryWriter a) {
            BinaryWriterBE bw = new BinaryWriterBE(a);

            bw.WriteByte((byte)'M');
            bw.WriteByte((byte)'T');
            bw.WriteByte((byte)'h');
            bw.WriteByte((byte)'d');
            
            bw.WriteBE4(6);
            bw.WriteBE2(1);
            bw.WriteBE2(nTrack);
            bw.WriteBE2(tempo);
        }
    }

    class TrackHeaderInfo
    {
        public void WriteTrackHeader(int trackDataBytes, BinaryWriter a)
        {
            BinaryWriterBE bw = new BinaryWriterBE(a);

            bw.WriteByte((byte)'M');
            bw.WriteByte((byte)'T');
            bw.WriteByte((byte)'r');
            bw.WriteByte((byte)'k');

            bw.WriteBE4(trackDataBytes);
        }

        public void WriteTrackFooter(BinaryWriter a)
        {
            BinaryWriterBE bw = new BinaryWriterBE(a);
            bw.WriteBE4(0x00ff2f00); 
        }
    }

    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();

            Part p = CreateMusic();

            using (BinaryWriter bw
                = new BinaryWriter(File.Open("C:\\output.mid", FileMode.Create))) {
                OutputMidi(p, bw);
            }
        }

        private Part CreateMusic()
        {
            Part p = new Part(PartType.Soprano);
            p.AddNote(new Note(0, 120, NoteType.C, 4, 60));
            p.AddNote(new Note(127, 120, NoteType.E, 4, 60));
            return p;
        }

        private void OutputMidi(Part p,BinaryWriter bw) {
            MidiHeaderInfo mhi = new MidiHeaderInfo(128, 1);
            mhi.WriteMidiHeader(bw);

            TrackHeaderInfo thi = new TrackHeaderInfo();
            thi.WriteTrackHeader(4, bw);
            thi.WriteTrackFooter(bw);
        }
    }
}
