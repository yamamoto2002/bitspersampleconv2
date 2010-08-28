// 日本語 UTF-8

#include "WasapiUser.h"
#include "WWUtil.h"
#include <avrt.h>
#include <assert.h>
#include <functiondiscoverykeys.h>
#include <strsafe.h>
#include <mmsystem.h>
#include <malloc.h>

#define FOOTER_SEND_FRAME_NUM (2)
#define PERIODS_PER_BUFFER_ON_TIMER_DRIVEN_MODE (4)

WWDeviceInfo::WWDeviceInfo(int id, const wchar_t * name)
{
    this->id = id;
    wcsncpy_s(this->name, name, WW_DEVICE_NAME_COUNT-1);
}

void
WWPcmData::Term(void)
{
    dprintf("D: %s() stream=%p\n", __FUNCTION__, stream);

    free(stream);
    stream = NULL;
}

WWPcmData::~WWPcmData(void)
{
}

void
WWPcmData::CopyFrom(WWPcmData *rhs)
{
    *this = *rhs;

    next = NULL;

    int bytes = nFrames * 4;

    stream = (BYTE*)malloc(bytes);
    CopyMemory(stream, rhs->stream, bytes);
}

static wchar_t*
WWSchedulerTaskTypeToStr(WWSchedulerTaskType t)
{
    switch (t) {
    case WWSTTNone: return L"None";
    case WWSTTAudio: return L"Audio";
    case WWSTTProAudio: return L"Pro Audio";
    default: assert(0); return L"";
    }
}

///////////////////////////////////////////////////////////////////////
// WasapiUser class

WasapiUser::WasapiUser(void)
{
    m_deviceCollection = NULL;
    m_deviceToUse      = NULL;

    m_shutdownEvent    = NULL;
    m_audioSamplesReadyEvent = NULL;

    m_audioClient      = NULL;

    m_renderClient     = NULL;
    m_captureClient    = NULL;

    m_thread           = NULL;
    m_capturedPcmData  = NULL;
    m_mutex            = NULL;
    m_coInitializeSuccess = false;
    m_glitchCount      = 0;
    m_schedulerTaskType = WWSTTAudio;
    m_shareMode         = AUDCLNT_SHAREMODE_EXCLUSIVE;

    m_audioClockAdjustment = NULL;
    m_nowPlayingPcmData = NULL;
    m_useDeviceId    = -1;

    memset(m_useDeviceName, 0, sizeof m_useDeviceName);
}

WasapiUser::~WasapiUser(void)
{
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;
}

HRESULT
WasapiUser::Init(void)
{
    HRESULT hr = S_OK;
    
    dprintf("D: %s()\n", __FUNCTION__);

    assert(!m_deviceCollection);
    assert(!m_deviceToUse);

    hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (S_OK == hr) {
        m_coInitializeSuccess = true;
    } else {
        dprintf("E: WasapiUser::Init() CoInitializeEx() failed %08x\n", hr);
        hr = S_OK;
    }

    assert(!m_mutex);
    m_mutex = CreateMutex(NULL, FALSE, NULL);

    return hr;
}

void
WasapiUser::Term(void)
{
    dprintf("D: %s() m_deviceCollection=%p m_deviceToUse=%p m_mutex=%p\n",
        __FUNCTION__, m_deviceCollection, m_deviceToUse, m_mutex);

    SafeRelease(&m_deviceCollection);
    SafeRelease(&m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;

    if (m_mutex) {
        CloseHandle(m_mutex);
        m_mutex = NULL;
    }

    if (m_coInitializeSuccess) {
        CoUninitialize();
    }
}

void
WasapiUser::SetSchedulerTaskType(WWSchedulerTaskType t)
{
    assert(0 <= t&& t <= WWSTTProAudio);

    dprintf("D: %s() t=%d\n", __FUNCTION__, (int)t);

    m_schedulerTaskType = t;
}

void
WasapiUser::SetShareMode(WWShareMode sm)
{
    dprintf("D: %s() sm=%d\n", __FUNCTION__, (int)sm);

    switch (sm) {
    case WWSMShared:
        m_shareMode = AUDCLNT_SHAREMODE_SHARED;
        break;
    case WWSMExclusive:
        m_shareMode = AUDCLNT_SHAREMODE_EXCLUSIVE;
        break;
    default:
        assert(0);
        break;
    }
}

static HRESULT
DeviceNameGet(
    IMMDeviceCollection *dc, UINT id, wchar_t *name, size_t nameBytes)
{
    HRESULT hr = 0;

    IMMDevice *device  = NULL;
    LPWSTR deviceId    = NULL;
    IPropertyStore *ps = NULL;
    PROPVARIANT pv;

    assert(dc);
    assert(name);

    name[0] = 0;

    assert(0 < nameBytes);

    PropVariantInit(&pv);

    HRR(dc->Item(id, &device));
    HRR(device->GetId(&deviceId));
    HRR(device->OpenPropertyStore(STGM_READ, &ps));

    HRG(ps->GetValue(PKEY_Device_FriendlyName, &pv));
    SafeRelease(&ps);

    wcsncpy(name, pv.pwszVal, nameBytes/sizeof name[0] -1);

end:
    PropVariantClear(&pv);
    CoTaskMemFree(deviceId);
    SafeRelease(&ps);
    return hr;
}

HRESULT
WasapiUser::DoDeviceEnumeration(WWDeviceType t)
{
    HRESULT hr = 0;
    IMMDeviceEnumerator *deviceEnumerator = NULL;

    dprintf("D: %s() t=%d\n", __FUNCTION__, (int)t);

    switch (t) {
    case WWDTPlay: m_dataFlow = eRender;  break;
    case WWDTRec:  m_dataFlow = eCapture; break;
    default:
        assert(0);
        return E_FAIL;
    }

    m_deviceInfo.clear();

    HRR(CoCreateInstance(__uuidof(MMDeviceEnumerator),
        NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&deviceEnumerator)));
    
    HRR(deviceEnumerator->EnumAudioEndpoints(
        m_dataFlow, DEVICE_STATE_ACTIVE, &m_deviceCollection));

    UINT nDevices = 0;
    HRG(m_deviceCollection->GetCount(&nDevices));

    for (UINT i=0; i<nDevices; ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];
        HRG(DeviceNameGet(m_deviceCollection, i, name, sizeof name));

        /*
        for (int j=0; j<wcslen(name); ++j) {
            if (name[j] < 0x20 || 127 <= name[j]) {
                name[j] = L'?';
            }
        }
        */

        m_deviceInfo.push_back(WWDeviceInfo(i, name));
    }

