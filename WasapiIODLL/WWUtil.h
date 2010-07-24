#pragma once

#ifdef _DEBUG
#  include <stdio.h>
#  define dprintf(x, ...) printf(__VA_ARGS__)
#else
#  define dprintf(x, ...)
#endif

#define HRG(x)                                    \
{                                                 \
    hr = x;                                       \
    if (FAILED(hr)) {                             \
        dprintf("E: %s failed (%08x)\n", #x, hr); \
        goto end;                                 \
    }                                             \
}                                                 \

#define HRR(x)                                    \
{                                                 \
    hr = x;                                       \
    if (FAILED(hr)) {                             \
        dprintf("E: %s failed (%08x)\n", #x, hr); \
        return hr;                                \
    }                                             \
}                                                 \

#define CHK(x)                          \
{   if (!x) {                           \
        dprintf("E: %s is NULL\n", #x); \
        assert(0);                      \
        return E_FAIL;                  \
    }                                   \
}                                       \

template <class T> void SafeRelease(T **ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

