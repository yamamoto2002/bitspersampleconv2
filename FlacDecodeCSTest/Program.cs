using System;
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

        struct CuesheetTrackIndexInfo {
            public int indexNr;
            public long offsetSamples;
        }

        class CuesheetTrackInfo {
            public int trackNr;
            public long offsetSamples;
            public List<CuesheetTrackIndexInfo> indices = new List<CuesheetTrackIndexInfo>();
        };

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
            long numFrames     = br.ReadInt64();
            int  numFramesPerBlock = br.ReadInt32();

            string titleStr = br.ReadString();
            string albumStr = br.ReadString();
            string artistStr = br.ReadString();

            int pictureBytes = br.ReadInt32();
            byte[] pictureData = new byte[0];
            if (0 < pictureBytes) {
                br.ReadBytes(pictureBytes);
            }

            var cuesheetTracks = new List<CuesheetTrackInfo>();
            {
                int numCuesheetTracks = br.ReadInt32();
                for (int trackId=0; trackId < numCuesheetTracks; ++trackId) {
                    var cti = new CuesheetTrackInfo();
                    cti.trackNr = br.ReadInt32();
                    cti.offsetSamples = br.ReadInt64();

                    int numCuesheetTrackIndices = br.ReadInt32();
                    for (int indexId=0; indexId < numCuesheetTrackIndices; ++indexId) {
                        var indexInfo = new CuesheetTrackIndexInfo();
                        indexInfo.indexNr = br.ReadInt32();
                        indexInfo.offsetSamples = br.ReadInt64();
                        cti.indices.Add(indexInfo);
                    }
                    cuesheetTracks.Add(cti);
                }
            }

            System.Console.WriteLine("ReadHeaderTest() completed. nChannels={0} bitsPerSample={1} sampleRate={2} numFrames={3} numFramesPerBlock={4}",
                nChannels, bitsPerSample, sampleRate, numFrames, numFramesPerBlock);

            System.Console.WriteLine("TITLE={0}", titleStr);
            System.Console.WriteLine("ALBUM={0}", albumStr);
            System.Console.WriteLine("ARTIST={0}", artistStr);

            StopChildProcess();
            return 0;
        }

        private int ReadAllTest(string flacFilePath, long skipFrames, long wantFrames) {
            StartChildProcess();
            SendString("A");
            SendBase64(flacFilePath);

            SendString(skipFrames.ToString());
            SendString(wantFrames.ToString());

            System.Console.WriteLine("ReadAllTest({0}) started", flacFilePath);

            int rv = br.ReadInt32();
            if (rv != 0) {
                StopChildProcess();
                return rv;
            }

            int  nChannels     = br.ReadInt32();
            int  bitsPerSample = br.ReadInt32();
            int  sampleRate    = br.ReadInt32();
            long numFrames     = br.ReadInt64();
            int  numFramesPerBlock = br.ReadInt32();

            string titleStr = br.ReadString();
            string albumStr = br.ReadString();
            string artistStr = br.ReadString();

            int pictureBytes = br.ReadInt32();
            byte[] pictureData = new byte[0];
            if (0 < pictureBytes) {
                br.ReadBytes(pictureBytes);
            }

            /*
            int numCuesheetTracks = br.ReadInt32();
            var cueSheetOffsets = new long[numCuesheetTracks];
            for (int i=0; i < numCuesheetTracks; ++i) {
                cueSheetOffsets[i] = br.ReadInt64();
            }
            */

            int bytesPerFrame = nChannels * bitsPerSample / 8;

            System.Console.WriteLine("ReadAllTest() nChannels={0} bitsPerSample={1} sampleRate={2} numFrames={3} numFramesPerBlock={4}",
                nChannels, bitsPerSample, sampleRate, numFrames, numFramesPerBlock);

            System.Console.WriteLine("TITLE={0}", titleStr);
            System.Console.WriteLine("ALBUM={0}", albumStr);
            System.Console.WriteLine("ARTIST={0}", artistStr);

            if (wantFrames != 0) {
                if (wantFrames < 0) {
                    wantFrames = numFrames - skipFrames;
                }

                long readFrames = 0;
                while (readFrames < wantFrames) {
                    int n = br.ReadInt32();
                    if (n == 0) {
                        break;
                    }

                    System.Console.WriteLine("ReadAllTest() n={0}", n);

                    byte[] buff = br.ReadBytes(n * bytesPerFrame);

                    System.Console.WriteLine("ReadAllTest() n={0} readCount={1} pos={2}",
                        n, buff.Length / bytesPerFrame, readFrames);
                    if (buff.Length == 0) {
                        break;
                    }
                    readFrames += buff.Length / bytesPerFrame;
                }

                System.Console.WriteLine("ReadAllTest() numFrames={0} read={1} ({2}M Frames)",
                    numFrames, readFrames, readFrames / 1048576);
            }

            int exitCode = StopChildProcess();
            System.Console.WriteLine("ReadAllTest() exitCode={0}",
                exitCode);
            return exitCode;
        }

        private int Run(string flacPath) {
            int exitCode;
            
            exitCode = ReadHeaderTest(flacPath);
            System.Console.WriteLine("Run() ReadHeaderTest result={0}", exitCode);


            
            exitCode = ReadAllTest(flacPath, 65536, 65536);
            System.Console.WriteLine("Run() ReadAllTest partial result={0}", exitCode);

            exitCode = ReadAllTest(flacPath, 0, 0);
            System.Console.WriteLine("Run() ReadAllTest onlyheader result={0}", exitCode);

            exitCode = ReadAllTest(flacPath, 0, -1);
            System.Console.WriteLine("Run() ReadAllTest till eof result={0}", exitCode);

            return exitCode;
        }

        static void Main(string[] args) {
            if (args.Length != 1) {
                System.Console.WriteLine("Usage: flacFilePath");
                return;
            }

            Program p = new Program();
            p.Run(args[0]);
        }
    }
}
