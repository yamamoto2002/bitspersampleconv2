#pragma once

#include <Windows.h>

/// @result HRESULT
extern "C" __declspec(dllexport)
int __stdcall
WWDCIO_Init(void);

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
