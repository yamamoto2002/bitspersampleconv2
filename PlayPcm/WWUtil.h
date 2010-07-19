#pragma once

#include <Windows.h>

void WWErrorDescription(HRESULT hr);

#define HRG(x)                                   \
{                                                \
    printf("D: invoke %s\n", #x);            \
    hr = x;                                      \
    if (FAILED(hr)) {                            \
        printf("E: %s failed (%08x)\n", #x, hr); \
        WWErrorDescription(hr);                  \
        goto end;                                \
    }                                            \
}                                                \

#define HRR(x)                                   \
{                                                \
      printf("D: invoke %s\n", #x);            \
    hr = x;                                      \
    if (FAILED(hr)) {                            \
        printf("E: %s failed (%08x)\n", #x, hr); \
        WWErrorDescription(hr);                  \
        return hr;                               \
    }                                            \
}                                                \

#define CHK(x)                     \
{   if (!x) {                      \
    printf("E: %s is NULL\n", #x); \
        assert(0);                 \
        return E_FAIL;             \
    }                              \
}                                  \

template <class T> void SafeRelease(T **ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

