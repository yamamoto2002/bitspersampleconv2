#pragma once


#define HRF(x)                            \
{   hr = x;                               \
    if (FAILED(hr)) {                     \
    printf("%s failed (%08x)\n", #x, hr); \
        return hr;                        \
    }                                     \
}                                         \

