#pragma once

#include <Windows.h>
#include <MMDeviceAPI.h>
#include <AudioClient.h>
#include <AudioPolicy.h>
#include <vector>

#define WW_DEVICE_NAME_COUNT (256)


struct WWDeviceInfo {
    int id;
    wchar_t name[WW_DEVICE_NAME_COUNT];

    WWDeviceInfo(void) {
        id = -1;
        name[0] = 0;
    }

    WWDeviceInfo(int id, const wchar_t * name);
};

struct WWPcmData {
    //int  bitsPerSample;
    //int  nChannels;
    //int  nSamplesPerSec;
    int  nFrames;
    int  posFrame;
    BYTE *stream;

    void Init(int samples);
    void Term(void);

    ~WWPcmData(void);

    void CopyFrom(WWPcmData *rhs);
};

class WasapiWrap {
public:
    WasapiWrap(void);
    ~WasapiWrap(void);

    HRESULT Init(void);
    void Term(void);

    // device enumeration
    HRESULT DoDeviceEnumeration(void);
    int GetDeviceCount(void);
    bool GetDeviceName(int id, LPWSTR name, size_t nameBytes);

    // if you choose no device, calll ChooseDevice(-1)
    HRESULT ChooseDevice(int id);

    HRESULT Setup(int sampleRate, int bitsPerSample, int latencyMillisec);
    void Unsetup(void);

    void SetOutputData(BYTE *data, int bytes);
    void ClearOutputData(void);

    HRESULT Start(void);

    bool Run(int millisec);

    void Stop(void);

    int GetPosFrame(void);
    int GetTotalFrameNum(void);
    bool SetPosFrame(int v);

private:
    std::vector<WWDeviceInfo> m_deviceInfo;
    IMMDeviceCollection       *m_deviceCollection;
    IMMDevice                 *m_deviceToUse;

    HANDLE       m_shutdownEvent;
    HANDLE       m_audioSamplesReadyEvent;

    IAudioClient *m_audioClient;
    WAVEFORMATEX *m_mixFormat;
    int          m_frameBytes;
    UINT32       m_bufferSamples;

    IAudioRenderClient *m_renderClient;
    HANDLE       m_renderThread;
    WWPcmData    *m_pcmData;
    HANDLE       m_mutex;
    int          m_footerCount;
    bool         m_coInitializeSuccess;

    static DWORD WINAPI RenderEntry(LPVOID lpThreadParameter);

    DWORD RenderMain(void);

    bool AudioSamplesReadyProc(void);
};

