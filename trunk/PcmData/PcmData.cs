using System;
using System.Collections.Generic;
using System.IO;

namespace PcmDataLib {
    /// <summary>
    /// PCMデータ情報置き場。
    /// ・PCMフォーマット情報
    ///   ・チャンネル数
    ///   ・サンプルレート
    ///   ・量子化ビット数
    ///   ・サンプルデータ形式(整数、浮動小数点数)
    /// ・PCMデータ
    ///   ・PCMデータ配列
    ///   ・PCMデータフレーム数(フレーム＝サンプル x チャンネル)
    /// ・ファイル管理情報
    ///   ・連番
    ///   ・ファイルグループ番号
    ///   ・ファイル名(ディレクトリ名を除く)
    ///   ・フルパスファイル名
    ///   ・表示名
    ///   ・開始Tick
    ///   ・終了Tick
    /// </summary>
    public class PcmData {

        // PCMフォーマット情報 //////////////////////////////////////////////

        public enum ValueRepresentationType {
            SInt,
            SFloat
        };

        public int NumChannels { get; set; }
        public int SampleRate { get; set; }

        /// <summary>
        /// 1サンプルのビット数(無効な0埋めビット含む)
        /// </summary>
        public int BitsPerSample { get; set; }

        /// <summary>
        /// サンプル値の有効なビット数
        /// </summary>
        public int ValidBitsPerSample { get; set; }

        public ValueRepresentationType
            SampleValueRepresentationType { get; set; }

        // PCMデータ ////////////////////////////////////////////////////////

        private long   m_numFrames;
        private byte[] m_sampleArray;

        // ファイル管理情報 /////////////////////////////////////////////////

        /// <summary>
        /// 連番
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ファイルグループ番号。
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// ファイル名(ディレクトリ名を除く)
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// フルパスファイル名
        /// </summary>
        public string FullPath { get; set; }

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
        /// アルバムタイトル
        /// </summary>
        public string AlbumTitle { get; set; }

        /// <summary>
        /// アルバム演奏者
        /// </summary>
        public string AlbumPerformer { get; set; }

        /// <summary>
        /// 曲演奏者
        /// </summary>
        public string Performer { get; set; }

        /// <summary>
        /// rhsの内容をコピーする。PCMデータ配列だけはコピーしない。(nullをセットする)
        /// PCMデータ配列は、SetSampleArrayで別途設定する。
        /// </summary>
        /// <param name="rhs">コピー元</param>
        private void CopyHeaderInfoFrom(PcmData rhs) {
            NumChannels   = rhs.NumChannels;
            SampleRate    = rhs.SampleRate;
            BitsPerSample = rhs.BitsPerSample;
            ValidBitsPerSample = rhs.ValidBitsPerSample;
            SampleValueRepresentationType = rhs.SampleValueRepresentationType;
            m_numFrames = rhs.m_numFrames;
            m_sampleArray = null;
            Id          = rhs.Id;
            GroupId     = rhs.GroupId;
            FileName    = rhs.FileName;
            FullPath    = rhs.FullPath;
            DisplayName = rhs.DisplayName;
            StartTick   = rhs.StartTick;
            EndTick     = rhs.EndTick;
            AlbumTitle  = rhs.AlbumTitle;
            AlbumPerformer = rhs.AlbumPerformer;
            Performer   = rhs.Performer;
        }

        // プロパティIO /////////////////////////////////////////////////////

        public long NumFrames {
            get { return m_numFrames; }
        }

        public byte[] GetSampleArray() {
            return m_sampleArray;
        }

        public int BitsPerFrame {
            get { return BitsPerSample * NumChannels; }
        }

        public void SetSampleArray(long numFrames, byte[] sampleArray) {
            m_numFrames = numFrames;
            m_sampleArray = null;
            m_sampleArray = sampleArray;
        }

        /// <summary>
        /// forget data part.
        /// PCMデータ配列を忘れる。
        /// サンプル数など、フォーマット情報は忘れない。
        /// </summary>
        public void ForgetDataPart() {
            m_sampleArray = null;
        }

        /// <summary>
        /// PCMデータの形式を設定する。
        /// </summary>
        public void SetFormat(
            int numChannels,
            int bitsPerSample,
            int validBitsPerSample,
            int sampleRate,
            ValueRepresentationType sampleValueRepresentation,
            long numFrames) {
            NumChannels = numChannels;
            BitsPerSample = bitsPerSample;
            ValidBitsPerSample = validBitsPerSample;
            SampleRate = sampleRate;
            SampleValueRepresentationType = sampleValueRepresentation;
            m_numFrames = numFrames;

            m_sampleArray = null;
        }

        /// <summary>
        /// サンプリング周波数と量子化ビット数、有効なビット数、チャンネル数、データ形式が同じならtrue
        /// </summary>
        public bool IsSameFormat(PcmData other) {
            return BitsPerSample      == other.BitsPerSample
                && ValidBitsPerSample == other.ValidBitsPerSample
                && SampleRate    == other.SampleRate
                && NumChannels   == other.NumChannels
                && SampleValueRepresentationType == other.SampleValueRepresentationType;
        }

