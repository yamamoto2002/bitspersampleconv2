﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WWUtil;
using WWDirectComputeCS;
using WWMath;

namespace WWWaveSimulatorCS {
    public class WaveSimFdtd2D {
        WWWave2DGpu mCS;

        /// <summary>
        /// 気圧P スカラー場
        /// </summary>
        float[] mP;

        /// <summary>
        /// 速度V 2要素ベクトル場 (Array of Structure)
        /// </summary>
        float[] mV;

        /// <summary>
        /// 密度ρ
        /// </summary>
        float[] mRoh;

        float[] mLoss;

        /// <summary>
        /// その場所の相対音速cr
        /// (最大音速をc0とすると、その場所の音速ca = cr *c0、0&lt;cR&le;1)
        /// </summary>
        float[] mCr;

        int mGridW; // x
        int mGridH; // y
        int mGridCount; // x * y

        private Delay [] mDelayArray;

        /// <summary>
        /// シミュレーションtick。Δtを掛けると時間になる。
        /// </summary>
        int mTimeTick;

        private float mC0 = 1.0f; // 334.0f;             // 334 (m/s)
        private float mΔt = 1.0f; // 1.0e-5f;            // 1x10^-5 (s)               Δt = Δx * Sc / c0
        //private float mΔx = 1.0f; // 334.0f * 1.0e-5f;   // 334 * 10^-5 (m)         Δx = c0 * Δt / Sc
        private float mSc = 1.0f / (float)Math.Sqrt(2.0); // クーラン数は1.0/sqrt(2)   Sc = c0 * Δt / Δx;

        private List<WaveEvent> mWaveEventList = new List<WaveEvent>();

        public float GetΔt() {
            return mΔt;
        }

        public WaveSimFdtd2D(int gridW, int gridH, float c0, float Δx) {
            int hr = 0;

            mC0 = c0;
            mΔt = Δx * mSc / mC0;

            VisualizeMode = VisualizeModeType.VM_Linear;

            mGridW = gridW;
            mGridH = gridH;
            mGridCount = mGridW * mGridH;

#if false
            mC0 = c0;
            mΔt = Δt;
            mΔx = Δx;

            mSc = mC0 * mΔt / mΔx;
#endif
            // 上下左右 x 2
            mDelayArray = new Delay[(gridW + gridH) * 2 * 2];
            for (int i = 0; i < mDelayArray.Length; ++i) {
                mDelayArray[i] = new Delay(2);
            }

            Reset();

            mCS = new WWWave2DGpu();
            do {
                hr = mCS.Init();
                if (hr < 0) {
                    return;
                }

                WWWave2DParams p;
                p.fieldW = gridW;
                p.fieldH = gridH;
                p.deltaT = mΔt;
                p.sc = mSc;
                p.c0 = mC0;

                hr = mCS.Setup(p, mLoss, mRoh, mCr);
            } while (false);
        }

        public void Term() {
            mCS.Term();
            mCS = null;
        }

