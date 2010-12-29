#include "WWDirectComputeIOIF.h"
#include "WWDirectComputeUser.h"
#include "WWUpsampleGpu.h"
#include "WWUtil.h"
#include <assert.h>

static WWUpsampleGpu g_upsampleGpu;

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_Init(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo)
{
    g_upsampleGpu.Init();
    return g_upsampleGpu.Setup(convolutionN, sampleFrom, sampleTotalFrom,
        sampleRateFrom, sampleRateTo, sampleTotalTo);
}

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_Dispatch(
        int startPos,
        int count)
{
    return g_upsampleGpu.Dispatch(startPos, count);
}

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_GetResultFromGpuMemory(
        float * outputTo,
        int outputToElemNum)
{
    int hr = g_upsampleGpu.GetResultFromGpuMemory(outputTo, outputToElemNum);
    if (hr < 0) {
        return hr;
    }

    // 何倍にスケールしたかわからなくなるので別の関数に分けた。
    //WWUpsampleGpu::LimitSampleData(outputTo, outputToElemNum);

    return hr;
}

extern "C" __declspec(dllexport)
void __stdcall
WWDCUpsample_Term(void)
{
    g_upsampleGpu.Unsetup();
    g_upsampleGpu.Term();
}
// CPU処理
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_UpsampleCpuSetup(
        int convolutionN,
        float * sampleData,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo)
{
    return g_upsampleGpu.UpsampleCpuSetup(convolutionN, sampleData, sampleTotalFrom,
        sampleRateFrom, sampleRateTo, sampleTotalTo);
}

extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_UpsampleCpuDo(
        int startPos,
        int count,
        float * outputTo)
{
    return g_upsampleGpu.UpsampleCpuDo(startPos, count, outputTo);
}

extern "C" __declspec(dllexport)
void __stdcall
WWDCUpsample_UpsampleCpuUnsetup(void)
{
    g_upsampleGpu.UpsampleCpuUnsetup();
}

/// @result サンプルデータのスケーリング(0.5=0.5倍スケール)
extern "C" __declspec(dllexport)
float __stdcall
WWDCUpsample_LimitSampleData(
        float * sampleInOut,
        int sampleElemNum)
{
    return WWUpsampleGpu::LimitSampleData(sampleInOut, sampleElemNum);
}

/////////////////////////////////////////////////////////////////////////////

/// 1スレッドグループに所属するスレッドの数。TGSMを共有する。
/// 2の乗数。
/// この数値を書き換えたらシェーダーも書き換える必要あるかも。
/// 1024にすると、±256サンプルの計算ができない。
#define GROUP_THREAD_COUNT 1024

#define PI_D 3.141592653589793238462643
#define PI_F 3.141592653589793238462643f

/// 物置 BSS: 起動時にゼロ塗される。
struct WWDCIOInfo {
    WWDirectComputeUser *pDCU;
    ID3D11ComputeShader *pCS;
    int precision;
    int convolutionN;
};

static WWDCIOInfo g_DC;

enum WWGpuPrecisionType {
    WWGpuPrecision_Float,
    WWGpuPrecision_Double,
    WWGpuPrecision_NUM
};

/// シェーダーに渡す定数。16バイトの倍数でないといけないらしい。
struct ConstShaderParams {
    unsigned int c_convOffs;
    unsigned int c_dispatchCount;
    unsigned int c_reserved1;
    unsigned int c_reserved2;
};

static double
ModuloD(double left, double right)
{
    if (right < 0) {
        right = -right;
    }

    if (0 < left) {
        while (0 <= left - right) {
            left -= right;
        }
        return left;
    }

    if (left < 0) {
        do{
            left += right;
        } while (left < 0);
    }

    return left;
}



