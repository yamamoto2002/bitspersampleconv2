using System;
using System.Collections.Generic;

namespace BpsConvWin2
{
    class SampledData1channel
    {
        private uint    sampleRate = 0;
        private float   sampleMax = float.MinValue;
        private float   sampleMin = float.MaxValue;
        private List<float> samples = new List<float>();

        public SampledData1channel(uint aSampleRate)
        {
            sampleRate = aSampleRate;
        }

        public uint SampleRate
        {
            get { return sampleRate; }
            set { sampleRate = value; }
        }

        public float SampleMax
        {
            get { return sampleMax; }
        }

        public float SampleMin
        {
            get { return sampleMin; }
        }

        public void SampleAdd(float sample)
        {
            samples.Add(sample);
            if (sampleMax < sample) {
                sampleMax = sample;
            }
            if (sample < sampleMin) {
                sampleMin = sample;
            }
        }

        public void Scale(float magnify)
        {
            for (int i=0; i<samples.Count; ++i) {
                samples[i] = magnify * samples[i];
            }
        }
    }

    class SampledData
    {
        private List<SampledData1channel> channels = null;
        private int valueVariation = 0;

        public int ValueVariation
        {
            get { return valueVariation; }
            set { valueVariation = value; }
        }

        public SampledData(uint sampleRate, int ch)
        {
            channels = new List<SampledData1channel>();
            for (int i=0; i < ch; ++i) {
                channels.Add(new SampledData1channel(sampleRate));
            }
        }

        public SampledData1channel Channel(int ch)
        {
            System.Diagnostics.Debug.Assert(0 <= ch && ch < channels.Count);
            return channels[ch];
        }
    }
}