        public void Reset() {
            mP = new float[mGridCount];
            mV = new float[mGridCount * 2];

            mRoh = new float[mGridCount];
            mCr = new float[mGridCount];
            mLoss = new float[mGridCount];

            /*
             * 音響インピーダンスη=ρ*Ca (Schneider17, pp.63, pp.325)
             * η1から前進しη2の界面に達した波が界面で反射するとき
             *
             *             η2-η1
             * 反射率 r = ────────
             *             η2+η1
             * 
             * 媒質1のインピーダンスη1と反射率→媒質2のインピーダンスη2を得る式:
             *
             *       -(r+1)η1
             * η2 = ─────────
             *         r-1
             *         
             * Courant number Sc = c0 Δt / Δx
             */

            // デフォルト値セット。
            for (int i = 0; i < mGridCount; ++i) {
                // 相対密度。
                mRoh[i] = 1.0f;

                mLoss[i] = 0.0f;

                // 相対音速。0 < Cr < 1
                mCr[i] = 1.0f;

            }

#if true
            // 全面がCr遅い
            for (int y = 0; y < mGridH; ++y) {
                for (int x = 0; x < mGridW; ++x) {

                    SetRoh(x, y, 1.0f);

                    SetLoss(x, y, 0);

                    SetCr(x, y, 0.8f);
                }
            }
#endif

#if false
            // 上下左右端領域は反射率80％の壁になっている。
            float r = 0.8f; // 0.8 == 80%
            float roh2 = -(r + 1) * 1.0f / (r - 1);
            float loss2 = 0.1f;
            int edge = 3;
            for (int y = edge; y < mGridH-edge; ++y) {
                for (int x = edge; x < mGridW * 1 / 20; ++x) {
                    SetRoh(x, y, roh2);
                    SetLoss(x, y, loss2);
                }
                for (int x = mGridW * 19 / 20; x < mGridW-edge; ++x) {
                    SetRoh(x, y, roh2);
                    SetLoss(x, y, loss2);
                }
            }
            for (int x = edge; x < mGridW-edge; ++x) {
                for (int y = edge; y < mGridH * 1 / 20; ++y) {
                    SetRoh(x, y, roh2);
                    SetLoss(x, y, loss2);
                }
                for (int y = mGridH * 19 / 20; y < mGridH-edge; ++y) {
                    SetRoh(x, y, roh2);
                    SetLoss(x, y, loss2);
                }
            }
#endif

            mWaveEventList.Clear();

            // 2次のABC用の過去データ置き場。
            for (int i = 0; i < mDelayArray.Length; ++i) {
                mDelayArray[i].FillZeroes();
            }

            //{
            //    // ABCの係数。

            //    float Cp = mRoh[0] * mCr[0] * mCr[0] * mC0 * mSc;
            //    float Cv = 2.0f * mSc / ((mRoh[0] + mRoh[0 + 1]) * mC0);

            //    mAbcCoef = new float[3];
            //    float ScPrime = 1.0f; // (float)Math.Sqrt(Cp * Cv);
            //    float denom = 1.0f / ScPrime + 2.0f + ScPrime;
            //    mAbcCoef[0] = -(1.0f / ScPrime - 2.0f + ScPrime) / denom;
            //    mAbcCoef[1] = +(2.0f * ScPrime - 2.0f / ScPrime) / denom;
            //    mAbcCoef[2] = -(4.0f * ScPrime + 4.0f / ScPrime) / denom;
            //}

            mTimeTick = 0;
        }

        public void AddStimulus(WaveEvent.EventType t, int x, int y, float freq, float magnitude) {
            

            int pos = x + y * mGridW;

            var ev = new WaveEvent(t, mSc, pos, freq, magnitude, mΔt);
            mWaveEventList.Add(ev);
        }

        private float mMagnitude = 0;

        public float Magnitude() {
            return mMagnitude;
        }

        public int Update(int nTimes) {
            if (mCS.Available) {
                // GPU実行。

                UpdateGPU(nTimes);

                // Stimuli更新
                for (int i = 0; i < nTimes; ++i) {
                    var toRemove = new List<WaveEvent>();
                    foreach (var v in mWaveEventList) {
                        if (v.UpdateTime()) {
                            toRemove.Add(v);
                        }
                    }
                    if (0 < toRemove.Count) {
                        foreach (var v in toRemove) {
                            mWaveEventList.Remove(v);
                        }
                    }
                }

                mTimeTick += nTimes;
            } else {
                // CPU実行。

                for (int i = 0; i < nTimes; ++i) {
                    UpdateCPU1();
                    ++mTimeTick;
                }
            }

            // mMagnitude計算。
            float pMax = 0.0f;
            for (int i = 1; i < mP.Length; ++i) {
                if (pMax < Math.Abs(mP[i])) {
                    pMax = Math.Abs(mP[i]);
                }
            }

            mMagnitude = pMax;

            return mWaveEventList.Count;
        }
        
        const int N_STIM = 4;

