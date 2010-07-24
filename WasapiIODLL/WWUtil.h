#pragma once

#ifdef _DEBUG
#  include <stdio.h>
#  define dprintf(x, ...) printf(x, __VA_ARGS__)
#else
#  define dprintf(x, ...)
#endif

#define HRG(x)                                    \
{                                                 \
    hr = x;                                       \
    if (FAILED(hr)) {                             \
        dprintf("E: %s:%d %s failed (%08x)\n",    \
            __FILE__, __LINE__, #x, hr);          \
        goto end;                                 \
    }                                             \
}                                                 \

#define HRR(x)                                    \
{                                                 \
    hr = x;                                       \
    if (FAILED(hr)) {                             \
        dprintf("E: %s:%d %s failed (%08x)\n",    \
            __FILE__, __LINE__, #x, hr);          \
        return hr;                                \
    }                                             \
}                                                 \

#define CHK(x)                           \
{   if (!x) {                            \
        dprintf("E: %s:%d %s is NULL\n", \
            __FILE__, __LINE__, #x);     \
        return E_FAIL;                   \
    }                                    \
}                                        \

template <class T> void SafeRelease(T **ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

