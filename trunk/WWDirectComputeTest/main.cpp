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
    ID3D11UnorderedAccessView*  pBufResultUav = NULL;

    // データ準備
    int dataCount = 16;
    double *from = new double[dataCount];
    assert(from);
    double *sinx = new double[dataCount];
    assert(sinx);
    double *to = new double[dataCount];
    assert(to);

    for (int i=0; i<dataCount; ++i) {
        from[i] = (double)(i-4);
        sinx[i] = from[i] / dataCount;
    }

    pDCU = new WWDirectComputeUser();
    assert(pDCU);

    HRG(pDCU->Init());

    HRG(pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", &pCS));
    assert(pCS);

    // 入力データをGPUメモリーに送る
    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(double), dataCount, from, "SampleDataFrom", &pBuf0Srv));
    assert(pBuf0Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(double), dataCount, sinx, "Sinx", &pBuf1Srv));
    assert(pBuf1Srv);

    // 結果出力領域をGPUに作成。
    HRG(pDCU->CreateBufferAndUnorderedAccessView(
        sizeof(double), dataCount, NULL, "ResultUav", &pBufResultUav));
    assert(pBufResultUav);

    // 実行。
    ID3D11ShaderResourceView* aRViews[2] = { pBuf0Srv, pBuf1Srv };
    HRG(pDCU->Run(pCS, sizeof aRViews/sizeof aRViews[0], aRViews,
        NULL, NULL, 0, pBufResultUav, dataCount, 1, 1));

    // 計算結果をCPUメモリーに持ってくる。
    HRG(pDCU->RecvResultToCpuMemory(pBufResultUav, to, dataCount * sizeof(double)));

    for (int i=0; i<dataCount; ++i) {
        printf("%d %f %f %f\n", i, from[i], sinx[i], to[i]);
    }

end:
    if (pDCU) {
        pDCU->DestroyDataAndUnorderedAccessView(pBufResultUav);
        pBufResultUav = NULL;

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

    delete[] sinx;
    sinx = NULL;

    delete[] from;
    from = NULL;

    return 0;
}
