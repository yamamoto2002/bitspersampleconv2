// 日本語UTF-8

/*

for (int toPos=0; toPos<sampleTotalTo; ++toPos) {
        int    fromPos  = resamplePosArray[toPos];
        double fraction = fractionArray[toPos];
        double sinPreCompute = sinPreComputeArray[toPos];

        double v = 0.0;

        for (int convOffs=CONV_START; convOffs < CONV_END; ++convOffs) {
            int pos = convOffs + fromPos;
            if (0 <= pos && pos < sampleTotalFrom) {
                double x = PI_D * (convOffs - fraction);
                
                double sinX = sinPreCompute;
                if (convOffs & 1) {
                    sinX *= -1.0;
                }

                double sinc =  SincD(sinX, x);

                v += sampleData[pos] * sinc;
            }
        }
        outputTo[toPos] = (float)v;
    }

を計算する

"CONV_START"   = -convolutionN
"CONV_END"     = convolutionN
"CONV_COUNT"   = convolutionN*2
"SAMPLE_TOTAL_FROM" = sampleTotalFrom
"SAMPLE_TOTAL_TO"   = sampleTotalTo

"SAMPLE_RATE_FROM"   = sampleRateFrom
"SAMPLE_RATE_TO"     = sampleRateTo
"ITERATE_N"          = convolutionN*2/GROUP_THREAD_COUNT
"GROUP_THREAD_COUNT" = 1024

を#defineしてCS5.0 DirectCompute シェーダーとしてコンパイルする。

// シェーダー定数を渡す
shaderParams.c_convOffs = 0
shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
ComputeShaderのrun(shaderParams, sampleN, 1, 1);

する。

用意するデータ

①SampleFromBuffer…サンプルデータsampleFrom[]
float
SAMPLE_TOTAL_FROM要素

②ResamplePosBuffer リサンプル地点配列
uint
SAMPLE_TOTAL_TO要素

③FractionBuffer リサンプル地点の小数点以下
float
SAMPLE_TOTAL_TO要素

④SinPreComputeBuffer sin(-fractionBuffer * π)
float
SAMPLE_TOTAL_TO要素

⑤出力バッファー
OutputBuffer[0]～OutputBuffer[sampleN-1]
OutputBuffer[]はsampleN個用意する

*/

StructuredBuffer<float>   g_SampleFromBuffer    : register(t0);
StructuredBuffer<uint>    g_ResamplePosBuffer   : register(t1);
StructuredBuffer<float>   g_FractionBuffer      : register(t2);
StructuredBuffer<float>   g_SinPreComputeBuffer : register(t3);
RWStructuredBuffer<float> g_OutputBuffer        : register(u0);

/// 定数。16バイトの倍数のサイズの構造体。
cbuffer consts {
    /// 畳み込み要素オフセット値。n * GROUP_THREAD_COUNTの飛び飛びの値が渡る。
    uint c_convOffs;
    /// Dispatch繰り返し回数。
    uint c_dispatchCount;
    uint c_reserved1;
    uint c_reserved2;
};

inline double
SincF(double sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0;
    } else {
        return sinx * rcp(x);
    }
}

#define PI_F 3.141592653589793238462643f
#define PI_D 3.141592653589793238462643

#if 1
[numthreads(1, 1, 1)]
void
CSMain(
        uint  tid:        SV_GroupIndex,
        uint3 groupIdXYZ: SV_GroupID)
{
    uint toPos = groupIdXYZ.x;

    for (int toPos=0; toPos<SAMPLE_TOTAL_TO; ++toPos) {
        int    fromPos       = g_ResamplePosBuffer[toPos];
        float  fraction      = g_FractionBuffer[toPos];
        float  sinPreCompute = g_SinPreComputeBuffer[toPos];

        double v = 0.0;

        for (int convOffs=CONV_START; convOffs < CONV_END; ++convOffs) {
            int pos = convOffs + fromPos;
            if (0 <= pos && pos < SAMPLE_TOTAL_FROM) {
                float x = PI_F * (convOffs - fraction);
                
                double sinX = sinPreCompute;
                if (convOffs & 1) {
                    sinX *= -1.0;
                }

                double sinc =  SincF(sinX, x);

                v += g_SampleFromBuffer[pos] * sinc;
            }
        }
        g_OutputBuffer[toPos] = (float)v;
    }
}
#endif

#if 0

