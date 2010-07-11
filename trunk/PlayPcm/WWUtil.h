#pragma once


#define HRG(x)                                \
{   hr = x;                                   \
    if (FAILED(hr)) {                         \
        printf("%s failed (%08x)\n", #x, hr); \
        goto end;                             \
    }                                         \
}                                             \

#define HRR(x)                                \
{   hr = x;                                   \
    if (FAILED(hr)) {                         \
        printf("%s failed (%08x)\n", #x, hr); \
        return hr;                            \
    }                                         \
}                                             \

template <class T> void SafeRelease(T **ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

