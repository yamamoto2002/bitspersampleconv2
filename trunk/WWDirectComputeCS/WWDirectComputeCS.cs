using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace WWDirectComputeCS {
    public class WWDirectComputeCS {
        /*
        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCIO_Init(int precision, int convolutionN);

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static void
        WWDCIO_Term();

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCIO_JitterAddGpu(
            int precision,
            int sampleTotal,
            int convolutionN,
            float [] sampleData,
            float [] jitterX,
            [In, Out] float[] outF);

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCIO_JitterAddGpuPortion(
            int precision,
            int sampleTotal,
            int convolutionN,
            float[] sampleData,
            float[] jitterX,
            [In, Out] float[] outF,
            int offs,
            int sampleToProcess);

        /////////////////////////////////////////////////////////////////////

        public enum GpuPrecisionType {
            PFloat,
            PDouble,
        };

        public int Init(GpuPrecisionType precision, int convolutionN) {
            return WWDCIO_Init((int)precision, convolutionN);
        }

        public void Term() {
            WWDCIO_Term();
        }

        public int JitterAdd(
                GpuPrecisionType precision,
                int sampleTotal,
                int convolutionN,
                float[] sampleData,
                float[] jitterX,
                ref float[] outF) {
            return WWDCIO_JitterAddGpu(
                (int)precision,
                sampleTotal,
                convolutionN,
                sampleData,
                jitterX,
                outF);
        }

        public int JitterAddPortion(
                GpuPrecisionType precision,
                int sampleTotal,
                int convolutionN,
                float[] sampleData,
                float[] jitterX,
                ref float[] outF,
                int offs,
                int sampleToProcess) {
            return WWDCIO_JitterAddGpuPortion(
                (int)precision,
                sampleTotal,
                convolutionN,
                sampleData,
                jitterX,
                outF,
                offs,
                sampleToProcess);
        }
        */
    }
}