// TGSM
groupshared double s_scratch[GROUP_THREAD_COUNT];
groupshared uint   s_resamplePos;
groupshared float  s_fraction;
groupshared float  s_sinFractionPiMinus1;

/// 畳み込み計算要素1回実行。
/// sample[t+x] * sinc(πx + XBuffer[t])
inline double
ConvolutionElemValue(uint pos, uint convOffs)
{
    const int offs = c_convOffs + convOffs;
    
    const float x = PI_F *(offs + CONV_START, s_xOffs);
    return ((double)g_SampleDataBuffer[offs + pos]) * SincF(s_sinX, x);
}

/* スレッドグループとTGSMを使用して、GPUメモリからの読み出し回数を減らす最適化。
 * 1個の出力サンプルを計算するためには、
 * ・g_ResamplePosBuffer   1回読み出し。
 * ・g_FractionBuffer      1回読み出し。
 * ・g_SinPreComputeBuffer 1回読み出し
 * で良いので、TGSMに蓄える。
 * 各スレッドは、自分の担当convolution位置の計算を行ってs_scratchに入れる。
 */

// groupIdXYZはDispatch()のパラメータXYZ=(nx,1,1)の場合(0,0,0)～(nx-1, 0, 0)。
// スレッドグループが作られ、tid==0～groupDim_x-1までのtidを持ったスレッドが同時に走る。
[numthreads(GROUP_THREAD_COUNT, 1, 1)]
void
CSMain(
        uint  tid:        SV_GroupIndex,
        uint3 groupIdXYZ: SV_GroupID)
{
    uint offs = tid;

    if (tid == 0) {
        s_resamplePos = g_ResamplePosBuffer[groupIdXYZ.x];
        s_fraction    = g_FractionBuffer[groupIdXYZ.x];
        s_sinFractionPiMinus1 = g_SinPreComputeBuffer[groupIdXYZ.x];
    }
    s_scratch[tid] = 0;

    GroupMemoryBarrierWithGroupSync();

    do {
        s_scratch[tid] +=
            ConvolutionElemValue(groupIdXYZ.x, offs) +
            ConvolutionElemValue(groupIdXYZ.x, offs + GROUP_THREAD_COUNT);
        offs += GROUP_THREAD_COUNT * 2;
    } while (offs < CONV_COUNT);

    GroupMemoryBarrierWithGroupSync();

#if 1024 <= GROUP_THREAD_COUNT
    if (tid < 512) { s_scratch[tid] += s_scratch[tid + 512]; }
    GroupMemoryBarrierWithGroupSync();
#endif

#if 512 <= GROUP_THREAD_COUNT
    if (tid < 256) { s_scratch[tid] += s_scratch[tid + 256]; }
    GroupMemoryBarrierWithGroupSync();
#endif

#if 256 <= GROUP_THREAD_COUNT
    if (tid < 128) { s_scratch[tid] += s_scratch[tid + 128]; }
    GroupMemoryBarrierWithGroupSync();
#endif

#if 128 <= GROUP_THREAD_COUNT
    if (tid < 64) { s_scratch[tid] += s_scratch[tid + 64]; }
    GroupMemoryBarrierWithGroupSync();
#endif

#if 64 <= GROUP_THREAD_COUNT
    if (tid < 32) { s_scratch[tid] += s_scratch[tid + 32]; }
    //GroupMemoryBarrierWithGroupSync(); // これ以降要らないらしい。2260_GTC2010.pdf参照。
#endif

#if 32 <= GROUP_THREAD_COUNT
    if (tid < 16) { s_scratch[tid] += s_scratch[tid + 16]; }
    //GroupMemoryBarrierWithGroupSync();
#endif

#if 16 <= GROUP_THREAD_COUNT
    if (tid < 8) { s_scratch[tid] += s_scratch[tid + 8]; }
    //GroupMemoryBarrierWithGroupSync();
#endif

#if 8 <= GROUP_THREAD_COUNT
    if (tid < 4) { s_scratch[tid] += s_scratch[tid + 4]; }
   // GroupMemoryBarrierWithGroupSync();
#endif

#if 4 <= GROUP_THREAD_COUNT
    if (tid < 2) { s_scratch[tid] += s_scratch[tid + 2]; }
    //GroupMemoryBarrierWithGroupSync();
#endif

    if (tid == 0) {
        s_scratch[0] += s_scratch[1];
        g_OutputBuffer[groupIdXYZ.x] = (float)s_scratch[0];
    }
}

#endif