end:
    SafeRelease(&deviceEnumerator);
    return hr;
}

int
WasapiUser::GetDeviceCount(void)
{
    assert(m_deviceCollection);
    return (int)m_deviceInfo.size();
}

bool
WasapiUser::GetDeviceName(int id, LPWSTR name, size_t nameBytes)
{
    assert(0 <= id && id < (int)m_deviceInfo.size());

    wcsncpy(name, m_deviceInfo[id].name, nameBytes/sizeof name[0] -1);
    return true;
}

bool
WasapiUser::InspectDevice(int id, LPWSTR result, size_t resultBytes)
{
    HRESULT hr;
    WAVEFORMATEX *waveFormat = NULL;
    REFERENCE_TIME hnsDefaultDevicePeriod;
    REFERENCE_TIME hnsMinimumDevicePeriod;

    assert(0 <= id && id < (int)m_deviceInfo.size());

    assert(m_deviceCollection);
    assert(!m_deviceToUse);

    result[0] = 0;

    int sampleRateList[]    = {44100, 48000, 88200, 96000, 176400, 192000};
    int bitsPerSampleList[] = {16, 32};

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));

    HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

    for (int j=0; j<sizeof bitsPerSampleList/sizeof bitsPerSampleList[0]; ++j) {
        for (int i=0; i<sizeof sampleRateList/sizeof sampleRateList[0]; ++i) {
            int sampleRate    = sampleRateList[i];
            int bitsPerSample = bitsPerSampleList[j];

            assert(!waveFormat);
            HRG(m_audioClient->GetMixFormat(&waveFormat));
            assert(waveFormat);

            WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;

            dprintf("original Mix Format:\n");
            WWWaveFormatDebug(waveFormat);
            WWWFEXDebug(wfex);

            if (waveFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
                dprintf("E: unsupported device ! mixformat == 0x%08x\n",
                    waveFormat->wFormatTag);
                hr = E_FAIL;
                goto end;
            }

            wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
            wfex->Format.wBitsPerSample = (WORD)bitsPerSample;
            wfex->Format.nSamplesPerSec = sampleRate;

            wfex->Format.nBlockAlign = (WORD)(
                (bitsPerSample / 8) * waveFormat->nChannels);
            wfex->Format.nAvgBytesPerSec =
                wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
            wfex->Samples.wValidBitsPerSample = (WORD)bitsPerSample;

            dprintf("preferred Format:\n");
            WWWaveFormatDebug(waveFormat);
            WWWFEXDebug(wfex);

            hr = m_audioClient->IsFormatSupported(
                m_shareMode,waveFormat,NULL);
            dprintf("IsFormatSupported=%08x\n", hr);
            if (S_OK == hr) {
                wchar_t s[256];
                StringCbPrintfW(s, sizeof s-1,
                    L"  %6dHz %dbit: ok 0x%08x\r\n",
                    sampleRate, bitsPerSample, hr);
                wcsncat(result, s, resultBytes/2 - wcslen(result) -1);
            } else {
                wchar_t s[256];
                StringCbPrintfW(s, sizeof s-1,
                    L"  %6dHz %dbit: na 0x%08x\r\n",
                    sampleRate, bitsPerSample, hr);
                wcsncat(result, s, resultBytes/2 - wcslen(result) -1);
            }

            if (waveFormat) {
                CoTaskMemFree(waveFormat);
                waveFormat = NULL;
            }

        }
    }

    {
        wchar_t s[256];

        HRG(m_audioClient->GetDevicePeriod(
            &hnsDefaultDevicePeriod, &hnsMinimumDevicePeriod));
        StringCbPrintfW(s, sizeof s-1,
            L"  Default scheduling period for a shared-mode stream:    %f ms\n"
            L"  Minimum scheduling period for a exclusive-mode stream: %f ms\n",
            ((double)hnsDefaultDevicePeriod)*0.0001,
            ((double)hnsMinimumDevicePeriod)*0.0001);
        wcsncat(result, s, resultBytes/2 - wcslen(result) -1);
    }

