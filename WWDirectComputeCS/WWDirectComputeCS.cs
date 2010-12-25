using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace WWDirectComputeCS {
    public class WWDirectComputeCS {
        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCIO_Init();

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static void
        WWDCIO_Term();

        [DllImport("WWDirectComputeDLL.dll")]
        private extern static int
        WWDCIO_JitterAddGpu(
            int precision,
            int sampleN,
            int convolutionN,
            float [] sampleData,
            float [] jitterX,
            [In, Out] float[] outF);

        /////////////////////////////////////////////////////////////////////

        public int Init() {
            return WWDCIO_Init();
        }

        public void Term() {
            WWDCIO_Term();
        }

        public enum GpuPrecisionType {
            PFloat,
            PDouble,
        };

        public int JitterAdd(
                GpuPrecisionType precision,
                int sampleN,
                int convolutionN,
                float[] sampleData,
                float[] jitterX,
                ref float[] outF) {
            return WWDCIO_JitterAddGpu(
                (int)precision,
                sampleN,
                convolutionN,
                sampleData,
                jitterX,
                outF);
        }

    }
}
