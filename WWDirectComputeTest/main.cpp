// 日本語UTF-8
// データをGPUメモリーに送ってGPUで計算。
// 結果をCPUメモリーに戻して確認する。
// DirectX11 ComputeShader 5.0 float-precision supportが必要。

#include "WWDirectComputeUser.h"
#include "WWUtil.h"
#include <assert.h>
#include <crtdbg.h>

/// 1スレッドグループに所属するスレッドの数。TGSMを共有する。
/// 2の乗数。
/// この数値を書き換えたらシェーダーも書き換える必要あり。
#define GROUP_THREAD_COUNT 1024

/// シェーダーに渡す定数。16バイトの倍数でないといけないらしい。
struct ConstShaderParams {
    unsigned int c_convOffs;
    unsigned int c_dispatchCount;
    unsigned int c_reserved1;
    unsigned int c_reserved2;
};

static HRESULT
JitterAddGpu(int sampleN, int convolutionN, float *sampleData, float *jitterX, float *outF)
{
    bool result = true;
    HRESULT             hr    = S_OK;
    WWDirectComputeUser *pDCU = NULL;
    ID3D11ComputeShader *pCS  = NULL;

    ID3D11ShaderResourceView*   pBuf0Srv = NULL;
    ID3D11ShaderResourceView*   pBuf1Srv = NULL;
    ID3D11ShaderResourceView*   pBuf2Srv = NULL;
    ID3D11UnorderedAccessView*  pBufResultUav = NULL;
    ID3D11Buffer * pBufConst = NULL;

    assert(0 < sampleN);
    assert(0 < convolutionN);
    assert(sampleData);
    assert(jitterX);
    assert(outF);

    // データ準備
    const int fromCount = convolutionN + sampleN + convolutionN;
    float *from = new float[fromCount];
    assert(from);
    ZeroMemory(from, sizeof(float)* fromCount);
    for (int i=0; i<sampleN; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    float *sinx = new float[sampleN];
    assert(sinx);
    for (int i=0; i<sampleN; ++i) {
        sinx[i] = sinf(jitterX[i]);
    }

    pDCU = new WWDirectComputeUser();
    assert(pDCU);

    HRG(pDCU->Init());

    // HLSLの#defineを作る。
    char convStartStr[32];
    char convEndStr[32];
    char convCountStr[32];
    char sampleNStr[32];
    char iterateNStr[32];
    char groupThreadCountStr[32];
    sprintf_s(convStartStr, "%d", -convolutionN);
    sprintf_s(convEndStr,   "%d", convolutionN);
    sprintf_s(convCountStr, "%d", convolutionN*2);
    sprintf_s(sampleNStr,   "%d", sampleN);
    sprintf_s(iterateNStr,  "%d", convolutionN*2/GROUP_THREAD_COUNT);
    sprintf_s(groupThreadCountStr, "%d", GROUP_THREAD_COUNT);

    const D3D_SHADER_MACRO defines[] = {
        "CONV_START", convStartStr,
        "CONV_END", convEndStr,
        "CONV_COUNT", convCountStr,
        "SAMPLE_N", sampleNStr,
        "ITERATE_N", iterateNStr,
        "GROUP_THREAD_COUNT", groupThreadCountStr,
        // "HIGH_PRECISION", "1",
        NULL, NULL
    };

    // HLSL ComputeShaderをコンパイルしてGPUに送る。
    HRG(pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", defines, &pCS));
    assert(pCS);

    // 入力データをGPUメモリーに送る
    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), fromCount, from, "SampleDataBuffer", &pBuf0Srv));
    assert(pBuf0Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), sampleN, sinx, "SinxBuffer", &pBuf1Srv));
    assert(pBuf1Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), sampleN, jitterX, "XBuffer", &pBuf2Srv));
    assert(pBuf1Srv);

    // 結果出力領域をGPUに作成。
    HRG(pDCU->CreateBufferAndUnorderedAccessView(
        sizeof(float), sampleN, NULL, "OutputBuffer", &pBufResultUav));
    assert(pBufResultUav);

    // 定数置き場をGPUに作成。
    ConstShaderParams shaderParams;
    ZeroMemory(&shaderParams, sizeof shaderParams);
    HRG(pDCU->CreateConstantBuffer(sizeof shaderParams, 1, "ConstShaderParams", &pBufConst));

    // GPU上でComputeShader実行。
    ID3D11ShaderResourceView* aRViews[] = { pBuf0Srv, pBuf1Srv, pBuf2Srv };
    DWORD t0 = GetTickCount();
#if 1
    // すこしだけ速い。中でループするようにした。
    shaderParams.c_convOffs = 0;
    shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
    HRGR(pDCU->Run(pCS, sizeof aRViews/sizeof aRViews[0], aRViews, pBufResultUav,
        pBufConst, &shaderParams, sizeof shaderParams, sampleN, 1, 1));
