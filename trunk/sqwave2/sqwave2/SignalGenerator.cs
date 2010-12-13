using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WavRWLib2;
using System.Threading.Tasks;

namespace sqwave2
{
    public enum SignalGeneratorResult
    {
        Success,
        LevelOver
    }

    public enum SignalShape
    {
        SineWave,
        SquareWave,
        SawToothWaveDesc,
        SawToothWaveAsc,
        TriangleWave,
    };

    public struct SignalGenerateParams
    {
        public int seconds;
        public int sampleRate;
        public int bitsPerSample;
        public double dB;
        public double freq;
        public SignalShape ss;
        public double truncationRatio; //< 打ち切り周波数比。ナイキスト周波数=1.0
        public int amplitude;
    };

    public class SignalGenerator
    {
        ////////////////////////////////////////////////////////

        public SignalGeneratorResult GenerateSignal(SignalGenerateParams s, out WavData wavData) {
            List<PcmSamples1Channel> samples = new List<PcmSamples1Channel>();

            int nSample = s.seconds * s.sampleRate;
            PcmSamples1Channel ch = new PcmSamples1Channel(nSample, s.bitsPerSample);
            samples.Add(ch);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            SignalGeneratorResult result = SignalGeneratorResult.Success;
            double truncFreq = (s.sampleRate / 2) * s.truncationRatio;

            switch (s.ss) {
            case SignalShape.SineWave:
                result = GenerateSineWave(ch, s.sampleRate, s.freq, s.amplitude);
                break;
            case SignalShape.SquareWave:
                result = GenerateSquareWave(ch, s.sampleRate, s.freq, s.amplitude, truncFreq);
                break;
            case SignalShape.SawToothWaveDesc:
                result = GenerateSawToothWave(ch, s.sampleRate, s.freq, s.amplitude, truncFreq, false);
                break;
            case SignalShape.SawToothWaveAsc:
                result = GenerateSawToothWave(ch, s.sampleRate, s.freq, s.amplitude, truncFreq, true);
                break;
            case SignalShape.TriangleWave:
                result = GenerateTriangleWave(ch, s.sampleRate, s.freq, s.amplitude, truncFreq);
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            sw.Stop();
            Console.WriteLine("{0} ms", sw.ElapsedMilliseconds);


            wavData = new WavData();
            wavData.Create(s.sampleRate, s.bitsPerSample, samples);
            return result;
        }

        private SignalGeneratorResult GenerateSineWave(PcmSamples1Channel ch, int sampleRate, double freq, int amplitude) {
            Console.WriteLine("CreateSineWave sampleRate={0} freq={1} amp={2}", sampleRate, freq, amplitude);

            SignalGeneratorResult result = SignalGeneratorResult.Success;

            double step = 2.0 * Math.PI * (freq / sampleRate);
            Parallel.For(0, ch.NumSamples, delegate(int i) {
                int v = (int)(amplitude * Math.Sin(step * i));
                short sv = (short)v;
                if (v < -32768) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = -32768;
                }
                if (32767 < v) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = 32767;
                }
                ch.Set16(i, sv);
            });
            return result;
        }

        private SignalGeneratorResult GenerateSquareWave(PcmSamples1Channel ch, int sampleRate, double freq, int amplitude, double truncFreq) {
            Console.WriteLine("CreateSquareWave sampleRate={0} freq={1} amp={2} trunc={3}", sampleRate, freq, amplitude, truncFreq);

            SignalGeneratorResult result = SignalGeneratorResult.Success;
            double step = 2.0 * Math.PI * (freq / sampleRate);
            Parallel.For(0, ch.NumSamples, delegate(int i) {
                double v = 0.0;
                for (int h = 1; ; ++h) {
                    double harmonics = 2 * h - 1;
                    if (amplitude / harmonics < 1.0) {
                        break;
                    }
                    if (truncFreq <= harmonics * freq) {
                        break;
                    }
                    double x = amplitude / harmonics * Math.Sin((step * i * harmonics) % (2.0 * Math.PI));
                    v += x;
                }

                short sv = (short)v;
                if (v < -32768) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = -32768;
                }
                if (32767 < v) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = 32767;
                }
                ch.Set16(i, sv);
            });

            return result;
        }

        private SignalGeneratorResult GenerateSawToothWave(PcmSamples1Channel ch, int sampleRate, double freq, int amplitude, double truncFreq, bool bInvert) {
            Console.WriteLine("CreateSawToothWave sampleRate={0} freq={1} amp={2} trunc={3} invert={4}", sampleRate, freq, amplitude, truncFreq, bInvert);

            double ampWithPhase = amplitude;
            if (bInvert) {
                ampWithPhase = -amplitude;
            }

            SignalGeneratorResult result = SignalGeneratorResult.Success;
            double step = 2.0 * Math.PI * (freq / sampleRate);
            Parallel.For(0, ch.NumSamples, delegate(int i) {
                double v = 0.0;
                for (int h = 1; ; ++h) {
                    double harmonics = h;
                    if (amplitude / harmonics < 1.0) {
                        break;
                    }
                    if (truncFreq <= harmonics * freq) {
                        break;
                    }
                    double x = ampWithPhase / harmonics * Math.Sin((step * i * harmonics) % (2.0 * Math.PI));
                    v += x;
                }

                short sv = (short)v;
                if (v < -32768) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = -32768;
                }
                if (32767 < v) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = 32767;
                }
                ch.Set16(i, sv);
            });

            return result;
        }

        private SignalGeneratorResult GenerateTriangleWave(PcmSamples1Channel ch, int sampleRate, double freq, double amplitude, double truncFreq) {
            Console.WriteLine("CreateTriangleWave sampleRate={0} freq={1} amp={2} trunc={3}", sampleRate, freq, amplitude, truncFreq);

            SignalGeneratorResult result = SignalGeneratorResult.Success;
            double step = 2.0 * Math.PI * (freq / sampleRate);

            Parallel.For(0, ch.NumSamples, delegate(int i) {
                double v = 0.0;
                for (int h = 1; ; ++h) {
                    double harmonics = 2 * h - 1;
                    if (amplitude / harmonics / harmonics < 1.0) {
                        break;
                    }
                    if (truncFreq <= harmonics * freq) {
                        break;
                    }
                    double x = amplitude / harmonics / harmonics * Math.Sin((step * i * harmonics) % (2.0 * Math.PI));
                    if ((h & 1) == 0) {
                        // hが偶数のときは-1倍する
                        x = -x;
                    }
                    v += x;
                }

                short sv = (short)v;
                if (v < -32768) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = -32768;
                }
                if (32767 < v) {
                    result = SignalGeneratorResult.LevelOver;
                    sv = 32767;
                }
                ch.Set16(i, sv);
            });

            return result;
        }

    }
}
