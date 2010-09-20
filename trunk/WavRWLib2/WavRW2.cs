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

    public enum ValueRepresentationType {
        SInt,
        SFloat
    };

    class FmtSubChunk
    {
        private byte[] m_subChunk1Id;
        private uint m_subChunk1Size;
        private ushort m_audioFormat;
        public ushort NumChannels { get; set; }
        public uint SampleRate { get; set; }

        public ValueRepresentationType SampleValueRepresentationType { get; set; }

        private uint   m_byteRate;
        private ushort m_blockAlign;
        public ushort BitsPerSample { get; set; }

        public bool Create(int numChannels, int sampleRate, int bitsPerSample)
        {
            m_subChunk1Id = new byte[4];
            m_subChunk1Id[0] = (byte)'f';
            m_subChunk1Id[1] = (byte)'m';
            m_subChunk1Id[2] = (byte)'t';
            m_subChunk1Id[3] = (byte)' ';

            m_subChunk1Size = 16;

            m_audioFormat = 1;

            System.Diagnostics.Debug.Assert(0 < numChannels);
            NumChannels = (ushort)numChannels;

            SampleRate = (uint)sampleRate;
            m_byteRate = (uint)(sampleRate * numChannels * bitsPerSample / 8);
            m_blockAlign = (ushort)(numChannels * bitsPerSample / 8);

            BitsPerSample = (ushort)bitsPerSample;

            SampleValueRepresentationType = ValueRepresentationType.SInt;

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
            if (1 == m_audioFormat) {
                SampleValueRepresentationType = ValueRepresentationType.SInt;
            } else if (3 == m_audioFormat) {
                SampleValueRepresentationType = ValueRepresentationType.SFloat;
            } else {
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
        private long   m_subChunk2Size;

        private List<PcmSamples1Channel> m_data;

        // rawDataモードの場合、numSamplesにサンプル数が入っている。
        // 通常モードの場合、data??[0].NumSamples
        private byte[] m_rawData;
        private long    m_numSamples;

        public byte[] SampleRawGet() {
            return m_rawData;
        }

        public void TrimRawData(long newNumSamples, long startBytes, long endBytes) {
            System.Diagnostics.Debug.Assert(0 <= startBytes);
            System.Diagnostics.Debug.Assert(0 <= endBytes);
            System.Diagnostics.Debug.Assert(startBytes <= endBytes);

            m_numSamples = newNumSamples;
            if (newNumSamples == 0 ||
                m_rawData.Length <= startBytes) {
                m_rawData = null;
                m_numSamples = 0;
            } else {
                byte[] newArray = new byte[endBytes - startBytes];
                Array.Copy(m_rawData, startBytes, newArray, 0, endBytes - startBytes);
                m_rawData = null;
                m_rawData = newArray;
            }
        }

        public short Sample16Get(int ch, int pos)
        {
            return m_data[ch].Get16(pos);
        }

        public void Sample16Set(int ch, int pos, short val)
        {
            m_data[ch].Set16(pos, val);
        }

        public long NumSamples
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

        public void CreateHeader(int numChannels, int bitsPerSample, long numSamples) {
            m_numSamples = numSamples;
            m_subChunk2Size = m_numSamples * numChannels * bitsPerSample / 8;
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

            int frameBytes = bitsPerSample / 8 * numChannels;
            m_numSamples = m_subChunk2Size / frameBytes;

            m_data    = null;
            m_rawData = null;
            return true;
        }

        /// <summary>
        /// readerのデータをcountバイトだけスキップする。
        /// </summary>
        private static void BinaryReaderSkip(BinaryReader reader, long count) {
            if (reader.BaseStream.CanSeek) {
                reader.BaseStream.Seek(count, SeekOrigin.Current);
            }
            else {
                for (long i = 0; i < count; ++i) {
                    reader.ReadByte();
                }
            }
        }

        /// <summary>
        /// PCMデータを無加工で読み出す。
        /// </summary>
        /// <param name="startBytes">0を指定すると最初から。</param>
        /// <param name="endBytes">負の値を指定するとファイルの最後まで。</param>
        /// <returns>false: ファイルの読み込みエラーなど</returns>
        public bool ReadRaw(BinaryReader br, int numChannels, int bitsPerSample,
            long startBytes, long endBytes) {
            if (!ReadHeader(br, numChannels, bitsPerSample)) {
                return false;
            }

            // ReadHeaderによって、m_numSamplesが判明。
            // endBytesがファイルの終わり指定(負の値)の場合の具体的位置を設定する。
            // startBytesとendBytesがファイルの終わり以降を指していたら修正する。
            // ・endBytesがファイルの終わり以降…ファイルの終わりを指す。
            // ・startBytesがファイルの終わり以降…サイズ0バイトのWAVファイルにする。

            int frameBytes = bitsPerSample / 8 * numChannels;

            System.Diagnostics.Debug.Assert(0 <= startBytes);

            if (endBytes < 0 ||
                (m_numSamples * frameBytes) < endBytes) {
                // 終了位置はファイルの終わり。
                endBytes = NumSamples * frameBytes;
            }

            long newNumSamples = (endBytes - startBytes) / frameBytes;
            if (newNumSamples <= 0 ||
                m_numSamples * frameBytes <= startBytes ||
                endBytes <= startBytes) {
                // サイズが0バイトのWAV。
                m_rawData = null;
                m_numSamples = 0;
                return true;
            }

            if (0 < startBytes) {
                BinaryReaderSkip(br, startBytes);
            }

            m_rawData = br.ReadBytes((int)newNumSamples * frameBytes);
            m_numSamples = newNumSamples;
            return true;
        }

        public void SetRawData(byte[] rawData) {
            m_rawData = rawData;
        }

        public void Clear() {
            m_subChunk2Size = 0;
            m_data = null;
            m_rawData = null;
            m_subChunk2Id = null;
            m_numSamples = 0;
        }

        public void SetRawData(long numSamples, byte[] rawData) {
            m_numSamples = numSamples;
            m_rawData = rawData;
        }

        public bool Read(BinaryReader br, int numChannels, int bitsPerSample)
        {
            System.Diagnostics.Debug.Assert(16 == bitsPerSample);
            if (!ReadHeader(br, numChannels, bitsPerSample)) {
                return false;
            }

            m_data = new List<PcmSamples1Channel>();
            for (int i=0; i < numChannels; ++i) {
                PcmSamples1Channel ps1 = new PcmSamples1Channel((int)m_numSamples, bitsPerSample);
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

            uint subChunk2Size = (uint)m_subChunk2Size;
            bw.Write(subChunk2Size);

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

        public ValueRepresentationType SampleValueRepresentationType {
            get { return m_fsc.SampleValueRepresentationType; }
            set { m_fsc.SampleValueRepresentationType = value; }
        }

        /// <summary>
        /// ファイルグループ番号。何でもありの、物置みたいになってきたな…
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// 表示名。CUEシートから来る
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 開始Tick(75分の1秒=1)。0のとき、ファイルの先頭が開始Tick
        /// </summary>
        public int    StartTick { get; set; }

        /// <summary>
        /// 終了Tick(75分の1秒=1)。-1のとき、ファイルの終わりが終了Tick
        /// </summary>
        public int    EndTick { get; set; }

        /// <summary>
        /// StartTickとEndTickを見て、必要な部分以外をカットする。
        /// </summary>
        public void Trim() {
            if (StartTick < 0) {
                // データ壊れ。先頭を読む。
                StartTick = 0;
            }

            if (StartTick == 0 && EndTick == -1) {
                return;
            }

            long startBytes = (long)(StartTick) * SampleRate / 75 * BitsPerSample / 8 * NumChannels;
            long endBytes = (long)(EndTick) * SampleRate / 75 * BitsPerSample / 8 * NumChannels;

            if (endBytes < 0 ||
                (NumSamples * BitsPerSample / 8 * NumChannels) < endBytes) {
                // 終了位置はファイルの終わり。
                endBytes = NumSamples * BitsPerSample / 8 * NumChannels;
            }

            if (endBytes < startBytes) {
                // 1サンプルもない。
                startBytes = endBytes;
            }

            long newNumSamples = (endBytes - startBytes) / (BitsPerSample / 8 * NumChannels);
            m_dsc.TrimRawData(newNumSamples, startBytes, endBytes);
        }

        /// <summary>
        /// StartTickとEndTickを見て、必要な部分以外をカットする。
        /// </summary>
        public bool TrimmedReadRaw(BinaryReader br) {
            if (StartTick < 0) {
                // データ壊れ。先頭を読む。
                StartTick = 0;
            }

            int frameBytes = m_fsc.BitsPerSample / 8 * m_fsc.NumChannels;
            long startBytes = (long)(StartTick) * m_fsc.SampleRate / 75 * frameBytes;
            long endBytes   = (long)(EndTick)   * m_fsc.SampleRate / 75 * frameBytes;

            if (0 <= endBytes && endBytes < startBytes) {
                // 1サンプルもない。
                startBytes = endBytes;
            }

            return m_dsc.ReadRaw(br, m_fsc.NumChannels, m_fsc.BitsPerSample, startBytes, endBytes);
        }

        /// <summary>
        /// rhsの内容(チャンネル数、サンプルレート、量子化ビット数の情報)を自分自身にコピーする。
        /// DSC(PCMデータ)はコピーしない。(空データとなる)
        /// </summary>
        /// <param name="rhs">from</param>
        public void CopyHeaderInfoFrom(WavData rhs) {
            m_rcd = new RiffChunkDescriptor();
            m_rcd.Create(36);

            m_fsc = new FmtSubChunk();
            m_fsc.Create(rhs.NumChannels, rhs.SampleRate, rhs.BitsPerSample);

            m_dsc = new DataSubChunk();

            Id = rhs.Id;
            FileName = rhs.FileName;
            FullPath = rhs.FullPath;
            GroupId = rhs.GroupId;
        }

        public void CreateHeader(int nChannels, int sampleRate, int bitsPerSample, long numSamples) {
            m_rcd = new RiffChunkDescriptor();
            m_rcd.Create(36);

            m_fsc = new FmtSubChunk();
            m_fsc.Create(nChannels, sampleRate, bitsPerSample);

            m_dsc = new DataSubChunk();
            m_dsc.CreateHeader(nChannels, bitsPerSample, numSamples);
        }

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
            if (!m_fsc.Create(samples.Count, sampleRate, bitsPerSample)) {
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
                if (!TrimmedReadRaw(br)) {
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

        public long NumSamples
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

        public void SetRawData(byte[] rawData) {
            m_dsc.SetRawData(rawData);
        }

        /// <summary>
        /// 量子化ビット数をbitsPerSampleに変更したWavDataを戻す。
        /// 自分自身の内容は変更しない。
        /// 
        /// RawDataモードの場合のみの対応。
        /// </summary>
        /// <param name="newBitsPerSample">新しい量子化ビット数</param>
        /// <returns>量子化ビット数変更後のWavData</returns>
        public WavData BitsPerSampleConvertTo(int newBitsPerSample, ValueRepresentationType newValueRepType) {
            WavData newWavData = new WavData();
            newWavData.CopyHeaderInfoFrom(this);
            newWavData.m_fsc.Create(NumChannels, SampleRate, newBitsPerSample);

            byte [] rawData = null;
            if (newBitsPerSample == 32) {
                if (newValueRepType == ValueRepresentationType.SFloat) {
                    switch (BitsPerSample) {
                    case 16:
                        rawData = ConvI16toF32(SampleRawGet());
                        break;
                    case 24:
                        rawData = ConvI24toF32(SampleRawGet());
                        break;
                    case 32:
                        if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                            rawData = (byte[])SampleRawGet().Clone();
                        } else {
                            rawData = ConvI32toF32(SampleRawGet());
                        }
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        return null;
                    }
                } else if (newValueRepType == ValueRepresentationType.SInt) {
                    switch (BitsPerSample) {
                    case 16:
                        rawData = ConvI16toI32(SampleRawGet());
                        break;
                    case 24:
                        rawData = ConvI24toI32(SampleRawGet());
                        break;
                    case 32:
                        if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                            rawData = ConvF32toI32(SampleRawGet());
                        } else {
                            rawData = (byte[])SampleRawGet().Clone();
                        }
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        return null;
                    }
                } else {
                    System.Diagnostics.Debug.Assert(false);
                    return null;
                }
            } else if (newBitsPerSample == 24) {
                switch (BitsPerSample) {
                case 16:
                    rawData = ConvI16toI24(SampleRawGet());
                    break;
                case 24:
                    rawData = (byte[])SampleRawGet().Clone();
                    break;
                case 32:
                    if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                        rawData = ConvF32toI24(SampleRawGet());
                    } else {
                        rawData = ConvI32toI24(SampleRawGet());
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return null;
                }
            } else if (newBitsPerSample == 16) {
                switch (BitsPerSample) {
                case 16:
                    rawData = (byte[])SampleRawGet().Clone();
                    break;
                case 24:
                    rawData = ConvI24toI16(SampleRawGet());
                    break;
                case 32:
                    if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                        rawData = ConvF32toI16(SampleRawGet());
                    } else {
                        rawData = ConvI32toI16(SampleRawGet());
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return null;
                }
            } else {
                System.Diagnostics.Debug.Assert(false);
                return null;
            }

            newWavData.SampleValueRepresentationType = newValueRepType;
            newWavData.m_dsc = new DataSubChunk();
            newWavData.m_dsc.SetRawData(NumSamples, rawData);

            return newWavData;
        }

        private byte[] ConvI16toI24(byte[] from) {
            int nSample = from.Length/2;
            byte[] to = new byte[nSample * 3];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットは、0埋めする。
                to[toPos++] = 0;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }
        private byte[] ConvI16toI32(byte[] from) {
            int nSample = from.Length/2;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットは、0埋めする。
                to[toPos++] = 0;
                to[toPos++] = 0;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }

        private byte[] ConvI24toI32(byte[] from) {
            int nSample = from.Length/3;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットは、0埋めする。
                to[toPos++] = 0;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }

        private byte[] ConvI24toI16(byte[] from) {
            int nSample = from.Length / 3;
            byte[] to = new byte[nSample * 2];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットの情報が失われる瞬間
                ++fromPos;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }

        private byte[] ConvI32toI16(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 2];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットの情報が失われる瞬間
                ++fromPos;
                ++fromPos;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }

        private byte[] ConvI32toI24(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 3];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットの情報が失われる瞬間
                ++fromPos;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }

        private byte[] ConvF32toI16(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 2];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                float fv = System.BitConverter.ToSingle(from, fromPos);
                int iv = (int)(fv * 32768.0f);

                to[toPos++] = (byte)(iv & 0xff);
                to[toPos++] = (byte)((iv >> 8) & 0xff);
                fromPos += 4;
            }
            return to;
        }
        private byte[] ConvF32toI24(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 3];
            int fromPos = 0;
            int toPos   = 0;
            for (int i = 0; i < nSample; ++i) {
                float fv = System.BitConverter.ToSingle(from, fromPos);
                int iv = (int)(fv * 8388608.0f);

                to[toPos++] = (byte)(iv & 0xff);
                to[toPos++] = (byte)((iv>>8) & 0xff);
                to[toPos++] = (byte)((iv>>16) & 0xff);
                fromPos += 4;
            }
            return to;
        }

        private byte[] ConvF32toI32(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos   = 0;
            for (int i = 0; i < nSample; ++i) {
                float fv = System.BitConverter.ToSingle(from, fromPos);
                int iv = (int)(fv * 8388608.0f);

                to[toPos++] = 0;
                to[toPos++] = (byte)(iv & 0xff);
                to[toPos++] = (byte)((iv>>8) & 0xff);
                to[toPos++] = (byte)((iv>>16) & 0xff);
                fromPos += 4;
            }
            return to;
        }

        private byte[] ConvI16toF32(byte[] from) {
            int nSample = from.Length / 2;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                short iv = (short)(from[fromPos]
                    + (from[fromPos+1]<<8));
                float fv = ((float)iv) * (1.0f / 32768.0f);

                byte [] b = System.BitConverter.GetBytes(fv);

                to[toPos++] = b[0];
                to[toPos++] = b[1];
                to[toPos++] = b[2];
                to[toPos++] = b[3];
                fromPos += 2;
            }
            return to;
        }
        private byte[] ConvI24toF32(byte[] from) {
            int nSample = from.Length / 3;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                int iv = ((int)from[fromPos]<<8)
                    + ((int)from[fromPos+1]<<16)
                    + ((int)from[fromPos+2]<<24);
                float fv = ((float)iv) * (1.0f / 2147483648.0f);

                byte [] b = System.BitConverter.GetBytes(fv);

                to[toPos++] = b[0];
                to[toPos++] = b[1];
                to[toPos++] = b[2];
                to[toPos++] = b[3];
                fromPos += 3;
            }
            return to;
        }
        private byte[] ConvI32toF32(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                int iv = ((int)from[fromPos+1]<<8)
                    + ((int)from[fromPos+2]<<16)
                    + ((int)from[fromPos+3]<<24);
                float fv = ((float)iv) * (1.0f / 2147483648.0f);

                byte [] b = System.BitConverter.GetBytes(fv);

                to[toPos++] = b[0];
                to[toPos++] = b[1];
                to[toPos++] = b[2];
                to[toPos++] = b[3];
                fromPos += 4;
            }
            return to;
        }
    }
}
