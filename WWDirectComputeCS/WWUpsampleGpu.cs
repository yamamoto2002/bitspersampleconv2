/*
 Copyright (C) 2010 yamamoto2002
 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"),
 to deal in the Software without restriction, including without limitation
 the rights to use, copy, modify, merge, publish, distribute, sublicense,
 and/or sell copies of the Software, and to permit persons to whom the
 Software is furnished to do so, subject to the following conditions:
 The above copyright notice and this permission notice shall be included
 in all copies or substantial portions of the Software.
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 THE X CONSORTIUM BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
 IN THE SOFTWARE.
*/

using System.Runtime.InteropServices;

namespace WWDirectComputeCS {
    public class WWUpsampleGpu {
        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCUpsample_Init(
            int convolutionN,
            float[] sampleFrom,
            int sampleTotalFrom,
            int sampleRateFrom,
            int sampleRateTo,
            int sampleTotalTo);

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCUpsample_InitWithResamplePosArray(
            int convolutionN,
            float[] sampleFrom,
            int sampleTotalFrom,
            int sampleRateFrom,
            int sampleRateTo,
            int sampleTotalTo,
            int[] resamplePosArray,
            double[] fractionArray);

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCUpsample_Dispatch(
            int startPos,
            int count);

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCUpsample_GetResultFromGpuMemory(
            [In, Out] float[] outputTo,
            int outputToElemNum);

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static void
        WWDCUpsample_Term();

        /////////////////////////////////////////////////////////////////////

        /// <returns>HRESULT</returns>
        public int Init(
                int convolutionN,
                float[] sampleFrom,
                int sampleTotalFrom,
                int sampleRateFrom,
                int sampleRateTo,
                int sampleTotalTo) {
            return WWDCUpsample_Init(convolutionN, sampleFrom,
                sampleTotalFrom, sampleRateFrom, sampleRateTo, sampleTotalTo);
        }

        /// <returns>HRESULT</returns>
        public int Init(
                int convolutionN,
                float[] sampleFrom,
                int sampleTotalFrom,
                int sampleRateFrom,
                int sampleRateTo,
                int sampleTotalTo,
                int[] resamplePosArray,
                double[] fractionArray) {
            return WWDCUpsample_InitWithResamplePosArray(convolutionN, sampleFrom,
                sampleTotalFrom, sampleRateFrom, sampleRateTo, sampleTotalTo,
                resamplePosArray, fractionArray);
        }

        /// <summary>
        /// サンプルデータの一部分を処理する。
        /// </summary>
        /// <param name="startPos">処理開始位置 0～sampleTotalTo-1</param>
        /// <param name="count">処理するサンプル数</param>
        /// <returns>HRESULT</returns>
        public int ProcessPortion(
                int startPos,
                int count) {
            return WWDCUpsample_Dispatch(startPos, count);
        }

        /// <summary>
        /// 処理結果をGPUメモリからCPUメモリに持ってくる。
        /// </summary>
        /// <returns>HRESULT</returns>
        public int GetResultFromGpuMemory(
                ref float[] outputTo,
                int outputToElemNum) {
            return WWDCUpsample_GetResultFromGpuMemory(
                outputTo, outputToElemNum);
        }

        public void Term() {
            WWDCUpsample_Term();
        }

    }
}
