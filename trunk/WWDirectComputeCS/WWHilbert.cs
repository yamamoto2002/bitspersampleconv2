using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WWDirectComputeCS {
    public class WWHilbert {

        public static double[] HilbertFirCoeff(int stageN) {
            System.Diagnostics.Debug.Assert(0 < stageN && ((stageN & 1) == 1));
            double [] rv = new double[stageN];

            for (int i=0; i < stageN; ++i) {
                int m = i - stageN / 2;
                if ((m & 1) == 0) {
                    rv[i] = 0;
                } else {
                    rv[i] = 2.0 / m / Math.PI;
                }
            }

            return rv;
        }
    }
}
