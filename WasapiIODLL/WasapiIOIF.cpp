#include "WasapiIOIF.h"
#include "WasapiWrap.h"
#include <assert.h>

static WasapiWrap* g_pWasapiWrap = 0;

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(void)
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
WasapiIO_Term(void)
{
    if (g_pWasapiWrap) {
        g_pWasapiWrap->Term();
        delete g_pWasapiWrap;
        g_pWasapiWrap = NULL;
    }
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->DoDeviceEnumeration();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetDeviceCount();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceName(int id, LPWSTR name, int nameBytes)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetDeviceName(id, name, nameBytes);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->ChooseDevice(id);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(int sampleRate, int bitsPerSample, int latencyMillisec)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Setup(sampleRate, bitsPerSample, latencyMillisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Unsetup();
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetOutputData(unsigned char *data, int bytes)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->SetOutputData(data, bytes);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_ClearOutputData(void)
{
    assert(g_pWasapiWrap);
    g_pWasapiWrap->ClearOutputData();
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Start(void)
{
    assert(g_pWasapiWrap);

    return g_pWasapiWrap->Start();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_Run(int millisec)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Run(millisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Stop(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->Stop();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPosFrame(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetPosFrame();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetTotalFrameNum(void)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->GetTotalFrameNum();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_SetPosFrame(int v)
{
    assert(g_pWasapiWrap);
    return g_pWasapiWrap->SetPosFrame(v);
}
