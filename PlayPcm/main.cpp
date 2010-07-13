#include "WasapiWrap.h"
#include "WWUtil.h"

#include <stdio.h>
#include <Windows.h>

static HRESULT
GetIntValueFromConsole(const char *prompt, int from, int to, int *value_return)
{
    *value_return = 0;

    printf("%s (%d to %d) ? ", prompt, from, to);
    fflush(stdout);

    char s[32];
    char *result = fgets(s, 31, stdin);
    if (NULL == result) {
        return E_INVALIDARG;
    }

    char *p = NULL;
    errno = 0;
    int v = (int)strtol(s, &p, 10);
    if (errno != 0 || p == s) {
        printf("E: malformed input...\n");
        return E_INVALIDARG;
    }
    if (v < from || to < v) {
        printf("E: value is out of range.\n");
        return E_INVALIDARG;
    }

    *value_return = v;

    return NOERROR;
}

static HRESULT
Run(void)
{
    HRESULT hr;
    WasapiWrap ww;
    int id;

    HRR(ww.Init());
    HRG(ww.DoDeviceEnumeration());

    for (int i=0; i<ww.GetDeviceCount(); ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];

        ww.GetDeviceName(i, name, sizeof name);
        printf("id=%d: %S\n", i, name);
    }

    HRG(GetIntValueFromConsole("specify device by id", 0, ww.GetDeviceCount()-1, &id));

    ww.ChooseDevice(id);

    ww.Setup(44100, 10);

    {
        WWPcmData data;
        data.Init(44100);

        ww.Start(&data);

        data.Term();
    }

    Sleep(3000);
    ww.Stop();
    ww.Unsetup();

end:
    ww.Term();
    return NOERROR;
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