static HRESULT
JitterAddGpu(
        WWGpuPrecisionType precision,
        int sampleTotal,
        int convolutionN,
        float *sampleData,
        float *jitterX,
        float *outF,
        int offs,
        int sampleToProcess)
{
    bool result = true;
    HRESULT             hr    = S_OK;

    ID3D11ShaderResourceView*   pBuf0Srv = NULL;
    ID3D11ShaderResourceView*   pBuf1Srv = NULL;
    ID3D11ShaderResourceView*   pBuf2Srv = NULL;
    ID3D11UnorderedAccessView*  pBufResultUav = NULL;
    ID3D11Buffer * pBufConst = NULL;

    assert(0 < sampleTotal);
    assert(0 < sampleToProcess);
    assert(offs + sampleToProcess <= sampleTotal);
    assert(0 < convolutionN);
    assert(sampleData);
    assert(jitterX);
    assert(outF);

    // データ準備          before         sample     after
    const int fromCount = convolutionN + sampleToProcess + convolutionN;
    float *from = new float[fromCount];
    assert(from);
    ZeroMemory(from, sizeof(float)* fromCount);

    // データの途中から(0<offs)の場合、before領域からサンプル値を詰める。
    int beforeN = convolutionN;
    if (offs < convolutionN) {
        beforeN = offs;
    }

    // fromには、最大count=beforeN + sampleToProcess + convolutionNサンプル詰めることができる。
    // だが、sampleDataのsampleTotalが、sampleTotal - (offs - beforeN) < countの場合、
    // sampleDataが足りない
    int count = beforeN + sampleToProcess + convolutionN;
    if (sampleTotal - (offs - beforeN) < count) {
        count = sampleTotal - (offs - beforeN);
    }
    dprintf("offs=%d fromSZ=%d sampleSZ=%d from[%d to %d] sampleData[%d to %d] count=%d\n",
        offs, convolutionN + sampleToProcess + convolutionN,
        sampleTotal,
        convolutionN-beforeN, convolutionN-beforeN + count,
        offs - beforeN, offs - beforeN + count, count);

    for (int i=0; i<count; ++i) {
        from[i+convolutionN-beforeN] =
            sampleData[i+offs - beforeN];
    }

    void *sinx = NULL;
    int sinxBufferElemBytes = 0;
    if (precision == WWGpuPrecision_Double) {
        // doubleprec

        double *sinxD = new double[sampleToProcess];
        assert(sinxD);
        for (int i=0; i<sampleToProcess; ++i) {
            sinxD[i] = sin(ModuloD(jitterX[i+offs], 2.0 * PI_D));
        }
        sinx = sinxD;

        sinxBufferElemBytes = 8;
    } else {
        // singleprec

        float *sinxF = new float[sampleToProcess];
        assert(sinxF);
        for (int i=0; i<sampleToProcess; ++i) {
            sinxF[i] = (float)sin(ModuloD(jitterX[i+offs], 2.0 * PI_D));
        }
        sinx = sinxF;

        sinxBufferElemBytes = 4;
    }

    // 入力データをGPUメモリーに送る
    HRG(g_DC.pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), fromCount, from, "SampleDataBuffer", &pBuf0Srv));
    assert(pBuf0Srv);

    HRG(g_DC.pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sinxBufferElemBytes, sampleToProcess, sinx, "SinxBuffer", &pBuf1Srv));
    assert(pBuf1Srv);

    HRG(g_DC.pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), sampleToProcess, &jitterX[offs], "XBuffer", &pBuf2Srv));
    assert(pBuf1Srv);

    // 結果出力領域をGPUに作成。
    HRG(g_DC.pDCU->CreateBufferAndUnorderedAccessView(
        sizeof(float), sampleToProcess, NULL, "OutputBuffer", &pBufResultUav));
    assert(pBufResultUav);

    // 定数置き場をGPUに作成。
    ConstShaderParams shaderParams;
    ZeroMemory(&shaderParams, sizeof shaderParams);
    HRG(g_DC.pDCU->CreateConstantBuffer(sizeof shaderParams, 1, "ConstShaderParams", &pBufConst));

    // GPU上でComputeShader実行。
    ID3D11ShaderResourceView* aRViews[] = { pBuf0Srv, pBuf1Srv, pBuf2Srv };
    DWORD t0 = GetTickCount();
