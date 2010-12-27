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
    unsigned int c_sampleToStartPos;
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

class WWUpsampleGpu {
public:
    void Init(void);
    void Term(void);

    HRESULT Setup(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo);

    HRESULT Dispatch(
        int startPos,
        int count);

    HRESULT ResultGetFromGpuMemory(
        float * outputTo,
        int outputToElemNum);

    void Unsetup(void);

private:
    int m_convolutionN;
    float * m_sampleFrom;
    int m_sampleTotalFrom;
    int m_sampleRateFrom;
    int m_sampleRateTo;
    int m_sampleTotalTo;

    WWDirectComputeUser *m_pDCU;
    ID3D11ComputeShader *m_pCS;

    ID3D11ShaderResourceView*   m_pBuf0Srv;
    ID3D11ShaderResourceView*   m_pBuf1Srv;
    ID3D11ShaderResourceView*   m_pBuf2Srv;
    ID3D11ShaderResourceView*   m_pBuf3Srv;
    ID3D11UnorderedAccessView*  m_pBufResultUav;
    ID3D11Buffer * m_pBufConst;
};

void
WWUpsampleGpu::Init(void)
{
    int m_convolutionN = 0;
    float * m_sampleFrom = NULL;
    int m_sampleTotalFrom = 0;
    int m_sampleRateFrom = 0;
    int m_sampleRateTo = 0;
    int m_sampleTotalTo = 0;

    m_pDCU = NULL;
    m_pCS  = NULL;

    m_pBuf0Srv = NULL;
    m_pBuf1Srv = NULL;
    m_pBuf2Srv = NULL;
    m_pBuf3Srv = NULL;
    m_pBufResultUav = NULL;
    m_pBufConst = NULL;
}

void
WWUpsampleGpu::Term(void)
{
    assert(m_pDCU == NULL);
    assert(m_pCS  == NULL);

    assert(m_pBuf0Srv == NULL);
    assert(m_pBuf1Srv == NULL);
    assert(m_pBuf2Srv == NULL);
    assert(m_pBuf3Srv == NULL);
    assert(m_pBufResultUav == NULL);
    assert(m_pBufConst == NULL);
}

HRESULT
WWUpsampleGpu::Setup(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo)
{
    bool    result = true;
    HRESULT hr     = S_OK;
    int * resamplePosArray = NULL;
    float * fractionArray = NULL;
    float * sinPreComputeArray = NULL;


    assert(0 < convolutionN);
    assert(sampleFrom);
    assert(0 < sampleTotalFrom);
    assert(sampleRateFrom <= sampleRateTo);
    assert(0 < sampleTotalTo);

    m_convolutionN    = convolutionN;
    m_sampleFrom      = sampleFrom;
    m_sampleTotalFrom = sampleTotalFrom;
    m_sampleRateFrom  = sampleRateFrom;
    m_sampleRateTo    = sampleRateTo;
    m_sampleTotalTo   = sampleTotalTo;

    resamplePosArray = new int[sampleTotalTo];
    assert(resamplePosArray);

    fractionArray = new float[sampleTotalTo];
    assert(fractionArray);

    sinPreComputeArray = new float[sampleTotalTo];
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
         * これは1に近い値が頻出するのでよくない。
         */
        int resamplePosI = (int)(resamplePos+0.5);
        assert(resamplePosI < sampleTotalFrom);
#endif
        double fraction = resamplePos - resamplePosI;

        resamplePosArray[i]   = resamplePosI;
        fractionArray[i]      = (float)fraction;
        sinPreComputeArray[i] = (float)sin(-PI_D * fraction);
    }

    /*
    for (int i=0; i<sampleTotalTo; ++i) {
        printf("i=%6d rPos=%6d fraction=%+f\n",
            i, resamplePosArray[i], fractionArray[i]);
    }
    printf("resamplePos created\n");
    */

    // HLSLの#defineを作る。
    char      convStartStr[32];
    sprintf_s(convStartStr, "%d", -convolutionN);
    char      convEndStr[32];
    sprintf_s(convEndStr,   "%d", convolutionN);
    char      convCountStr[32];
    sprintf_s(convCountStr, "%d", convolutionN*2);
    char      sampleTotalFromStr[32];
    sprintf_s(sampleTotalFromStr,   "%d", sampleTotalFrom);
    char      sampleTotalToStr[32];
    sprintf_s(sampleTotalToStr,   "%d", sampleTotalTo);

    char      sampleRateFromStr[32];
    sprintf_s(sampleRateFromStr,   "%d", sampleRateFrom);
    char      sampleRateToStr[32];
    sprintf_s(sampleRateToStr,   "%d", sampleRateTo);
    char      iterateNStr[32];
    sprintf_s(iterateNStr,  "%d", convolutionN*2/GROUP_THREAD_COUNT);
    char      groupThreadCountStr[32];
    sprintf_s(groupThreadCountStr, "%d", GROUP_THREAD_COUNT);

    // doubleprec
    int sinxBufferElemBytes = 8;
    const D3D_SHADER_MACRO defines[] = {
            "CONV_START", convStartStr,
            "CONV_END", convEndStr,
            "CONV_COUNT", convCountStr,
            "SAMPLE_TOTAL_FROM", sampleTotalFromStr,
            "SAMPLE_TOTAL_TO", sampleTotalToStr,

            "SAMPLE_RATE_FROM", sampleRateFromStr,
            "SAMPLE_RATE_TO", sampleRateToStr,
            "ITERATE_N", iterateNStr,
            "GROUP_THREAD_COUNT", groupThreadCountStr,
            NULL, NULL
        };

    m_pDCU = new WWDirectComputeUser();
    assert(m_pDCU);

    HRG(m_pDCU->Init());

    // HLSL ComputeShaderをコンパイルしてGPUに送る。
    HRG(m_pDCU->CreateComputeShader(L"SincConvolution2.hlsl", "CSMain", defines, &m_pCS));
    assert(m_pCS);

    // 入力データをGPUメモリーに送る
    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), sampleTotalFrom, sampleFrom, "SampleFromBuffer", &m_pBuf0Srv));
    assert(m_pBuf0Srv);

    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(int), sampleTotalTo, resamplePosArray, "ResamplePosBuffer", &m_pBuf1Srv));
    assert(m_pBuf1Srv);

    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), sampleTotalTo, fractionArray, "FractionBuffer", &m_pBuf2Srv));
    assert(m_pBuf2Srv);

    HRG(m_pDCU->SendReadOnlyDataAndCreateShaderResourceView(
        sizeof(float), sampleTotalTo, sinPreComputeArray, "SinPreComputeBuffer", &m_pBuf3Srv));
    assert(m_pBuf3Srv);
    
    // 結果出力領域をGPUに作成。
    HRG(m_pDCU->CreateBufferAndUnorderedAccessView(
        sizeof(float), sampleTotalTo, NULL, "OutputBuffer", &m_pBufResultUav));
    assert(m_pBufResultUav);

    // 定数置き場をGPUに作成。
    HRG(m_pDCU->CreateConstantBuffer(sizeof(ConstShaderParams), 1, "ConstShaderParams", &m_pBufConst));

