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

StructuredBuffer<float>   SampleDataBuffer : register(t0);
StructuredBuffer<float>   SinxBuffer       : register(t1);
StructuredBuffer<float>   XBuffer          : register(t2);
RWStructuredBuffer<float> OutputBuffer     : register(u0);

cbuffer consts {
    uint pos;
    uint reserved0;
    uint reserved1;
    uint reserved2;
};

inline float
SincF(float sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0f;
    } else {
        return sinx * rcp(x);
    }
}

#define PI 3.141592653589793238462643f

/// @param threadIdx numThreadsに指定したパラメータxyz
/// @param groupIdx Dispatchに指定したパラメータxyz
[numthreads(1, 1, 1)]
void
CSMain(uint3 threadIdx : SV_GroupThreadID,
       uint3 groupIdx  : SV_GroupID)
{
    int i;
    float sinx = SinxBuffer[pos];
    float xOffs = XBuffer[pos];
    float r = 0.0f;

    for (i=CONV_START; i<CONV_END; ++i) {
        float x = mad(PI, i, xOffs);
        r = mad(SampleDataBuffer[pos+i+CONV_N], SincF(sinx, x), r);
    }

    OutputBuffer[pos] = r;
}