end:
    SafeRelease(&m_deviceToUse);
    SafeRelease(&m_audioClient);

    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }

    return true;
}

HRESULT
WasapiUser::ChooseDevice(int id)
{
    HRESULT hr = 0;

    dprintf("D: %s(%d)\n", __FUNCTION__, id);

    if (id < 0) {
        goto end;
    }

    assert(m_deviceCollection);
    assert(!m_deviceToUse);

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));
    m_useDeviceId = id;
    wcscpy_s(m_useDeviceName, m_deviceInfo[id].name);

end:
    SafeRelease(&m_deviceCollection);
    return hr;
}

void
WasapiUser::UnchooseDevice(void)
{
    dprintf("D: %s()\n", __FUNCTION__);

    SafeRelease(&m_deviceToUse);
    m_useDeviceId = -1;
    m_useDeviceName[0] = 0;
}

int
WasapiUser::GetUseDeviceId(void)
{
    dprintf("D: %s() %d\n", __FUNCTION__, m_useDeviceId);
    return m_useDeviceId;
}

bool
WasapiUser::GetUseDeviceName(LPWSTR name, size_t nameBytes)
{
    wcsncpy(name, m_useDeviceName, nameBytes/sizeof name[0] -1);
    return true;
}

HRESULT
WasapiUser::Setup(
    WWDataFeedMode mode,
    int sampleRate,
    int bitsPerSample,
    int latencyMillisec)
{
    HRESULT      hr          = 0;
    WAVEFORMATEX *waveFormat = NULL;

    dprintf("D: %s(%d %d %d %d)\n", __FUNCTION__,
        (int)mode, sampleRate, bitsPerSample, latencyMillisec);

    m_dataFeedMode        = mode;
    m_latencyMillisec     = latencyMillisec;
    m_sampleRate          = sampleRate;
    m_dataBitsPerSample   = bitsPerSample;
    m_deviceBitsPerSample = m_dataBitsPerSample;

    if (24 == m_deviceBitsPerSample) {
        m_deviceBitsPerSample = 32;
    }
    if (WWSMShared == m_shareMode) {
        m_deviceBitsPerSample = 32;
    }

    m_audioSamplesReadyEvent =
        CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_audioSamplesReadyEvent);

    assert(m_deviceToUse);
    assert(!m_audioClient);
    HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL,
        (void**)&m_audioClient));

    assert(!waveFormat);
    HRG(m_audioClient->GetMixFormat(&waveFormat));
    assert(waveFormat);

    WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;

    dprintf("original Mix Format:\n");
    WWWaveFormatDebug(waveFormat);
    WWWFEXDebug(wfex);

    if (waveFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
        dprintf("E: unsupported device ! mixformat == 0x%08x\n",
            waveFormat->wFormatTag);
        hr = E_FAIL;
        goto end;
    }

    if (WWSMExclusive == m_shareMode) {
        wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
        wfex->Format.wBitsPerSample = (WORD)m_deviceBitsPerSample;
        wfex->Format.nSamplesPerSec = sampleRate;

        wfex->Format.nBlockAlign = (WORD)(
            (m_deviceBitsPerSample / 8) * waveFormat->nChannels);
        wfex->Format.nAvgBytesPerSec =
            wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
        wfex->Samples.wValidBitsPerSample = (WORD)m_deviceBitsPerSample;

        dprintf("preferred Format:\n");
        WWWaveFormatDebug(waveFormat);
        WWWFEXDebug(wfex);
    
        HRG(m_audioClient->IsFormatSupported(
            m_shareMode,waveFormat,NULL));
    }

    m_frameBytes = waveFormat->nBlockAlign;
    
    DWORD streamFlags = 0;
    int periodsPerBuffer = 1;
    switch (m_dataFeedMode) {
    case WWDFMTimerDriven:
        streamFlags      = AUDCLNT_STREAMFLAGS_NOPERSIST;
        periodsPerBuffer = PERIODS_PER_BUFFER_ON_TIMER_DRIVEN_MODE;
        break;
    case WWDFMEventDriven:
        streamFlags      =
            AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST;
        periodsPerBuffer = 1;
        break;
    default:
        assert(0);
        break;
    }

    REFERENCE_TIME bufferPeriodicity = latencyMillisec * 10000;
    REFERENCE_TIME bufferDuration    = bufferPeriodicity * periodsPerBuffer;

    bool needClockAdjustmentOnSharedMode = false;

    if (WWSMShared == m_shareMode) {
        // 共有モードでデバイスサンプルレートとWAVファイルのサンプルレートが異なる場合、
        // 入力サンプリング周波数調整を行う。
        // 共有モード イベント駆動の場合、bufferPeriodicityに0をセットする。

        if (waveFormat->nSamplesPerSec != (DWORD)sampleRate) {
            // 共有モードのサンプルレート変更。
            needClockAdjustmentOnSharedMode = true;
            streamFlags |= AUDCLNT_STREAMFLAGS_RATEADJUST;
        }

        if (WWDFMEventDriven == m_dataFeedMode) {
            bufferPeriodicity = 0;
        }
    }

    hr = m_audioClient->Initialize(
        m_shareMode, streamFlags, 
        bufferDuration, bufferPeriodicity, waveFormat, NULL);
    if (hr == AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED) {
        HRG(m_audioClient->GetBufferSize(&m_bufferFrameNum));

        SafeRelease(&m_audioClient);

        bufferPeriodicity = (REFERENCE_TIME)(
            10000.0 *                         // (REFERENCE_TIME(100ns) / ms) *
            1000 *                            // (ms / s) *
            m_bufferFrameNum /                 // frames /
            waveFormat->nSamplesPerSec +     // (frames / s)
            0.5);
        bufferDuration = bufferPeriodicity * periodsPerBuffer;

        HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL,
        (void**)&m_audioClient));

        hr = m_audioClient->Initialize(
            m_shareMode, streamFlags, 
            bufferDuration, bufferPeriodicity, waveFormat, NULL);
    }
    if (FAILED(hr)) {
        dprintf("E: audioClient->Initialize failed 0x%08x\n", hr);
        goto end;
    }

    if (needClockAdjustmentOnSharedMode) {
        assert(!m_audioClockAdjustment);
        HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_audioClockAdjustment)));

        assert(m_audioClockAdjustment);
        HRG(m_audioClockAdjustment->SetSampleRate((float)sampleRate));
        dprintf("IAudioClockAdjustment::SetSampleRate(%d) %08x\n", sampleRate, hr);
    }

    // サンプルレート変更後にGetBufferSizeする。なんとなく。

    HRG(m_audioClient->GetBufferSize(&m_bufferFrameNum));
    dprintf("m_audioClient->GetBufferSize() rv=%u\n", m_bufferFrameNum);

    if (WWDFMEventDriven == m_dataFeedMode) {
        HRG(m_audioClient->SetEventHandle(m_audioSamplesReadyEvent));
    }

    switch (m_dataFlow) {
    case eRender:
        HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_renderClient)));
        break;
    case eCapture:
        HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_captureClient)));
        break;
    default:
        assert(0);
        break;
    }

