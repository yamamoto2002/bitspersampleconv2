﻿using System;

namespace BpsConvWin {
    class SigmaDelta {
        private int mQuantizedBit;
        private uint mMask;
        private double mDelayX;
        private double mDelayY;
        private double mQuantizationError;

        /// <summary>
        /// SigmaDelta noise shaping system
        /// </summary>
        /// <param name="quantizedBit">quantizer parameter</param>
        public SigmaDelta(int quantizedBit) {
            mQuantizedBit = quantizedBit;

            mMask = 0xffffff00U << (24 - mQuantizedBit);
        }

        public double QuantizationError() {
            return mQuantizationError / 256;
        }

        /// <summary>
        /// input sampleFrom, returns quantized sample value
        /// </summary>
        /// <param name="sampleFrom">input data. 24bit signed (-2^23 to +2^23-1)</param>
        /// <returns>filtered value. 24bit signed</returns>
        public int Filter24(double sampleFrom) {
            // convert quantized bit rate to 32bit
            sampleFrom *= 256;

            mDelayX += sampleFrom - mDelayY;
            if (mDelayX > Int32.MaxValue) {
                mDelayX = Int32.MaxValue;
            }
            if (mDelayX < Int32.MinValue) {
                mDelayX = Int32.MinValue;
            }

            int sampleY = (int)(((int)mDelayX) & mMask);
            mDelayY = sampleY;

            mQuantizationError = mDelayX - sampleY;

            return sampleY / 256;
        }
    }

    class NoiseShaperMash {
        private SigmaDelta [] mSds;
        private double mDelayY1;
        private double mDelayY2;

        public NoiseShaperMash(int quantizedBit) {
            mSds = new SigmaDelta[2];
            for (int i=0; i < 2; ++i) {
                mSds[i] = new SigmaDelta(quantizedBit);
            }
        }

        public int Filter24(int sampleFrom) {
            double y1 = mSds[0].Filter24(sampleFrom);
            double y2 = mSds[1].Filter24(mSds[0].QuantizationError());

            double r = mDelayY1 - (y2 - mDelayY2);

            mDelayY1 = y1;
            mDelayY2 = y2;

            r *= 256;
            if (r > Int32.MaxValue) {
                r = Int32.MaxValue;
            }
            if (r < Int32.MinValue) {
                r = Int32.MinValue;
            }

            r /= 256;
            return (int)r;
        }
    }
}
