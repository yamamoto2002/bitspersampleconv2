using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WWDirectComputeCS {
    /// <summary>
    /// 窓関数置き場。
    /// </summary>
    public class WWWindowFunc {
        /// <summary>
        /// ブラックマン窓
        /// </summary>
        /// <param name="window">[out]窓Wk 左右対称の形状が出てくる。</param>
        /// <param name="n">窓の長さn(nは奇数) 要素番号(n-1)/2が山のピーク</param>
        public static void BlackmanWindow(int n, out double [] window) {
            window = new double[n];
            // nは奇数
            System.Diagnostics.Debug.Assert((n & 1) == 1);

            for (int i=0; i < n; ++i) {
                int m = n + 1;
                int pos = i + 1;
                double v = 0.42 - 0.5 * Math.Cos(2.0 * Math.PI * pos / m) + 0.08 * Math.Cos(4.0 * Math.PI * pos / m);
                window[i] = v;
            }
        }
    }
}
