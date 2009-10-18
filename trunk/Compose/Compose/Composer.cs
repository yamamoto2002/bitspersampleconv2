using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Compose
{
    // Chord type
    enum CT
    {
        Unspecified,
        I,
        IU2, // I^2
        II,
        IV,
        V,
        V7,
        VI
    }

    class Chord
    {
        CT       chordType;
        Pitch [] pitches;

        public Chord()
        {
            chordType = CT.Unspecified;
            pitches = new Pitch[4];
            for (int i = 0; i < 4; ++i) {
                pitches[i] = new Pitch();
            }
        }
        public Chord(CT chordType, Pitch soprano, Pitch alto, Pitch tenor, Pitch bass)
        {
            this.chordType = chordType;
            pitches = new Pitch[4];
            for (int i = 0; i < 4; ++i) {
                pitches[i] = new Pitch();
            }
            pitches[0] = soprano;
            pitches[1] = alto;
            pitches[2] = tenor;
            pitches[3] = bass;
        }

        public void Set(Pitch soprano, Pitch alto, Pitch tenor, Pitch bass)
        {
            pitches[0] = soprano;
            pitches[1] = alto;
            pitches[2] = tenor;
            pitches[3] = bass;
        }

        public Pitch GetPitch(int part)
        {
            return pitches[part];
        }
    }

    class Music
    {
        public List<Chord> chordList;

        public Music()
        {
            chordList = new List<Chord>();
        }

        public void Add(Chord chord)
        {
            chordList.Add(chord);
        }

        public int GetNumOfChords()
        {
            return chordList.Count;
        }

        public Chord GetChord(int pos)
        {
            return chordList[pos];
        }

        public Chord GetLastChord()
        {
            if (chordList.Count == 0) {
                return null;
            }
            return chordList[chordList.Count - 1];
        }

        public bool AddWithRuleCheck(Chord chord)
        {
            if (!Check(chord)) {
                return false;
            }
            Add(chord);
            return true;
        }

        /// <summary>
        /// check rules of harmony
        /// </summary>
        /// <param name="chord"></param>
        /// <returns></returns>
        public bool Check(Chord chord)
        {
            return true;
        }
    }

    class Composer
    {
        Pitch P(MN n, int octave)
        {
            Pitch p = new Pitch();
            p.musicalNote = n;
            p.octave = octave;
            return p;
        }




        public Music CreateMusic()
        {
            Music music = new Music();
            music.Add(new Chord(CT.V7, P(MN.B, 4), P(MN.G, 4), P(MN.E, 3), P(MN.C, 3)));
            music.Add(new Chord(CT.I,  P(MN.E, 4), P(MN.C, 4), P(MN.G, 3), P(MN.C, 3)));
            return music;
        }
    }
}