end:

    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }

    return hr;
}

void
WasapiUser::Unsetup(void)
{
    dprintf("D: %s() ASRE=%p ACA=%p CC=%p RC=%p AC=%p\n", __FUNCTION__,
        m_audioSamplesReadyEvent, m_audioClockAdjustment, m_captureClient,
        m_renderClient, m_audioClient);

    if (m_audioSamplesReadyEvent) {
        CloseHandle(m_audioSamplesReadyEvent);
        m_audioSamplesReadyEvent = NULL;
    }

    SafeRelease(&m_audioClockAdjustment);
    SafeRelease(&m_captureClient);
    SafeRelease(&m_renderClient);
    SafeRelease(&m_audioClient);
}

HRESULT
WasapiUser::Start(int wavDataId)
{
    HRESULT hr      = 0;
    BYTE    *pData  = NULL;
    UINT32  nFrames = 0;
    DWORD   flags   = 0;

    dprintf("D: %s()\n", __FUNCTION__);

    assert(m_playPcmDataList.size());

    // イベント駆動モードで最初の音が繰り返される問題の修正。
    m_nowPlayingPcmData = FindPlayPcmDataById(wavDataId);

    HRG(m_audioClient->Reset());

    assert(!m_shutdownEvent);
    m_shutdownEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_shutdownEvent);

    switch (m_dataFlow) {
    case eRender:
        m_thread = CreateThread(NULL, 0, RenderEntry, this, 0, NULL);
        assert(m_thread);

        nFrames = m_bufferFrameNum;
        if (WWDFMTimerDriven == m_dataFeedMode || WWSMShared == m_shareMode) {
            // タイマー駆動の場合、パッド計算必要。
            // 共有モードの場合イベント駆動でもパッドが必要になる。
            // RenderSharedEventDrivenのWASAPIRenderer.cpp参照。

            UINT32 padding = 0; //< frame now using
            HRG(m_audioClient->GetCurrentPadding(&padding));
            nFrames = m_bufferFrameNum - padding;
        }

        if (0 <= nFrames) {
            assert(m_renderClient);
            HRG(m_renderClient->GetBuffer(nFrames, &pData));

            memset(pData, 0, nFrames * m_frameBytes);

            HRG(m_renderClient->ReleaseBuffer(nFrames, 0));
        }

        m_footerCount = 0;

        break;

    case eCapture:
        m_thread = CreateThread(NULL, 0, CaptureEntry, this, 0, NULL);
        assert(m_thread);

        hr = m_captureClient->GetBuffer(
            &pData, &nFrames,&flags, NULL, NULL);
        if (SUCCEEDED(hr)) {
            m_captureClient->ReleaseBuffer(nFrames);
        } else {
            hr = S_OK;
        }
        m_glitchCount = 0;
        break;

    default:
        assert(0);
        break;
    }

    assert(m_audioClient);
    HRG(m_audioClient->Start());

end:
    return hr;
}

