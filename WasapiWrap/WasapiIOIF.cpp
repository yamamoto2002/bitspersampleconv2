#include "WasapiIOIF.h"
#include "WasapiWrap.h"
#include <assert.h>

static WasapiWrap* g_pWasapiWrap = 0;

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOInit(void)
{
    HRESULT hr = S_OK;

    if(!g_pWasapiWrap) {
        g_pWasapiWrap = new WasapiWrap();
        hr = g_pWasapiWrap->Init();
    }

    return hr;
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOTerm(void)
{
    if (g_pWasapiWrap) {
        g_pWasapiWrap->Term();
        delete g_pWasapiWrap;
        g_pWasapiWrap = NULL;
    }
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIODoDeviceEnumeration(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->DoDeviceEnumeration();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIOGetDeviceCount(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetDeviceCount();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIOGetDeviceName(int id, LPWSTR name, int nameBytes)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetDeviceName(id, name, nameBytes);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOChooseDevice(int id)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->ChooseDevice(id);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOSetup(int sampleRate, int latencyMillisec)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Setup(sampleRate, latencyMillisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOUnsetup(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Unsetup();
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOSetOutputData(unsigned char *data, int bytes)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->SetOutputData(data, bytes);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOStart(void)
{
    assert(g_pWasapiWrap);

    return g_pWasapiWrap->Start();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIORun(int millisec)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Run(millisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOStop(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Stop();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIOGetPosFrame(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetPosFrame();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIOGetTotalFrameNum(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetTotalFrameNum();
}