#if 1
    // すこしだけ速い。中でループするようにした。
    shaderParams.c_convOffs = 0;
    shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
    HRGR(g_DC.pDCU->Run(g_DC.pCS, sizeof aRViews/sizeof aRViews[0], aRViews, pBufResultUav,
        pBufConst, &shaderParams, sizeof shaderParams, sampleToProcess, 1, 1));
#else
    // 遅い。こちらに切り替えるにはシェーダーも書き換える必要あり。
    for (int i=0; i<convolutionN*2/GROUP_THREAD_COUNT; ++i) {
        shaderParams.c_convOffs = i * GROUP_THREAD_COUNT;
        shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
        HRGR(g_DC.pDCU->Run(g_DC.pCS, sizeof aRViews/sizeof aRViews[0], aRViews, pBufResultUav,
            pBufConst, &shaderParams, sizeof shaderParams, sampleToProcess, 1, 1));
    }
#endif

    // 計算結果をCPUメモリーに持ってくる。
    HRG(g_DC.pDCU->RecvResultToCpuMemory(pBufResultUav, &outF[offs], sampleToProcess * sizeof(float)));
end:

    DWORD t1 = GetTickCount();
    dprintf("RunGpu=%dms ###################################\n", t1-t0);

    if (g_DC.pDCU) {
        if (hr == DXGI_ERROR_DEVICE_REMOVED) {
            dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
                g_DC.pDCU->GetDevice()->GetDeviceRemovedReason());
        }

        g_DC.pDCU->DestroyConstantBuffer(pBufConst);
        pBufConst = NULL;

        g_DC.pDCU->DestroyDataAndUnorderedAccessView(pBufResultUav);
        pBufResultUav = NULL;

        g_DC.pDCU->DestroyDataAndShaderResourceView(pBuf2Srv);
        pBuf2Srv = NULL;

        g_DC.pDCU->DestroyDataAndShaderResourceView(pBuf1Srv);
        pBuf1Srv = NULL;

        g_DC.pDCU->DestroyDataAndShaderResourceView(pBuf0Srv);
        pBuf0Srv = NULL;
    }

    delete[] sinx;
    sinx = NULL;

    delete[] from;
    from = NULL;

    return hr;
}

static float
SincF(float sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0f;
    } else {
        return sinx / x;
    }
}

static double
SincD(double sinx, double x)
{
    if (-0.000000001 < x && x < 0.000000001) {
        return 1.0;
    } else {
        return sinx / x;
    }
}