void
WasapiUser::Stop(void)
{
    HRESULT hr;

    dprintf("D: %s() AC=%p SE=%p T=%p\n", __FUNCTION__,
        m_audioClient, m_shutdownEvent, m_thread);

    if (NULL != m_audioClient) {
        hr = m_audioClient->Stop();
        if (FAILED(hr)) {
            dprintf("E: %s m_audioClient->Stop() failed 0x%x\n", __FUNCTION__, hr);
        }
    }

    if (NULL != m_shutdownEvent) {
        SetEvent(m_shutdownEvent);
    }
    if (NULL != m_thread) {
        WaitForSingleObject(m_thread, INFINITE);
        dprintf("D: %s:%d CloseHandle(%p)\n", __FILE__, __LINE__, m_thread);
        if (m_thread) {
            // ここでなぜかNULLになることがある。
            // 暇なときに原因を調べましょう
            CloseHandle(m_thread);
        }
        m_thread = NULL;
    }

    if (NULL != m_shutdownEvent) {
        dprintf("D: %s:%d CloseHandle(%p)\n", __FILE__, __LINE__, m_shutdownEvent);
        CloseHandle(m_shutdownEvent);
        m_shutdownEvent = NULL;
    }
}

bool
WasapiUser::Run(int millisec)
{
    // dprintf("%s WaitForSingleObject(%p, %d)\n",
    // __FUNCTION__, m_thread, millisec);
    DWORD rv = WaitForSingleObject(m_thread, millisec);
    if (rv == WAIT_TIMEOUT) {
        Sleep(10);
        //dprintf(".\n");
        return false;
    }
    // dprintf("%s rv=0x%08x return true\n", __FUNCTION__, rv);
    return true;
}

////////////////////////////////////////////////////////////////////////////
// PCM data buffer management

bool
WasapiUser::AddPlayPcmData(int id, BYTE *data, int bytes)
{
    WWPcmData pcmData;

    pcmData.id       = id;
    pcmData.next     = NULL;
    pcmData.posFrame = 0;

    // pcmData.stream create

    if (WWSMShared == m_shareMode) {
        BYTE *p = NULL;
        if (24 == m_dataBitsPerSample) {
            // 共有モード 24ビット
            pcmData.nFrames = bytes /3 / 2; // 3==24bit, 2==stereo
            p = WWStereo24ToStereoFloat32(data, bytes);
        } else if (16 == m_dataBitsPerSample) {
            // 共有モード 16ビット
            pcmData.nFrames = bytes /2 / 2; // 3==16bit, 2==stereo
            p = WWStereo16ToStereoFloat32(data, bytes);
        } else {
            assert(0);
        }
        pcmData.stream = p;
    } else if (24 == m_dataBitsPerSample) {
        // 排他モード 24bit
        BYTE *p = WWStereo24ToStereo32(data, bytes);
        pcmData.stream = p;
        pcmData.nFrames = bytes /3 / 2; // 3==24bit, 2==stereo
    } else if (16 == m_dataBitsPerSample) {
        // 排他モード 16bit
        BYTE *p = (BYTE *)malloc(bytes);
        if (NULL != p) {
            memcpy(p, data, bytes);
            pcmData.nFrames = bytes/m_frameBytes;
        }
        pcmData.stream = p;
    } else {
        assert(0);
    }

    if (NULL == pcmData.stream) {
        return false;
    }

    m_playPcmDataList.push_back(pcmData);
    return true;
}

void
WasapiUser::ClearPlayList(void)
{
    ClearPlayPcmData();
}

void
WasapiUser::ClearPlayPcmData(void)
{
    for (int i=0; i<m_playPcmDataList.size(); ++i) {
        m_playPcmDataList[i].Term();
    }
    m_playPcmDataList.clear();

    m_nowPlayingPcmData = NULL;
}

void
WasapiUser::ClearCapturedPcmData(void)
{
    if (m_capturedPcmData) {
        m_capturedPcmData->Term();
        delete m_capturedPcmData;
        m_capturedPcmData = NULL;
    }
}

void
WasapiUser::SetPlayRepeat(bool repeat)
{
    // 最初のpcmDataから、最後のpcmDataまでnextでつなげる。
    // リピートフラグが立っていたら最後のpcmDataのnextを最初のpcmDataにする。
    dprintf("D: %s(%d)\n", __FUNCTION__, (int)repeat);

    for (int i=0; i<m_playPcmDataList.size(); ++i) {
        if (i == m_playPcmDataList.size()-1) {
            // 最後の項目。
            if (repeat) {
                m_playPcmDataList[i].next = 
                    &m_playPcmDataList[0];
            } else {
                m_playPcmDataList[i].next = NULL;
            }
        } else {
            // 最後の項目以外は、連続にnextをつなげる。
            m_playPcmDataList[i].next = 
                &m_playPcmDataList[i+1];
        }
    }
}

int
WasapiUser::GetNowPlayingPcmDataId(void)
{
    WWPcmData *nowPlaying = m_nowPlayingPcmData;

    if (!nowPlaying) {
        return -1;
    }
    return nowPlaying->id;
}

WWPcmData *
WasapiUser::FindPlayPcmDataById(int id)
{
    for (int i=0; i<m_playPcmDataList.size(); ++i) {
        if (m_playPcmDataList[i].id == id) {
            return &m_playPcmDataList[i];
        }
    }

    return NULL;
}

bool
WasapiUser::SetNowPlayingPcmDataId(int id)
{
    dprintf("D: %s(%d)\n", __FUNCTION__, id);

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        WWPcmData *p = FindPlayPcmDataById(id);
        if (NULL == p) {
            goto end;
        }

        WWPcmData *nowPlaying = m_nowPlayingPcmData;

        if (nowPlaying) {
            nowPlaying->posFrame = 0;
            m_nowPlayingPcmData = p;
        }
    }

