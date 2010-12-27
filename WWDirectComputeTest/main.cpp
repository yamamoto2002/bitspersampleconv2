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

#define PI_D 3.141592653589793238462643
#define PI_F 3.141592653589793238462643f

/// シェーダーに渡す定数。16バイトの倍数でないといけないらしい。
struct ConstShaderParams {
    unsigned int c_convOffs;
    unsigned int c_dispatchCount;
    unsigned int c_reserved1;
    unsigned int c_reserved2;
};

enum WWGpuPrecisionType {
    WWGpuPrecision_Float,
    WWGpuPrecision_Double,
    WWGpuPrecision_NUM
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
    } else if (left < 0) {
        do{
            left += right;
        } while (left < 0);
    }

    return left;
}

static float
ModuloF(float left, float right)
{
    if (right < 0) {
        right = -right;
    }

    if (0 < left) {
        while (0 <= left - right) {
            left -= right;
        }
    } else if (left < 0) {
        do{
            left += right;
        } while (left < 0);
    }

    return left;
}

static HRESULT
JitterAddGpu(
        WWGpuPrecisionType precision,
        int sampleN,
        int convolutionN,
        float *sampleData,
        float *jitterX,
        float *outF)
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

    void *sinx = NULL;
    const D3D_SHADER_MACRO *defines = NULL;
    int sinxBufferElemBytes = 0;
    if (precision == WWGpuPrecision_Double) {
        // doubleprec

        const D3D_SHADER_MACRO definesD[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "SAMPLE_N", sampleNStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            "HIGH_PRECISION", "1",
            NULL, NULL
        };
        defines = definesD;

        double *sinxD = new double[sampleN];
        assert(sinxD);
        for (int i=0; i<sampleN; ++i) {
            sinxD[i] = sin(ModuloD(jitterX[i], 2.0 * PI_D));
        }
        sinx = sinxD;

        sinxBufferElemBytes = 8;
    } else {
        // singleprec

        const D3D_SHADER_MACRO definesF[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "SAMPLE_N", sampleNStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            // "HIGH_PRECISION", "1",
            NULL, NULL
        };
        defines = definesF;

        float *sinxF = new float[sampleN];
        assert(sinxF);
        for (int i=0; i<sampleN; ++i) {
            sinxF[i] = (float)sin(ModuloD(jitterX[i], 2.0 * PI_D));
        }
        sinx = sinxF;

        sinxBufferElemBytes = 4;
    }

    pDCU = new WWDirectComputeUser();
    assert(pDCU);

    HRG(pDCU->Init());

    // HLSL ComputeShaderをコンパイルしてGPUに送る。
    HRG(pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", defines, &pCS));
    assert(pCS);

    // 入力データをGPUメモリーに送る
    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), fromCount, from, "SampleDataBuffer", &pBuf0Srv));
    assert(pBuf0Srv);

    HRG(pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sinxBufferElemBytes, sampleN, sinx, "SinxBuffer", &pBuf1Srv));
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
JitterAddCpuD(int sampleN, int convolutionN, float *sampleData, float *jitterX, float *outF)
{
    // サンプルデータから、前後を0で水増ししたfromを作成。
    const int fromCount = convolutionN + sampleN + convolutionN;
    float *from = new float[fromCount];
    assert(from);

    ZeroMemory(from, sizeof(float) * fromCount);
    for (int i=0; i<sampleN; ++i) {
        from[i+convolutionN] = sampleData[i];
    }

    for (int pos=0; pos<sampleN; ++pos) {
        float xOffs = jitterX[pos];
        double r = 0.0f;

        for (int i=-convolutionN; i<convolutionN; ++i) {
            double x = PI_D * (i + xOffs);
            double sinx = sin(ModuloD(xOffs, 2.0 * PI_D));
            int    posS = pos + i + convolutionN;
            double sinc =  SincD(sinx, x);

            r += from[posS] * sinc;
        }

        outF[pos] = (float)r;
    }

    delete[] from;
    from = NULL;
}

static void
Test1(void)
{
    HRESULT hr = S_OK;

    // データ準備
    int convolutionN = 65536 * 256;
    int sampleN      = 16384;

    float *sampleData = new float[sampleN];
    assert(sampleData);

    float *jitterX = new float[sampleN];
    assert(jitterX);

    float *outputGpu = new float[sampleN];
    assert(outputGpu);

    float *outputCpu = new float[sampleN];
    assert(outputCpu);

#if 1
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

    HRG(JitterAddGpu(WWGpuPrecision_Double, sampleN, convolutionN, sampleData, jitterX, outputGpu));

    DWORD t1 = GetTickCount()+1;

    //JitterAddCpuD(sampleN, convolutionN, sampleData, jitterX, outputCpu);

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
}

/////////////////////////////////////////////////////////////////////////////