static void
JitterAddCpuF(int sampleToProcess, int convolutionN, float *sampleData, float *jitterX, float *outF)
{
    const int fromCount = convolutionN + sampleToProcess + convolutionN;
    float *from = new float[fromCount];
    assert(from);

    ZeroMemory(from, sizeof(float) * fromCount);
    for (int i=0; i<sampleToProcess; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    for (int pos=0; pos<sampleToProcess; ++pos) {
        float xOffs = jitterX[pos];
        float sinx  = sinf(xOffs);
        float r = 0.0f;

        for (int i=-convolutionN; i<convolutionN; ++i) {
            float x = PI_F * i + xOffs;
            int posS = pos + i + convolutionN;
            float sinc =  SincF(sinx, x);

            r += from[posS] * sinc;
        }

        outF[pos] = r;
    }

    delete[] from;
    from = NULL;
}

static void
JitterAddCpuD(int sampleToProcess, int convolutionN, float *sampleData, float *jitterX, float *outF)
{
    const int fromCount = convolutionN + sampleToProcess + convolutionN;
    float *from = new float[fromCount];
    assert(from);

    ZeroMemory(from, sizeof(float) * fromCount);
    for (int i=0; i<sampleToProcess; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    for (int pos=0; pos<sampleToProcess; ++pos) {
        float xOffs = jitterX[pos];
        double sinx  = sin((double)xOffs);
        double r = 0.0f;

        for (int i=-convolutionN; i<convolutionN; ++i) {
            double x = PI_D * i + xOffs;
            int posS = pos + i + convolutionN;
            double sinc =  SincD(sinx, x);

            r += from[posS] * sinc;
        }

        outF[pos] = (float)r;
    }

    delete[] from;
    from = NULL;
}

/*
class WWDCIOInfo {
    WWDirectComputeUser dcu;
    float *outputBuffer;
};
*/

/////////////////////////////////////////////////////////////////////////////

extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_Init(int precision,
        int convolutionN)
{
    HRESULT hr = S_OK;

    assert(NULL == g_DC.pDCU);
    g_DC.precision = precision;
    g_DC.convolutionN = convolutionN;

    // HLSLの#defineを作る。
    char convStartStr[32];
    char convEndStr[32];
    char convCountStr[32];
    char iterateNStr[32];
    char groupThreadCountStr[32];
    sprintf_s(convStartStr, "%d", -convolutionN);
    sprintf_s(convEndStr,   "%d", convolutionN);
    sprintf_s(convCountStr, "%d", convolutionN*2);
    sprintf_s(iterateNStr,  "%d", convolutionN*2/GROUP_THREAD_COUNT);
    sprintf_s(groupThreadCountStr, "%d", GROUP_THREAD_COUNT);

    const D3D_SHADER_MACRO *defines = NULL;
    if (precision == WWGpuPrecision_Double) {
        // doubleprec

        const D3D_SHADER_MACRO definesD[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            "HIGH_PRECISION", "1",
            NULL, NULL
        };
        defines = definesD;
    } else {
        // singleprec

        const D3D_SHADER_MACRO definesF[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            // "HIGH_PRECISION", "1",
            NULL, NULL
        };
        defines = definesF;
    }

    g_DC.pDCU = new WWDirectComputeUser();
    assert(g_DC.pDCU);

    HRG(g_DC.pDCU->Init());

    // HLSL ComputeShaderをコンパイルしてGPUに送る。
    HRG(g_DC.pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", defines, &g_DC.pCS));
    assert(g_DC.pCS);

end:
    return hr;
}

extern "C" __declspec(dllexport)
void __stdcall
WWDCIO_Term(void)
{
    if (g_DC.pCS) {
        g_DC.pDCU->DestroyComputeShader(g_DC.pCS);
        g_DC.pCS = NULL;
    }

    g_DC.pDCU->Term();
    SAFE_DELETE(g_DC.pDCU);
}

extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_JitterAddGpu(
        int precision,
        int sampleTotal,
        int convolutionN,
        float *sampleData,
        float *jitterX,
        float *outF)
{
    assert(0 <= precision && precision < WWGpuPrecision_NUM);
    assert(0 < sampleTotal);
    assert(65536 <= convolutionN);
    assert(sampleData);
    assert(jitterX);
    assert(outF);

    assert(g_DC.precision == precision);
    assert(g_DC.convolutionN == convolutionN);

    HRESULT hr = S_OK;

    int sampleRemain = sampleTotal;
    int offs = 0;
    while (0 < sampleRemain) {
        int sampleToProcess = 32768;
        if (sampleRemain < sampleToProcess) {
            sampleToProcess = sampleRemain;
        }

        hr = JitterAddGpu(
            (WWGpuPrecisionType)precision,
            sampleTotal,
            convolutionN,
            &sampleData[0],
            &jitterX[0],
            &outF[0],
            offs,
            sampleToProcess);
        if (FAILED(hr)) {
            return (int)hr;
        }

        sampleRemain -= sampleToProcess;
        offs         += sampleToProcess;
    }

    return hr;
}

extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_JitterAddGpuPortion(
        int precision,
        int sampleTotal,
        int convolutionN,
        float *sampleData,
        float *jitterX,
        float *outF,
        int offs,
        int sampleToProcess)
{
    assert(0 <= precision && precision < WWGpuPrecision_NUM);
    assert(0 < sampleToProcess && sampleToProcess <= 32768);
    assert(offs + sampleToProcess <= sampleTotal);
    assert(65536 <= convolutionN);
    assert(sampleData);
    assert(jitterX);
    assert(outF);

    return JitterAddGpu(
        (WWGpuPrecisionType)precision,
        sampleTotal,
        convolutionN,
        &sampleData[0],
        &jitterX[0],
        &outF[0],
        offs,
        sampleToProcess);
}
