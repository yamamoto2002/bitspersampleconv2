// 日本語UTF-8

/*

OutputBuffer[t+convolutionN] = Σ[sample[t+x] * sinc(πx + XBuffer[t])]
x=CONV_START～CONV_END
を計算する

convolutionN = 256
sampleN = 100
の場合

CONV_START = -256
CONV_END   =  256
CONV_N     =  256
SAMPLE_N   =  100
DTid.xは0～99まで変化。

OutputBuffer[]はsampleN個用意する

用意するデータ

①SampleDataBuffer…前後を水増しされたサンプルデータsample[t]
SampleDataBuffer[0]～SampleDataBuffer[convolutionN-1]…0を詰める
SampleDataBuffer[convolutionN]～SampleDataBuffer[convolutionN + sampleN-1]…サンプルデータsample[t]
SampleDataBuffer[convolutionN+SampleN]～SampleDataBuffer[convolutionN*2 + sampleN-1]…0を詰める

②SinxBuffer リサンプル地点のsin(x) 適当に作る
SinxBuffer[0]～SinxBuffer[sampleN-1] sin(x)の値

③XBuffer リサンプル地点x
XBuffer[0]～XBuffer[sampleN-1] xの値

④出力バッファー
OutputBuffer[0]～OutputBuffer[sampleN-1]

*/

struct BufTypeF
{
    float f;
};

StructuredBuffer<BufTypeF>   SampleDataBuffer : register(t0);
StructuredBuffer<BufTypeF>   SinxBuffer       : register(t1);
StructuredBuffer<BufTypeF>   XBuffer          : register(t2);
RWStructuredBuffer<BufTypeF> OutputBuffer     : register(u0);
RWStructuredBuffer<BufTypeF> ScratchBuffer    : register(u1);

#ifdef HIGH_PRECISION

// 主にdoubleで計算。

inline double
SincD(float sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0;
    } else {
        return ((double)sinx) * ((double)rcp(x));
    }
}

[numthreads(1, 1, 1)]
void
CSMain(uint3 DTid : SV_DispatchThreadID)
{
    double r = 0.0;
    float pi = 3.141592653589793238462643;
    float sinx = SinxBuffer[DTid.x].f;
    float offs = XBuffer[DTid.x].f;
    int i;

    for (i=CONV_START; i<CONV_END; ++i) {
        float x = mad(pi, i, offs);
        r += ((double)SampleDataBuffer[DTid.x+i+CONV_N].f) * SincD(sinx, x);
    }
    OutputBuffer[DTid.x].f = (float)r;
}

#else

// 主にfloatで計算。

inline float
SincF(float sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0f;
    } else {
        return sinx * rcp(x);
    }
}

[numthreads(1, 1, 1)]
void
CSMain(uint3 DTid : SV_DispatchThreadID)
{
    float r = 0.0f;
    float pi = 3.141592653589793238462643f;
    float sinx = SinxBuffer[DTid.x].f;
    float offs = XBuffer[DTid.x].f;
    int i;

#if 0
    // これは、速くならない。
    for (i=CONV_START; i<CONV_END; i+=4) {
        float x0 = mad(pi, i  , offs);
        float x1 = mad(pi, i+1, offs);
        float x2 = mad(pi, i+2, offs);
        float x3 = mad(pi, i+3, offs);
        float r0 = SampleDataBuffer[DTid.x+i  +CONV_N].f * SincF(sinx, x0);
        float r1 = SampleDataBuffer[DTid.x+i+1+CONV_N].f * SincF(sinx, x1);
        float r2 = SampleDataBuffer[DTid.x+i+2+CONV_N].f * SincF(sinx, x2);
        float r3 = SampleDataBuffer[DTid.x+i+3+CONV_N].f * SincF(sinx, x3);
        r += r0 + r1 + r2 + r3;
    }
#else
    for (i=CONV_START; i<CONV_END; ++i) {
        float x = mad(pi, i  , offs);
        r = mad(SampleDataBuffer[DTid.x+i+CONV_N].f, SincF(sinx, x), r);
    }
#endif
    OutputBuffer[DTid.x].f = r;
}

#endif
