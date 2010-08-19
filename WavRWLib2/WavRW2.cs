using System;
using System.IO;
using System.Collections.Generic;

namespace WavRWLib2
{
    class RiffChunkDescriptor
    {
        private byte[] m_chunkId;
        private uint   m_chunkSize;
        private byte[] m_format;

        public void Create(int chunkSize)
        {
            m_chunkId = new byte[4];
            m_chunkId[0] = (byte)'R';
            m_chunkId[1] = (byte)'I';
            m_chunkId[2] = (byte)'F';
            m_chunkId[3] = (byte)'F';

            m_chunkSize = (uint)chunkSize;

            m_format = new byte[4];
            m_format[0] = (byte)'W';
            m_format[1] = (byte)'A';
            m_format[2] = (byte)'V';
            m_format[3] = (byte)'E';
        }

        public bool Read(BinaryReader br)
        {
            m_chunkId = br.ReadBytes(4);
            if (m_chunkId[0] != 'R' || m_chunkId[1] != 'I' || m_chunkId[2] != 'F' || m_chunkId[3] != 'F') {
                Console.WriteLine("E: RiffChunkDescriptor.chunkId mismatch. \"{0}{1}{2}{3}\" should be \"RIFF\"",
                    (char)m_chunkId[0], (char)m_chunkId[1], (char)m_chunkId[2], (char)m_chunkId[3]);
                return false;
            }

            m_chunkSize = br.ReadUInt32();
            if (m_chunkSize < 36) {
                Console.WriteLine("E: chunkSize is too small {0}", m_chunkSize);
                return false;
            }

            m_format = br.ReadBytes(4);
            if (m_format[0] != 'W' || m_format[1] != 'A' || m_format[2] != 'V' || m_format[3] != 'E') {
                Console.WriteLine("E: RiffChunkDescriptor.format mismatch. \"{0}{1}{2}{3}\" should be \"WAVE\"",
                    (char)m_format[0], (char)m_format[1], (char)m_format[2], (char)m_format[3]);
                return false;
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(m_chunkId);
            bw.Write(m_chunkSize);
            bw.Write(m_format);
        }
    }

    class FmtSubChunk
    {
        private byte[] m_subChunk1Id;
        private uint m_subChunk1Size;
        private ushort m_audioFormat;
        public ushort NumChannels { get; set; }
        public uint SampleRate { get; set; }

        private uint   m_byteRate;
        private ushort m_blockAlign;
        public ushort BitsPerSample { get; set; }

        public bool Create(ushort numChannels, uint sampleRate, ushort bitsPerSample)
        {
            m_subChunk1Id = new byte[4];
            m_subChunk1Id[0] = (byte)'f';
            m_subChunk1Id[1] = (byte)'m';
            m_subChunk1Id[2] = (byte)'t';
            m_subChunk1Id[3] = (byte)' ';

            m_subChunk1Size = 16;

            m_audioFormat = 1;

            System.Diagnostics.Debug.Assert(0 < numChannels);
            NumChannels = numChannels;

            SampleRate = sampleRate;
            m_byteRate = sampleRate * numChannels * bitsPerSample / 8;
            m_blockAlign = (ushort)(numChannels * bitsPerSample / 8);

            BitsPerSample = bitsPerSample;

            return true;
        }

        public bool Read(BinaryReader br)
        {
            m_subChunk1Id = br.ReadBytes(4);
            while (m_subChunk1Id[0] != 'f' || m_subChunk1Id[1] != 'm' || m_subChunk1Id[2] != 't' || m_subChunk1Id[3] != ' ') {
                // Windows Media Playerで取り込んだWAV。"LIST"のあとに、チャンクサイズがあるので、スキップする。
                Console.WriteLine("D: FmtSubChunk skip \"{0}{1}{2}{3}\"",
                    (char)m_subChunk1Id[0], (char)m_subChunk1Id[1], (char)m_subChunk1Id[2], (char)m_subChunk1Id[3]);

                int waveChunkSize = br.ReadInt32();
                if (waveChunkSize <= 0) {
                    Console.WriteLine("E: FmtSubChunk chunk corrupted");
                    return false;
                }
                br.ReadBytes(waveChunkSize);
                m_subChunk1Id = br.ReadBytes(4);
            }

            m_subChunk1Size = br.ReadUInt32();
            if (16 != m_subChunk1Size && 18 != m_subChunk1Size) {
                Console.WriteLine("E: FmtSubChunk.subChunk1Size != 16 {0} this file type is not supported", m_subChunk1Size);
                return false;
            }

            m_audioFormat = br.ReadUInt16();
            if (1 != m_audioFormat) {
                Console.WriteLine("E: this wave file is not PCM format {0}. Cannot read this file", m_audioFormat);
                return false;
            }

            NumChannels = br.ReadUInt16();
            Console.WriteLine("D: numChannels={0}", NumChannels);

            SampleRate = br.ReadUInt32();
            Console.WriteLine("D: sampleRate={0}", SampleRate);

            m_byteRate = br.ReadUInt32();
            Console.WriteLine("D: byteRate={0}", m_byteRate);

            m_blockAlign = br.ReadUInt16();
            Console.WriteLine("D: blockAlign={0}", m_blockAlign);

            BitsPerSample = br.ReadUInt16();
            Console.WriteLine("D: bitsPerSample={0}", BitsPerSample);

            if (16 < m_subChunk1Size) {
                br.ReadBytes((int)(m_subChunk1Size - 16));
            }

            if (m_byteRate != SampleRate * NumChannels * BitsPerSample / 8) {
                Console.WriteLine("E: byteRate is wrong value. corrupted file?");
                return false;
            }

            if (m_blockAlign != NumChannels * BitsPerSample / 8) {
                Console.WriteLine("E: blockAlign is wrong value. corrupted file?");
                return false;
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(m_subChunk1Id);
            bw.Write(m_subChunk1Size);
            bw.Write(m_audioFormat);
            bw.Write(NumChannels);
            bw.Write(SampleRate);

            bw.Write(m_byteRate);
            bw.Write(m_blockAlign);
            bw.Write(BitsPerSample);
        }
    }