end:
    delete [] sinPreComputeArray;
    sinPreComputeArray = NULL;

    delete [] fractionArray;
    fractionArray = NULL;

    delete [] resamplePosArray;
    resamplePosArray = NULL;

    return hr;
}

HRESULT
WWUpsampleGpu::Dispatch(
        int startPos,
        int count)
{
    HRESULT hr = S_OK;
    bool result = true;

    // GPU上でComputeShader実行。
    ID3D11ShaderResourceView* aRViews[] = { m_pBuf0Srv, m_pBuf1Srv, m_pBuf2Srv, m_pBuf3Srv };
    ConstShaderParams shaderParams;
    ZeroMemory(&shaderParams, sizeof shaderParams);
#if 1
    // すこしだけ速い。中でループするようにした。
    shaderParams.c_convOffs = 0;
    shaderParams.c_dispatchCount = m_convolutionN*2/GROUP_THREAD_COUNT;
    shaderParams.c_sampleToStartPos = startPos;
    HRGR(m_pDCU->Run(m_pCS, sizeof aRViews/sizeof aRViews[0], aRViews, m_pBufResultUav,
        m_pBufConst, &shaderParams, sizeof shaderParams, count, 1, 1));
#else
    // 遅い
    for (int i=0; i<convolutionN*2/GROUP_THREAD_COUNT; ++i) {
        shaderParams.c_convOffs = i * GROUP_THREAD_COUNT;
        shaderParams.c_dispatchCount = convolutionN*2/GROUP_THREAD_COUNT;
        shaderParams.c_sampleToStartPos = startPos;
        HRGR(m_pDCU->Run(m_pCS, sizeof aRViews/sizeof aRViews[0], aRViews, m_pBufResultUav,
            m_pBufConst, &shaderParams, sizeof shaderParams, count, 1, 1));
    }
#endif

end:
    if (hr == DXGI_ERROR_DEVICE_REMOVED) {
        dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
            m_pDCU->GetDevice()->GetDeviceRemovedReason());
    }

    return hr;
}

HRESULT
WWUpsampleGpu::ResultGetFromGpuMemory(
        float *outputTo,
        int outputToElemNum)
{
    HRESULT hr = S_OK;

    assert(m_pDCU);
    assert(m_pBufResultUav);

    assert(outputTo);
    assert(outputToElemNum <= m_sampleTotalTo);

    // 計算結果をCPUメモリーに持ってくる。
    HRG(m_pDCU->RecvResultToCpuMemory(m_pBufResultUav, outputTo, outputToElemNum * sizeof(float)));
end:
    if (hr == DXGI_ERROR_DEVICE_REMOVED) {
        dprintf("DXGI_ERROR_DEVICE_REMOVED reason=%08x\n",
            m_pDCU->GetDevice()->GetDeviceRemovedReason());
    }

    return hr;
}

