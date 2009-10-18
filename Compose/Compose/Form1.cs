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
    /// <summary>
    /// Musical note do re mi
    /// </summary>
    enum MN
    {
        Silence = -1,
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

    struct Pitch
    {
        public MN  musicalNote;
        public int octave;
    }

    class MidiWriter
    {
        public MidiWriter(BinaryWriter a)
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

        public static int CountMidiNumberBytes(ushort v) {
            if (128 * 128 <= v)
            {
                return 3;
            }
            else if (128 <= v)
            {
                return 2;
            }
            else {
                return 1;
            }
        }

        public void WriteMidiNumber(ushort v)
        {
            if (128 * 128 <= v)
            {
                int v2 = v/128*128;
                int v1 = v - v2 * 128*128;
                v1 = v1 / 128;
                int v0 = v - v2 * 128*128- v1 *128;

                v2 += 128;
                v1 += 128;

                bw.Write((byte)v2);
                bw.Write((byte)v1);
                bw.Write((byte)v0);
            }
            else if (128 <= v)
            {
                int v1 = v / 128;
                int v0 = v - v1 *128;

                v1 += 128;

                bw.Write((byte)v1);
                bw.Write((byte)v0);
            } else {
                bw.Write((byte)v);
            }
        }
    }

    class Note {
        int      offset;
        int      length;
        Pitch    pitch;
        int      volume;

        public Note(int offset, int length, Pitch pitch, int volume) {
            this.offset = offset;
            this.length = length;
            this.pitch  = pitch;
            this.volume = volume;
        }

        public int CountMidiBytes(ref int timeCursorRW) {
            if (pitch.musicalNote == MN.Silence)
            {
                return 0;
            }

            int bytes =
                MidiWriter.CountMidiNumberBytes((ushort)(offset - timeCursorRW))
                + 3 +
                MidiWriter.CountMidiNumberBytes((ushort)(length))
                + 3;
            timeCursorRW = offset + length;
            return bytes;
        }

        // returns new time cursor
        public int Write(int timeCursor, MidiWriter mw) {
            if (pitch.musicalNote == MN.Silence)
            {
                return timeCursor;
            }

            // pitch
            int p = (int)pitch.musicalNote + 0x3c;
            switch (pitch.octave) {
                case 1: p -= 12 * 3; break;
                case 2: p -= 12 * 2; break;
                case 3: p -= 12; break;
                case 4: break;
                case 5: p += 12; break;
                case 6: p += 12 * 2; break;
                case 7: p += 12 * 2; break;
                case 8: p += 12 * 4; break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
            }

            mw.WriteMidiNumber((ushort)(offset - timeCursor));
            mw.WriteByte(0x90);

            System.Diagnostics.Debug.Assert(p <= 0x7f);
            mw.WriteByte((byte)p);

            System.Diagnostics.Debug.Assert(volume <= 0x7f);
            mw.WriteByte((byte)volume);

            // stop note
            mw.WriteMidiNumber((ushort)(length));
            mw.WriteByte(0x90);
            mw.WriteByte((byte)p);
            mw.WriteByte((byte)0);
            
            return offset + length;
        }
    }

    class MidiTrackInfo {
        public MidiTrackInfo() {
            noteList = new System.Collections.Generic.List<Note>();
        }

        public void AddNote(Note note) {
            noteList.Add(note);
        }

        public void Write(BinaryWriter a) {
            MidiWriter mw = new MidiWriter(a);

            int timeCursor = 0;
            foreach (Note n in noteList) {
                timeCursor = n.Write(timeCursor, mw);
            }
        }

        public int CountMidiBytes() {
            int bytes = 0;
            int timeCursor = 0;
            foreach (Note n in noteList)
            {
                bytes += n.CountMidiBytes(ref timeCursor);
            }

            return bytes;
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
            MidiWriter mw = new MidiWriter(a);

            mw.WriteByte((byte)'M');
            mw.WriteByte((byte)'T');
            mw.WriteByte((byte)'h');
            mw.WriteByte((byte)'d');
            
            mw.WriteBE4(6);
            mw.WriteBE2(1);
            mw.WriteBE2(nTrack);
            mw.WriteBE2(tempo);
        }
    }

    class MidiTrackHeaderInfo
    {
        public void WriteTrackHeader(int partDataBytes, BinaryWriter a)
        {
            MidiWriter mw = new MidiWriter(a);

            mw.WriteByte((byte)'M');
            mw.WriteByte((byte)'T');
            mw.WriteByte((byte)'r');
            mw.WriteByte((byte)'k');

            // trackData = partData + trackFooter(4 bytes)
            mw.WriteBE4(partDataBytes + 4);
        }

        public void WriteTrackFooter(BinaryWriter a)
        {
            MidiWriter mw = new MidiWriter(a);
            mw.WriteBE4(0x00ff2f00); 
        }
    }

    class MidiFileInfo
    {
        MidiTrackInfo[] tracks;

        public MidiFileInfo(int nTrack)
        {
            tracks = new MidiTrackInfo[nTrack];
            for (int i = 0; i < nTrack; ++i)
            {
                tracks[i] = new MidiTrackInfo();
            }
        }

        public MidiTrackInfo GetTrack(int n)
        {
            return tracks[n];
        }

        public void Write(BinaryWriter bw)
        {
            MidiHeaderInfo mhi = new MidiHeaderInfo(128, (short)tracks.Length);
            MidiTrackHeaderInfo thi = new MidiTrackHeaderInfo();

            mhi.WriteMidiHeader(bw);

            for (int i = 0; i < tracks.Length; ++i)
            {
                MidiTrackInfo p = GetTrack(i);
                thi.WriteTrackHeader(p.CountMidiBytes(), bw);
                p.Write(bw);
                thi.WriteTrackFooter(bw);
            }
        }
    }

    class Chord {
        Pitch [] pitches;

        public Chord() {
            pitches = new Pitch[4];
            for (int i = 0; i < 4; ++i)
            {
                pitches[i] = new Pitch();
            }
        }
        public Chord(Pitch soprano, Pitch alto, Pitch tenor, Pitch bass)
        {
            pitches = new Pitch[4];
            for (int i = 0; i < 4; ++i)
            {
                pitches[i] = new Pitch();
            }
            pitches[0] = soprano;
            pitches[1] = alto;
            pitches[2] = tenor;
            pitches[3] = bass;
        }

        public void Set(Pitch soprano, Pitch alto, Pitch tenor, Pitch bass) {
            pitches[0] = soprano;
            pitches[1] = alto;
            pitches[2] = tenor;
            pitches[3] = bass;            
        }

        public Pitch GetPitch(int part) {
            return pitches[part];
        }
    }

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            CreateMusic();

            using (BinaryWriter bw
                = new BinaryWriter(File.Open("C:\\output.mid", FileMode.Create))) {
                OutputMidi(bw);
            }

            Application.Exit();
        }

        Pitch P(MN n, int octave) {
            Pitch p = new Pitch();
            p.musicalNote = n;
            p.octave = octave;
            return p;
        }

        List<Chord> chordList;

        private MidiFileInfo ChordListToMidiFile(int interval, int volume)
        {
            MidiFileInfo r = new MidiFileInfo(4);
            MidiTrackInfo s = r.GetTrack(0);
            MidiTrackInfo a = r.GetTrack(1);
            MidiTrackInfo t = r.GetTrack(2);
            MidiTrackInfo b = r.GetTrack(3);

            int now =0;
            for (int i = 0; i < chordList.Count; ++i)
            {
                now += interval;
                s.AddNote(new Note(now, interval - 1, chordList[i].GetPitch(0), volume));
                a.AddNote(new Note(now, interval - 1, chordList[i].GetPitch(1), volume));
                t.AddNote(new Note(now, interval - 1, chordList[i].GetPitch(2), volume));
                b.AddNote(new Note(now, interval - 1, chordList[i].GetPitch(3), volume));
            }
            return r;
        }

        private void CreateMusic()
        {
            chordList = new List<Chord>();
            chordList.Add(new Chord(P(MN.B, 4), P(MN.G, 4), P(MN.E, 3), P(MN.C, 3)));
            chordList.Add(new Chord(P(MN.E, 4), P(MN.C, 4), P(MN.G, 3), P(MN.C, 3)));
        }

        private void OutputMidi(BinaryWriter bw) {
            MidiFileInfo mfi = ChordListToMidiFile(128, 0x60);
            mfi.Write(bw);
        }
    }
}
