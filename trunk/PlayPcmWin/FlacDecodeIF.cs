using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace PlayPcmWin {
    class FlacDecodeIF {
        private Process m_childProcess;
        private BinaryReader m_br;
        private AnonymousPipeServerStream m_pss;

        public static string ErrorCodeToStr(int ercd) {
            switch (ercd) {
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.Success:
                return "成功。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.Completed:
                return "成功のうちに終了。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.DataNotReady:
                return "データの準備がまだ出来ていません(内部エラー)。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.WriteOpenFailed:
                return "ファイルが開けませんでした。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FlacStreamDecoderNewFailed:
                return "FlacStreamDecoderの作成に失敗。";

            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FlacStreamDecoderInitFailed:
                return "FlacStreamDecoderの初期化失敗。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FlacStreamDecorderProcessFailed:
                return "FlacStreamDecoderが失敗を戻しました。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.LostSync:
                return "デコード中に同期を見失いました(データが壊れている)。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.BadHeader:
                return "ヘッダー部分が壊れています。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FrameCrcMismatch:
                return "CRCエラー。ファイルの内容が壊れています。";

            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.Unparseable:
                return "解析失敗。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.NumFrameIsNotAligned:
                return "フレーム数のアラインエラー(内部エラー)";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.RecvBufferSizeInsufficient:
                return "受信バッファサイズが小さすぎて１ブロックも入りませんでした。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.OtherError:
            default:
                return "分類外のエラー。";
            }
        }

        private void SendString(string s) {
            m_childProcess.StandardInput.WriteLine(s);
        }

        private void SendBase64(string s) {
            byte[] b = new byte[s.Length * 2];

            for (int i = 0; i < s.Length; ++i) {
                b[i * 2 + 0] = (byte)((s[i] >> 0) & 0xff);
                b[i * 2 + 1] = (byte)((s[i] >> 8) & 0xff);
            }
            string sSend = Convert.ToBase64String(b);
            m_childProcess.StandardInput.WriteLine(sSend);
        }

        private void StartChildProcess() {
            System.Diagnostics.Debug.Assert(null == m_childProcess);

            m_childProcess = new Process();
            m_childProcess.StartInfo.FileName = "FlacDecodeCS.exe";

            m_pss = new AnonymousPipeServerStream(
                PipeDirection.In, HandleInheritability.Inheritable);

            m_childProcess.StartInfo.Arguments = m_pss.GetClientHandleAsString();
            m_childProcess.StartInfo.UseShellExecute = false;
            m_childProcess.StartInfo.CreateNoWindow = true;
            m_childProcess.StartInfo.RedirectStandardInput = true;
            m_childProcess.StartInfo.RedirectStandardOutput = false;
            m_childProcess.Start();

            m_pss.DisposeLocalCopyOfClientHandle();
            m_br = new BinaryReader(m_pss);
        }

        private int StopChildProcess() {
            System.Diagnostics.Debug.Assert(null != m_childProcess);
            m_childProcess.WaitForExit();
            int exitCode = m_childProcess.ExitCode;
            m_childProcess.Close();
            m_childProcess = null;
            m_pss.Close();
            m_br.Close();
            m_br = null;

            return exitCode;
        }

        public int ReadHeader(string flacFilePath, out PcmDataLib.PcmData pcmData) {
            pcmData = new PcmDataLib.PcmData();

            StartChildProcess();
            SendString("H");
            SendBase64(flacFilePath);

            int rv = m_br.ReadInt32();
            if (rv != 0) {
                StopChildProcess();
                return rv;
            }

            int nChannels     = m_br.ReadInt32();
            int bitsPerSample = m_br.ReadInt32();
            int sampleRate    = m_br.ReadInt32();
            long numFrames    = m_br.ReadInt64();

            System.Console.WriteLine("nChannels={0} bitsPerSample={1} sampleRate={2} numFrames={3}",
                nChannels, bitsPerSample, sampleRate, numFrames);

            StopChildProcess();

            pcmData.SetFormat(
                nChannels,
                bitsPerSample,
                sampleRate,
                PcmDataLib.PcmData.ValueRepresentationType.SInt,
                numFrames);

            return 0;
        }

        public int ReadAll(string flacFilePath, out PcmDataLib.PcmData pcmData) {
            pcmData = new PcmDataLib.PcmData();

            StartChildProcess();
            SendString("A");
            SendBase64(flacFilePath);

            int rv = m_br.ReadInt32();
            if (rv != 0) {
                StopChildProcess();
                return rv;
            }

            int nChannels     = m_br.ReadInt32();
            int bitsPerSample = m_br.ReadInt32();
            int sampleRate    = m_br.ReadInt32();
            long numFrames    = m_br.ReadInt64();

            int bytesPerFrame = nChannels * bitsPerSample / 8;
            int frameCount = 1048576;

            byte[] sampleArray = new byte[numFrames * bytesPerFrame];

            long pos = 0;
            while (pos < numFrames) {
                byte[] buff = m_br.ReadBytes(frameCount * bytesPerFrame);

                System.Console.WriteLine("frameCount={0} readCount={1} pos={2}",
                    frameCount, buff.Length / bytesPerFrame, pos);
                if (buff.Length == 0) {
                    break;
                }

                buff.CopyTo(sampleArray, pos * bytesPerFrame);
                pos += buff.Length / bytesPerFrame;
            }

            System.Console.WriteLine("numFrames={0} pos={1} ({2}M frames)",
                numFrames, pos, pos / 1048576);

            int exitCode = StopChildProcess();
            System.Console.WriteLine("exitCode={0}",
                exitCode);

            if (0 == exitCode) {
                pcmData.SetFormat(
                    nChannels,
                    bitsPerSample,
                    sampleRate,
                    PcmDataLib.PcmData.ValueRepresentationType.SInt,
                    numFrames);
                pcmData.SetSampleArray(numFrames, sampleArray);
            }
            return exitCode;
        }
    }
}
