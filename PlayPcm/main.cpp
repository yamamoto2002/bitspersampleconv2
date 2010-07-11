#include "WasapiWrap.h"
#include "WWUtil.h"

#include <stdio.h>
#include <Windows.h>


static HRESULT
Run(void)
{
    HRESULT hr;

    WasapiWrap ww;

    HRR(ww.Init());

    HRR(ww.DoDeviceEnumeration());

    for (int i=0; i<ww.GetDeviceCount(); ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];

        ww.GetDeviceName(i, name, sizeof name);

        printf("id=%d: %S\n", i, name);
    }

    ww.ChooseDevice(-1);

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