#else
    // 遅い
    for (int i=0; i<convolutionN*2/GROUP_THREAD_COUNT; ++i) {
        shaderParams.c_convOffs = i * GROUP_THREAD_COUNT;
        shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
        HRGR(pDCU->Run(pCS, sizeof aRViews/sizeof aRViews[0], aRViews, pBufResultUav,
            pBufConst, &shaderParams, sizeof shaderParams, sampleN, 1, 1));
    }
#endif

    // 計算結果をCPUメモリーに持ってくる。
    HRG(pDCU->RecvResultToCpuMemory(pBufResultUav, outF, sampleN * sizeof(float)));
end:

    DWORD t1 = GetTickCount();
    printf("RunGpu=%dms ###################################\n", t1-t0);

    if (pDCU) {
        if (hr == DXGI_ERROR_DEVICE_REMOVED) {
            dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
                pDCU->GetDevice()->GetDeviceRemovedReason());
        }

        pDCU->DestroyConstantBuffer(pBufConst);
        pBufConst = NULL;

        pDCU->DestroyDataAndUnorderedAccessView(pBufResultUav);
        pBufResultUav = NULL;

        pDCU->DestroyDataAndShaderResourceView(pBuf2Srv);
        pBuf2Srv = NULL;

        pDCU->DestroyDataAndShaderResourceView(pBuf1Srv);
        pBuf1Srv = NULL;

        pDCU->DestroyDataAndShaderResourceView(pBuf0Srv);
        pBuf0Srv = NULL;

        if (pCS) {
            pDCU->DestroyComputeShader(pCS);
            pCS = NULL;
        }

        pDCU->Term();
    }

    SAFE_DELETE(pDCU);

    delete[] sinx;
    sinx = NULL;

    delete[] from;
    from = NULL;

    return hr;
}

#define PI_D 3.141592653589793238462643
#define PI_F 3.141592653589793238462643f

static float
SincF(float sinx, float x)
{
    if (-0.000000001f < x && x < 0.000000001f) {
        return 1.0f;
    } else {
        return sinx / x;
    }
}

static void
JitterAddCpu(int sampleN, int convolutionN, float *sampleData, float *jitterX, float *outF)
{
    const int fromCount = convolutionN + sampleN + convolutionN;
    float *from = new float[fromCount];
    assert(from);

    ZeroMemory(from, sizeof(float) * fromCount);
    for (int i=0; i<sampleN; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    for (int pos=0; pos<sampleN; ++pos) {
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

int
main(void)
{
#if defined(DEBUG) || defined(_DEBUG)
    _CrtSetDbgFlag( _CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF );
#endif

    HRESULT hr = S_OK;

    // データ準備
    int convolutionN = 65536 * 256;
    int sampleN      = 16;

    float *sampleData = new float[sampleN];
    assert(sampleData);

    float *jitterX = new float[sampleN];
    assert(jitterX);

    float *outputGpu = new float[sampleN];
    assert(outputGpu);

    float *outputCpu = new float[sampleN];
    assert(outputCpu);

#if 0
    for (int i=0; i<sampleN; ++i) {
        sampleData[i] = 1.0f;
        jitterX[i]    = 0.5f;
    }
#else
    // 44100Hzサンプリングで1000Hzのsin
    for (int i=0; i<sampleN; ++i) {
        float xS = PI_F * i * 1000 / 44100;
        float xJ = PI_F * i * 4000 / 44100;
        sampleData[i] = sinf(xS);
        jitterX[i]    = sinf(xJ)*0.5f;
    }
#endif

    DWORD t0 = GetTickCount();

    HRG(JitterAddGpu(sampleN, convolutionN, sampleData, jitterX, outputGpu));

    DWORD t1 = GetTickCount()+1;

    JitterAddCpu(sampleN, convolutionN, sampleData, jitterX, outputCpu);

    DWORD t2 = GetTickCount()+2;

    for (int i=0; i<sampleN; ++i) {
        printf("%7d sampleData=%f jitterX=%f outGpu=%f outCpu=%f diff=%12.8f\n",
            i, sampleData[i], jitterX[i], outputGpu[i], outputCpu[i], fabsf(outputGpu[i]- outputCpu[i]));
    }

    if (0 < (t1-t0)) {
        /*
            1 (秒)       x(サンプル/秒)
          ───── ＝ ────────
           14 (秒)       256(サンプル)

             x = 256 ÷ 14
         */

        printf("GPU=%dms(%fsamples/s) CPU=%dms(%fsamples/s)\n",
            (t1-t0),  sampleN / ((t1-t0)/1000.0),
            (t2-t1),  sampleN / ((t2-t1)/1000.0));
    }

end:
    delete[] outputCpu;
    outputGpu = NULL;

    delete[] outputGpu;
    outputGpu = NULL;

    delete[] jitterX;
    jitterX = NULL;

    delete[] sampleData;
    sampleData = NULL;

    return 0;
}
