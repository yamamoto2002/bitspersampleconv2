using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace PlayPcmWin {
    class FlacDecodeIF {
        private Process childProcess;
        private BinaryReader br;
        private AnonymousPipeServerStream pss;

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
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.LostSync:
                return "デコード中に同期を見失いました(データが壊れている)。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.BadHeader:
                return "ヘッダー部分が壊れています。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FrameCrcMismatch:
                return "CRCエラー。ファイルの内容が壊れています。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.Unparseable:
                return "解析失敗。";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.OtherError:
            default:
                return "分類外のエラー。";
            }
        }

        private void SendString(string s) {
            childProcess.StandardInput.WriteLine(s);
        }

        private void SendBase64(string s) {
            byte[] b = new byte[s.Length * 2];

            for (int i = 0; i < s.Length; ++i) {
                b[i * 2 + 0] = (byte)((s[i] >> 0) & 0xff);
                b[i * 2 + 1] = (byte)((s[i] >> 8) & 0xff);
            }
            string sSend = Convert.ToBase64String(b);
            childProcess.StandardInput.WriteLine(sSend);
        }

        private void StartChildProcess() {
            System.Diagnostics.Debug.Assert(null == childProcess);

            childProcess = new Process();
            childProcess.StartInfo.FileName = "FlacDecodeCS.exe";

            pss = new AnonymousPipeServerStream(
                PipeDirection.In, HandleInheritability.Inheritable);

            childProcess.StartInfo.Arguments = pss.GetClientHandleAsString();
            childProcess.StartInfo.UseShellExecute = false;
            childProcess.StartInfo.CreateNoWindow = true;
            childProcess.StartInfo.RedirectStandardInput = true;
            childProcess.StartInfo.RedirectStandardOutput = false;
            childProcess.Start();

            pss.DisposeLocalCopyOfClientHandle();
            br = new BinaryReader(pss);
        }

        private int StopChildProcess() {
            System.Diagnostics.Debug.Assert(null != childProcess);
            childProcess.WaitForExit();
            int exitCode = childProcess.ExitCode;
            childProcess.Close();
            childProcess = null;
            pss.Close();
            br.Close();
            br = null;

            return exitCode;
        }

        public int ReadHeader(string flacFilePath, out WavRWLib2.WavData wavData) {
            wavData = new WavRWLib2.WavData();

            StartChildProcess();
            SendString("H");
            SendBase64(flacFilePath);

            int rv = br.ReadInt32();
            if (rv != 0) {
                StopChildProcess();
                return rv;
            }

            int nChannels = br.ReadInt32();
            int bitsPerSample = br.ReadInt32();
            int sampleRate = br.ReadInt32();
            long numSamples = br.ReadInt64();

            System.Console.WriteLine("nChannels={0} bitsPerSample={1} sampleRate={2} numSamples={3}",
                nChannels, bitsPerSample, sampleRate, numSamples);

            StopChildProcess();

            wavData.CreateHeader(nChannels, sampleRate, bitsPerSample, numSamples);

            return 0;
        }

        public int ReadAll(string flacFilePath, out WavRWLib2.WavData wavData) {
            wavData = new WavRWLib2.WavData();

            StartChildProcess();
            SendString("A");
            SendBase64(flacFilePath);

            int rv = br.ReadInt32();
            if (rv != 0) {
                StopChildProcess();
                return rv;
            }

            int nChannels = br.ReadInt32();
            int bitsPerSample = br.ReadInt32();
            int sampleRate = br.ReadInt32();
            long numSamples = br.ReadInt64();

            int bytesPerFrame = nChannels * bitsPerSample / 8;
            int frameCount = 1048576;

            byte[] rawData = new byte[numSamples * bytesPerFrame];

            long pos = 0;
            while (pos < numSamples) {
                byte[] buff = br.ReadBytes(frameCount * bytesPerFrame);

                System.Console.WriteLine("frameCount={0} readCount={1} pos={2}",
                    frameCount, buff.Length / bytesPerFrame, pos);
                if (buff.Length == 0) {
                    break;
                }

                buff.CopyTo(rawData, pos * bytesPerFrame);
                pos += buff.Length / bytesPerFrame;
            }

            System.Console.WriteLine("numSamples={0} pos={1} ({2}M samples)",
                numSamples, pos, pos / 1048576);

            int exitCode = StopChildProcess();
            System.Console.WriteLine("exitCode={0}",
                exitCode);

            if (0 == exitCode) {
                wavData.CreateHeader(nChannels, sampleRate, bitsPerSample, numSamples);
                wavData.SetRawData(rawData);
            }
            return exitCode;
        }
    }
}
