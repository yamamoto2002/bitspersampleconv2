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
            NotSupportBitsPerSample
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

        private ResultType ReadFormChunkHeader(BinaryReader br) {
            byte[] ckID = br.ReadBytes(4);
            if (ckID[0] != 'F' ||
                ckID[1] != 'O' ||
                ckID[2] != 'R' ||
                ckID[3] != 'M') {
                return ResultType.NotAiff;
            }

            m_ckSize = ReadBigU32(br);
            if (0 != (m_ckSize & 0x80000000)) {
                return ResultType.HeaderError;
            }

            byte[] formType = br.ReadBytes(4);
            if (formType[0] != 'A' ||
                formType[1] != 'I' ||
                formType[2] != 'F' ||
                formType[3] != 'F') {
                return ResultType.NotAiff;
            }

            return ResultType.Success;
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
        private void SkipToChunk(BinaryReader br, string findCkId) {
            System.Diagnostics.Debug.Assert(findCkId.Length == 4);

            while (true) {
                byte[] ckID = br.ReadBytes(4);
                if (ckID[0] == findCkId[0] &&
                    ckID[1] == findCkId[1] &&
                    ckID[2] == findCkId[2] &&
                    ckID[3] == findCkId[3]) {
                    return;
                }

                long ckSize = ReadBigU32(br);

                BinaryReaderSkip(br, ckSize);
            }
        }

        private ResultType ReadCommonChunk(BinaryReader br) {
            SkipToChunk(br, "COMM");
            long ckSize = ReadBigU32(br);
            NumChannels = ReadBigU16(br);
            NumFrames = ReadBigU32(br);
            BitsPerSample = ReadBigU16(br);
            byte[] sampleRate80 = br.ReadBytes(10);
            BinaryReaderSkip(br, ckSize - (2 + 4 + 2 + 10));

            SampleRate = (int)IEEE754ExtendedDoubleBigEndianToDouble(sampleRate80);
            return ResultType.Success;
        }

        private ResultType ReadSoundDataChunk(BinaryReader br) {
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

            if (16 != BitsPerSample) {
                return ResultType.NotSupportBitsPerSample;
            }

            m_sampleArray = br.ReadBytes((int)(NumFrames * BitsPerFrame / 8));
            

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            /*
            // 案1 (1サンプルごとの処理)
            Parallel.For(0, NumFrames * NumChannels, delegate(long sample) {
                long pos = sample * 2;
                byte v0 = m_sampleArray[pos + 0];
                byte v1 = m_sampleArray[pos + 1];
                m_sampleArray[pos + 1] = v0;
                m_sampleArray[pos + 0] = v1;
            });
            */
            /*
            // 案2 (1フレームごとの処理) 案1の半分ぐらいの速度
            int numChannels = NumChannels;
            Parallel.For(0, NumFrames, delegate(long frame) {
                for (int i = 0; i < numChannels; ++i) {
                    long pos = frame * 2 * i;
                    byte v0 = m_sampleArray[pos + 0];
                    byte v1 = m_sampleArray[pos + 1];
                    m_sampleArray[pos + 1] = v0;
                    m_sampleArray[pos + 0] = v1;
                }
            });
            */
            /*
            // 案3 (全部1スレッド) 6コア12スレッドCPUでも案1よりも少し速い
            for (long i = 0; i < NumFrames * NumChannels; ++i) {
                long pos = i * 2;
                byte v0 = m_sampleArray[pos + 0];
                byte v1 = m_sampleArray[pos + 1];
                m_sampleArray[pos + 1] = v0;
                m_sampleArray[pos + 0] = v1;
            }
            */

            {
                // 案4 (1Mサンプルごとの処理) 案3よりも6倍ぐらい速い
                int workUnit = 1048576;
                long sampleUnits = NumFrames * NumChannels / workUnit;
                Parallel.For(0, sampleUnits, delegate(long m) {
                    long pos = m * workUnit * 2;
                    for (int i = 0; i < workUnit; ++i) {
                        byte v0 = m_sampleArray[pos + 0];
                        byte v1 = m_sampleArray[pos + 1];
                        m_sampleArray[pos + 1] = v0;
                        m_sampleArray[pos + 0] = v1;
                        pos += 2;
                    }
                });
                for (long i = workUnit * sampleUnits;
                    i < NumFrames * NumChannels; ++i) {
                    long pos = i * 2;
                    byte v0 = m_sampleArray[pos + 0];
                    byte v1 = m_sampleArray[pos + 1];
                    m_sampleArray[pos + 1] = v0;
                    m_sampleArray[pos + 0] = v1;
                }
            }

            sw.Stop();
            System.Console.WriteLine("{0} bytes : {1} ms", m_sampleArray.Length, sw.ElapsedMilliseconds);

            /*
            //PCMデータのテスト出力。
            using (BinaryWriter bw = new BinaryWriter(File.Open("C:\\tmp\\test.bin", FileMode.Create))) {
                bw.Write(m_sampleArray);
            }
            */

            return ResultType.Success;
        }

        public ResultType ReadHeader(BinaryReader br, out PcmDataLib.PcmData pcmData) {
            pcmData = new PcmDataLib.PcmData();

            ResultType result = ReadFormChunkHeader(br);
            if (result != ResultType.Success) {
                return result;
            }

            ReadCommonChunk(br);

            System.Console.WriteLine("nChannels={0} bitsPerSample={1} sampleRate={2} numFrames={3}",
                NumChannels, BitsPerSample, SampleRate, NumFrames);

            pcmData.SetFormat(
                NumChannels,
                BitsPerSample,
                SampleRate,
                PcmDataLib.PcmData.ValueRepresentationType.SInt,
                NumFrames);

            return 0;
        }

        public ResultType ReadHeaderAndPcmData(BinaryReader br) {
            ResultType result = ReadFormChunkHeader(br);
            if (result != ResultType.Success) {
                return result;
            }

            ReadCommonChunk(br);
            result = ReadSoundDataChunk(br);
            return result;
        }

        /// <summary>
        /// ビッグエンディアンバイトオーダーの拡張倍精度浮動小数点数→double
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
    }
}
