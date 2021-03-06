﻿// 日本語

using System;
using System.Collections.Generic;
using System.Globalization;

namespace WWAudioFilterCore {
    public class AWeightingFilter : FilterBase {

        private List<BiquadFilter2T> mF = new List<BiquadFilter2T>();
        private double mScale = 0.0;

        public int Fs { get; set; }

        public AWeightingFilter(int fs) : base(FilterType.AWeighting) {
            Fs = fs;
            switch (fs) {
            case 44100:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -0.140536082420711,   0.004937597615540}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.884901217428792,   0.886421471816167}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.994138881266328,   0.994147469444531}));
                mScale = 0.255741125204258;
                break;
            case 48000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -0.224558458059779,   0.012606625271546}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.893870494723070,   0.895159769094661}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.994614455993022,   0.994621707014084}));
                mScale = 0.234301792299513;
                break;
            case 88200:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -0.788728610908997,   0.155523205416609}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.941142662727710,   0.941533948268554}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.997067292014450,   0.997069442208482}));
                mScale = 0.111887636688211;
                break;
            case 96000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -0.859073102837477,   0.184501649004703}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.945824527367811,   0.946155603519763}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.997305414020089,   0.997307229218489}));
                mScale = 0.099518989759728;
                break;
            case 176400:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.286304426932267,   0.413644769686387}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.970231862410663,   0.970331142204023}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.998533108261586,   0.998533646204429}));
                mScale = 0.039452153812125;
                break;
            case 192000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.334646603086623,   0.445320388782665}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.972624833530474,   0.972708737212715}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.998652253057543,   0.998652707162998}));
                mScale = 0.034332134245487;
                break;
            case 352800:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.608198872158177,   0.646575903102708}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.985029432568073,   0.985054438455929}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999266419620418,   0.999266554155462}));
                mScale = 0.011983309100398;
                break;
            case 384000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.637144946215471,   0.670060893729714}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.986239289410912,   0.986260409764013}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999326012983279,   0.999326126547903}));
                mScale = 0.010284635034851;
                break;
            case 705600:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.794011100695278,   0.804618957354470}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.992492883038461,   0.992499157985497}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999633176173364,   0.999633209813294}));
                mScale = 0.003325373716723;
                break;
            case 768000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.809952511657206,   0.818982023613557}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.993101200805516,   0.993106499113920}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999662978098091,   0.999663006494032}));
                mScale = 0.002831496518680;
                break;
            case 1411200:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.894283497088503,   0.897077491835462}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.996240956836053,   0.996242528521903}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999816579676314,   0.999816588087067}));
                mScale = 0.000877594516281;
                break;
            case 1536000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.902663946773491,   0.905032523587919}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.996545968956997,   0.996547295822103}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999831481949761,   0.999831489049345}));
                mScale = 0.000744089799555;
                break;
            case 2822400:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.946433895894169,   0.947151227771438}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.998119103931873,   0.998119497222897}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999908287735420,   0.999908289838205}));
                mScale = 0.000225536399645;
                break;
            case 3072000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.950732535822303,   0.951339356578928}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.998271824044742,   0.998272156047678}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999915739199947,   0.999915740974918}));
                mScale = 0.000190805875717;
                break;
            case 5644800:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.973036406112429,   0.973218164961262}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999059207928791,   0.999059306297800}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999954143342008,   0.999954143867716}));
                mScale = 0.000057175014923;
                break;
            case 6144000:
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,   2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.975213622666480,   0.975367213791810}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999135621591545,   0.999135704628153}));
                mF.Add(new BiquadFilter2T(new double[] {1.000000000000000,  -2.000000000000000,   1.000000000000000,   1.000000000000000,  -1.999957869156226,   0.999957869599978}));
                mScale = 0.000048316365005;
                break;
            default:
                throw new ArgumentOutOfRangeException("fs");
            }
        }

        public override FilterBase CreateCopy() {
            return new AWeightingFilter(Fs);
        }

        public override string ToDescriptionText() {
            return string.Format(CultureInfo.CurrentCulture, Properties.Resources.FilterAWeightingDesc, Fs);
        }

        public override string ToSaveText() {
            return string.Format(CultureInfo.InvariantCulture, "{0}", Fs);
        }

        public static FilterBase Restore(string[] tokens) {
            if (tokens.Length != 2) {
                return null;
            }

            int fs;
            if (!Int32.TryParse(tokens[1], out fs)) {
                return null;
            }

            return new AWeightingFilter(fs);
        }

        // ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

        public override void FilterStart() {
            foreach (var f in mF) {
                f.Reset();
            }
        }

        public override void FilterEnd() {
        }

        public override WWUtil.LargeArray<double> FilterDo(WWUtil.LargeArray<double> inPcmLA) {
            var y = new WWUtil.LargeArray<double>(inPcmLA.LongLength);

            for (long pos = 0; pos < inPcmLA.LongLength; ++pos) {
                double x = inPcmLA.At(pos);

                x = mScale * x;

                foreach (var f in mF) {
                    x = f.Filter(x);
                }

                y.Set(pos, x);
            }

            return y;
        }
    }
}
