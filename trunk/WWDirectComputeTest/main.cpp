// 日本語UTF-8
// データをGPUメモリーに送ってGPUで計算。
// 結果をCPUメモリーに戻して確認する。
// DirectX11 ComputeShader 5.0 float-precision supportが必要。

#include "WWDirectComputeUser.h"
#include "WWUtil.h"
#include <assert.h>
#include <crtdbg.h>

static HRESULT
JitterAddGpu(int sampleN, int convolutionN, float *sampleData, float *jitterX, float *result)
{
    HRESULT             hr    = S_OK;
    WWDirectComputeUser *pDCU = NULL;
    ID3D11ComputeShader *pCS  = NULL;

    ID3D11ShaderResourceView*   pBuf0Srv = NULL;
    ID3D11ShaderResourceView*   pBuf1Srv = NULL;
    ID3D11ShaderResourceView*   pBuf2Srv = NULL;
    ID3D11UnorderedAccessView*  pBufResultUav = NULL;

    assert(0 < sampleN);
    assert(0 < convolutionN);
    assert(sampleData);
    assert(jitterX);
    assert(result);

    // データ準備
    float *from = new float[convolutionN + sampleN + convolutionN];
    assert(from);
    ZeroMemory(from, sizeof(float)* (convolutionN + sampleN + convolutionN));
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
    char sampleNStr[32];
    sprintf_s(convStartStr, "%d", -convolutionN);
    sprintf_s(convEndStr,   "%d", convolutionN);
    sprintf_s(sampleNStr,   "%d", sampleN);

    const D3D_SHADER_MACRO defines[] = {
        "CONV_START", convStartStr,
        "CONV_END", convEndStr,
        "CONV_N", convEndStr,
        "SAMPLE_N", sampleNStr,
        "HIGH_PRECISION", "1",
        NULL, NULL
    };

    // HLSL ComputeShaderをコンパイルしてGPUに送る。
    HRG(pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", defines, &pCS));
    assert(pCS);

    // 入力データをGPUメモリーに送る
    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), convolutionN + sampleN + convolutionN, from, "SampleDataBuffer", &pBuf0Srv));
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

    // GPU上でComputeShader実行。
    ID3D11ShaderResourceView* aRViews[] = { pBuf0Srv, pBuf1Srv, pBuf2Srv };

    HRG(pDCU->Run(pCS, sizeof aRViews/sizeof aRViews[0], aRViews,
        NULL, NULL, 0, pBufResultUav, sampleN, 1, 1));

    // 計算結果をCPUメモリーに持ってくる。
    HRG(pDCU->RecvResultToCpuMemory(pBufResultUav, result, sampleN * sizeof(float)));

end:
    if (pDCU) {
        if (hr == DXGI_ERROR_DEVICE_REMOVED) {
            dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
                pDCU->GetDevice()->GetDeviceRemovedReason());
        }

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

int
main(void)
{
#if defined(DEBUG) || defined(_DEBUG)
    _CrtSetDbgFlag( _CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF );
#endif

    HRESULT hr = S_OK;

    // データ準備
    int convolutionN = 16777216;
    int sampleN = 1024;

    float *sampleData = new float[sampleN];
    assert(sampleData);

    float *jitterX = new float[sampleN];
    assert(jitterX);

    float *result = new float[sampleN];
    assert(result);

    for (int i=0; i<sampleN; ++i) {
        sampleData[i] = ((float)(i))/sampleN;
        jitterX[i]    = 0;
    }

    DWORD t0 = GetTickCount();

    HRG(JitterAddGpu(sampleN, convolutionN, sampleData, jitterX, result));

    DWORD t1 = GetTickCount();

    for (int i=0; i<sampleN; ++i) {
        printf("%7d sampleData=%f jitterX=%f result=%f\n", i, sampleData[i], jitterX[i], result[i]);
    }

    if (0 < (t1-t0)) {
        /*
            1 (秒)       x(サンプル/秒)
          ───── ＝ ────────
           14 (秒)       256(サンプル)

             x = 256 ÷ 14
         */

        printf("%dms %fsamples/s\n", (t1-t0),  sampleN / ((t1-t0)/1000.0));
    }

end:
    delete[] result;
    result = NULL;

    delete[] jitterX;
    jitterX = NULL;

    delete[] sampleData;
    sampleData = NULL;

    return 0;
}
