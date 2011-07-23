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

using System;

namespace WWDirectComputeCS {
    public class WWHilbert {

        public static double[] HilbertFirCoeff(int filterLength) {
            System.Diagnostics.Debug.Assert(0 < filterLength && ((filterLength & 1) == 1));

            double [] rv = new double[filterLength];

            for (int i=0; i < filterLength; ++i) {
                int m = i - filterLength / 2;
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
