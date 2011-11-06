using System;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Pipes;

namespace FlacDecodeCS {
    public class FlacDecode {

        public enum DecodeResultType {
            /// ヘッダの取得やデータの取得に成功。
            Success = 0,

            /// ファイルの最後まで行き、デコードを完了した。もうデータはない。
            Completed = 1,

            // 以下、FLACデコードエラー。
            DataNotReady               = -2,
            WriteOpenFailed            = -3,
            FlacStreamDecoderNewFailed = -4,

            FlacStreamDecoderInitFailed     = -5,
            FlacStreamDecorderProcessFailed = -6,
            LostSync                        = -7,
            BadHeader                       = -8,
            FrameCrcMismatch                = -9,

            Unparseable                = -10,
            NumFrameIsNotAligned       = -11,
            RecvBufferSizeInsufficient = -12,
            OtherError                 = -13,
            FileOpenError              = -14,
        };

        [DllImport("FlacDecodeDLL.dll", CharSet = CharSet.Unicode)]
        private extern static
        int FlacDecodeDLL_DecodeStart(string path);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        void FlacDecodeDLL_DecodeEnd(int id);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetNumOfChannels(int id);
        
        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetBitsPerSample(int id);
        
        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetSampleRate(int id);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        long FlacDecodeDLL_GetNumSamples(int id);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetNumFramesPerBlock(int id);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetLastResult(int id);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetNextPcmData(int id, int numFrame, byte[] buff);

        [DllImport("FlacDecodeDLL.dll", CharSet = CharSet.Unicode)]
        private extern static
        bool FlacDecodeDLL_GetTitleStr(int id, System.Text.StringBuilder name, int nameBytes);

        [DllImport("FlacDecodeDLL.dll", CharSet = CharSet.Unicode)]
        private extern static
        bool FlacDecodeDLL_GetAlbumStr(int id, System.Text.StringBuilder name, int nameBytes);

        [DllImport("FlacDecodeDLL.dll", CharSet = CharSet.Unicode)]
        private extern static
        bool FlacDecodeDLL_GetArtistStr(int id, System.Text.StringBuilder name, int nameBytes);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetPictureBytes(int id);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetPictureData(int id, int offs, int pictureBytes, byte[] buff);

        enum OperationType {
            DecodeAll,
            DecodeHeaderOnly
        }

        private static string Base64Decode(string s) {
            byte[] bytes = Convert.FromBase64String(s);
            char[] chars = new char[bytes.Length / 2];

            int count = 0;
            for (int i = 0; i < chars.Length; ++i) {
                int c0 = bytes[count++];
                int c1 = bytes[count++];
                chars[i] = (char)(c0 + (c1 << 8));
            }

            return new string(chars);
        }

#if DEBUG == true
        static private System.IO.StreamWriter m_logFile;
#endif

        static private void LogOpen() {
#if DEBUG == true
            m_logFile = new System.IO.StreamWriter(
                string.Format("logDecodeCS{0}.txt",
                    System.Diagnostics.Process.GetCurrentProcess().Id));
#endif
        }
        static private void LogClose() {
#if DEBUG == true
            m_logFile.Close();
            m_logFile = null;
#endif
        }

        static private void LogWriteLine(string s) {
#if DEBUG == true
            m_logFile.WriteLine(s);
            LogFlush();
#endif
        }

        static private void LogFlush() {
#if DEBUG == true
            m_logFile.Flush();
#endif
        }

