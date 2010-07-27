#include "WasapiIOIF.h"
#include "WasapiUser.h"
#include <assert.h>

static WasapiUser* g_pWasapi = 0;

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(void)
{
    HRESULT hr = S_OK;

    if(!g_pWasapi) {
        g_pWasapi = new WasapiUser();
        hr = g_pWasapi->Init();
    }

    return hr;
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Term(void)
{
    if (g_pWasapi) {
        g_pWasapi->Term();
        delete g_pWasapi;
        g_pWasapi = NULL;
    }
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(int deviceType)
{
    assert(g_pWasapi);
    WWDeviceType t = (WWDeviceType)deviceType;
    return g_pWasapi->DoDeviceEnumeration(t);
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void)
{
    assert(g_pWasapi);
    return g_pWasapi->GetDeviceCount();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceName(int id, LPWSTR name, int nameBytes)
{
    assert(g_pWasapi);
    return g_pWasapi->GetDeviceName(id, name, nameBytes);
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_InspectDevice(int id, LPWSTR result, int resultBytes)
{
    assert(g_pWasapi);
    return g_pWasapi->InspectDevice(id, result, resultBytes);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id)
{
    assert(g_pWasapi);
    return g_pWasapi->ChooseDevice(id);
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(int dataFeedMode, int sampleRate, int bitsPerSample, int latencyMillisec)
{
    assert(g_pWasapi);
    assert(0 <= dataFeedMode && dataFeedMode < (int)WWDFMNum);
    return g_pWasapi->Setup((WWDataFeedMode)dataFeedMode, sampleRate, bitsPerSample, latencyMillisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void)
{
    assert(g_pWasapi);
    return g_pWasapi->Unsetup();
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetOutputData(unsigned char *data, int bytes)
{
    assert(g_pWasapi);
    return g_pWasapi->SetOutputData(data, bytes);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_ClearOutputData(void)
{
    assert(g_pWasapi);
    g_pWasapi->ClearOutputData();
}

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Start(void)
{
    assert(g_pWasapi);

    return g_pWasapi->Start();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_Run(int millisec)
{
    assert(g_pWasapi);
    return g_pWasapi->Run(millisec);
}

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Stop(void)
{
    assert(g_pWasapi);
    return g_pWasapi->Stop();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPosFrame(void)
{
    assert(g_pWasapi);
    return g_pWasapi->GetPosFrame();
}

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetTotalFrameNum(void)
{
    assert(g_pWasapi);
    return g_pWasapi->GetTotalFrameNum();
}

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_SetPosFrame(int v)
{
    assert(g_pWasapi);
    return g_pWasapi->SetPosFrame(v);
}
