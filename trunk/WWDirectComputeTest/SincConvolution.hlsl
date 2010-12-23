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

struct BufTypeD
{
    double d;
};

StructuredBuffer<BufTypeD>   SampleDataBuffer : register(t0);
StructuredBuffer<BufTypeD>   SinxBuffer       : register(t1);
StructuredBuffer<BufTypeD>   XBuffer          : register(t2);
RWStructuredBuffer<BufTypeD> OutputBuffer     : register(u0);

inline double Sinc(double sinx, float x)
{
    if (-0.000000001 < x && x < 0.000000001) {
        return 1.0;
    } else {
        float  odxf = 1.0f / x;
        double odxd = odxf;

        return sinx * odxd;
    }
}

[numthreads(1, 1, 1)]
void CSMain(uint3 DTid : SV_DispatchThreadID)
{
    double r = 0.0;
    double sinx = SinxBuffer[DTid.x].d;
    double offs = XBuffer[DTid.x].d;
    int i;

    for (i=CONV_START; i<CONV_END; ++i) {
        double x = 3.141592653589793238 * i + offs;
        r += SampleDataBuffer[DTid.x+i+CONV_N].d * Sinc(sinx, x);
    }
    OutputBuffer[DTid.x].d = r;
}
