#include "WasapiWrap.h"
#include "WWUtil.h"

#include <stdio.h>
#include <Windows.h>


static HRESULT
Run(void)
{
    HRESULT hr;

    WasapiWrap ww;

    HRF(ww.Init());

    ww.Term();

    return 0;
}

int
main(int argc, char *argv[])
{
    HRESULT hr = Run();
    if (FAILED(hr)) {
        printf("Run failed (%08x)\n", hr);
    }

    return 0;
}

