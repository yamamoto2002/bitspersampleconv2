#pragma once

// 京 UTF-8

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

/*
 * play
 *   pcmData->posFrame: playing position
 *   pcmData->nFrames: total frames to play
 * record
 *   pcmData->posFrame: available recorded frame num
 *   pcmData->nFrames: recording buffer size
 */
struct WWPcmData {
    int  nFrames;
    int  posFrame;
    BYTE *stream;

    void Init(int samples);
    void Term(void);

    ~WWPcmData(void);

    void CopyFrom(WWPcmData *rhs);
};

enum WWDataFeedMode {
    WWDFMEventDriven,
    WWDFMTimerDriven,

    WWDFMNum
};

enum WWDeviceType {
    WWDTPlay,
    WWDTRec,

    WWDTNum
};

enum WWSchedulerTaskType {
    WWSTTAudio,
    WWSTTProAudio,
};

enum WWShareMode {
    WWSMShared,
    WWSMExclusive,
};

class WasapiUser {
public:
    WasapiUser(void);
    ~WasapiUser(void);

    HRESULT Init(void);
    void Term(void);

    void SetSchedulerTaskType(WWSchedulerTaskType t);
    void SetShareMode(WWShareMode sm);

    // device enumeration
    HRESULT DoDeviceEnumeration(WWDeviceType t);
    int GetDeviceCount(void);
    bool GetDeviceName(int id, LPWSTR name, size_t nameBytes);
    bool InspectDevice(int id, LPWSTR result, size_t resultBytes);

    // if you choose no device, calll ChooseDevice(-1)
    HRESULT ChooseDevice(int id);

    HRESULT Setup(WWDataFeedMode mode, int sampleRate, int bitsPerSample, int latencyMillisec);
    void Unsetup(void);

    // before play start
    void SetOutputData(BYTE *data, int bytes);
    void ClearOutputData(void);

    // recording
    void SetupCaptureBuffer(int bytes);
    int GetCapturedData(BYTE *data, int bytes);
    int GetCaptureGlitchCount(void);

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
    int          m_frameBytes;
    UINT32       m_bufferFrameNum;

    int          m_deviceBitsPerSample;
    int          m_dataBitsPerSample;
    int          m_sampleRate;
    DWORD        m_latencyMillisec;

    IAudioRenderClient  *m_renderClient;
    IAudioCaptureClient *m_captureClient;
    HANDLE       m_thread;
    WWPcmData    *m_pcmData;
    HANDLE       m_mutex;
    int          m_footerCount;
    bool         m_coInitializeSuccess;
    WWDataFeedMode m_dataFeedMode;
    int          m_footerNeedSendCount;

    EDataFlow    m_dataFlow;
    int          m_glitchCount;

    WWSchedulerTaskType m_schedulerTaskType;

    AUDCLNT_SHAREMODE m_shareMode;

    IAudioClockAdjustment *m_audioClockAdjustment;

    static DWORD WINAPI RenderEntry(LPVOID lpThreadParameter);
    static DWORD WINAPI CaptureEntry(LPVOID lpThreadParameter);

    DWORD RenderMain(void);
    DWORD CaptureMain(void);

    bool AudioSamplesSendProc(void);
    bool AudioSamplesRecvProc(void);

    void ClearPcmData(void);
};