        public void UpdateGPU(int nTimes) {

            WWWave1DStim[] stim = new WWWave1DStim[N_STIM];
            for (int i = 0; i < N_STIM; ++i) {
                stim[i] = new WWWave1DStim();
            }

            int nStim = N_STIM;
            if (mWaveEventList.Count < nStim) {
                nStim = mWaveEventList.Count;
            }

            for (int i = 0; i < nStim; ++i) {
                var w = mWaveEventList[i];
                stim[i].type = (int)w.mType;
                stim[i].counter = w.mTime;
                stim[i].pos = w.mPos;
                stim[i].magnitude = w.mMagnitude;
                stim[i].halfPeriod = w.HalfPeriod;
                stim[i].width = w.GaussianWidth;
                stim[i].omega = (float)(2.0f * Math.PI * w.mFreq);
                stim[i].period = WaveEvent.SINE_PERIOD;
            }

            mCS.Run(nTimes, nStim, stim);
            mCS.GetResultVP(mGridCount, mV, mP);
        }
        
        public int UpdateCPU1() {
            int nStimuli = 0;
            // Stimuli
            var toRemove = new List<WaveEvent>();
            foreach (var v in mWaveEventList) {
                if (v.Update(mP)) {
                    toRemove.Add(v);
                }

                ++nStimuli;
            }
            if (0 < toRemove.Count) {
                foreach (var v in toRemove) {
                    mWaveEventList.Remove(v);
                }
            }

#if false
            // これはうまくいかない。
            // ABC for V (Schneider17, pp.53)
            for (int y = 0; y < mGridH; ++y) {
                SetV(mGridW - 1, y, V(mGridW - 2, y));
            }
            for (int x = 0; x < mGridW; ++x) {
                SetV(x, mGridH - 1, V(x, mGridH - 2));
            }
#endif

            // Update V (Schneider17, pp.328)
#if true
            Parallel.For(0, mGridH - 1, y => {
                for (int x = 0; x < mGridW - 1; ++x) {
                    int pos = x + y * mGridW;
                    float loss = mLoss[pos];
                    float Cv = 2.0f * mSc / ((mRoh[pos] + mRoh[pos + 1]) * mC0);

                    var v = V(x, y);
                    float vx = (1.0f - loss) / (1.0f + loss) * v.X - (Cv / (1.0f + loss)) * (P(x + 1, y) - P(x, y));
                    float vy = (1.0f - loss) / (1.0f + loss) * v.Y - (Cv / (1.0f + loss)) * (P(x, y + 1) - P(x, y));
                    SetV(x, y, new WWVectorF2(vx, vy));
                }
            });
#else
            for (int y=0; y<mGridH-1; ++y) {
                for (int x = 0; x < mGridW - 1; ++x) {
                    pos = x + y * mGridW;
                    float loss = mLoss[pos];
                    float Cv = 2.0f * mSc / ((mRoh[pos] + mRoh[pos + 1]) * mC0);
                    WWVectorF2 v = V(x, y);
                    float vx = (1.0f - loss) / (1.0f + loss) * v.X - (Cv / (1.0f + loss)) * (P(x + 1, y) - P(x, y));
                    float vy = (1.0f - loss) / (1.0f + loss) * v.Y - (Cv / (1.0f + loss)) * (P(x, y + 1) - P(x, y));
                    SetV(x, y, new WWVectorF2(vx, vy));
                }
            }
#endif

            // Update P (Schneider17, pp.325)
#if true
            Parallel.For(1, mGridH, y => {
                for (int x = 1; x < mGridW; ++x) {
                    int pos = x + y * mGridW;
                    float loss = mLoss[pos];
                    var v = V(x, y);
                    float Cp = mRoh[pos] * mCr[pos] * mCr[pos] * mC0 * mSc;
                    mP[pos] = (1.0f - loss) / (1.0f + loss) * mP[pos]
                        - (Cp / (1.0f + loss))
                        * (v.X - V(x - 1, y).X + v.Y - V(x, y - 1).Y);
                }
            });
#else
            for (int y = 1; y < mGridH; ++y) {
                for (int x = 1; x < mGridW; ++x) {
                    pos = x + y * mGridW;
                    float loss = mLoss[pos];
                    var v = V(x, y);
                    float Cp = mRoh[pos] * mCr[pos] * mCr[pos] * mC0 * mSc;
                    mP[pos] = (1.0f - loss) / (1.0f + loss) * mP[pos] - (Cp / (1.0f + loss))
                        * (v.X - V(x - 1, y).X + v.Y - V(x, y - 1).Y);
                }
            }
#endif

#if false
            // 2次のABC

#else
            // 1次のABC pp.148

            // 左端(x==0)、右端(x==mGridW-1)
            for (int y = 0; y < mGridH; ++y) {
                int offs = 0;

                {
                    // 左端 (x==0)
                    int x = 0;
                    int pos = x + y*mGridW;
                    float ScPrime = mSc * mCr[pos];

                    // m:位置、q:時刻
                    //     m q
                    //    p0_0をこれから計算する。
                    float p0_1 = (float)mDelayArray[offs + y * 2 + 0].GetNthDelayedSampleValue(0);

                    float p1_0 = (float)P(1, y);
                    float p1_1 = (float)mDelayArray[offs + y * 2 + 1].GetNthDelayedSampleValue(0);

                    float p0_0 = p1_1 + (ScPrime - 1) / (ScPrime + 1) * (p1_0 - p0_1);
                    SetP(0, y, p0_0);

                    mDelayArray[offs + y * 2 + 0].Filter(p0_0);
                    mDelayArray[offs + y * 2 + 1].Filter(p1_0);
                }

                offs = mGridH*2;
                {
                    // 右端 (x==mGridW-1)
                    int x = mGridW-1;
                    int pos = x + y * mGridW;
                    float ScPrime = mSc * mCr[pos];

                    //     m q
                    //    p0_0をこれから計算する。
                    float p0_1 = (float)mDelayArray[offs + y * 2 + 0].GetNthDelayedSampleValue(0);

                    float p1_0 = (float)P(mGridW-2, y);
                    float p1_1 = (float)mDelayArray[offs + y * 2 + 1].GetNthDelayedSampleValue(0);

                    float p0_0 = p1_1 + (ScPrime - 1) / (ScPrime + 1) * (p1_0 - p0_1);
                    SetP(mGridW-1, y, p0_0);

                    mDelayArray[offs + y * 2 + 0].Filter(p0_0);
                    mDelayArray[offs + y * 2 + 1].Filter(p1_0);
                }
            }

            for (int x = 0; x < mGridW; ++x) {
                int offs = mGridH*4;

                {
                    // 上 (y==0)
                    int y = 0;
                    int pos = x + y * mGridW;
                    float ScPrime = mSc * mCr[pos];

                    //     m q
                    //    p0_0をこれから計算する。
                    float p0_1 = (float)mDelayArray[offs + x * 2 + 0].GetNthDelayedSampleValue(0);

                    float p1_0 = (float)P(x, 1);
                    float p1_1 = (float)mDelayArray[offs + x * 2 + 1].GetNthDelayedSampleValue(0);

                    float p0_0 = p1_1 + (ScPrime - 1) / (ScPrime + 1) * (p1_0 - p0_1);
                    SetP(x, 0, p0_0);

                    mDelayArray[offs + x * 2 + 0].Filter(p0_0);
                    mDelayArray[offs + x * 2 + 1].Filter(p1_0);
                }

                offs = mGridH*4 + mGridW * 2;
                {
                    // 下端 (y==mGridH-1)
                    int y = mGridH-1;
                    int pos = x + y * mGridW;
                    float ScPrime = mSc * mCr[pos];

                    //     m q
                    //    p0_0をこれから計算する。
                    float p0_1 = (float)mDelayArray[offs + x * 2 + 0].GetNthDelayedSampleValue(0);

                    float p1_0 = (float)P(x, mGridH - 2);
                    float p1_1 = (float)mDelayArray[offs + x * 2 + 1].GetNthDelayedSampleValue(0);

                    float p0_0 = p1_1 + (ScPrime - 1) / (ScPrime + 1) * (p1_0 - p0_1);
                    SetP(x, mGridH - 1, p0_0);

                    mDelayArray[offs + x * 2 + 0].Filter(p0_0);
                    mDelayArray[offs + x * 2 + 1].Filter(p1_0);
                }
            }
#endif

#if false
# if true
            {   // Ricker wavelet
                float length = 20.0f;
                var p = (float)Math.PI * ((mSc * mTimeTick) / length - 1.0f);

                p *= p;
                p = (1.0f - 2.0f * p) * (float)Math.Exp(-p);
                SetP(mGridW / 5, mGridH / 2, p);
            }
# else
            {
                // 平面波
                float d = 0.2f * (float)Math.Sin(2.0f * Math.PI * mTimeTick * 0.01f);
                Console.WriteLine("{0}", d);
                for (int y = mGridH / 20; y < mGridH * 19 / 20; ++y) {
                    int x = mGridW / 20;

                    SetP(x, y, d);
                }
            }
# endif
#endif

            return nStimuli;
        }