        private int DecodeOne(BinaryWriter bw) {
            string operationStr = System.Console.ReadLine();
            if (null == operationStr) {
                LogWriteLine("stdinの1行目には、ヘッダーのみ抽出の場合H、内容も抽出する場合Aを入力してください。");
                return -2;
            }

            OperationType operationType = OperationType.DecodeAll;
            switch (operationStr[0]) {
            case 'H':
                operationType = OperationType.DecodeHeaderOnly;
                break;
            case 'A':
                operationType = OperationType.DecodeAll;
                break;
            default:
                LogWriteLine("stdinの1行目には、ヘッダーのみ抽出の場合H、内容も抽出する場合Aを入力してください。");
                return -3;
            }

            string sbase64 = System.Console.ReadLine();
            if (null == sbase64) {
                LogWriteLine("stdinの2行目には、FLACファイルのパスを入力してください。");
                return -4;
            }

            string path = Base64Decode(sbase64);

            LogWriteLine(string.Format("FlacDecodeCS DecodeOne operationType={0} path={1}", operationType, path));

            /* パイプ出力の内容
             * オフセット サイズ(バイト) 内容
             * 0          4              リターンコード (0以外の場合 以降のデータは無い)
             * 4          4              チャンネル数   nChannels
             * 8          4              サンプルレート sampleRate
             * 12         4              量子化ビット数 bitsPerSample
             * 16         8              総サンプル数   numSamples
             * 24         4              NumFramesPerBlock
             * 28         titleLen       タイトル文字列
             * 28+titleLen albumLen      アルバム文字列
             * 28+t+al     artistLen      アーティスト文字列
             * 28+t+al+ar  4             画像データバイト数
             * 32+t+al+ar  pictureBytes   画像データ
             * 32+t+al+ar+pic   4              frameCount1 (ヘッダのみの場合なし)
             * 36+t+al+ar+pic   ※1            PCMデータ1(リトルエンディアン、LRLRLR…) (ヘッダのみの場合なし) frameCount1個
             * ※2        4              frameCount2
             * ※2+4      ※3            PCMデータ2 frameCount2個
             * 
             * ※1…frameCount1 * nChannels * (bitsPerSample/8)
             * ※2…32+t+al+ar+pic+※1
             */

            int rv = FlacDecodeDLL_DecodeStart(path);
            bw.Write(rv);
            if (rv < 0) {
                LogWriteLine(string.Format("FLACデコード開始エラー。{0}", rv));
                // FlacDecodeDLL_DecodeEnd(-1);
                return rv;
            }

            int id = rv;

            LogWriteLine(string.Format("FlacDecodeCS DecodeOne id={0}", id));

            int nChannels     = FlacDecodeDLL_GetNumOfChannels(id);
            LogWriteLine(string.Format("FlacDecodeCS DecodeOne GetNumChannels() {0}", nChannels));

            int bitsPerSample = FlacDecodeDLL_GetBitsPerSample(id);
            LogWriteLine(string.Format("FlacDecodeCS DecodeOne GetBitsPerSample() {0}", bitsPerSample));

            int sampleRate    = FlacDecodeDLL_GetSampleRate(id);
            LogWriteLine(string.Format("FlacDecodeCS DecodeOne GetSampleRate() {0}", sampleRate));

            long numSamples   = FlacDecodeDLL_GetNumSamples(id);
            LogWriteLine(string.Format("FlacDecodeCS DecodeOne GetNumSamples() {0}", numSamples));

            int numFramesPerBlock = FlacDecodeDLL_GetNumFramesPerBlock(id);
            LogWriteLine(string.Format("FlacDecodeCS DecodeOne GetNumFramesPerBlock() {0}", numFramesPerBlock));

            StringBuilder buf = new StringBuilder(256);
            FlacDecodeDLL_GetTitleStr(id, buf, buf.Capacity *2);
            string titleStr = buf.ToString();

            LogWriteLine(string.Format("FlacDecodeCS DecodeOne titleStr {0}", titleStr));
            
            FlacDecodeDLL_GetAlbumStr(id, buf, buf.Capacity*2);
            string albumStr = buf.ToString();

            LogWriteLine(string.Format("FlacDecodeCS DecodeOne albumStr {0}", albumStr));
            
            FlacDecodeDLL_GetArtistStr(id, buf, buf.Capacity*2);
            string artistStr = buf.ToString();

            LogWriteLine(string.Format("FlacDecodeCS DecodeOne artistStr {0}", artistStr));

            int pictureBytes = FlacDecodeDLL_GetPictureBytes(id);

            LogWriteLine(string.Format("FlacDecodeCS DecodeOne pictureBytes {0}", pictureBytes));
            
            byte[] pictureData = null;
            if (0 < pictureBytes) {
                pictureData = new byte[pictureBytes];
                FlacDecodeDLL_GetPictureData(id, 0, pictureBytes, pictureData);
            }

            LogWriteLine(string.Format("FlacDecodeCS DecodeOne output start"));

            bw.Write(nChannels);
            bw.Write(bitsPerSample);
            bw.Write(sampleRate);
            bw.Write(numSamples);
            bw.Write(numFramesPerBlock);

            bw.Write(titleStr);
            bw.Write(albumStr);
            bw.Write(artistStr);

            bw.Write(pictureBytes);
            if (0 < pictureBytes) {
                bw.Write(pictureData);
            }

            int ercd = 0;

            if (operationType == OperationType.DecodeAll) {
                // デコードしたデータを全部パイプに出力する。
                int frameBytes = nChannels * bitsPerSample / 8;

                const int numFramePerCall = 1024 * 1024;
                byte[] buff = new byte[numFramePerCall * frameBytes];

                while (true) {
                    LogWriteLine("FlacDecodeDLL_GetNextPcmData 呼び出し");
                    rv = FlacDecodeDLL_GetNextPcmData(id, numFramePerCall, buff);
                    ercd = FlacDecodeDLL_GetLastResult(id);
                    LogWriteLine(string.Format("FlacDecodeDLL_GetNextPcmData rv={0} ercd={1}", rv, ercd));

                    if (0 < rv) {
                        bw.Write(rv);
                        bw.Write(buff, 0, rv * frameBytes);
                    }

                    if (rv <= 0 || ercd == 1) {
                        // これでおしまい。
                        int v0 = 0;
                        bw.Write(v0);
                        LogWriteLine(string.Format("FlacDecodeDLL_GetNextPcmData 終了。rv={0} ercd={1}", rv, ercd));
                        if (0 <= rv && ercd == 1) {
                            ercd = 0;
                        }
                        break;
                    }
                }
            }

            LogWriteLine("FlacDecodeDLL_DecodeEnd 呼び出し");
            FlacDecodeDLL_DecodeEnd(id);
            return ercd;
        }