        /// <summary>
        /// StartTickとEndTickを見て、必要な部分以外をカットする。
        /// </summary>
        public void Trim() {
            if (StartTick < 0) {
                // データ壊れ。先頭を読む。
                StartTick = 0;
            }

            if (StartTick == 0 && EndTick == -1) {
                // データTrimの必要はない。
                return;
            }

            long startFrame = (long)(StartTick) * SampleRate / 75;
            long endFrame   = (long)(EndTick)   * SampleRate / 75;

            if (endFrame < 0 ||
                NumFrames < endFrame) {
                // 終了位置はファイルの終わり。
                endFrame = NumFrames;
            }

            if (endFrame < startFrame) {
                // 1サンプルもない。
                startFrame = endFrame;
            }

            TrimInternal(startFrame, endFrame);
        }

        private void TrimInternal(long startFrame, long endFrame) {
            long startBytes = startFrame * BitsPerFrame / 8;
            long endBytes = endFrame * BitsPerFrame / 8;

            System.Diagnostics.Debug.Assert(0 <= startBytes);
            System.Diagnostics.Debug.Assert(0 <= endBytes);
            System.Diagnostics.Debug.Assert(startBytes <= endBytes);
            System.Diagnostics.Debug.Assert(null != m_sampleArray);
            System.Diagnostics.Debug.Assert(startBytes <= m_sampleArray.Length);
            System.Diagnostics.Debug.Assert(endBytes <= m_sampleArray.Length);

            long newNumSamples = endFrame - startFrame;
            m_numFrames = newNumSamples;
            if (newNumSamples == 0 ||
                m_sampleArray.Length <= startBytes) {
                m_sampleArray = null;
                m_numFrames = 0;
            } else {
                byte[] newArray = new byte[endBytes - startBytes];
                Array.Copy(m_sampleArray, startBytes, newArray, 0, endBytes - startBytes);
                m_sampleArray = null;
                m_sampleArray = newArray;
            }
        }

        /// <summary>
        /// 量子化ビット数をbitsPerSampleに変更した、新しいPcmDataを戻す。
        /// 自分自身の内容は変更しない。
        /// </summary>
        /// <param name="newBitsPerSample">新しい量子化ビット数</param>
        /// <returns>量子化ビット数変更後のPcmData</returns>
        public PcmData BitsPerSampleConvertTo(int newBitsPerSample, ValueRepresentationType newValueRepType) {
            byte [] newSampleArray        = null;

            if (newBitsPerSample == 32) {
                if (newValueRepType == ValueRepresentationType.SFloat) {
                    switch (BitsPerSample) {
                    case 16:
                        newSampleArray = ConvI16toF32(GetSampleArray());
                        break;
                    case 24:
                        newSampleArray = ConvI24toF32(GetSampleArray());
                        break;
                    case 32:
                        if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                            newSampleArray = (byte[])GetSampleArray().Clone();
                        } else {
                            newSampleArray = ConvI32toF32(GetSampleArray());
                        }
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        return null;
                    }
                } else if (newValueRepType == ValueRepresentationType.SInt) {
                    switch (BitsPerSample) {
                    case 16:
                        newSampleArray = ConvI16toI32(GetSampleArray());
                        break;
                    case 24:
                        newSampleArray = ConvI24toI32(GetSampleArray());
                        break;
                    case 32:
                        if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                            newSampleArray = ConvF32toI32(GetSampleArray());
                        } else {
                            newSampleArray = (byte[])GetSampleArray().Clone();
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
                    newSampleArray = ConvI16toI24(GetSampleArray());
                    break;
                case 24:
                    newSampleArray = (byte[])GetSampleArray().Clone();
                    break;
                case 32:
                    if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                        newSampleArray = ConvF32toI24(GetSampleArray());
                    } else {
                        newSampleArray = ConvI32toI24(GetSampleArray());
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return null;
                }
            } else if (newBitsPerSample == 16) {
                switch (BitsPerSample) {
                case 16:
                    newSampleArray = (byte[])GetSampleArray().Clone();
                    break;
                case 24:
                    newSampleArray = ConvI24toI16(GetSampleArray());
                    break;
                case 32:
                    if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                        newSampleArray = ConvF32toI16(GetSampleArray());
                    } else {
                        newSampleArray = ConvI32toI16(GetSampleArray());
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

            // 有効なビット数の計算
            int newValidBitsPerSample = ValidBitsPerSample;
            if (newBitsPerSample < newValidBitsPerSample) {
                // 新しい量子化ビット数が、元の量子化ビット数よりも減った。
                newValidBitsPerSample = newBitsPerSample;
            }
            if (newBitsPerSample == 32 &&
                newValueRepType == ValueRepresentationType.SFloat) {
                // FLOAT32は、全てのビット(=32)を有効にしないと意味ないデータになると思われる。
                newValidBitsPerSample = 32;
            }

            PcmData newPcmData = new PcmData();
            newPcmData.CopyHeaderInfoFrom(this);
            newPcmData.SetFormat(NumChannels, newBitsPerSample, newValidBitsPerSample, SampleRate, newValueRepType, NumFrames);
            newPcmData.SetSampleArray(NumFrames, newSampleArray);

            return newPcmData;
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
