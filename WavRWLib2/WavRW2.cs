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

        public void Create(uint chunkSize)
        {
            m_chunkId = new byte[4];
            m_chunkId[0] = (byte)'R';
            m_chunkId[1] = (byte)'I';
            m_chunkId[2] = (byte)'F';
            m_chunkId[3] = (byte)'F';

            m_chunkSize = chunkSize;

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

        public PcmDataLib.PcmData.ValueRepresentationType SampleValueRepresentationType { get; set; }

        private uint   m_byteRate;
        private ushort m_blockAlign;
        public ushort BitsPerFrame { get; set; }

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

            BitsPerFrame = (ushort)bitsPerSample;

            SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SInt;

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
                SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SInt;
            } else if (3 == m_audioFormat) {
                SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SFloat;
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

            BitsPerFrame = br.ReadUInt16();
            Console.WriteLine("D: bitsPerSample={0}", BitsPerFrame);

            if (16 < m_subChunk1Size) {
                br.ReadBytes((int)(m_subChunk1Size - 16));
            }

            if (m_byteRate != SampleRate * NumChannels * BitsPerFrame / 8) {
                Console.WriteLine("E: byteRate is wrong value. corrupted file?");
                return false;
            }

            if (m_blockAlign != NumChannels * BitsPerFrame / 8) {
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
            bw.Write(BitsPerFrame);
        }
    }

    class DataSubChunk
    {
        private byte[] m_subChunk2Id;
        private long   m_subChunk2Size;

        private byte[] m_rawData;

        private long m_numFrames;

        public long NumFrames {
            get { return m_numFrames; }
        }

        public byte[] GetSampleArray() {
            return m_rawData;
        }

        public void Clear() {
            m_subChunk2Id = null;
            m_subChunk2Size = 0;
            m_rawData = null;
            m_numFrames = 0;
        }

        public void SetRawData(long numSamples, byte[] rawData) {
            m_numFrames = numSamples;
            m_rawData = rawData;
        }

        public void Create(long numSamples, byte[] rawData) {
            SetRawData(numSamples, rawData);
            m_subChunk2Id = new byte[4];
            m_subChunk2Id[0] = (byte)'d';
            m_subChunk2Id[1] = (byte)'a';
            m_subChunk2Id[2] = (byte)'t';
            m_subChunk2Id[3] = (byte)'a';
            m_subChunk2Size = rawData.LongLength;
        }

        public void TrimRawData(long newNumSamples, long startBytes, long endBytes) {
            System.Diagnostics.Debug.Assert(0 <= startBytes);
            System.Diagnostics.Debug.Assert(0 <= endBytes);
            System.Diagnostics.Debug.Assert(startBytes <= endBytes);

            m_numFrames = newNumSamples;
            if (newNumSamples == 0 ||
                m_rawData.Length <= startBytes) {
                m_rawData = null;
                m_numFrames = 0;
            } else {
                byte[] newArray = new byte[endBytes - startBytes];
                Array.Copy(m_rawData, startBytes, newArray, 0, endBytes - startBytes);
                m_rawData = null;
                m_rawData = newArray;
            }
        }

        /// <summary>
        /// readerのデータをcountバイトだけスキップする。
        /// </summary>
        private static void BinaryReaderSkip(BinaryReader reader, long count) {
            if (reader.BaseStream.CanSeek) {
                reader.BaseStream.Seek(count, SeekOrigin.Current);
            } else {
                for (long i = 0; i < count; ++i) {
                    reader.ReadByte();
                }
            }
        }

        private bool SkipToDataHeader(BinaryReader br) {
            while (true) {
                m_subChunk2Id = br.ReadBytes(4);
                if (m_subChunk2Id[0] != 'd' || m_subChunk2Id[1] != 'a' || m_subChunk2Id[2] != 't' || m_subChunk2Id[3] != 'a') {
                    Console.WriteLine("D: DataSubChunk.subChunk2Id mismatch. \"{0}{1}{2}{3}\" should be \"data\". skipping.",
                        (char)m_subChunk2Id[0], (char)m_subChunk2Id[1], (char)m_subChunk2Id[2], (char)m_subChunk2Id[3]);
                    m_subChunk2Size = br.ReadUInt32();

                    // skip this header
                    br.ReadBytes((int)m_subChunk2Size);
                } else {
                    return true;
                }
            }
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
            m_numFrames = m_subChunk2Size / frameBytes;

            m_rawData = null;
            return true;
        }

        /// <summary>
        /// PCMデータを無加工で読み出す。
        /// </summary>
        /// <param name="startFrame">0を指定すると最初から。</param>
        /// <param name="endFrame">負の値を指定するとファイルの最後まで。</param>
        /// <returns>false: ファイルの読み込みエラーなど</returns>
        public bool ReadRaw(BinaryReader br, int numChannels, int bitsPerSample,
            long startFrame, long endFrame) {
            if (!ReadHeader(br, numChannels, bitsPerSample)) {
                return false;
            }

            // ReadHeaderによって、m_numSamplesが判明。
            // endBytesがファイルの終わり指定(負の値)の場合の具体的位置を設定する。
            // startBytesとendBytesがファイルの終わり以降を指していたら修正する。
            // ・endBytesがファイルの終わり以降…ファイルの終わりを指す。
            // ・startBytesがファイルの終わり以降…サイズ0バイトのWAVファイルにする。

            int frameBytes = bitsPerSample / 8 * numChannels;
            long startBytes = startFrame * frameBytes;
            long endBytes   = endFrame   * frameBytes;

            System.Diagnostics.Debug.Assert(0 <= startBytes);

            if (endBytes < 0 ||
                (m_numFrames * frameBytes) < endBytes) {
                // 終了位置はファイルの終わり。
                endBytes = m_numFrames * frameBytes;
            }

            long newNumFrames = (endBytes - startBytes) / frameBytes;
            if (newNumFrames <= 0 ||
                m_numFrames * frameBytes <= startBytes ||
                endBytes <= startBytes) {
                // サイズが0バイトのWAV。
                m_rawData = null;
                m_numFrames = 0;
                return true;
            }

            if (0 < startBytes) {
                BinaryReaderSkip(br, startBytes);
            }

            m_rawData = br.ReadBytes((int)newNumFrames * frameBytes);
            m_numFrames = newNumFrames;
            return true;
        }

        public void Write(BinaryWriter bw) {
            bw.Write(m_subChunk2Id);

            uint subChunk2Size = (uint)m_subChunk2Size;
            bw.Write(subChunk2Size);

            bw.Write(m_rawData);
        }
    }

    public class WavData
    {
        private RiffChunkDescriptor m_rcd;
        private FmtSubChunk         m_fsc;
        private DataSubChunk        m_dsc;

        public PcmDataLib.PcmData.ValueRepresentationType SampleValueRepresentationType {
            get { return m_fsc.SampleValueRepresentationType; }
            set { m_fsc.SampleValueRepresentationType = value; }
        }

        private enum ReadMode {
            HeaderAndPcmData,
            OnlyHeader
        }

        /// <summary>
        /// StartTickとEndTickを見て、必要な部分だけ読み込む。
        /// </summary>
        private bool ReadHeaderAndPcmData(BinaryReader br, long startFrame, long endFrame) {
            if (startFrame < 0) {
                // データ壊れ。先頭を読む。
                startFrame = 0;
            }

            if (0 <= endFrame && endFrame < startFrame) {
                // 1サンプルもない。
                startFrame = endFrame;
            }

            return m_dsc.ReadRaw(br, m_fsc.NumChannels, m_fsc.BitsPerFrame, startFrame, endFrame);
        }

        private bool Read(BinaryReader br, ReadMode mode, long startFrame, long endFrame) {
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
            case ReadMode.HeaderAndPcmData:
                if (!ReadHeaderAndPcmData(br, startFrame, endFrame)) {
                    return false;
                }
                break;
            case ReadMode.OnlyHeader:
                if (!m_dsc.ReadHeader(br, m_fsc.NumChannels, m_fsc.BitsPerFrame)) {
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
            return Read(br, ReadMode.OnlyHeader, 0, -1);
        }

        public bool ReadAll(BinaryReader br, long startFrame, long endFrame) {
            return Read(br, ReadMode.HeaderAndPcmData, startFrame, endFrame);
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

        public int BitsPerFrame
        {
            get { return m_fsc.BitsPerFrame; }
        }

        public long NumFrames
        {
            get { return m_dsc.NumFrames; }
        }

        public int SampleRate
        {
            get { return (int)m_fsc.SampleRate; }
        }

        public byte[] GetSampleArray() {
            return m_dsc.GetSampleArray();
        }

        public bool Set(
                int numChannels,
                int bitsPerSample,
                int validBitsPerSample,
                int sampleRate,
                PcmDataLib.PcmData.ValueRepresentationType sampleValueRepresentation,
                long numFrames,
                byte[] sampleArray) {
            m_rcd = new RiffChunkDescriptor();

            if (0xffffffffL < sampleArray.LongLength + 36) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }
            m_rcd.Create((uint)(36 + sampleArray.LongLength));

            m_fsc = new FmtSubChunk();
            m_fsc.Create(numChannels, sampleRate, bitsPerSample);

            m_dsc = new DataSubChunk();
            m_dsc.Create(numFrames, sampleArray);

            return true;
        }
    }
}
