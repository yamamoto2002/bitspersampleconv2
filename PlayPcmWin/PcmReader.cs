using WavRWLib2;
using PcmDataLib;
using System;
using System.IO;

namespace PlayPcmWin {
    class PcmReader {
        private PcmData m_pcmData;
        private FlacDecodeIF m_flacR;
        private AiffReader m_aiffR;
        private WavData m_waveR;
        private BinaryReader m_br;

        public long NumFrames { get; set; }

        enum Format {
            FLAC,
            AIFF,
            WAVE
        };
        Format m_format;

        /// <summary>
        /// StreamBegin()を呼んだら、成功しても失敗してもStreamEnd()を呼んでください。
        /// </summary>
        /// <param name="path">ファイルパス。</param>
        /// <param name="startFrame">読み出し開始フレーム</param>
        /// <param name="wantFrames">取得したいフレーム数。-1: 最後まで。0: 取得しない。</param>
        /// <returns>0以上: 成功。負: 失敗。</returns>
        public int StreamBegin(string path, long startFrame, long wantFrames) {
            string ext = System.IO.Path.GetExtension(path);
            switch (ext.ToLower()) {
            case ".flac":
                // FLACファイル読み込み。
                m_format = Format.FLAC;
                return StreamBeginFlac(path, startFrame, wantFrames);
            case ".aiff":
            case ".aif":
                // AIFFファイル読み込み。
                m_format = Format.AIFF;
                return StreamBeginAiff(path, startFrame);
            case ".wav":
            case ".wave":
                // WAVEファイル読み込み。
                m_format = Format.WAVE;
                return StreamBeginWave(path, startFrame);
            default:
                System.Diagnostics.Debug.Assert(false);
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
                result = m_flacR.ReadStreamReadOne(preferredFrames);
                break;
            case Format.AIFF:
                result = m_aiffR.ReadStreamReadOne(m_br, preferredFrames);
                break;
            case Format.WAVE:
                result = m_waveR.ReadStreamReadOne(m_br, preferredFrames);
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
                m_flacR.ReadStreamAbort();
                break;
            case Format.AIFF:
                m_aiffR.ReadStreamEnd();
                break;
            case Format.WAVE:
                m_waveR.ReadStreamEnd();
                break;
            }

            if (null != m_br) {
                m_br.Close();
                m_br = null;
            }
            m_pcmData = null;
            m_flacR = null;
            m_aiffR = null;
            m_waveR = null;
        }

        /// <summary>
        /// 読み込み処理を通常終了する。
        /// </summary>
        /// <returns>Error code</returns>
        public int StreamEnd() {
            int rv = 0;
            switch (m_format) {
            case Format.FLAC:
                rv = m_flacR.ReadStreamEnd();
                break;
            case Format.AIFF:
                m_aiffR.ReadStreamEnd();
                break;
            case Format.WAVE:
                m_waveR.ReadStreamEnd();
                break;
            }

            if (null != m_br) {
                m_br.Close();
                m_br = null;
            }
            m_pcmData = null;
            m_flacR = null;
            m_aiffR = null;
            m_waveR = null;

            return rv;
        }

        /// <summary>
        /// StreamEndの戻り値を文字列に変換。
        /// </summary>
        public string ErrorCodeToStr(int ercd) {
            return FlacDecodeIF.ErrorCodeToStr(ercd);
        }

        private int StreamBeginFlac(string path, long startFrame, long wantFrames)
        {
            // m_pcmData = new PcmDataLib.PcmData();
            m_flacR = new FlacDecodeIF();
            int ercd = m_flacR.ReadStreamBegin(path, startFrame, wantFrames, out m_pcmData);
            if (ercd < 0) {
                return ercd;
            }

            NumFrames = m_flacR.NumFrames;
            return ercd;
        }

        private int StreamBeginAiff(string path, long startFrame)
        {
            int ercd = -1;

            m_aiffR = new AiffReader();
            try {
                m_br = new BinaryReader(
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

                AiffReader.ResultType result = m_aiffR.ReadStreamBegin(m_br, out m_pcmData);
                if (result == AiffReader.ResultType.Success) {

                    NumFrames = m_aiffR.NumFrames;

                    m_aiffR.ReadStreamSkip(m_br, startFrame);
                    ercd = 0;
                }
            } catch (Exception ex) {
                Console.WriteLine("E: StreamBeginAiff {0}", ex);
            }

            return ercd;
        }

        private int StreamBeginWave(string path, long startFrame) {
            int ercd = -1;

            m_waveR = new WavData();
            try {
                m_br = new BinaryReader(
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read));

                bool readSuccess = m_waveR.ReadStreamBegin(m_br, out m_pcmData);
                if (readSuccess) {

                    NumFrames = m_waveR.NumFrames;

                    if (m_waveR.ReadStreamSkip(m_br, startFrame)) {
                        ercd = 0;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("E: StreamBeginWave {0}", ex);
            }

            return ercd;
        }
    }
}