end:
    ReleaseMutex(m_mutex);
    return true;
}

int
WasapiUser::GetTotalFrameNum(void)
{
    WWPcmData *nowPlaying = m_nowPlayingPcmData;

    if (!nowPlaying) {
        return 0;
    }
    return nowPlaying->nFrames;
}

int
WasapiUser::GetPosFrame(void)
{
    int result = 0;

    WWPcmData *nowPlaying = m_nowPlayingPcmData;

    // assert(m_mutex);
    // WaitForSingleObject(m_mutex, INFINITE);
    if (nowPlaying) {
        result = nowPlaying->posFrame;
    }
    //ReleaseMutex(m_mutex);

    return result;
}

bool
WasapiUser::SetPosFrame(int v)
{
    if (m_dataFlow != eRender) {
        assert(0);
        return false;
    }

    if (v < 0 || GetTotalFrameNum() <= v) {
        return false;
    }

    assert(m_mutex);
    WaitForSingleObject(m_mutex, INFINITE);
    {
        WWPcmData *nowPlaying = m_nowPlayingPcmData;

        if (nowPlaying) {
            nowPlaying->posFrame = v;
        }
    }
    ReleaseMutex(m_mutex);

    return true;
}

void
WasapiUser::SetupCaptureBuffer(int bytes)
{
    if (m_dataFlow != eCapture) {
        assert(0);
        return;
    }

    ClearCapturedPcmData();

    /* 録音時は
     *   pcmData->posFrame: 有効な録音データのフレーム数
     *   pcmData->nFrames: 録音可能総フレーム数
     */
    m_capturedPcmData = new WWPcmData();
    m_capturedPcmData->posFrame = 0;
    m_capturedPcmData->nFrames = bytes/m_frameBytes;
    m_capturedPcmData->stream = (BYTE*)malloc(bytes);
}

int
WasapiUser::GetCapturedData(BYTE *data, int bytes)
{
    if (m_dataFlow != eCapture) {
        assert(0);
        return 0;
    }

    assert(m_capturedPcmData);

    if (m_capturedPcmData->posFrame * m_frameBytes < bytes) {
        bytes = m_capturedPcmData->posFrame * m_frameBytes;
    }
    memcpy(data, m_capturedPcmData->stream, bytes);

    return bytes;
}

int
WasapiUser::GetCaptureGlitchCount(void)
{
    return m_glitchCount;
}

/////////////////////////////////////////////////////////////////////////////////
// render thread

DWORD
WasapiUser::RenderEntry(LPVOID lpThreadParameter)
{
    WasapiUser* self = (WasapiUser*)lpThreadParameter;
    return self->RenderMain();
}

int
WasapiUser::CreateWritableFrames(BYTE *pData_return, int wantFrames)
{
    int pos = 0;

    WWPcmData *pcmData = m_nowPlayingPcmData;
    while (NULL != pcmData && 0 < wantFrames) {
        int copyFrames = wantFrames;
        if (pcmData->nFrames <= pcmData->posFrame + wantFrames) {
            // pcmDataが持っているフレーム数よりも要求フレーム数が多い。
            copyFrames = pcmData->nFrames - pcmData->posFrame;
        }

        dprintf("pcmData->posFrame=%d copyFrames=%d nFrames=%d\n",
            pcmData->posFrame, copyFrames, pcmData->nFrames);

        CopyMemory(&pData_return[pos*m_frameBytes],
            &pcmData->stream[pcmData->posFrame * m_frameBytes],
            copyFrames * m_frameBytes);
        pos += copyFrames;
        pcmData->posFrame += copyFrames;
        wantFrames -= copyFrames;

        if (pcmData->nFrames <= pcmData->posFrame) {
            // pcmDataの最後まで来た。
            // このpcmDataの再生位置は巻き戻して、次のpcmDataの先頭をポイントする。
            pcmData->posFrame = 0;
            pcmData = pcmData->next;
        }
    }

    m_nowPlayingPcmData = pcmData;
    return pos;
}

bool
WasapiUser::AudioSamplesSendProc(void)
{
    bool    result     = true;
    BYTE    *from      = NULL;
    BYTE    *to        = NULL;
    HRESULT hr         = 0;
    int     copyFrames = 0;
    int     writableFrames = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    writableFrames = m_bufferFrameNum;
    if (WWDFMTimerDriven == m_dataFeedMode || WWSMShared == m_shareMode) {
        // 共有モードの場合イベント駆動でもパッドが必要になる。
        // RenderSharedEventDrivenのWASAPIRenderer.cpp参照。

        UINT32 padding = 0; //< frame num now using
        assert(m_audioClient);
        HRGR(m_audioClient->GetCurrentPadding(&padding));

        writableFrames = m_bufferFrameNum - padding;
        // dprintf("m_bufferFrameNum=%d padding=%d writableFrames=%d\n", m_bufferFrameNum, padding, writableFrames);
        if (writableFrames <= 0) {
            goto end;
        }
    }

    from = (BYTE*)_malloca(writableFrames * m_frameBytes);
    copyFrames = CreateWritableFrames(from, writableFrames);

    assert(m_renderClient);
    HRGR(m_renderClient->GetBuffer(writableFrames, &to));

    assert(to);
    if (0 < copyFrames) {
        CopyMemory(to, from, copyFrames * m_frameBytes);
    }
    if (0 < writableFrames - copyFrames) {
        memset(&to[copyFrames*m_frameBytes], 0,
            (writableFrames - copyFrames)*m_frameBytes);
        /* dprintf("fc=%d bs=%d cb=%d memset %d bytes\n",
            m_footerCount, m_bufferFrameNum, copyFrames,
            (m_bufferFrameNum - copyFrames)*m_frameBytes);
        */
    }

    HRGR(m_renderClient->ReleaseBuffer(writableFrames, 0));
    to = NULL;

    if (NULL == m_nowPlayingPcmData) {
        ++m_footerCount;
        if (m_footerNeedSendCount < m_footerCount) {
            result = false;
        }
    }

end:
    _freea(from);
    from = NULL;

    ReleaseMutex(m_mutex);
    return result;
}

