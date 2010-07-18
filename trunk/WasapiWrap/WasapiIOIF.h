#ifndef WasapiIOIF_H
#define WasapiIOIF_H

#include <Windows.h>

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOInit(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOTerm(void);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIODoDeviceEnumeration(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIOGetDeviceCount(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIOGetDeviceName(int id, LPWSTR name, int nameBytes);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOChooseDevice(int id);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOSetup(int sampleRate, int latencyMillisec);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOUnsetup(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOSetOutputData(unsigned char *data, int bytes);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOClearOutputData(void);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIOStart(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIORun(int millisec);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIOStop(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIOGetPosFrame(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIOGetTotalFrameNum(void);

#endif /* WasapiIOIF_H */
