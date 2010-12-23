// 日本語UTF-8
// データをGPUメモリーに送ってGPUで計算。
// 結果をCPUメモリーに戻して確認する。
// DirectX11 ComputeShader 5.0 double-precision supportが必要。

#include "WWDirectComputeUser.h"
#include "WWUtil.h"
#include <assert.h>
#include <crtdbg.h>

int main(void)
{
#if defined(DEBUG) || defined(_DEBUG)
    _CrtSetDbgFlag( _CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF );
#endif

    HRESULT             hr    = S_OK;
    WWDirectComputeUser *pDCU = NULL;
    ID3D11ComputeShader *pCS  = NULL;

    ID3D11ShaderResourceView*   pBuf0Srv = NULL;
    ID3D11ShaderResourceView*   pBuf1Srv = NULL;
    ID3D11ShaderResourceView*   pBuf2Srv = NULL;
    ID3D11UnorderedAccessView*  pBufResultUav = NULL;

    // データ準備
    int convolutionN = 1;
    int sampleN = 16;

    double *from = new double[convolutionN + sampleN + convolutionN];
    assert(from);
    double *sinx = new double[sampleN];
    assert(sinx);
    double *xb = new double[sampleN];
    assert(xb);
    double *to = new double[sampleN];
    assert(to);

    ZeroMemory(from, sizeof(double)* (convolutionN + sampleN + convolutionN));

    for (int i=0; i<sampleN; ++i) {
        from[i+convolutionN] = (double)(i-4);
        xb[i]   = (double)i/sampleN;
        sinx[i] = sin(xb[i]);
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
        NULL, NULL
    };

    HRG(pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", defines, &pCS));
    assert(pCS);

    // 入力データをGPUメモリーに送る
    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(double), convolutionN + sampleN + convolutionN, from, "SampleDataFrom", &pBuf0Srv));
    assert(pBuf0Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(double), sampleN, sinx, "Sinx", &pBuf1Srv));
    assert(pBuf1Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(double), sampleN, xb, "XBuffer", &pBuf2Srv));
    assert(pBuf1Srv);

    // 結果出力領域をGPUに作成。
    HRG(pDCU->CreateBufferAndUnorderedAccessView(
        sizeof(double), sampleN, NULL, "ResultUav", &pBufResultUav));
    assert(pBufResultUav);

    // 実行。
    ID3D11ShaderResourceView* aRViews[] = { pBuf0Srv, pBuf1Srv, pBuf2Srv };
    HRG(pDCU->Run(pCS, sizeof aRViews/sizeof aRViews[0], aRViews,
        NULL, NULL, 0, pBufResultUav, sampleN, 1, 1));

    // 計算結果をCPUメモリーに持ってくる。
    HRG(pDCU->RecvResultToCpuMemory(pBufResultUav, to, sampleN * sizeof(double)));

    for (int i=0; i<sampleN; ++i) {
        printf("%d from=%f sinx=%f xb=%f result=%f\n", i, from[i+convolutionN], sinx[i], xb[i], to[i]);
    }

end:
    if (pDCU) {
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

    delete[] to;
    to = NULL;

    delete[] xb;
    xb = NULL;

    delete[] sinx;
    sinx = NULL;

    delete[] from;
    from = NULL;

    return 0;
}
