#pragma once

#include <Windows.h>

/////////////////////////////////////////////////////////////////////////////
// アップサンプル

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_Init(
        int convolutionN,
        float * sampleFrom,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo);

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_Dispatch(
        int startPos,
        int count);

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_GetResultFromGpuMemory(
        float * outputTo,
        int outputToElemNum);

extern "C" __declspec(dllexport)
void __stdcall
WWDCUpsample_Term(void);

// CPU処理
extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_UpsampleCpuSetup(
        int convolutionN,
        float * sampleData,
        int sampleTotalFrom,
        int sampleRateFrom,
        int sampleRateTo,
        int sampleTotalTo);

extern "C" __declspec(dllexport)
int __stdcall
WWDCUpsample_UpsampleCpuDo(
        int startPos,
        int count,
        float * outputTo);

extern "C" __declspec(dllexport)
void __stdcall
WWDCUpsample_UpsampleCpuUnsetup(void);

/// @result サンプルデータのスケーリング(0.5=0.5倍スケール)
extern "C" __declspec(dllexport)
float __stdcall
WWDCUpsample_LimitSampleData(
        float * sampleInOut,
        int sampleElemNum);

/////////////////////////////////////////////////////////////////////////////
// 古い

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_Init(
        int precision,
        int convolutionN);

extern "C" __declspec(dllexport)
void __stdcall
WWDCIO_Term(void);

/// @param precision 0: singleprec 1: doubleprec
/// @param convolutionN must be a power of 2 and larger than 256
/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_JitterAddGpu(
        int precision,
        int sampleTotal,
        int convolutionN,
        float *sampleData,
        float *jitterX,
        float *outF);

/// convolute the specified portion (offs to offs+sampleToProcess-1)
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
        int sampleToProcess);
