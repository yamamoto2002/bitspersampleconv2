#pragma once

// 日本語 UTF-8

#include <Windows.h>
#include <MMDeviceAPI.h>
#include <AudioClient.h>
#include <AudioPolicy.h>
#include <vector>
#include "WWPcmData.h"

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
    WWSTTNone,
    WWSTTAudio,
    WWSTTProAudio,
    WWSTTPlayback
};

enum WWShareMode {
    WWSMShared,
    WWSMExclusive,
};

enum WWBitFormatType {
    WWSInt,
    WWSFloat
};

class WasapiUser {
public:
    WasapiUser(void);
    ~WasapiUser(void);

    HRESULT Init(void);
    void Term(void);

    // device enumeration
    HRESULT DoDeviceEnumeration(WWDeviceType t);
    int GetDeviceCount(void);
    bool GetDeviceName(int id, LPWSTR name, size_t nameBytes);
    bool InspectDevice(int id, LPWSTR result, size_t resultBytes);

    HRESULT ChooseDevice(int id);
    void UnchooseDevice(void);
    int  GetUseDeviceId(void);
    bool GetUseDeviceName(LPWSTR name, size_t nameBytes);

    void SetSchedulerTaskType(WWSchedulerTaskType t);
    void SetShareMode(WWShareMode sm);
    void SetDataFeedMode(WWDataFeedMode mode);
    void SetLatencyMillisec(DWORD millisec);

    HRESULT Setup(
        int sampleRate, WWPcmDataFormatType format, int numChannels);

    void Unsetup(void);

    /// Setup後に呼ぶ
    int GetBufferFormatSampleRate(void);
    WWPcmDataFormatType GetBufferFormatType(void);

    // PCMデータのセット方法
    //     1. ClearPlayList()を呼ぶ。
    //     2. AddPlayPcmDataStart()を呼ぶ。
    //     3. PCMデータの数だけAddPlayPcmData()を呼ぶ。
    //     4. AddPlayPcmDataEnd()を呼ぶ。
    // 注1: サンプルフォーマット変換は上のレイヤーに任せた。
    //      ここでは、来たdataを中のメモリにそのままコピーする。
    //      Setupでセットアップした形式でdataを渡してください。
    // 注2: AddPlayPcmDataEnd()後に、
    //      ClearPlayList()をしないでAddPlayPcmData()することはできません。

    void ClearPlayList(void);

    bool AddPlayPcmDataStart(void);

    /// @param id WAVファイルID。
    /// @param format データフォーマット。
    /// @param data WAVファイルのPCMデータ。LRLRLR…で、リトルエンディアン。
    /// @param bytes dataのバイト数。
    /// @return true: 追加成功。false: 追加失敗。
    bool AddPlayPcmData(int id, BYTE *data, int bytes);

    bool AddPlayPcmDataEnd(void);

    void SetPlayRepeat(bool b);

    /// -1: not playing
    int GetNowPlayingPcmDataId(void);
    bool UpdatePlayPcmDataById(int id);

    // recording
    void SetupCaptureBuffer(int bytes);
    int GetCapturedData(BYTE *data, int bytes);
    int GetCaptureGlitchCount(void);

    HRESULT Start(int wavDataId);

    bool Run(int millisec);

    void Stop(void);

    /// negative number returns when playing pregap
    int GetPosFrame(void);

    /// return total frames without pregap frame num
    int GetTotalFrameNum(void);

    /// v must be 0 or greater number
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

    int          m_sampleRate;
    WWPcmDataFormatType m_format;
    int          m_numChannels;

    IAudioRenderClient  *m_renderClient;
    IAudioCaptureClient *m_captureClient;
    HANDLE       m_thread;
    WWPcmData    *m_capturedPcmData;
    std::vector<WWPcmData> m_playPcmDataList;
    HANDLE       m_mutex;
    int          m_footerCount;
    bool         m_coInitializeSuccess;
    int          m_footerNeedSendCount;

    EDataFlow    m_dataFlow;
    int          m_glitchCount;

    WWDataFeedMode m_dataFeedMode;
    WWSchedulerTaskType m_schedulerTaskType;
    AUDCLNT_SHAREMODE m_shareMode;
    DWORD        m_latencyMillisec;

    IAudioClockAdjustment *m_audioClockAdjustment;

    WWPcmData    *m_nowPlayingPcmData;
    int          m_useDeviceId;
    wchar_t      m_useDeviceName[WW_DEVICE_NAME_COUNT];
    WWPcmData    m_spliceBuffer;
    int          m_deviceSampleRate;

    static DWORD WINAPI RenderEntry(LPVOID lpThreadParameter);
    static DWORD WINAPI CaptureEntry(LPVOID lpThreadParameter);

    DWORD RenderMain(void);
    DWORD CaptureMain(void);

    bool AudioSamplesSendProc(void);
    bool AudioSamplesRecvProc(void);

    void ClearCapturedPcmData(void);
    void ClearPlayPcmData(void);
    void ClearAllPcmData(void);

    int CreateWritableFrames(BYTE *pData_return, int wantFrames);

    WWPcmData *FindPlayPcmDataById(int id);

    void PlayPcmDataListDebug(void);

    bool AddPcmDataSilence(int nFrames);

    void SetFirstPlayPcmData(WWPcmData *pcmData);
};

