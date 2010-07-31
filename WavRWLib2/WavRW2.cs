using System;
using System.IO;
using System.Collections.Generic;

namespace WavRWLib2
{
    class RiffChunkDescriptor
    {
        public byte[] chunkId;
        public uint   chunkSize;
        public byte[] format;

        public void Create(int chunkSize)
        {
            chunkId = new byte[4];
            chunkId[0] = (byte)'R';
            chunkId[1] = (byte)'I';
            chunkId[2] = (byte)'F';
            chunkId[3] = (byte)'F';

            this.chunkSize = (uint)chunkSize;

            format = new byte[4];
            format[0] = (byte)'W';
            format[1] = (byte)'A';
            format[2] = (byte)'V';
            format[3] = (byte)'E';
        }

        public bool Read(BinaryReader br)
        {
            chunkId = br.ReadBytes(4);
            if (chunkId[0] != 'R' || chunkId[1] != 'I' || chunkId[2] != 'F' || chunkId[3] != 'F') {
                Console.WriteLine("E: RiffChunkDescriptor.chunkId mismatch. \"{0}{1}{2}{3}\" should be \"RIFF\"",
                    (char)chunkId[0], (char)chunkId[1], (char)chunkId[2], (char)chunkId[3]);
                return false;
            }

            chunkSize = br.ReadUInt32();
            if (chunkSize < 36) {
                Console.WriteLine("E: chunkSize is too small {0}", chunkSize);
                return false;
            }

            format = br.ReadBytes(4);
            if (format[0] != 'W' || format[1] != 'A' || format[2] != 'V' || format[3] != 'E') {
                Console.WriteLine("E: RiffChunkDescriptor.format mismatch. \"{0}{1}{2}{3}\" should be \"WAVE\"",
                    (char)format[0], (char)format[1], (char)format[2], (char)format[3]);
                return false;
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(chunkId);
            bw.Write(chunkSize);
            bw.Write(format);
        }
    }

    class FmtSubChunk
    {
        public byte[] subChunk1Id;
        public uint   subChunk1Size;
        public ushort audioFormat;
        public ushort numChannels;
        public uint   sampleRate;

        public uint   byteRate;
        public ushort blockAlign;
        public ushort bitsPerSample;

        public bool Create(ushort numChannels, uint sampleRate, ushort bitsPerSample)
        {
            subChunk1Id = new byte[4];
            subChunk1Id[0] = (byte)'f';
            subChunk1Id[1] = (byte)'m';
            subChunk1Id[2] = (byte)'t';
            subChunk1Id[3] = (byte)' ';

            subChunk1Size = 16;

            audioFormat = 1;

            System.Diagnostics.Debug.Assert(0 < numChannels);
            this.numChannels = numChannels;

            this.sampleRate = sampleRate;
            this.byteRate = sampleRate * numChannels * bitsPerSample / 8;
            this.blockAlign = (ushort)(numChannels * bitsPerSample / 8);

            this.bitsPerSample = bitsPerSample;

            return true;
        }

        public bool Read(BinaryReader br)
        {
            subChunk1Id = br.ReadBytes(4);
            if (subChunk1Id[0] != 'f' || subChunk1Id[1] != 'm' || subChunk1Id[2] != 't' || subChunk1Id[3] != ' ') {
                Console.WriteLine("E: FmtSubChunk.subChunk1Id mismatch. \"{0}{1}{2}{3}\" should be \"fmt \"",
                    (char)subChunk1Id[0], (char)subChunk1Id[1], (char)subChunk1Id[2], (char)subChunk1Id[3]);
                return false;
            }

            subChunk1Size = br.ReadUInt32();
            if (16 != subChunk1Size) {
                Console.WriteLine("E: FmtSubChunk.subChunk1Size != 16 {0} this file type is not supported", subChunk1Size);
                return false;
            }

            audioFormat = br.ReadUInt16();
            if (1 != audioFormat) {
                Console.WriteLine("E: this wave file is not PCM format {0}. Cannot read this file", audioFormat);
                return false;
            }

            numChannels = br.ReadUInt16();
            Console.WriteLine("D: numChannels={0}", numChannels);

            sampleRate = br.ReadUInt32();
            Console.WriteLine("D: sampleRate={0}", sampleRate);

            byteRate = br.ReadUInt32();
            Console.WriteLine("D: byteRate={0}", byteRate);

            blockAlign = br.ReadUInt16();
            Console.WriteLine("D: blockAlign={0}", blockAlign);

            bitsPerSample = br.ReadUInt16();
            Console.WriteLine("D: bitsPerSample={0}", bitsPerSample);

            if (byteRate != sampleRate * numChannels * bitsPerSample / 8) {
                Console.WriteLine("E: byteRate is wrong value. corrupted file?");
                return false;
            }

            if (blockAlign != numChannels * bitsPerSample / 8) {
                Console.WriteLine("E: blockAlign is wrong value. corrupted file?");
                return false;
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(subChunk1Id);
            bw.Write(subChunk1Size);
            bw.Write(audioFormat);
            bw.Write(numChannels);
            bw.Write(sampleRate);

            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);
        }
    }