    public class PcmSamples1Channel
    {
        private int     m_bitsPerSample;

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
        private byte[] m_subChunk2Id;
        private uint   m_subChunk2Size;

        private List<PcmSamples1Channel> m_data;

        // rawDataモードの場合、numSamplesにサンプル数が入っている。
        // 通常モードの場合、data??[0].NumSamples
        private byte[] m_rawData;
        private int    m_numSamples;

        public byte[] SampleRawGet() {
            return m_rawData;
        }

        public short Sample16Get(int ch, int pos)
        {
            return m_data[ch].Get16(pos);
        }

        public void Sample16Set(int ch, int pos, short val)
        {
            m_data[ch].Set16(pos, val);
        }

        public int NumSamples
        {
            get {
                if (m_data != null && 0 < m_data.Count) {
                    return m_data[0].NumSamples;
                }
                return m_numSamples;
            }
        }

        public void Create(uint subChunk2Size, List<PcmSamples1Channel> allChannelSamples)
        {
            m_subChunk2Id = new byte[4];
            m_subChunk2Id[0] = (byte)'d';
            m_subChunk2Id[1] = (byte)'a';
            m_subChunk2Id[2] = (byte)'t';
            m_subChunk2Id[3] = (byte)'a';

            this.m_subChunk2Size = subChunk2Size;
            this.m_data = allChannelSamples;
        }

        private bool SkipToDataHeader(BinaryReader br) {
            while (true) {
                m_subChunk2Id = br.ReadBytes(4);
                if (m_subChunk2Id[0] != 'd' || m_subChunk2Id[1] != 'a' || m_subChunk2Id[2] != 't' || m_subChunk2Id[3] != 'a') {
                    Console.WriteLine("D: DataSubChunk.subChunk2Id mismatch. \"{0}{1}{2}{3}\" should be \"data\". skipping.",
                        (char)m_subChunk2Id[0], (char)m_subChunk2Id[1], (char)m_subChunk2Id[2], (char)m_subChunk2Id[3]);
                    m_subChunk2Size = br.ReadUInt32();
                    if (0x80000000 <= m_subChunk2Size) {
                        Console.WriteLine("E: file too large to handle. {0} bytes", m_subChunk2Size);
                        return false;
                    }

                    // skip this header
                    br.ReadBytes((int)m_subChunk2Size);
                } else {
                    return true;
                }
            }
        }

        /// <summary>
        /// forget data part.
        /// </summary>
        public void ForgetDataPart() {
            m_data = null;
            m_rawData = null;
        }

        public bool ReadHeader(BinaryReader br, int numChannels, int bitsPerSample) {
            if (!SkipToDataHeader(br)) {
                return false;
            }

            m_subChunk2Size = br.ReadUInt32();
            Console.WriteLine("D: subChunk2Size={0}", m_subChunk2Size);
            if (0x80000000 <= m_subChunk2Size) {
                Console.WriteLine("E: file too large to handle. {0} bytes", m_subChunk2Size);
                return false;
            }

            m_numSamples = (int)(m_subChunk2Size / (bitsPerSample / 8) / numChannels);

            m_data    = null;
            m_rawData = null;
            return true;
        }

        public bool ReadRaw(BinaryReader br, int numChannels, int bitsPerSample) {
            if (!ReadHeader(br, numChannels, bitsPerSample)) {
                return false;
            }

            m_rawData = br.ReadBytes((int)m_subChunk2Size);
            return true;
        }

