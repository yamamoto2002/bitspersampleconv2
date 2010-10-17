#pragma once

#include <Windows.h>

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Init(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Term(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetSchedulerTaskType(int type);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetShareMode(int sm);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_DoDeviceEnumeration(int deviceType);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetDeviceCount(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetDeviceName(int id, LPWSTR name, int nameBytes);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_InspectDevice(int id, LPWSTR result, int resultBytes);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_ChooseDevice(int id);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_UnchooseDevice(void);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetUseDeviceId(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_GetUseDeviceName(LPWSTR name, int nameBytes);

/// @param bitFormatType 0: SInt 1:SFloat
extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Setup(int dataFeedMode, int sampleRate,
    int bitsPerSample, int validBitsPerSample,
    int bitFormatType, int latencyMillisec, int numChannels);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_Unsetup(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataStart(void);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmData(int id, unsigned char *data, int bytes);

extern "C" __declspec(dllexport)
bool __stdcall
WasapiIO_AddPlayPcmDataEnd(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_ClearPlayList(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetPlayRepeat(bool b);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetNowPlayingPcmDataId(void);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetNowPlayingPcmDataId(int id);

extern "C" __declspec(dllexport)
void __stdcall
WasapiIO_SetupCaptureBuffer(int bytes);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetCapturedData(unsigned char *data, int bytes);

extern "C" __declspec(dllexport)
int __stdcall
WasapiIO_GetCaptureGlitchCount(void);

extern "C" __declspec(dllexport)
HRESULT __stdcall
WasapiIO_Start(int wavDataId);

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
