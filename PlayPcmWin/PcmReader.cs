﻿using WavRWLib2;
using PcmDataLib;
using System;
using System.IO;
using System.Globalization;

namespace PlayPcmWin {
    class PcmReader : IDisposable {
        private PcmData mPcmData;
        private FlacDecodeIF mFlacR;
        private AiffReader mAiffR;
        private WavData mWaveR;
        private BinaryReader mBr;

        public long NumFrames { get; set; }

        public enum Format {
            FLAC,
            AIFF,
            WAVE,
            Unknown
        };
        Format m_format;

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                mFlacR.Dispose();
                mBr.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        public static bool IsTheFormatCompressed(Format fmt) {
            switch (fmt) {
            case Format.FLAC:
                return true;
            case Format.AIFF:
            case Format.WAVE:
                return false;
            default:
                System.Diagnostics.Debug.Assert(false);
                return false;
            }
        }

        public static Format GuessFileFormatFromFilePath(string path) {
            string ext = System.IO.Path.GetExtension(path);
            switch (ext.ToUpperInvariant()) {
            case ".FLAC":
                return Format.FLAC;
            case ".AIF":
            case ".AIFF":
            case ".AIFC":
            case ".AIFFC":
                return Format.AIFF;
            case ".WAV":
            case ".WAVE":
                return Format.WAVE;
            default:
                return Format.Unknown;
            }
        }

        /// <summary>
        /// StreamBegin()を呼んだら、成功しても失敗してもStreamEnd()を呼んでください。
        /// </summary>
        /// <param name="path">ファイルパス。</param>
        /// <param name="startFrame">読み出し開始フレーム</param>
        /// <param name="wantFrames">取得したいフレーム数。-1: 最後まで。0: 取得しない。</param>
        /// <returns>0以上: 成功。負: 失敗。</returns>
        public int StreamBegin(string path, long startFrame, long wantFrames) {
            var fmt = GuessFileFormatFromFilePath(path);
            try {
                switch (fmt) {
                case Format.FLAC:
                    // FLACファイル読み込み。
                    m_format = Format.FLAC;
                    return StreamBeginFlac(path, startFrame, wantFrames);
                case Format.AIFF:
                    // AIFFファイル読み込み。
                    m_format = Format.AIFF;
                    return StreamBeginAiff(path, startFrame);
                case Format.WAVE:
                    // WAVEファイル読み込み。
                    m_format = Format.WAVE;
                    return StreamBeginWave(path, startFrame);
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return -1;
                }
            } catch (IOException ex) {
                Console.WriteLine("E: StreamBegin {0}" + ex);
                return -1;
            } catch (ArgumentException ex) {
                Console.WriteLine("E: StreamBegin {0}" + ex);
                return -1;
            } catch (UnauthorizedAccessException ex) {
                Console.WriteLine("E: StreamBegin {0}" + ex);
                return -1;
            }
        }

        /// <summary>
        /// PCMデータを読み出す。
        /// </summary>
        /// <param name="preferredFrames">読み込みたいフレーム数。1Mフレームぐらいにすると良い。(このフレーム数のデータが戻るとは限らない)</param>
        /// <returns>PCMデータが詰まったバイト列。0要素の配列の場合、もう終わり。</returns>
        public byte[] StreamReadOne(int preferredFrames) {
            byte[] result;
            switch (m_format) {
            case Format.FLAC:
                result = mFlacR.ReadStreamReadOne(preferredFrames);
                break;
            case Format.AIFF:
                result = mAiffR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            case Format.WAVE:
                result = mWaveR.ReadStreamReadOne(mBr, preferredFrames);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                result = new byte[0];
                break;
            }
            return result;
        }

        public void StreamAbort() {
            switch (m_format) {
            case Format.FLAC:
                mFlacR.ReadStreamAbort();
                break;
            case Format.AIFF:
                mAiffR.ReadStreamEnd();
                break;
            case Format.WAVE:
                mWaveR.ReadStreamEnd();
                break;
            }

            if (null != mBr) {
                mBr.Close();
                mBr = null;
            }
            mPcmData = null;
            mFlacR = null;
            mAiffR = null;
            mWaveR = null;
        }

        /// <summary>
        /// 読み込み処理を通常終了する。
        /// </summary>
        /// <returns>Error code</returns>
        public int StreamEnd() {
            int rv = 0;
            switch (m_format) {
            case Format.FLAC:
                rv = mFlacR.ReadStreamEnd();
                break;
            case Format.AIFF:
                mAiffR.ReadStreamEnd();
                break;
            case Format.WAVE:
                mWaveR.ReadStreamEnd();
                break;
            }

            if (null != mBr) {
                mBr.Close();
                mBr = null;
            }
            mPcmData = null;
            mFlacR = null;
            mAiffR = null;
            mWaveR = null;

            return rv;
        }

        /// <summary>
        /// StreamEndの戻り値を文字列に変換。
        /// </summary>
        public static string ErrorCodeToStr(int ercd) {
            return FlacDecodeIF.ErrorCodeToStr(ercd);
        }

        private int StreamBeginFlac(string path, long startFrame, long wantFrames)
        {
            // m_pcmData = new PcmDataLib.PcmData();
            mFlacR = new FlacDecodeIF();
            int ercd = mFlacR.ReadStreamBegin(path, startFrame, wantFrames, out mPcmData);
            if (ercd < 0) {
                return ercd;
            }

            NumFrames = mFlacR.NumFrames;
            return ercd;
        }

        private int StreamBeginAiff(string path, long startFrame)
        {
            int ercd = -1;

            mAiffR = new AiffReader();
            mBr = new BinaryReader(
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

            AiffReader.ResultType result = mAiffR.ReadStreamBegin(mBr, out mPcmData);
            if (result == AiffReader.ResultType.Success) {

                NumFrames = mAiffR.NumFrames;

                mAiffR.ReadStreamSkip(mBr, startFrame);
                ercd = 0;
            }

            return ercd;
        }

        private int StreamBeginWave(string path, long startFrame) {
            int ercd = -1;

            mWaveR = new WavData();
            mBr = new BinaryReader(
                File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

            bool readSuccess = mWaveR.ReadStreamBegin(mBr, out mPcmData);
            if (readSuccess) {

                NumFrames = mWaveR.NumFrames;

                if (mWaveR.ReadStreamSkip(mBr, startFrame)) {
                    ercd = 0;
                }
            }
            return ercd;
        }
    }
}