        public bool Read(BinaryReader br, int numChannels, int bitsPerSample)
        {
            System.Diagnostics.Debug.Assert(16 == bitsPerSample);
            if (!ReadHeader(br, numChannels, bitsPerSample)) {
                return false;
            }

            m_data = new List<PcmSamples1Channel>();
            for (int i=0; i < numChannels; ++i) {
                PcmSamples1Channel ps1 = new PcmSamples1Channel(m_numSamples, bitsPerSample);
                m_data.Add(ps1);
            }

            for (int pos=0; pos < m_numSamples; ++pos) {
                for (int ch=0; ch < numChannels; ++ch) {
                    Sample16Set(ch, pos, br.ReadInt16());
                }
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(m_subChunk2Id);
            bw.Write(m_subChunk2Size);

            switch (m_data[0].BitsPerSample) {
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
            int numSamples = m_data[0].NumSamples;
            int numChannels = m_data.Count;
            for (int pos = 0; pos < numSamples; ++pos) {
                for (int ch = 0; ch < numChannels; ++ch) {
                    bw.Write(m_data[ch].Get16(pos));
                }
            }
        }

        private void Write32(BinaryWriter bw) {
            int numSamples = m_data[0].NumSamples;
            int numChannels = m_data.Count;
            for (int pos = 0; pos < numSamples; ++pos) {
                for (int ch = 0; ch < numChannels; ++ch) {
                    bw.Write(m_data[ch].Get32(pos));
                }
            }
        }
    }

    public class WavData
    {
        private RiffChunkDescriptor m_rcd;
        private FmtSubChunk         m_fsc;
        private DataSubChunk        m_dsc;

        public int Id { get; set; }
        public string FileName { get; set; }
        public string FullPath { get; set; }

        /// <summary>
        /// サンプリング周波数と量子化ビット数が同じならtrue
        /// </summary>
        public bool IsSameFormat(WavData other) {
            return m_fsc.BitsPerSample == other.m_fsc.BitsPerSample
                && m_fsc.SampleRate    == other.m_fsc.SampleRate;
        }

        public bool Create(int sampleRate, int bitsPerSample, List<PcmSamples1Channel> samples)
        {
            int subChunk2Size = samples[0].NumSamples * (bitsPerSample / 8) * samples.Count;
            int chunkSize     = subChunk2Size + 36;

            m_rcd = new RiffChunkDescriptor();
            m_rcd.Create(chunkSize);

            m_fsc = new FmtSubChunk();
            if (!m_fsc.Create((ushort)samples.Count, (uint)sampleRate, (ushort)bitsPerSample)) {
                return false;
            }

            m_dsc = new DataSubChunk();
            m_dsc.Create((uint)subChunk2Size, samples);

            return true;
        }

        private enum ReadMode {
            ChannelList,
            RawData,
            OnlyHeader
        }

        private bool Read(BinaryReader br, ReadMode mode)
        {
            m_rcd = new RiffChunkDescriptor();
            if (!m_rcd.Read(br)) {
                return false;
            }

            m_fsc = new FmtSubChunk();
            if (!m_fsc.Read(br)) {
                return false;
            }

            m_dsc = new DataSubChunk();
            switch (mode) {
            case ReadMode.ChannelList:
                if (!m_dsc.Read(br, m_fsc.NumChannels, m_fsc.BitsPerSample)) {
                    return false;
                }
                break;
            case ReadMode.RawData:
                if (!m_dsc.ReadRaw(br, m_fsc.NumChannels, m_fsc.BitsPerSample)) {
                    return false;
                }
                break;
            case ReadMode.OnlyHeader:
                if (!m_dsc.ReadHeader(br, m_fsc.NumChannels, m_fsc.BitsPerSample)) {
                    return false;
                }
                break;
            }

            return true;
        }

        /// <summary>
        /// read only header part.
        /// NumChannels
        /// BitsPerSample
        /// SampleRate
        /// NumSamples
        /// </summary>
        public bool ReadHeader(BinaryReader br) {
            return Read(br, ReadMode.OnlyHeader);
        }

        /// <summary>
        /// forget data part
        /// Raw Data
        /// Channel List
        /// </summary>
        public void ForgetDataPart() {
            m_dsc.ForgetDataPart();
        }

        public bool Read(BinaryReader br) {
            return Read(br, ReadMode.ChannelList);
        }

        public bool ReadRaw(BinaryReader br) {
            return Read(br, ReadMode.RawData);
        }

        public void Write(BinaryWriter bw)
        {
            m_rcd.Write(bw);
            m_fsc.Write(bw);
            m_dsc.Write(bw);
        }

        public int NumChannels
        {
            get { return m_fsc.NumChannels; }
        }

        public int BitsPerSample
        {
            get { return m_fsc.BitsPerSample; }
        }

        public int NumSamples
        {
            get { return m_dsc.NumSamples; }
        }

        public int SampleRate
        {
            get { return (int)m_fsc.SampleRate; }
        }

        public short Sample16Get(int ch, int pos)
        {
            return m_dsc.Sample16Get(ch, pos);
        }

        public void Sample16Set(int ch, int pos, short val)
        {
            m_dsc.Sample16Set(ch, pos, val);
        }

        public byte[] SampleRawGet() {
            return m_dsc.SampleRawGet();
        }
    }
}
