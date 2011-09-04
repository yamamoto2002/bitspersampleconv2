﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace FlacDecodeCSTest {
    class Program {
        private Process                   childProcess;
        private BinaryReader              br;
        private AnonymousPipeServerStream pss;
        
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

        public void StartChildProcess() {
            System.Diagnostics.Debug.Assert(null == childProcess);

            childProcess = new Process();
            childProcess.StartInfo.FileName = "FlacDecodeCS.exe";

            pss = new AnonymousPipeServerStream(
                PipeDirection.In, HandleInheritability.Inheritable);

            childProcess.StartInfo.Arguments = pss.GetClientHandleAsString();
            childProcess.StartInfo.UseShellExecute        = false;
            childProcess.StartInfo.CreateNoWindow         = true;
            childProcess.StartInfo.RedirectStandardInput  = true;
            childProcess.StartInfo.RedirectStandardOutput = false;
            childProcess.Start();

            pss.DisposeLocalCopyOfClientHandle();
            br = new BinaryReader(pss);
        }

        public int StopChildProcess() {
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

        private int ReadHeaderTest(string flacFilePath) {
            StartChildProcess();
            SendString("H");
            SendBase64(flacFilePath);

            System.Console.WriteLine("ReadHeaderTest({0}) started", flacFilePath);

            int rv = br.ReadInt32();
            if (rv != 0) {
                StopChildProcess();
                return rv;
            }

            int  nChannels     = br.ReadInt32();
            int  bitsPerSample = br.ReadInt32();
            int  sampleRate    = br.ReadInt32();
            long numSamples    = br.ReadInt64();

            string titleStr = br.ReadString();
            string albumStr = br.ReadString();
            string artistStr = br.ReadString();

            int pictureBytes = br.ReadInt32();
            byte[] pictureData = new byte[0];
            if (0 < pictureBytes) {
                br.ReadBytes(pictureBytes);
            }

            System.Console.WriteLine("ReadHeaderTest() completed. nChannels={0} bitsPerSample={1} sampleRate={2} numSamples={3}",
                nChannels, bitsPerSample, sampleRate, numSamples);

            System.Console.WriteLine("TITLE={0}", titleStr);
            System.Console.WriteLine("ALBUM={0}", albumStr);
            System.Console.WriteLine("ARTIST={0}", artistStr);

            StopChildProcess();
            return 0;
        }

        private int ReadAllTest(string flacFilePath) {
            StartChildProcess();
            SendString("A");
            SendBase64(flacFilePath);

            System.Console.WriteLine("ReadAllTest({0}) started", flacFilePath);

            int rv = br.ReadInt32();
            if (rv != 0) {
                StopChildProcess();
                return rv;
            }

            int  nChannels     = br.ReadInt32();
            int  bitsPerSample = br.ReadInt32();
            int  sampleRate    = br.ReadInt32();
            long numSamples    = br.ReadInt64();

            string titleStr = br.ReadString();
            string albumStr = br.ReadString();
            string artistStr = br.ReadString();

            int pictureBytes = br.ReadInt32();
            byte[] pictureData = new byte[0];
            if (0 < pictureBytes) {
                br.ReadBytes(pictureBytes);
            }

            int bytesPerFrame = nChannels * bitsPerSample / 8;

            System.Console.WriteLine("ReadAllTest() nChannels={0} bitsPerSample={1} sampleRate={2} numSamples={3}",
                nChannels, bitsPerSample, sampleRate, numSamples);

            System.Console.WriteLine("TITLE={0}", titleStr);
            System.Console.WriteLine("ALBUM={0}", albumStr);
            System.Console.WriteLine("ARTIST={0}", artistStr);

            long pos = 0;
            while (pos < numSamples) {
                int frameCount = br.ReadInt32();
                if (frameCount == 0) {
                    break;
                }

                System.Console.WriteLine("ReadAllTest() frameCount={0}", frameCount);

                byte[] buff = br.ReadBytes(frameCount * bytesPerFrame);

                System.Console.WriteLine("ReadAllTest() frameCount={0} readCount={1} pos={2}",
                    frameCount, buff.Length / bytesPerFrame, pos);
                if (buff.Length == 0) {
                    break;
                }
                pos += buff.Length / bytesPerFrame;
            }

            System.Console.WriteLine("ReadAllTest() numSamples={0} pos={1} ({2}M samples)",
                numSamples, pos, pos / 1048576);

            int exitCode = StopChildProcess();
            System.Console.WriteLine("ReadAllTest() exitCode={0}",
                exitCode);
            return exitCode;
        }

        private int Run(string flacPath) {
            int exitCode;
            
            exitCode = ReadHeaderTest(flacPath);
            System.Console.WriteLine("Run() ReadHeaderTest result={0}", exitCode);

            exitCode = ReadAllTest(flacPath);
            System.Console.WriteLine("Run() ReadAllTest result={0}", exitCode);

            return exitCode;
        }

        static void Main(string[] args) {
            if (args.Length != 1) {
                System.Console.WriteLine("E: args[0] must be flacFilePath");
                return;
            }

            Program p = new Program();
            p.Run(args[0]);
        }
    }
}
