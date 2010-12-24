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

StructuredBuffer<float>   g_SampleDataBuffer : register(t0);
StructuredBuffer<float>   g_SinxBuffer       : register(t1);
StructuredBuffer<float>   g_XBuffer          : register(t2);
RWStructuredBuffer<float> g_OutputBuffer     : register(u0);

/// 定数。16バイトの倍数のサイズの構造体。
cbuffer consts {
    /// 畳み込み要素オフセット値。n * GROUP_THREAD_COUNTの飛び飛びの値が渡る。
    uint c_convOffs;
    uint c_reserved0;
    uint c_reserved1;
    uint c_reserved2;
};

inline float
SincF(float sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0f;
    } else {
        // return sinx * rcp(x);
        return sinx / x;
    }
}

#define PI_F 3.141592653589793238462643f

groupshared float s_scratch[GROUP_THREAD_COUNT];
groupshared float s_sinx;
groupshared float s_xOffs;
groupshared float s_acc;

inline float
ConvolutionElemValue(int pos, int tid)
{
    int tmpi = c_convOffs + tid;
    float x = mad(PI_F, tmpi + CONV_START, s_xOffs);
    return g_SampleDataBuffer[tmpi + pos] * SincF(s_sinx, x);
}
#if 1

// groupIdXYZはDispatch()のパラメータXYZ=(nx,1,1)の場合(0,0,0)～(nx-1, 0, 0)。
// スレッドグループが作られ、tid==0～groupDim_x-1までのtidを持ったスレッドが同時に走る。
[numthreads(GROUP_THREAD_COUNT, 1, 1)]
void
CSMain(
        uint  tid:        SV_GroupIndex,
        uint3 groupIdXYZ: SV_GroupID)
{
    if (tid == 0) {
        s_sinx  = g_SinxBuffer[groupIdXYZ.x];
        s_xOffs = g_XBuffer[groupIdXYZ.x];
        s_acc   = g_OutputBuffer[groupIdXYZ.x];
    }
    GroupMemoryBarrierWithGroupSync();

    s_scratch[tid] = ConvolutionElemValue(groupIdXYZ.x, tid);

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
    GroupMemoryBarrierWithGroupSync();
#endif

#if 32 <= GROUP_THREAD_COUNT
    if (tid < 16) { s_scratch[tid] += s_scratch[tid + 16]; }
    GroupMemoryBarrierWithGroupSync();
#endif

#if 16 <= GROUP_THREAD_COUNT
    if (tid < 8) { s_scratch[tid] += s_scratch[tid + 8]; }
    GroupMemoryBarrierWithGroupSync();
#endif

#if 8 <= GROUP_THREAD_COUNT
    if (tid < 4) { s_scratch[tid] += s_scratch[tid + 4]; }
    GroupMemoryBarrierWithGroupSync();
#endif

#if 4 <= GROUP_THREAD_COUNT
    if (tid < 2) { s_scratch[tid] += s_scratch[tid + 2]; }
    GroupMemoryBarrierWithGroupSync();
#endif

    if (tid == 0) {
        s_scratch[0] += s_scratch[1];
        g_OutputBuffer[groupIdXYZ.x] = s_acc + s_scratch[0];
    }
}
#endif

#if 0
// オリジナル。
[numthreads(1, 1, 1)]
void
CSMain(uint3 groupIdXYZ  : SV_GroupID,
       uint threadIdx : SV_GroupIndex)
{
    int   i;
    float sinx  = SinxBuffer[c_pos];
    float xOffs = XBuffer[c_pos];
    float r = 0.0f;

    for (i=CONV_START; i<CONV_END; ++i) {
        float x = mad(PI, i, xOffs);
        r = mad(SampleDataBuffer[c_pos+i+CONV_N], SincF(sinx, x), r);
    }

    OutputBuffer[c_pos] = r;
}
#endif
