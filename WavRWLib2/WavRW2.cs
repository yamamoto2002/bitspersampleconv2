using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

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

        public bool Read(BinaryReader br, byte[] chunkId)
        {
            m_chunkId = chunkId;
            if (!PcmDataLib.Util.FourCCHeaderIs(m_chunkId, 0, "RIFF")) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            m_chunkSize = br.ReadUInt32();
            if (m_chunkSize < 36) {
                Console.WriteLine("E: chunkSize is too small {0}", m_chunkSize);
                return false;
            }

            m_format = br.ReadBytes(4);
            if (!PcmDataLib.Util.FourCCHeaderIs(m_format, 0, "WAVE")) {
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
        public ushort BitsPerSample { get; set; }

        public bool Create(
                int numChannels, int sampleRate, int bitsPerSample,
                PcmDataLib.PcmData.ValueRepresentationType sampleValueRepresentation)
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

            SampleValueRepresentationType = sampleValueRepresentation;
            if (sampleValueRepresentation == PcmDataLib.PcmData.ValueRepresentationType.SInt) {
                m_audioFormat = 1;
            } else if (sampleValueRepresentation == PcmDataLib.PcmData.ValueRepresentationType.SFloat) {
                m_audioFormat = 3;
            } else {
                System.Diagnostics.Debug.Assert(false);
            }

            return true;
        }
        
        public bool Read(BinaryReader br, byte[] fourcc)
        {
            m_subChunk1Id = fourcc;
            if (!PcmDataLib.Util.FourCCHeaderIs(m_subChunk1Id, 0, "fmt ")) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            m_subChunk1Size = br.ReadUInt32();

            if (40 == m_subChunk1Size) {
                // Console.WriteLine("D: FmtSubChunk.Read() WAVEFORMATEXTENSIBLE\n");
            } else if (16 != m_subChunk1Size && 18 != m_subChunk1Size) {
                Console.WriteLine("E: FmtSubChunk.Read() subChunk1Size!=16 {0} this file type is not supported", m_subChunk1Size);
                return false;
            }

            m_audioFormat = br.ReadUInt16();
            if (1 == m_audioFormat) {
                SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SInt;
            } else if (3 == m_audioFormat) {
                SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SFloat;
            } else if (0xfffe == m_audioFormat) {
                // WAVEFORMATEXTENSIBLE
            } else {
                Console.WriteLine("E: this wave file is not PCM format {0}. Cannot read this file", m_audioFormat);
                return false;
            }

            NumChannels = br.ReadUInt16();
            SampleRate = br.ReadUInt32();
            m_byteRate = br.ReadUInt32();
            m_blockAlign = br.ReadUInt16();
            BitsPerSample = br.ReadUInt16();

            ushort extensibleSize = 0;
            if (16 < m_subChunk1Size) {
                // cbSize 2bytes
                extensibleSize = br.ReadUInt16();
            }

            if (0 != extensibleSize && 22 != extensibleSize) {
                Console.WriteLine("E: FmtSubChunk.Read() cbSize != 0 nor 22 {0}", extensibleSize);
                return false;
            }

            if (22 == extensibleSize) {
                // WAVEFORMATEX(22 bytes)

                ushort bitsPerSampleEx = br.ReadUInt16();
                uint dwChannelMask = br.ReadUInt32();
                var formatGuid = br.ReadBytes(16);

                var pcmGuid   = Guid.Parse("00000001-0000-0010-8000-00aa00389b71");
                var pcmGuidByteArray = pcmGuid.ToByteArray();
                var floatGuid = Guid.Parse("00000003-0000-0010-8000-00aa00389b71");
                var floatGuidByteArray = floatGuid.ToByteArray();
                if (pcmGuidByteArray.SequenceEqual(formatGuid)) {
                    SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SInt;
                } else if (floatGuidByteArray.SequenceEqual(formatGuid)) {
                    SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SFloat;
                } else {
                    Console.WriteLine("E: FmtSubChunk.Read() unknown format guid on WAVEFORMATEX.SubFormat.");
                    Console.WriteLine("{3:X2}{2:X2}{1:X2}{0:X2}-{5:X2}{4:X2}-{7:X2}{6:X2}-{9:X2}{8:X2}-{10:X2}{11:X2}{12:X2}{13:X2}{14:X2}{15:X2}",
                        formatGuid[0], formatGuid[1], formatGuid[2], formatGuid[3],
                        formatGuid[4], formatGuid[5], formatGuid[6], formatGuid[7],
                        formatGuid[8], formatGuid[9], formatGuid[10], formatGuid[11],
                        formatGuid[12], formatGuid[13], formatGuid[14], formatGuid[15]);
                    return false;
                }
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

        public bool ReadHeader(BinaryReader br, byte[] fourcc, int numChannels, int bitsPerSample) {
            m_subChunk2Id = fourcc;
            if (!PcmDataLib.Util.FourCCHeaderIs(m_subChunk2Id, 0, "data")) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }

            m_subChunk2Size = br.ReadUInt32();

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
        public bool ReadRaw(BinaryReader br, byte[] fourcc, int numChannels, int bitsPerSample,
            long startFrame, long endFrame) {
            if (!ReadHeader(br, fourcc, numChannels, bitsPerSample)) {
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
                PcmDataLib.Util.BinaryReaderSkip(br, startBytes);
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
        /// StartTickとEndTickを見て、DSCヘッダ以降の必要な部分だけ読み込む。
        /// </summary>
        private bool ReadDscHeaderAndPcmDataInternal(BinaryReader br, byte[] fourcc, long startFrame, long endFrame) {
            if (startFrame < 0) {
                // データ壊れ。先頭を読む。
                startFrame = 0;
            }

            if (0 <= endFrame && endFrame < startFrame) {
                // 1サンプルもない。
                startFrame = endFrame;
            }

            return m_dsc.ReadRaw(br, fourcc, m_fsc.NumChannels, m_fsc.BitsPerSample, startFrame, endFrame);
        }

        private bool SkipUnknownChunk(BinaryReader br, byte[] fourcc) {
            // Console.WriteLine("D: SkipUnknownChunk skip \"{0}{1}{2}{3}\"", (char)fourcc[0], (char)fourcc[1], (char)fourcc[2], (char)fourcc[3]);

            int chunkSize = br.ReadInt32();
            if (chunkSize <= 0) {
                Console.WriteLine("E: SkipUnknownChunk chunk \"{0}{1}{2}{3}\" corrupted. chunkSize={4}",
                    (char)fourcc[0], (char)fourcc[1], (char)fourcc[2], (char)fourcc[3], chunkSize);

                return false;
            }
            br.ReadBytes(chunkSize);
            return true;
        }

        private bool ReadListHeader(BinaryReader br) {
            uint listHeaderBytes = br.ReadUInt32();
            if (listHeaderBytes < 4) {
                Console.WriteLine("LIST header size is too short {0}", listHeaderBytes);
                return false;
            }
            if (65535 < listHeaderBytes) {
                Console.WriteLine("LIST header size is too large {0}", listHeaderBytes);
                return false;
            }

            byte[] data = br.ReadBytes((int)listHeaderBytes);
            if (!PcmDataLib.Util.FourCCHeaderIs(data, 0, "INFO")) {
                Console.WriteLine("LIST header does not follows INFO");
                return false;
            }

            int pos = 4;
            while (pos+8 < data.Length) {
                if (data[pos+6] != 0 || data[pos+7] != 0) {
                    Console.WriteLine("LIST header contains very long text. parse aborted");
                    return false;
                }
                int bytes = data[pos+4] + 256 * data[pos+5];
                if (0 < bytes) {
                    if (PcmDataLib.Util.FourCCHeaderIs(data, pos, "INAM")) {
                        Title = System.Text.Encoding.UTF8.GetString(data, pos + 8, bytes).Trim(new char[] { '\0' });
                    }
                    if (PcmDataLib.Util.FourCCHeaderIs(data, pos, "IART")) {
                        ArtistName = System.Text.Encoding.UTF8.GetString(data, pos + 8, bytes).Trim(new char[] { '\0' });
                    }
                    if (PcmDataLib.Util.FourCCHeaderIs(data, pos, "IPRD")) {
                        AlbumName = System.Text.Encoding.UTF8.GetString(data, pos + 8, bytes).Trim(new char[] { '\0' });
                    }
                }
                pos += 8 + bytes;
            }
            return true;
        }

        private bool Read(BinaryReader br, ReadMode mode, long startFrame, long endFrame) {
            bool result = true;
            bool firstHeader = true;
            try {
                do {
                    var fourcc = br.ReadBytes(4);

                    if (firstHeader) {
                        if (!PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "RIFF")) {
                            // ファイルの先頭がRIFFで始まっていない。WAVではない。
                            return false;
                        }
                        firstHeader = false;
                    }

                    if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "RIFF")) {
                        m_rcd = new RiffChunkDescriptor();
                        if (!m_rcd.Read(br, fourcc)) {
                            return false;
                        }
                    } else if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "fmt ")) {
                        m_fsc = new FmtSubChunk();
                        if (!m_fsc.Read(br, fourcc)) {
                            return false;
                        }
                    } else if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "LIST")) {
                        if (!ReadListHeader(br)) {
                            return false;
                        }
                    } else if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "data")) {
                        m_dsc = new DataSubChunk();
                        switch (mode) {
                        case ReadMode.HeaderAndPcmData:
                            if (!ReadDscHeaderAndPcmDataInternal(br, fourcc, startFrame, endFrame)) {
                                return false;
                            }
                            break;
                        case ReadMode.OnlyHeader:
                            if (!m_dsc.ReadHeader(br, fourcc, m_fsc.NumChannels, m_fsc.BitsPerSample)) {
                                return false;
                            }
                            // switchから抜ける
                            break;
                        }
                        // do-whileから抜ける
                        break;
                    } else {
                        if (!SkipUnknownChunk(br, fourcc)) {
                            return false;
                        }
                    }
                } while (true);
            } catch (Exception ex) {
                Console.WriteLine("E: WavRWLib2.WavData.Read() {0}", ex);
                result = false;
            }

            return result && m_rcd != null && m_fsc != null && m_dsc != null;
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

        public bool ReadHeaderAndSamples(BinaryReader br, long startFrame, long endFrame) {
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
            get { return m_fsc.BitsPerSample; }
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

        public string Title { get; set; }
        public string ArtistName { get; set; }
        public string AlbumName { get; set; }

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
            m_fsc.Create(numChannels, sampleRate, bitsPerSample, sampleValueRepresentation);

            m_dsc = new DataSubChunk();
            m_dsc.Create(numFrames, sampleArray);

            return true;
        }

        public bool ReadStreamBegin(BinaryReader br,out PcmDataLib.PcmData pcmData) {
            if (!ReadHeader(br)) {
                pcmData = new PcmDataLib.PcmData();
                return false;
            }

            pcmData = new PcmDataLib.PcmData();
            pcmData.SetFormat(m_fsc.NumChannels, m_fsc.BitsPerSample,
                m_fsc.BitsPerSample, (int)m_fsc.SampleRate,
                m_fsc.SampleValueRepresentationType, m_dsc.NumFrames);

            return true;
        }

        public void ReadStreamSkip(BinaryReader br, long skipFrames) {
            int frameBytes = m_fsc.BitsPerSample / 8 * m_fsc.NumChannels;

            PcmDataLib.Util.BinaryReaderSkip(br, frameBytes * skipFrames);
        }

        public byte[] ReadStreamReadOne(BinaryReader br, long preferredFrames) {
            int frameBytes = m_fsc.BitsPerSample / 8 * m_fsc.NumChannels;
            return br.ReadBytes((int)(preferredFrames * frameBytes));
        }

        public void ReadStreamEnd() {
        }
    }
}