    public class PcmSamples1Channel
    {
        private int m_bitsPerSample;

        private short[] m_data16;
        private int[]   m_data32;

        public PcmSamples1Channel(int numSamples, int bitsPerSample)
        {
            m_bitsPerSample = bitsPerSample;
            switch (bitsPerSample) {
            case 16:
                m_data16 = new short[numSamples];
                m_data32 = null;
                break;
            case 32:
                m_data16 = null;
                m_data32 = new int[numSamples];
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        public void Set16(int pos, short val)
        {
            m_data16[pos] = val;
        }

        public short Get16(int pos)
        {
            return m_data16[pos];
        }

        public void Set32(int pos, int val) {
            m_data32[pos] = val;
        }

        public int Get32(int pos) {
            return m_data32[pos];
        }

        public int NumSamples
        {
            get {
                if (m_data16 != null) {
                    return m_data16.Length;
                }
                if (m_data32 != null) {
                    return m_data32.Length;
                }
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }
        }

        public int BitsPerSample {
            get {
                return m_bitsPerSample;
            }
        }
    }

    class DataSubChunk
    {
        public byte[] subChunk2Id;
        public uint   subChunk2Size;

        private List<PcmSamples1Channel> data;

        // rawDataモードの場合、numSamplesにサンプル数が入っている。
        // 通常モードの場合、data??[0].NumSamples
        private byte[] rawData;
        int            numSamples;

        public byte[] SampleRawGet() {
            return rawData;
        }

        public short Sample16Get(int ch, int pos)
        {
            return data[ch].Get16(pos);
        }

        public void Sample16Set(int ch, int pos, short val)
        {
            data[ch].Set16(pos, val);
        }

        public int NumSamples
        {
            get {
                if (data != null && 0 < data.Count) {
                    return data[0].NumSamples;
                }
                return numSamples;
            }
        }

        public void Create(uint subChunk2Size, List<PcmSamples1Channel> allChannelSamples)
        {
            subChunk2Id = new byte[4];
            subChunk2Id[0] = (byte)'d';
            subChunk2Id[1] = (byte)'a';
            subChunk2Id[2] = (byte)'t';
            subChunk2Id[3] = (byte)'a';

            this.subChunk2Size = subChunk2Size;
            this.data = allChannelSamples;
        }

        private bool SkipToDataHeader(BinaryReader br) {
            while (true) {
                subChunk2Id = br.ReadBytes(4);
                if (subChunk2Id[0] != 'd' || subChunk2Id[1] != 'a' || subChunk2Id[2] != 't' || subChunk2Id[3] != 'a') {
                    Console.WriteLine("D: DataSubChunk.subChunk2Id mismatch. \"{0}{1}{2}{3}\" should be \"data\". skipping.",
                        (char)subChunk2Id[0], (char)subChunk2Id[1], (char)subChunk2Id[2], (char)subChunk2Id[3]);
                    subChunk2Size = br.ReadUInt32();
                    if (0x80000000 <= subChunk2Size) {
                        Console.WriteLine("E: file too large to handle. {0} bytes", subChunk2Size);
                        return false;
                    }

                    // skip this header
                    br.ReadBytes((int)subChunk2Size);
                } else {
                    return true;
                }
            }
        }

        public bool ReadRaw(BinaryReader br, int numChannels, int bitsPerSample) {
            if (!SkipToDataHeader(br)) {
                return false;
            }

            subChunk2Size = br.ReadUInt32();
            Console.WriteLine("D: subChunk2Size={0}", subChunk2Size);
            if (0x80000000 <= subChunk2Size) {
                Console.WriteLine("E: file too large to handle. {0} bytes", subChunk2Size);
                return false;
            }

            numSamples = (int)(subChunk2Size / (bitsPerSample / 8) / numChannels);

            rawData = br.ReadBytes((int)subChunk2Size);
            return true;
        }

        public bool Read(BinaryReader br, int numChannels, int bitsPerSample)
        {
            System.Diagnostics.Debug.Assert(16 == bitsPerSample);

            if (!SkipToDataHeader(br)) {
                return false;
            }

            subChunk2Size = br.ReadUInt32();
            Console.WriteLine("D: subChunk2Size={0}", subChunk2Size);
            if (0x80000000 <= subChunk2Size) {
                Console.WriteLine("E: file too large to handle. {0} bytes", subChunk2Size);
                return false;
            }

            numSamples = (int)(subChunk2Size / (bitsPerSample / 8) / numChannels);

            data = new List<PcmSamples1Channel>();
            for (int i=0; i < numChannels; ++i) {
                PcmSamples1Channel ps1 = new PcmSamples1Channel(numSamples, bitsPerSample);
                data.Add(ps1);
            }

            for (int pos=0; pos < numSamples; ++pos) {
                for (int ch=0; ch < numChannels; ++ch) {
                    Sample16Set(ch, pos, br.ReadInt16());
                }
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(subChunk2Id);
            bw.Write(subChunk2Size);

            switch (data[0].BitsPerSample) {
            case 16:
                Write16(bw);
                break;
            case 32:
                Write32(bw);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
        }

        private void Write16(BinaryWriter bw) {
            int numSamples = data[0].NumSamples;
            int numChannels = data.Count;
            for (int pos = 0; pos < numSamples; ++pos) {
                for (int ch = 0; ch < numChannels; ++ch) {
                    bw.Write(data[ch].Get16(pos));
                }
            }
        }

        private void Write32(BinaryWriter bw) {
            int numSamples = data[0].NumSamples;
            int numChannels = data.Count;
            for (int pos = 0; pos < numSamples; ++pos) {
                for (int ch = 0; ch < numChannels; ++ch) {
                    bw.Write(data[ch].Get32(pos));
                }
            }
        }
    }

    public class WavData
    {
        private RiffChunkDescriptor rcd;
        private FmtSubChunk         fsc;
        private DataSubChunk        dsc;

        public bool Create(int sampleRate, int bitsPerSample, List<PcmSamples1Channel> samples)
        {
            int subChunk2Size = samples[0].NumSamples * (bitsPerSample / 8) * samples.Count;
            int chunkSize     = subChunk2Size + 36;

            rcd = new RiffChunkDescriptor();
            rcd.Create(chunkSize);

            fsc = new FmtSubChunk();
            if (!fsc.Create((ushort)samples.Count, (uint)sampleRate, (ushort)bitsPerSample)) {
                return false;
            }

            dsc = new DataSubChunk();
            dsc.Create((uint)subChunk2Size, samples);

            return true;
        }

        private bool Read(BinaryReader br, bool raw=false)
        {
            rcd = new RiffChunkDescriptor();
            if (!rcd.Read(br)) {
                return false;
            }

            fsc = new FmtSubChunk();
            if (!fsc.Read(br)) {
                return false;
            }

            dsc = new DataSubChunk();
            if (raw) {
                if (!dsc.ReadRaw(br, fsc.numChannels, fsc.bitsPerSample)) {
                    return false;
                }
            } else {
                if (!dsc.Read(br, fsc.numChannels, fsc.bitsPerSample)) {
                    return false;
                }
            }
            return true;
        }

        public bool Read(BinaryReader br) {
            return Read(br, false);
        }

        public bool ReadRaw(BinaryReader br) {
            return Read(br, true);
        }

        public void Write(BinaryWriter bw)
        {
            rcd.Write(bw);
            fsc.Write(bw);
            dsc.Write(bw);
        }

        public int NumChannels
        {
            get { return fsc.numChannels; }
        }

        public int BitsPerSample
        {
            get { return fsc.bitsPerSample; }
        }

        public int NumSamples
        {
            get { return dsc.NumSamples; }
        }

        public int SampleRate
        {
            get { return (int)fsc.sampleRate; }
        }

        public short Sample16Get(int ch, int pos)
        {
            return dsc.Sample16Get(ch, pos);
        }

        public void Sample16Set(int ch, int pos, short val)
        {
            dsc.Sample16Set(ch, pos, val);
        }

        public byte[] SampleRawGet() {
            return dsc.SampleRawGet();
        }
    }
}