DWORD
WasapiUser::RenderMain(void)
{
    bool    stillPlaying   = true;
    HANDLE  waitArray[2]   = {m_shutdownEvent, m_audioSamplesReadyEvent};
    int     waitArrayCount;
    DWORD   timeoutMillisec;
    HANDLE  mmcssHandle    = NULL;
    DWORD   mmcssTaskIndex = 0;
    DWORD   waitResult;
    HRESULT hr             = 0;
    
    HRG(CoInitializeEx(NULL, COINIT_MULTITHREADED));

    timeBeginPeriod(1);

    if (WWSTTNone != m_schedulerTaskType) {
        dprintf("D: %s() AvSetMmThreadCharacteristics(%S)\n",
            __FUNCTION__,
            WWSchedulerTaskTypeToStr(m_schedulerTaskType));

        mmcssHandle = AvSetMmThreadCharacteristics(
            WWSchedulerTaskTypeToStr(m_schedulerTaskType),
            &mmcssTaskIndex);
        if (NULL == mmcssHandle) {
            dprintf("Unable to enable MMCSS on render thread: 0x%08x\n",
                GetLastError());
        }
    }

    if (m_dataFeedMode == WWDFMTimerDriven) {
        waitArrayCount        = 1;
        m_footerNeedSendCount = FOOTER_SEND_FRAME_NUM * 2;
        timeoutMillisec       = m_latencyMillisec     / 2;
    } else {
        waitArrayCount        = 2;
        m_footerNeedSendCount = FOOTER_SEND_FRAME_NUM;
        timeoutMillisec       = INFINITE;
    }

    // dprintf("D: %s() waitArrayCount=%d m_shutdownEvent=%p m_audioSamplesReadyEvent=%p\n",
    //    __FUNCTION__, waitArrayCount, m_shutdownEvent, m_audioSamplesReadyEvent);

    while (stillPlaying) {
        waitResult = WaitForMultipleObjects(
            waitArrayCount, waitArray, FALSE, timeoutMillisec);
        switch (waitResult) {
        case WAIT_OBJECT_0 + 0:     // m_shutdownEvent
            dprintf("D: %s() shutdown event flagged\n", __FUNCTION__);
            stillPlaying = false;
            break;
        case WAIT_OBJECT_0 + 1:     // m_audioSamplesReadyEvent
            //dprintf("D: %s() samples ready event flagged\n", __FUNCTION__);
            // only in EventDriven mode
            stillPlaying = AudioSamplesSendProc();
            break;
        case WAIT_TIMEOUT:
            //dprintf("D: %s() timeout event flagged\n", __FUNCTION__);
            // only in TimerDriven mode
            stillPlaying = AudioSamplesSendProc();
            break;
        default:
            break;
        }
    }

end:
    if (NULL != mmcssHandle) {
        AvRevertMmThreadCharacteristics(mmcssHandle);
        mmcssHandle = NULL;
    }

    timeEndPeriod(1);

    CoUninitialize();
    return hr;
}

///////////////////////////////////////////////////////////////////////////////////
// capture thread

DWORD
WasapiUser::CaptureEntry(LPVOID lpThreadParameter)
{
    WasapiUser* self = (WasapiUser*)lpThreadParameter;
    return self->CaptureMain();
}

DWORD
WasapiUser::CaptureMain(void)
{
    bool    stillRecording   = true;
    HANDLE  waitArray[2]   = {m_shutdownEvent, m_audioSamplesReadyEvent};
    int     waitArrayCount;
    DWORD   timeoutMillisec;
    HANDLE  mmcssHandle    = NULL;
    DWORD   mmcssTaskIndex = 0;
    DWORD   waitResult;
    HRESULT hr             = 0;
    
    HRG(CoInitializeEx(NULL, COINIT_MULTITHREADED));

    timeBeginPeriod(1);

    dprintf("D: %s AvSetMmThreadCharacteristics(%S)\n",
        __FUNCTION__,
        WWSchedulerTaskTypeToStr(m_schedulerTaskType));

    mmcssHandle = AvSetMmThreadCharacteristics(
        WWSchedulerTaskTypeToStr(m_schedulerTaskType),
        &mmcssTaskIndex);
    if (NULL == mmcssHandle) {
        dprintf("Unable to enable MMCSS on render thread: 0x%08x\n",
            GetLastError());
    }

    if (m_dataFeedMode == WWDFMTimerDriven) {
        waitArrayCount  = 1;
        timeoutMillisec = m_latencyMillisec / 2;
    } else {
        waitArrayCount  = 2;
        timeoutMillisec = INFINITE;
    }

    while (stillRecording) {
        waitResult = WaitForMultipleObjects(
            waitArrayCount, waitArray, FALSE, timeoutMillisec);
        switch (waitResult) {
        case WAIT_OBJECT_0 + 0:     // m_shutdownEvent
            stillRecording = false;
            break;
        case WAIT_OBJECT_0 + 1:     // m_audioSamplesReadyEvent
            // only in EventDriven mode
            stillRecording = AudioSamplesRecvProc();
            break;
        case WAIT_TIMEOUT:
            // only in TimerDriven mode
            stillRecording = AudioSamplesRecvProc();
            break;
        default:
            break;
        }
    }

end:
    if (NULL != mmcssHandle) {
        AvRevertMmThreadCharacteristics(mmcssHandle);
        mmcssHandle = NULL;
    }

    timeEndPeriod(1);

    CoUninitialize();
    return hr;
}

