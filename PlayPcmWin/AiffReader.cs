// 日本語UTF-8

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace PlayPcmWin {

    class AiffReader {
        public enum ResultType {
            Success,
            NotAiff,
            HeaderError,
            NotSupportOffsetNonzero,
            NotSupportBlockSizeNonzero,
            NotSupportBitsPerSample,
            NotSupportID3version,
            NotSupportID3Unsynchronization,
            ReadError
        }

        public int NumChannels { get; set; }
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public long NumFrames { get; set; }

        public int BitsPerFrame {
            get { return BitsPerSample * NumChannels; }
        }

        public byte[] GetSampleArray() { return m_sampleArray; }

        private byte[] m_sampleArray = null;

        private long m_ckSize;

        private ID3Reader m_Id3Reader = new ID3Reader();

        private ResultType ReadFormChunkHeader(BinaryReader br) {
            byte[] ckID = br.ReadBytes(4);
            if (!PcmDataLib.Util.FourCCHeaderIs(ckID, 0, "FORM")) {
                return ResultType.NotAiff;
            }

            m_ckSize = ReadBigU32(br);
            if (0 != (m_ckSize & 0x80000000)) {
                return ResultType.HeaderError;
            }

            byte[] formType = br.ReadBytes(4);
            if (!PcmDataLib.Util.FourCCHeaderIs(formType, 0, "AIFF")) {
                return ResultType.NotAiff;
            }

            return ResultType.Success;
        }

        private UInt16 ReadBigU16(BinaryReader br) {
            UInt16 result = (UInt16)(((int)br.ReadByte() << 8) + br.ReadByte());
            return result;
        }

        private UInt32 ReadBigU32(BinaryReader br) {
            UInt32 result = 
                (UInt32)((UInt32)br.ReadByte() << 24) +
                (UInt32)((UInt32)br.ReadByte() << 16) +
                (UInt32)((UInt32)br.ReadByte() << 8) +
                (UInt32)((UInt32)br.ReadByte() << 0);
            return result;
        }
        
        /// <summary>
        /// 目的のチャンクが見つかるまでスキップする。
        /// </summary>
        /// <param name="ckId">チャンクID</param>
        private bool SkipToChunk(BinaryReader br, string findCkId) {
            System.Diagnostics.Debug.Assert(findCkId.Length == 4);

            try {
                while (true) {
                    byte[] ckID = br.ReadBytes(4);
                    if (PcmDataLib.Util.FourCCHeaderIs(ckID, 0, findCkId)) {
                        return true;
                    }

                    long ckSize = ReadBigU32(br);
                    PcmDataLib.Util.BinaryReaderSkip(br, ChunkSizeWithPad(ckSize));
                }
            } catch (System.IO.EndOfStreamException ex) {
                return false;
            }
        }

        /// <summary>
        /// チャンクサイズが奇数の場合、近い偶数に繰上げ。
        /// </summary>
        private static long ChunkSizeWithPad(long ckSize) {
            return ( ( ~( 1L ) ) & ( ckSize + 1 ) );
        }

        private ResultType ReadCommonChunk(BinaryReader br) {
            SkipToChunk(br, "COMM");
            long ckSize = ReadBigU32(br);
            NumChannels = ReadBigU16(br);
            NumFrames = ReadBigU32(br);
            BitsPerSample = ReadBigU16(br);
            byte[] sampleRate80 = br.ReadBytes(10);
            PcmDataLib.Util.BinaryReaderSkip(br, ckSize - (2 + 4 + 2 + 10));
            
            SampleRate = (int)IEEE754ExtendedDoubleBigEndianToDouble(sampleRate80);
            return ResultType.Success;
        }

        enum ReadHeaderMode {
            AllHeadersWithID3,
            ReadStopBeforeSoundData,
        };

        /// <summary>
        /// サウンドデータチャンクのヘッダ情報を読む
        /// </summary>
        /// <param name="mode">ReadStopBeforeSoundData サウンドデータチャンクのサウンドデータの手前で読み込みを止める。
        ///                    AllHeadersWithID3 サウンドデータチャンクの終わりまで読み進む。</param>
        private ResultType ReadSoundDataChunk(BinaryReader br, ReadHeaderMode mode) {
            SkipToChunk(br, "SSND");
            long ckSize = ReadBigU32(br);
            long offset = ReadBigU32(br);
            long blockSize = ReadBigU32(br);

            if (offset != 0) {
                return ResultType.NotSupportOffsetNonzero;
            }
            if (blockSize != 0) {
                return ResultType.NotSupportBlockSizeNonzero;
            }

            if (mode != ReadHeaderMode.ReadStopBeforeSoundData) {
                // SoundDataチャンクの最後まで移動。
                // sizeof offset + blockSize == 8
                PcmDataLib.Util.BinaryReaderSkip(br, ChunkSizeWithPad(ckSize) - 8);
            }

            return ResultType.Success;
        }

        private ResultType ReadID3Chunk(BinaryReader br) {
            if (!SkipToChunk(br, "ID3 ")) {
                return ResultType.ReadError;
            }

            long ckSize = ReadBigU32(br);
            if (ckSize < 10) {
                return ResultType.ReadError;
            }

            var id3r = m_Id3Reader.Read(br);

            ResultType result = ResultType.Success;
            switch (id3r) {
            case ID3Reader.ID3Result.ReadError:
                result = ResultType.ReadError;
                break;
            case ID3Reader.ID3Result.NotSupportedID3version:
                result = ResultType.NotSupportID3version;
                break;
            case ID3Reader.ID3Result.Success:
                result = ResultType.Success;
                PcmDataLib.Util.BinaryReaderSkip(br, ChunkSizeWithPad(ckSize) - m_Id3Reader.ReadBytes);
                break;
            default:
                // 追加忘れ
                System.Diagnostics.Debug.Assert(false);
                result = ResultType.ReadError;
                break;
            }
            return result;
        }

        public string TitleName { get { return m_Id3Reader.TitleName; } }
        public string AlbumName { get { return m_Id3Reader.AlbumName; } }
        public string ArtistName { get { return m_Id3Reader.ArtistName; } }

        private ResultType ReadHeader1(BinaryReader br, out PcmDataLib.PcmData pcmData, ReadHeaderMode mode) {
            pcmData = new PcmDataLib.PcmData();

            ResultType result = ReadFormChunkHeader(br);
            if (result != ResultType.Success) {
                return result;
            }

            result = ReadCommonChunk(br);
            if (ResultType.Success != result) {
                return result;
            }

            result = ReadSoundDataChunk(br, mode);
            if (ResultType.Success != result) {
                return result;
            }

            if (16 != BitsPerSample) {
                return ResultType.NotSupportBitsPerSample;
            }

            System.Console.WriteLine("nChannels={0} bitsPerSample={1} sampleRate={2} numFrames={3}",
                NumChannels, BitsPerSample, SampleRate, NumFrames);

            pcmData.SetFormat(
                NumChannels,
                BitsPerSample,
                BitsPerSample,
                SampleRate,
                PcmDataLib.PcmData.ValueRepresentationType.SInt,
                NumFrames);

            if (mode == ReadHeaderMode.AllHeadersWithID3) {
                result = ReadID3Chunk(br);
                if (ResultType.Success == result) {
                    // ID3読み込み成功
                    pcmData.DisplayName = TitleName;
                    pcmData.AlbumTitle = AlbumName;
                    pcmData.ArtistName = ArtistName;
                }
            }

            return 0;
        }

        /// <summary>
        /// ビッグエンディアンバイトオーダーの80ビット拡張倍精度浮動小数点数→double
        /// 手抜き実装: subnormal numberとか、NaNとかがどうなるかは確かめてない
        /// </summary>
        private double IEEE754ExtendedDoubleBigEndianToDouble(byte[] extended) {
            System.Diagnostics.Debug.Assert(extended.Length == 10);

            byte[] resultBytes = new byte[8];

            // 7777777777666666666655555555554444444444333333333322222222221111111111
            // 98765432109876543210987654321098765432109876543210987654321098765432109876543210
            // seeeeeeeeeeeeeeeffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff
            // 00000000111111112222222233333333444444445555555566666666777777778888888899999999 (big-endian)

            // 666655555555554444444444333333333322222222221111111111
            // 3210987654321098765432109876543210987654321098765432109876543210
            // seeeeeeeeeeeffffffffffffffffffffffffffffffffffffffffffffffffffff
            // 0000000011111111222222223333333344444444555555556666666677777777 (big-endian)

            int exponent =
                ((extended[0] & 0x7f) << 8) + (extended[1]);
            exponent -= 16383;

            int exponent64 = exponent + 1023;

            // extended double precisionには、fractionのimplicit/hidden bitはない。
            // subnormal numberでなければfractionのMSBは1になる
            // double precisionは、fractionのimplicit/hidden bitが存在する。なので1ビット余計に左シフトする

            long fraction =
                ((long)extended[2] << 57) +
                ((long)extended[3] << 49) +
                ((long)extended[4] << 41) +
                ((long)extended[5] << 33) +
                ((long)extended[6] << 25) +
                ((long)extended[7] << 17) +
                ((long)extended[8] << 9) +
                ((long)extended[9] << 1);

            resultBytes[7] = (byte)((extended[0] & 0x80) + (0x7f & (exponent64 >> 4)));
            resultBytes[6] = (byte)(((exponent64 & 0x0f) << 4) + (0x0f & (fraction >> 60)));
            resultBytes[5] = (byte)(0xff & (fraction >> 52));
            resultBytes[4] = (byte)(0xff & (fraction >> 44));
            resultBytes[3] = (byte)(0xff & (fraction >> 36));
            resultBytes[2] = (byte)(0xff & (fraction >> 28));
            resultBytes[1] = (byte)(0xff & (fraction >> 20));
            resultBytes[0] = (byte)(0xff & (fraction >> 12));

            double result = System.BitConverter.ToDouble(resultBytes, 0);
            return result;
        }

        public ResultType ReadHeader(BinaryReader br, out PcmDataLib.PcmData pcmData) {
            return ReadHeader1(br, out pcmData, ReadHeaderMode.AllHeadersWithID3);
        }

        private int m_bytesPerFrame;
        private long m_posFrame;

        public ResultType ReadStreamBegin(BinaryReader br, out PcmDataLib.PcmData pcmData) {
            ResultType rt = ResultType.Success;

            rt = ReadHeader1(br, out pcmData, ReadHeaderMode.ReadStopBeforeSoundData);
            if (rt == ResultType.Success) {
                m_bytesPerFrame = pcmData.BitsPerFrame / 8;
            }
            m_posFrame = 0;

            return rt;
        }

        /// <summary>
        /// フレーム指定でスキップする。
        /// </summary>
        /// <param name="skipFrames">スキップするフレーム数。負の値は指定できない。</param>
        /// <returns>実際にスキップできたフレーム数。</returns>
        public long ReadStreamSkip(BinaryReader br, long skipFrames) {
            System.Diagnostics.Debug.Assert(0 < m_bytesPerFrame);
            if (skipFrames < 0) {
                System.Diagnostics.Debug.Assert(false);
                skipFrames = 0;
            }
            if (NumFrames < m_posFrame + skipFrames) {
                // 最後に移動。
                skipFrames = NumFrames - m_posFrame;
            }

            PcmDataLib.Util.BinaryReaderSkip(br, skipFrames * m_bytesPerFrame / 8);
            m_posFrame += skipFrames;
            return skipFrames;
        }

        /// <summary>
        /// preferredFramesフレームぐらい読み出す。
        /// 1Mフレームぐらいにすると効率が良い。
        /// </summary>
        /// <returns>読みだしたフレーム</returns>
        public byte[] ReadStreamReadOne(BinaryReader br, int preferredFrames) {
            int readFrames = preferredFrames;
            if (NumFrames < m_posFrame + readFrames) {
                readFrames = (int)(NumFrames - m_posFrame);
            }

            if (readFrames == 0) {
                // 1バイトも読めない。
                return new byte[0];
            }

            byte[] sampleArray = br.ReadBytes((int)(readFrames * BitsPerFrame / 8));

            // エンディアン変換
            /// @todo 16bppの場合以外にも対応する必要あり。
            {
                const int workUnit = 256 * 1024;
                int sampleUnits = (int)(readFrames * NumChannels / workUnit);
                Parallel.For(0, sampleUnits, delegate(int m) {
                    int pos = m * workUnit * 2;
                    for (int i = 0; i < workUnit; ++i) {
                        byte v0 = sampleArray[pos + 0];
                        byte v1 = sampleArray[pos + 1];
                        sampleArray[pos + 1] = v0;
                        sampleArray[pos + 0] = v1;
                        pos += 2;
                    }
                });
                for (int i = workUnit * sampleUnits;
                    i < readFrames * NumChannels; ++i) {
                    int pos = i * 2;
                    byte v0 = sampleArray[pos + 0];
                    byte v1 = sampleArray[pos + 1];
                    sampleArray[pos + 1] = v0;
                    sampleArray[pos + 0] = v1;
                }
            }

            return sampleArray;
        }

        public void ReadStreamEnd() {
            m_bytesPerFrame = 0;
            m_posFrame = 0;
        }
    }
}