        private int Run(string pipeHandleAsString) {
            int exitCode = -1;
            using (PipeStream pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, pipeHandleAsString)) {
                using (BinaryWriter bw = new BinaryWriter(pipeClient)) {
                    try {
                        exitCode = DecodeOne(bw);
                    } catch (System.Exception ex) {
                        LogWriteLine(string.Format("E: {0}", ex));
                        exitCode = -5;
                    }
                }
            }
            return exitCode;
        }

        static int Main1(string[] args) {
            if (1 != args.Length) {
                LogWriteLine(string.Format("E: FlacDecode.cs args[0] must be pipeHandleAsStream"));
                return (int)DecodeResultType.OtherError;
            }

            LogWriteLine(string.Format("FlacDecode.cs Main1 開始 args[0]={0}", args[0]));

            FlacDecode p = new FlacDecode();
            int exitCode = p.Run(args[0]);

            LogWriteLine(string.Format("FlacDecode.cs Main1 終了 exitCode={0}", exitCode));
            return exitCode;
        }

        static void Main(string[] args) {
            LogOpen();
            LogWriteLine("started");

            int exitCode = (int)DecodeResultType.OtherError;
            try {
                exitCode = Main1(args);
            } catch (Exception ex) {
                LogWriteLine(string.Format("FlacDecode.cs Main {0}", ex));
            }
            LogClose();

            System.Environment.ExitCode = exitCode;
        }
    }
}