#ifndef WasapiIOIF_H
#define WasapiIOIF_H

#include <Windows.h>

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Term(void);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceName(int id, LPWSTR name, int nameBytes);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(int sampleRate, int latencyMillisec);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetOutputData(unsigned char *data, int bytes);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_ClearOutputData(void);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Start(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_Run(int millisec);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Stop(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetPosFrame(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetTotalFrameNum(void);

#endif /* WasapiIOIF_H */
