using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WWDirectComputeCS {
    /// <summary>
    /// CPUでFIRする。
    /// </summary>
    public class WWFirCpu {
        private int m_stageN = 0;
        private double [] m_sampleFrom;
        private double [] m_coeff;

        public int Setup(
                double [] coeff,
                double [] sampleFrom) {
            m_stageN     = coeff.Length;
            m_coeff      = coeff;
            m_sampleFrom = sampleFrom;
            return 0;
        }

        /// <summary>
        /// 直接型構成FIR。output[0]～output[count-1]に書き込む。
        /// </summary>
        /// <param name="startPos">sampleFrom[startPos]から計算。</param>
        /// <param name="count">output配列の要素数。</param>
        /// <param name="output">[out]書き出し先。</param>
        /// <returns></returns>
        public int Do(
                int startPos,
                int count,
                double [] output) {
            System.Diagnostics.Debug.Assert(0 < m_stageN);

            int hr = 0;

            Parallel.For(startPos, startPos + count, delegate(int pos) {
                double v = 0.0;

                int sampleLength = m_sampleFrom.Length;

                for (int convOffs = 0; convOffs < m_stageN; ++convOffs) {
                    int fromPos = pos + convOffs;
                    if (0 <= fromPos && fromPos < sampleLength) {
                        v += m_sampleFrom[fromPos] * m_coeff[convOffs];
                    }
                }
                output[pos - startPos] = v;
            });

            return hr;
        }

        public void Unsetup() {
            m_sampleFrom = null;
            m_coeff      = null;
            m_stageN     = 0;
        }
    }
}