bool
WasapiUser::AudioSamplesRecvProc(void)
{
    bool    result     = true;
    UINT32  packetLength = 0;
    UINT32  numFramesAvailable = 0;
    DWORD   flags      = 0;
    BYTE    *pData     = NULL;
    HRESULT hr         = 0;
    UINT64  devicePosition = 0;
    int     writeFrames = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    HRG(m_captureClient->GetNextPacketSize(&packetLength));

    if (packetLength == 0) {
        goto end;
    }
        
    numFramesAvailable = packetLength;
    flags = 0;

    HRG(m_captureClient->GetBuffer(&pData,
        &numFramesAvailable, &flags, &devicePosition, NULL));

    if ((m_capturedPcmData->nFrames - m_capturedPcmData->posFrame) < (int)numFramesAvailable) {
        HRG(m_captureClient->ReleaseBuffer(numFramesAvailable));
        result = false;
        goto end;
    }

    if (flags & AUDCLNT_BUFFERFLAGS_DATA_DISCONTINUITY) {
        ++m_glitchCount;
    }

    writeFrames = (int)(numFramesAvailable);

    if (flags & AUDCLNT_BUFFERFLAGS_SILENT) {
        // record silence
        dprintf("flags & AUDCLNT_BUFFERFLAGS_SILENT\n");
        memset(&m_capturedPcmData->stream[m_capturedPcmData->posFrame * m_frameBytes],
            0, writeFrames * m_frameBytes);
    } else {
        m_capturedPcmData->posFrame,
            devicePosition;

        dprintf("numFramesAvailable=%d fb=%d pos=%d devPos=%lld nextPos=%d te=%d\n",
            numFramesAvailable, m_frameBytes,
            m_capturedPcmData->posFrame,
            devicePosition,
            (m_capturedPcmData->posFrame + numFramesAvailable),
            !!(flags & AUDCLNT_BUFFERFLAGS_TIMESTAMP_ERROR));

        memcpy(&m_capturedPcmData->stream[m_capturedPcmData->posFrame * m_frameBytes],
            pData, writeFrames * m_frameBytes);
    }
    m_capturedPcmData->posFrame += writeFrames;

    HRG(m_captureClient->ReleaseBuffer(numFramesAvailable));

end:
    ReleaseMutex(m_mutex);
    return result;
}

/*
/////////////////////////////////////////////////////////////////////////
// test code of new features on Windows 7 

HRESULT
WasapiUser::SetDeviceSampleRate(int id, int sampleRate)
{
    HRESULT              hr;
    WAVEFORMATEX         *waveFormat = NULL;
    WAVEFORMATEXTENSIBLE *wfex       = NULL;
    REFERENCE_TIME       hnsDefaultDevicePeriod;
    REFERENCE_TIME       hnsMinimumDevicePeriod;
    IAudioClockAdjustment *audioClockAdjustment = NULL;

    assert(0 <= id && id < (int)m_deviceInfo.size());

    assert(m_deviceCollection);
    assert(!m_deviceToUse);

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));

    HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

    assert(!waveFormat);
    HRG(m_audioClient->GetMixFormat(&waveFormat));
    assert(waveFormat);

    wfex = (WAVEFORMATEXTENSIBLE*)waveFormat;
    dprintf("original Mix Format:\n");
    WWWaveFormatDebug(waveFormat);
    WWWFEXDebug(wfex);

    HRG(m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_SHARED,
        AUDCLNT_STREAMFLAGS_RATEADJUST,
        100 * 10000,
        0,
        waveFormat,
        NULL));
    dprintf("m_audioClient->Initialize() %08x\n", hr);

    HRG(m_audioClient->GetService(IID_PPV_ARGS(&audioClockAdjustment)));

    HRG(audioClockAdjustment->SetSampleRate((float)sampleRate));
    dprintf("IAudioClockAdjustment::SetSampleRate(%d) %08x\n", sampleRate, hr);

end:
    SafeRelease(&audioClockAdjustment);
    SafeRelease(&m_deviceToUse);
    SafeRelease(&m_audioClient);

    if (waveFormat) {
        CoTaskMemFree(waveFormat);
        waveFormat = NULL;
    }

    return hr;
}

*/

