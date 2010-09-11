using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WavRWLib2;

namespace WavRWTest {
    class Program {
        static void Main(string[] args) {
            {
                byte[] rawData16 = new byte[65536 * 2];
                for (int i = 0; i < 65536; ++i) {
                    rawData16[i * 2] = (byte)(i & 0xff);
                    rawData16[i * 2 + 1] = (byte)(i >> 8);
                }

                WavData wdOrigI16 = new WavData();
                wdOrigI16.CreateHeader(1, 44100, 16, 65536);
                wdOrigI16.SetRawData(rawData16);

                {
                    System.Console.WriteLine("I16 ==> I24 ==> I16 test");
                    WavData wdI24 = wdOrigI16.BitsPerSampleConvertTo(24, ValueRepresentationType.SInt);
                    WavData wdNew = wdI24.BitsPerSampleConvertTo(16, ValueRepresentationType.SInt);

                    byte[] b16to24 = wdI24.SampleRawGet();
                    byte[] bNew = wdNew.SampleRawGet();
                    int iMin = Int32.MaxValue;
                    int iMax = Int32.MinValue;

                    for (int i = 0; i < 65536; ++i) {

                        short vOrig = (short)((int)rawData16[i * 2] + (rawData16[i * 2 + 1] << 8));
                        int v16to24 =
                            ((int)b16to24[i * 3] << 8)
                            + (b16to24[i * 3 + 1] << 16)
                            + (b16to24[i * 3 + 2] << 24);
                        int vNew24 = v16to24 / 256;

                        if (vNew24 < iMin) {
                            iMin = vNew24;
                        }
                        if (iMax < vNew24) {
                            iMax = vNew24;
                        }

                        short vNew = (short)((int)bNew[i * 2] + (bNew[i * 2 + 1] << 8));

                        if (0 != (vNew - vOrig)) {
                            System.Console.WriteLine("{0} ==> {1} via {2} diff={3} DIFFERENT! ################",
                            vOrig, vNew, vNew24, vNew - vOrig);
                        }
                    }
                    System.Console.WriteLine("  i24 min={0} max={1}",
                        iMin, iMax);
                }

                {
                    System.Console.WriteLine("I16 ==> I32 ==> I16 test");
                    WavData wdI32 = wdOrigI16.BitsPerSampleConvertTo(32, ValueRepresentationType.SInt);
                    WavData wdNew = wdI32.BitsPerSampleConvertTo(16, ValueRepresentationType.SInt);
                    byte[] b16toI32 = wdI32.SampleRawGet();
                    byte[] bNew = wdNew.SampleRawGet();
                    int iMin = Int32.MaxValue;
                    int iMax = Int32.MinValue;
                    for (int i = 0; i < 65536; ++i) {

                        short vOrig = (short)((int)rawData16[i * 2] + (rawData16[i * 2 + 1] << 8));
                        int vI32 = BitConverter.ToInt32(b16toI32, i * 4);

                        if (vI32 < iMin) {
                            iMin = vI32;
                        }
                        if (iMax < vI32) {
                            iMax = vI32;
                        }

                        short vNew = (short)((int)bNew[i * 2] + (bNew[i * 2 + 1] << 8));
                        if (0 != (vNew - vOrig)) {
                            System.Console.WriteLine("{0} ==> {1} via {2} diff={3} DIFFERENT!",
                                vOrig, vNew, vI32, vNew - vOrig);
                        }
                    }
                    System.Console.WriteLine("  i32 min={0} max={1}",
                        iMin, iMax);
                }

                {
                    System.Console.WriteLine("I16 ==> F32 ==> I16 test");
                    WavData wdF32 = wdOrigI16.BitsPerSampleConvertTo(32, ValueRepresentationType.SFloat);
                    WavData wdNew = wdF32.BitsPerSampleConvertTo(16, ValueRepresentationType.SInt);
                    byte[] b16toF32 = wdF32.SampleRawGet();
                    byte[] bNew = wdNew.SampleRawGet();
                    float fMin = float.MaxValue;
                    float fMax = float.MinValue;
                    for (int i = 0; i < 65536; ++i) {

                        short vOrig = (short)((int)rawData16[i * 2] + (rawData16[i * 2 + 1] << 8));
                        float vF32 = BitConverter.ToSingle(b16toF32, i * 4);

                        if (vF32 < fMin) {
                            fMin = vF32;
                        }
                        if (fMax < vF32) {
                            fMax = vF32;
                        }

                        short vNew = (short)((int)bNew[i * 2] + (bNew[i * 2 + 1] << 8));
                        if (0 != (vNew - vOrig)) {
                            System.Console.WriteLine("{0} ==> {1} via {2} diff={3} DIFFERENT!",
                                vOrig, vNew, vF32, vNew - vOrig);
                        }
                    }

                    System.Console.WriteLine("  fMin={0} fMax={1}", fMin, fMax);
                }
            }

            {
                byte[] rawData24 = new byte[16777216 * 3];
                for (int i = 0; i < 16777216; ++i) {
                    rawData24[i * 3] = (byte)(i & 0xff);
                    rawData24[i * 3 + 1] = (byte)(i >> 8);
                    rawData24[i * 3 + 2] = (byte)(i >> 16);
                }

                WavData wdOrigI24 = new WavData();
                wdOrigI24.CreateHeader(1, 44100, 24, 16777216);
                wdOrigI24.SetRawData(rawData24);

                {
                    System.Console.WriteLine("I24 ==> I32 ==> I24 test");
                    WavData wdI32 = wdOrigI24.BitsPerSampleConvertTo(32, ValueRepresentationType.SInt);
                    WavData wdNew = wdI32.BitsPerSampleConvertTo(24, ValueRepresentationType.SInt);

                    byte[] b24to32 = wdI32.SampleRawGet();
                    byte[] bNew = wdNew.SampleRawGet();
                    int iMin = Int32.MaxValue;
                    int iMax = Int32.MinValue;
                    for (int i = 0; i < 16777216; ++i) {

                        int vOrig32 = ((int)rawData24[i * 3]<<8)
                            + (rawData24[i * 3 + 1] << 16)
                            + (rawData24[i * 3 + 2] << 24);
                        int vOrig24 = vOrig32 / 256;

                        int v24to32 = BitConverter.ToInt32(b24to32, i * 4);

                        if (v24to32 < iMin) {
                            iMin = v24to32;
                        }
                        if (iMax < v24to32) {
                            iMax = v24to32;
                        }

                        int vNew32 = ((int)bNew[i * 3] << 8)
                            + (bNew[i * 3 + 1] << 16)
                            + (bNew[i * 3 + 2] << 24);
                        int vNew24 = vNew32 / 256;

                        if (0 != (vNew24 - vOrig24)) {
                            System.Console.WriteLine("{0} ==> {1} via {2} diff={3} DIFFERENT! ################",
                            vOrig24, vNew24, v24to32, vNew24 - vOrig24);
                        }
                    }
                    System.Console.WriteLine("  i32 min={0} max={1}",
                                            iMin, iMax);
                }

                {
                    System.Console.WriteLine("I24 ==> F32 ==> I24 test");
                    WavData wdF32 = wdOrigI24.BitsPerSampleConvertTo(32, ValueRepresentationType.SFloat);
                    WavData wdNew = wdF32.BitsPerSampleConvertTo(24, ValueRepresentationType.SInt);

                    byte[] b24toF32 = wdF32.SampleRawGet();
                    byte[] bNew = wdNew.SampleRawGet();
                    float fMin = float.MaxValue;
                    float fMax = float.MinValue; 
                    for (int i = 0; i < 16777216; ++i) {

                        int vOrig32 = ((int)rawData24[i * 3] << 8)
                            + (rawData24[i * 3 + 1] << 16)
                            + (rawData24[i * 3 + 2] << 24);
                        int vOrig24 = vOrig32 / 256;

                        float vF32 = BitConverter.ToSingle(b24toF32, i * 4);

                        if (vF32 < fMin) {
                            fMin = vF32;
                        }
                        if (fMax < vF32) {
                            fMax = vF32;
                        }

                        int vNew32 = ((int)bNew[i * 3] << 8)
                            + (bNew[i * 3 + 1] << 16)
                            + (bNew[i * 3 + 2] << 24);
                        int vNew24 = vNew32 / 256;

                        if (0 != (vNew24 - vOrig24)) {
                            System.Console.WriteLine("{0} ==> {1} via {2} diff={3} DIFFERENT! ################",
                            vOrig24, vNew24, vF32, vNew24 - vOrig24);
                        }
                    }
                    System.Console.WriteLine("  fMin={0} fMax={1}", fMin, fMax);
                }
            }

            System.Console.WriteLine("done.");

        }
    }
}