static HRESULT
ResampleCpu(
        int convolutionN,
        float * sampleData,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        float * outputTo,
        int sampleTotalTo)
{
    HRESULT hr = S_OK;

    assert(sampleRateFrom <= sampleRateTo);

    unsigned int * resamplePosArray = new unsigned int[sampleTotalTo];
    assert(resamplePosArray);

    float * fractionArray = new float[sampleTotalTo];
    assert(fractionArray);

    double *sinPreComputeArray = new double[sampleTotalTo];
    assert(sinPreComputeArray);

    for (int i=0; i<sampleTotalTo; ++i) {
        double resamplePos = (double)i * sampleRateFrom / sampleRateTo;
#if 1
        /* -0.5 <= fraction<+0.5になるようにresamplePosを選ぶ。
         * 最後のほうで範囲外を指さないようにする。
         */
        int resamplePosI = (int)(resamplePos+0.5);
        if (sampleTotalFrom <= resamplePosI) {
            resamplePosI = sampleTotalFrom -1;
        }
#else
        /* 0<=fraction<1になるにresamplePosIを選ぶ。
         */
        int resamplePosI = (int)(resamplePos+0.5);
        assert(resamplePosI < sampleTotalFrom);
#endif
        double fraction = resamplePos - resamplePosI;

        resamplePosArray[i]   = resamplePosI;
        fractionArray[i]      = (float)fraction;
        sinPreComputeArray[i] = sin(-PI_D * fraction);
    }

    /*
    for (int i=0; i<sampleTotalTo; ++i) {
        printf("i=%6d rPos=%6d fraction=%+f\n",
            i, resamplePosArray[i], fractionArray[i]);
    }
    printf("resamplePos created\n");
    */

    for (int toPos=0; toPos<sampleTotalTo; ++toPos) {
        int    fromPos  = resamplePosArray[toPos];
        double fraction = fractionArray[toPos];
        double sinPreCompute = sinPreComputeArray[toPos];

        double v = 0.0;

        for (int convOffs=-convolutionN; convOffs < convolutionN; ++convOffs) {
            int pos = convOffs + fromPos;
            if (0 <= pos && pos < sampleTotalFrom) {
                double x = PI_D * (convOffs - fraction);
                
                double sinX = sinPreCompute;
                if (convOffs & 1) {
                    sinX *= -1.0;
                }

#if 1
                // 合っていた。
                assert(fabs(sinX - sin(x)) < 0.000001);
#endif

                double sinc =  SincD(sinX, x);

                /*
                if (pos == 0) {
                    printf("toPos=%d pos=%d x=%f sinX=%f",
                        toPos, pos, x, sinX);
                    printf("\n");
                }
                */

                v += sampleData[pos] * sinc;
            }
        }
        outputTo[toPos] = (float)v;
    }

    /*
    for (int i=0; i<sampleTotalTo; ++i) {
        printf("i=%6d rPos=%6d fraction=%+6.2f output=%f\n",
            i, resamplePosArray[i], fractionArray[i], outputTo[i]);
    }
    printf("resampled\n");
    */
    for (int i=0; i<sampleTotalTo; ++i) {
        printf("%d, %f\n", i, outputTo[i]);
    }

//end:
    delete [] sinPreComputeArray;
    sinPreComputeArray = NULL;

    delete [] fractionArray;
    fractionArray = NULL;

    delete [] resamplePosArray;
    resamplePosArray = NULL;

    return hr;
}

static void
Test2(void)
{
    HRESULT hr = S_OK;

    // データ準備
    int convolutionN    = 256*256;
    int sampleTotalFrom = 256;
    int sampleRateFrom = 44100;
    int sampleRateTo   = 44100*10;

    int sampleTotalTo   = sampleTotalFrom * sampleRateTo / sampleRateFrom;

    float *sampleData = new float[sampleTotalFrom];
    assert(sampleData);

    float *outputCpu = new float[sampleTotalTo];
    assert(outputCpu);

#if 1
    // 最初のサンプルだけ1で、残りは0
    for (int i=0; i<sampleTotalFrom; ++i) {
        sampleData[i] = 0;
    }
    sampleData[0] = 1.0f;
#else
    // 真ん中のサンプルだけ1で、残りは0
    for (int i=0; i<sampleTotalFrom; ++i) {
        sampleData[i] = 0;
    }
    sampleData[127] = 1.0f;
#endif

    DWORD t0 = GetTickCount();

    HRG(ResampleCpu(convolutionN, sampleData, sampleTotalFrom, sampleRateFrom, sampleRateTo, outputCpu, sampleTotalTo));

    DWORD t1 = GetTickCount()+1;

    /*
    for (int i=0; i<sampleTotalTo; ++i) {
        printf("%7d outCpu=%f\n",
            i, outputCpu[i]);
    }
    */

    /*
        1 (秒)       x(サンプル/秒)
        ───── ＝ ────────
        14 (秒)       256(サンプル)

            x = 256 ÷ 14
        */

    printf("CPU=%dms(%fsamples/s)\n",
        (t1-t0),  sampleTotalTo / ((t1-t0)/1000.0));

end:
    delete[] outputCpu;
    outputCpu = NULL;

    delete[] sampleData;
    sampleData = NULL;
}

int
main(void)
{
#if defined(DEBUG) || defined(_DEBUG)
    _CrtSetDbgFlag( _CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF );
#endif

    Test2();

    return 0;
}
