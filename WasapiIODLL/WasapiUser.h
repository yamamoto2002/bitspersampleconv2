#pragma once

// 日本語 UTF-8

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

enum WWPcmDataContentType {
    WWPcmDataContentSilence,
    WWPcmDataContentPcmData
};

const char *
WWPcmDataContentTypeToStr(WWPcmDataContentType w);

/*
 * play
 *   pcmData->posFrame: playing position
 *   pcmData->nFrames: total frames to play (frame == sample point)
 * record
 *   pcmData->posFrame: available recorded frame num
 *   pcmData->nFrames: recording buffer size
 */
struct WWPcmData {
    int       id;
    WWPcmData *next;
    WWPcmDataContentType contentType;
    int       nFrames;
    int       posFrame;
    BYTE      *stream;

    WWPcmData(void) {
        id       = 0;
        next     = NULL;
        contentType = WWPcmDataContentPcmData;
        nFrames  = 0;
        posFrame = 0;
        stream   = NULL;
    }

    ~WWPcmData(void);

    void Init(int samples);
    void Term(void);


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

    void SetSchedulerTaskType(WWSchedulerTaskType t);
    void SetShareMode(WWShareMode sm);

    // device enumeration
    HRESULT DoDeviceEnumeration(WWDeviceType t);
    int GetDeviceCount(void);
    bool GetDeviceName(int id, LPWSTR name, size_t nameBytes);
    bool InspectDevice(int id, LPWSTR result, size_t resultBytes);

    HRESULT ChooseDevice(int id);
    void UnchooseDevice(void);
    int  GetUseDeviceId(void);
    bool GetUseDeviceName(LPWSTR name, size_t nameBytes);

    /// @param bitsPerSample 1サンプルのビット数
    /// @param validBitsPerSample 1サンプルの有効なビット数
    ///        (bitsPerSample==32, validBitsPerSample==24という場合あり)
    HRESULT Setup(WWDataFeedMode mode, int sampleRate,
        int bitsPerSample, int validBitsPerSample,
        WWBitFormatType bitFormatType, int latencyMillisec, int numChannels);
    void Unsetup(void);

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
    /// @param data WAVファイルのPCMデータ。LRLRLR…で、リトルエンディアン。
    /// @param bytes dataのバイト数。
    /// @return true: 追加成功。false: 追加失敗。
    bool AddPlayPcmData(int id, BYTE *data, int bytes);

    bool AddPlayPcmDataEnd(void);

    void SetPlayRepeat(bool b);

    /// -1: not playing
    int GetNowPlayingPcmDataId(void);
    bool SetNowPlayingPcmDataId(int id);

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

    int          m_deviceBitsPerSample;
    int          m_validBitsPerSample;
    int          m_sampleRate;
    DWORD        m_latencyMillisec;
    WWBitFormatType m_bitFormatType;
    int          m_numChannels;

    IAudioRenderClient  *m_renderClient;
    IAudioCaptureClient *m_captureClient;
    HANDLE       m_thread;
    WWPcmData    *m_capturedPcmData;
    std::vector<WWPcmData> m_playPcmDataList;
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

    WWPcmData    *m_nowPlayingPcmData;
    int          m_useDeviceId;
    wchar_t      m_useDeviceName[WW_DEVICE_NAME_COUNT];

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