void
WWUpsampleGpu::Unsetup(void)
{
    if (m_pDCU) {
        m_pDCU->DestroyConstantBuffer(m_pBufConst);
        m_pBufConst = NULL;

        m_pDCU->DestroyDataAndUnorderedAccessView(m_pBufResultUav);
        m_pBufResultUav = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf3Srv);
        m_pBuf3Srv = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf2Srv);
        m_pBuf2Srv = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf1Srv);
        m_pBuf1Srv = NULL;

        m_pDCU->DestroyDataAndShaderResourceView(m_pBuf0Srv);
        m_pBuf0Srv = NULL;

        if (m_pCS) {
            m_pDCU->DestroyComputeShader(m_pCS);
            m_pCS = NULL;
        }

        m_pDCU->Term();
    }

    SAFE_DELETE(m_pDCU);
}

//////////////////////////////////////////////////////////////////////////////////////

static HRESULT
UpsampleCpu(
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
         * これは1に近い値が頻出するのでよくない。
         */
        int resamplePosI = (int)(resamplePos+0.5);
        assert(resamplePosI < sampleTotalFrom);
#endif
        double fraction = resamplePos - resamplePosI;

        resamplePosArray[i]   = resamplePosI;
        fractionArray[i]      = (float)fraction;
        sinPreComputeArray[i] = sin(-PI_D * fraction);
    }

    for (int i=0; i<sampleTotalTo; ++i) {
        printf("i=%6d rPos=%6d fraction=%+f\n",
            i, resamplePosArray[i], fractionArray[i]);
    }
    printf("resamplePos created\n");

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
    /*
    for (int i=0; i<sampleTotalTo; ++i) {
        printf("%d, %f\n", i, outputTo[i]);
    }
    */

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
    WWUpsampleGpu us;

    us.Init();

    // データ準備
    int convolutionN    = 256*256;
    int sampleTotalFrom = 16;
    int sampleRateFrom = 44100;
    int sampleRateTo   = 44100*10;

    int sampleTotalTo   = sampleTotalFrom * sampleRateTo / sampleRateFrom;

    float *sampleData = new float[sampleTotalFrom];
    assert(sampleData);

    float *outputCpu = new float[sampleTotalTo];
    assert(outputCpu);

    float *outputGpu = new float[sampleTotalTo];
    assert(outputGpu);

    // 全部1
    for (int i=0; i<sampleTotalFrom; ++i) {
        sampleData[i] = 1.0f;
    }

    /*
    // 44100Hzサンプリングで1000Hzのsin
    for (int i=0; i<sampleTotalFrom; ++i) {
        float xS = PI_F * i * 1000 / 44100;
        sampleData[i] = sinf(xS);
    }
    */
    /*
    // 最初のサンプルだけ1で、残りは0
    for (int i=0; i<sampleTotalFrom; ++i) {
        sampleData[i] = 0;
    }
    sampleData[0] = 1.0f;
    */

    /*
    // 真ん中のサンプルだけ1で、残りは0
    for (int i=0; i<sampleTotalFrom; ++i) {
        sampleData[i] = 0;
    }
    sampleData[127] = 1.0f;
    */



    HRG(us.Setup(convolutionN, sampleData, sampleTotalFrom, sampleRateFrom, sampleRateTo, sampleTotalTo));
    DWORD t0 = GetTickCount();
    for (int i=0; i<1; ++i ) { // sampleTotalTo; ++i) {
        HRG(us.Dispatch(0, sampleTotalTo));
    }
    DWORD t1 = GetTickCount()+1;
    HRG(us.ResultGetFromGpuMemory(outputGpu, sampleTotalTo));

    DWORD t2 = GetTickCount();

    HRG(UpsampleCpu(convolutionN, sampleData, sampleTotalFrom, sampleRateFrom, sampleRateTo, outputCpu, sampleTotalTo));

    DWORD t3 = GetTickCount()+1;

    for (int i=0; i<sampleTotalTo; ++i) {
        printf("%7d outGpu=%f outCpu=%f diff=%12.8f\n",
            i, outputGpu[i], outputCpu[i],
            fabsf(outputGpu[i]-outputCpu[i]));
    }

    /*
        1 (秒)       x(サンプル/秒)
        ───── ＝ ────────
        14 (秒)       256(サンプル)

            x = 256 ÷ 14
        */

    printf("GPU=%dms(%fsamples/s) CPU=%dms(%fsamples/s)\n",
        (t1-t0),  sampleTotalTo / ((t1-t0)/1000.0),
        (t3-t2),  sampleTotalTo / ((t3-t2)/1000.0));

end:
    us.Unsetup();
    us.Term();

    delete[] outputGpu;
    outputGpu = NULL;

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
