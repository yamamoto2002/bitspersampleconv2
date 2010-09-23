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
            Completed,

            // 以下、FLACデコードエラー。
            DataNotReady,
            WriteOpenFailed,
            FlacStreamDecoderNewFailed,

            FlacStreamDecoderInitFailed,
            FlacStreamDecorderProcessFailed,
            LostSync,
            BadHeader,
            FrameCrcMismatch,
 
            Unparseable,
            NumFrameIsNotAligned,
            RecvBufferSizeInsufficient,
            OtherError
        };

        [DllImport("FlacDecodeDLL.dll", CharSet = CharSet.Ansi)]
        private extern static
        int FlacDecodeDLL_DecodeStart(string path);

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        void FlacDecodeDLL_DecodeEnd();

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetNumOfChannels();
        
        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetBitsPerSample();
        
        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetSampleRate();

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        long FlacDecodeDLL_GetNumSamples();

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetLastResult();

        [DllImport("FlacDecodeDLL.dll")]
        private extern static
        int FlacDecodeDLL_GetNextPcmData(int numFrame, byte [] buff);

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
            m_logFile = new System.IO.StreamWriter("logDecodeCS.txt");
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
             * 24         ※1            PCMデータ(リトルエンディアン、LRLRLR…) (ヘッダのみの場合なし)
             * 
             * ※1…numSamples * nChannels * (bitsPerSample/8)
             */

            int rv = FlacDecodeDLL_DecodeStart(path);
            bw.Write(rv);
            if (rv != 0) {
                LogWriteLine(string.Format("FLACデコード開始エラー。{0}", rv));
                FlacDecodeDLL_DecodeEnd();
                return rv;
            }

            int nChannels     = FlacDecodeDLL_GetNumOfChannels();
            int bitsPerSample = FlacDecodeDLL_GetBitsPerSample();
            int sampleRate    = FlacDecodeDLL_GetSampleRate();
            long numSamples   = FlacDecodeDLL_GetNumSamples();

            bw.Write(nChannels);
            bw.Write(bitsPerSample);
            bw.Write(sampleRate);
            bw.Write(numSamples);

            int ercd = 0;

            if (operationType == OperationType.DecodeAll) {
                // デコードしたデータを全部パイプに出力する。
                int frameBytes = nChannels * bitsPerSample / 8;

                const int numFramePerCall = 1024 * 1024;
                byte[] buff = new byte[numFramePerCall * frameBytes];

                while (true) {
                    LogWriteLine("FlacDecodeDLL_GetNextPcmData 呼び出し");
                    rv = FlacDecodeDLL_GetNextPcmData(numFramePerCall, buff);
                    ercd = FlacDecodeDLL_GetLastResult();
                    LogWriteLine(string.Format("FlacDecodeDLL_GetNextPcmData rv={0} ercd={1}", rv, ercd));

                    if (0 < rv) {
                        bw.Write(buff, 0, rv * frameBytes);
                    }

                    if (rv <= 0 || ercd == 1) {
                        // これでおしまい。
                        LogWriteLine(string.Format("FlacDecodeDLL_GetNextPcmData 終了。rv={0} ercd={1}", rv, ercd));
                        if (0 <= rv && ercd == 1) {
                            ercd = 0;
                        }
                        break;
                    }
                }
            }

            LogWriteLine("FlacDecodeDLL_DecodeEnd 呼び出し");
            FlacDecodeDLL_DecodeEnd();
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

        static void Main(string[] args) {
            LogOpen();

            if (1 != args.Length) {
                LogWriteLine(string.Format("E: args[0] must be pipeHandleAsStream"));
                return;
            }

            LogWriteLine(string.Format("FlacDecode.cs Main 開始 args[0]={0}", args[0]));

            FlacDecode p = new FlacDecode();
            int exitCode = p.Run(args[0]);

            LogWriteLine(string.Format("FlacDecode.cs Main 終了 exitCode={0}", exitCode));

            LogClose();

            System.Environment.ExitCode = exitCode;
        }
    }
}