        private WWVectorF2 V(int x, int y) {
            int pos = x + y * mGridW;
            return new WWVectorF2(mV[pos*2], mV[pos*2+1]);
        }
        private void SetV(int x, int y, WWVectorF2 v) {
            int pos = x + y * mGridW;
            mV[pos * 2 + 0] = v.X;
            mV[pos * 2 + 1] = v.Y;
        }
        private float P(int x, int y) {
            int pos = x + y * mGridW;
            return mP[pos];
        }
        private void SetP(int x, int y, float v) {
            int pos = x + y * mGridW;
            mP[pos] = v;
        }
        private void SetRoh(int x, int y, float v) {
            int pos = x + y * mGridW;
            mRoh[pos] = v;
        }
        private void SetCr(int x, int y, float v) {
            int pos = x + y * mGridW;
            mCr[pos] = v;
        }
        private void SetLoss(int x, int y, float v) {
            int pos = x + y * mGridW;
            mLoss[pos] = v;
        }

        public enum VisualizeModeType {
            VM_Linear,
            VM_Log,
        };

        public VisualizeModeType VisualizeMode { get; set; }

        public float[] Pshow() {
            var p = new float[mGridCount];

            switch (VisualizeMode) {
            case VisualizeModeType.VM_Linear:
                Parallel.For(0, mGridH, y => {
                    for (int x = 0; x < mGridW; ++x) {
                        int pos = x + y * mGridW;
                        p[pos] = Math.Abs(mP[pos]);
                    }
                });
                break;
            case VisualizeModeType.VM_Log:
                Parallel.For(0, mGridH, y => {
                    for (int x = 0; x < mGridW; ++x) {
                        int pos = x + y * mGridW;
                        float v = Math.Abs(mP[pos]);
                        if (v < float.Epsilon) {
                            v = 0;
                        } else {
                            v = (float)Math.Log10(v);
                            if (v < -3.0f) {
                                v = 0;
                            } else {
                                v = v / 3.0f + 1.0f;
                            }
                        }
                        p[pos] = v;
                    }
                });
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            return p;
        }

        public float[] LossShow() {
            var l = new float[mGridCount];
            int pos = 0;

#if true
            float maxL = 1.0f;
#else
            // 最大値を探す。
            float maxL = 0;
            for (int y = 0; y < mGridH; ++y) {
                for (int x = 0; x < mGridW; ++x) {
                    if (maxL < mLoss[pos]) {
                        maxL = mLoss[pos];
                    }

                    ++pos;
                }
            }
            if (maxL < 0.000001f) {
                maxL = 1.0f;
            }
#endif

            pos = 0;
            for (int y = 0; y < mGridH; ++y) {
                for (int x = 0; x < mGridW; ++x) {
                    // 表示用の加工をしてコピーする。
                    l[pos] = mLoss[pos] / maxL;

                    ++pos;
                }
            }

            return l;
        }

        public float[] CrShow() {
            var v = new float[mGridCount];
            int pos = 0;

#if true
            float maxL = 1.0f;
#else
            // 最大値を探す。
            float maxL = 0;
            for (int y = 0; y < mGridH; ++y) {
                for (int x = 0; x < mGridW; ++x) {
                    if (maxL < mLoss[pos]) {
                        maxL = mLoss[pos];
                    }

                    ++pos;
                }
            }
            if (maxL < 0.000001f) {
                maxL = 1.0f;
            }
#endif

            pos = 0;
            for (int y = 0; y < mGridH; ++y) {
                for (int x = 0; x < mGridW; ++x) {
                    // 表示用の加工をしてコピーする。
                    v[pos] = mCr[pos] / maxL;

                    ++pos;
                }
            }

            return v;
        }

        public float ElapsedTime() {
            return mTimeTick * mΔt;
        }
        public int TimeTick() {
            return mTimeTick;
        }
    }
}
