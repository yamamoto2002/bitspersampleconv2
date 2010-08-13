// 京 UTF-8

#include "WasapiUser.h"
#include "WWUtil.h"
#include <avrt.h>
#include <assert.h>
#include <functiondiscoverykeys.h>
#include <strsafe.h>
#include <mmsystem.h>

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
    free(stream);
    stream = NULL;
}

WWPcmData::~WWPcmData(void)
{
    assert(!stream);
}

void
WWPcmData::CopyFrom(WWPcmData *rhs)
{
    *this = *rhs;

    int bytes = nFrames * 4;

    stream = (BYTE*)malloc(bytes);
    CopyMemory(stream, rhs->stream, bytes);
}

static wchar_t*
WWSchedulerTaskTypeToStr(WWSchedulerTaskType t)
{
    switch (t) {
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
    m_pcmData          = NULL;
    m_mutex            = NULL;
    m_coInitializeSuccess = false;
    m_glitchCount      = 0;
    m_schedulerTaskType = WWSTTAudio;
}

WasapiUser::~WasapiUser(void)
{
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);
}

HRESULT
WasapiUser::Init(void)
{
    HRESULT hr = S_OK;
    
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);

    hr = CoInitializeEx(NULL, COINIT_MULTITHREADED);
    if (S_OK == hr) {
        m_coInitializeSuccess = true;
    } else {
        dprintf("WasapiUser::Init() CoInitializeEx() failed %08x\n", hr);
        hr = S_OK;
    }

    assert(!m_mutex);
    m_mutex = CreateMutex(
        NULL, FALSE, NULL);

    return hr;
}

void
WasapiUser::Term(void)
{
    SafeRelease(&m_deviceCollection);

    assert(!m_deviceToUse);

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

    m_schedulerTaskType = t;
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
            wfex->Format.wBitsPerSample = bitsPerSample;
            wfex->Format.nSamplesPerSec = sampleRate;

            wfex->Format.nBlockAlign =
                (bitsPerSample / 8) * waveFormat->nChannels;
            wfex->Format.nAvgBytesPerSec =
                wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
            wfex->Samples.wValidBitsPerSample = bitsPerSample;

            dprintf("preferred Format:\n");
            WWWaveFormatDebug(waveFormat);
            WWWFEXDebug(wfex);

            hr = m_audioClient->IsFormatSupported(
                AUDCLNT_SHAREMODE_EXCLUSIVE,waveFormat,NULL);
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

    if (id < 0) {
        goto end;
    }

    assert(m_deviceCollection);
    assert(!m_deviceToUse);

    HRG(m_deviceCollection->Item(id, &m_deviceToUse));

end:
    SafeRelease(&m_deviceCollection);
    return hr;
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

    m_dataFeedMode        = mode;
    m_latencyMillisec     = latencyMillisec;
    m_sampleRate          = sampleRate;
    m_dataBitsPerSample   = bitsPerSample;
    m_deviceBitsPerSample = m_dataBitsPerSample;
    if (24 == m_deviceBitsPerSample) {
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

    wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
    wfex->Format.wBitsPerSample = m_deviceBitsPerSample;
    wfex->Format.nSamplesPerSec = sampleRate;

    wfex->Format.nBlockAlign =
        (m_deviceBitsPerSample / 8) * waveFormat->nChannels;
    wfex->Format.nAvgBytesPerSec =
        wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
    wfex->Samples.wValidBitsPerSample = m_deviceBitsPerSample;

    dprintf("preferred Format:\n");
    WWWaveFormatDebug(waveFormat);
    WWWFEXDebug(wfex);
    
    HRG(m_audioClient->IsFormatSupported(
        AUDCLNT_SHAREMODE_EXCLUSIVE,waveFormat,NULL));

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

    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_EXCLUSIVE, streamFlags, 
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
            AUDCLNT_SHAREMODE_EXCLUSIVE, streamFlags, 
            bufferDuration, bufferPeriodicity, waveFormat, NULL);
    }
    if (FAILED(hr)) {
        dprintf("E: audioClient->Initialize failed 0x%08x\n", hr);
        goto end;
    }

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
    if (m_audioSamplesReadyEvent) {
        CloseHandle(m_audioSamplesReadyEvent);
        m_audioSamplesReadyEvent = NULL;
    }

    SafeRelease(&m_captureClient);
    SafeRelease(&m_renderClient);
    SafeRelease(&m_audioClient);
    SafeRelease(&m_deviceToUse);
}

HRESULT
WasapiUser::Start(void)
{
    HRESULT hr      = 0;
    BYTE    *pData  = NULL;
    UINT32  nFrames = 0;
    DWORD   flags   = 0;

    assert(m_pcmData);

    assert(!m_shutdownEvent);
    m_shutdownEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_shutdownEvent);

    switch (m_dataFlow) {
    case eRender:
        m_thread = CreateThread(NULL, 0, RenderEntry, this, 0, NULL);
        assert(m_thread);

        nFrames = m_bufferFrameNum;
        if (WWDFMTimerDriven == m_dataFeedMode) {
            UINT32 padding = 0; //< frame now now using
            HRG(m_audioClient->GetCurrentPadding(&padding));
            nFrames = m_bufferFrameNum - padding;
        }

        assert(m_renderClient);
        HRG(m_renderClient->GetBuffer(nFrames, &pData));

        memset(pData, 0, nFrames * m_frameBytes);

        HRG(m_renderClient->ReleaseBuffer(nFrames, 0));

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

    if (m_shutdownEvent) {
        SetEvent(m_shutdownEvent);
    }

    if (m_audioClient) {
        hr = m_audioClient->Stop();
        if (FAILED(hr)) {
            dprintf("E: %s m_audioClient->Stop() failed 0x%x\n", __FUNCTION__, hr);
        }
    }

    if (m_thread) {
        SetEvent(m_thread);
        WaitForSingleObject(m_thread, INFINITE);
        CloseHandle(m_thread);
        m_thread = NULL;
    }

    if (m_shutdownEvent) {
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

void
WasapiUser::SetOutputData(BYTE *data, int bytes)
{
    if (m_pcmData) {
        ClearPcmData();
    }

    m_pcmData = new WWPcmData();
    m_pcmData->nFrames = bytes/m_frameBytes;
    m_pcmData->posFrame = 0;

    // m_pcmData->stream create
    if (24 == m_dataBitsPerSample) {
        BYTE *p = WWStereo24ToStereo32(data, bytes);
        m_pcmData->stream = p;
        m_pcmData->nFrames = bytes /3 / 2; // 3==24bit, 2==stereo
    } else {
        BYTE *p = (BYTE *)malloc(bytes);
        memcpy(p, data, bytes);
        m_pcmData->stream = p;
    }
}

void
WasapiUser::ClearOutputData(void)
{
    ClearPcmData();
}

void
WasapiUser::ClearPcmData(void)
{
    if (m_pcmData) {
        m_pcmData->Term();
        delete m_pcmData;
        m_pcmData = NULL;
    }
}

int
WasapiUser::GetTotalFrameNum(void)
{
    if (!m_pcmData) {
        return 0;
    }

    return m_pcmData->nFrames;
}

int
WasapiUser::GetPosFrame(void)
{
    int result = 0;

    assert(m_mutex);

    //WaitForSingleObject(m_mutex, INFINITE);
    if (m_pcmData) {
        result = m_pcmData->posFrame;
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
    if (m_pcmData) {
        m_pcmData->posFrame = v;
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

    if (m_pcmData) {
        ClearPcmData();
    }

    /* 録音時は
     *   pcmData->posFrame: 有効な録音データのフレーム数
     *   pcmData->nFrames: 録音可能総フレーム数
     */
    m_pcmData = new WWPcmData();
    m_pcmData->posFrame = 0;
    m_pcmData->nFrames = bytes/m_frameBytes;
    m_pcmData->stream = (BYTE*)malloc(bytes);
}

int
WasapiUser::GetCapturedData(BYTE *data, int bytes)
{
    if (m_dataFlow != eCapture) {
        assert(0);
        return 0;
    }

    assert(m_pcmData);

    if (m_pcmData->posFrame * m_frameBytes < bytes) {
        bytes = m_pcmData->posFrame * m_frameBytes;
    }
    memcpy(data, m_pcmData->stream, bytes);

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

bool
WasapiUser::AudioSamplesSendProc(void)
{
    bool    result     = true;
    BYTE    *pFrames   = NULL;
    BYTE    *pData     = NULL;
    HRESULT hr         = 0;
    int     copyFrames = 0;
    int     writableFrames = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    writableFrames = m_bufferFrameNum;
    if (WWDFMTimerDriven == m_dataFeedMode) {
        UINT32 padding = 0; //< frame num now using
        HRG(m_audioClient->GetCurrentPadding(&padding));
        writableFrames = m_bufferFrameNum - padding;
    }

    copyFrames = writableFrames;
    if (m_pcmData->nFrames < m_pcmData->posFrame + copyFrames) {
        copyFrames = m_pcmData->nFrames - m_pcmData->posFrame;
    }

    if (copyFrames <= 0) {
        copyFrames = 0;
    } else {
        pFrames = (BYTE *)m_pcmData->stream;
        pFrames += m_pcmData->posFrame * m_frameBytes;
    }

    assert(m_renderClient);
    hr = m_renderClient->GetBuffer(writableFrames, &pData);
    if (FAILED(hr)) {
        result = false;
        goto end;
    }

    if (0 < copyFrames) {
        CopyMemory(pData, pFrames, copyFrames * m_frameBytes);
    }
    if (0 < writableFrames - copyFrames) {
        memset(&pData[copyFrames*m_frameBytes], 0,
            (m_bufferFrameNum - copyFrames)*m_frameBytes);
        /* dprintf("fc=%d bs=%d cb=%d memset %d bytes\n",
            m_footerCount, m_bufferFrameNum, copyFrames,
            (m_bufferFrameNum - copyFrames)*m_frameBytes);
        */
    }

    hr = m_renderClient->ReleaseBuffer(writableFrames, 0);
    if (FAILED(hr)) {
        result = false;
        goto end;
    }

    m_pcmData->posFrame += copyFrames;
    if (m_pcmData->nFrames <= m_pcmData->posFrame) {
        ++m_footerCount;
        if (m_footerNeedSendCount < m_footerCount) {
            result = false;
        }
    }

end:
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

    if (m_dataFeedMode == WWDFMTimerDriven) {
        waitArrayCount        = 1;
        m_footerNeedSendCount = FOOTER_SEND_FRAME_NUM * 2;
        timeoutMillisec       = m_latencyMillisec     / 2;
    } else {
        waitArrayCount        = 2;
        m_footerNeedSendCount = FOOTER_SEND_FRAME_NUM;
        timeoutMillisec       = INFINITE;
    }

    while (stillPlaying) {
        waitResult = WaitForMultipleObjects(
            waitArrayCount, waitArray, FALSE, timeoutMillisec);
        switch (waitResult) {
        case WAIT_OBJECT_0 + 0:     // m_shutdownEvent
            stillPlaying = false;
            break;
        case WAIT_OBJECT_0 + 1:     // m_audioSamplesReadyEvent
            // only in EventDriven mode
            stillPlaying = AudioSamplesSendProc();
            break;
        case WAIT_TIMEOUT:
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
    BYTE    *pFrames   = NULL;
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

    if ((m_pcmData->nFrames - m_pcmData->posFrame) < (int)numFramesAvailable) {
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
        memset(&m_pcmData->stream[m_pcmData->posFrame * m_frameBytes],
            0, writeFrames * m_frameBytes);
    } else {
        m_pcmData->posFrame,
            devicePosition;

        dprintf("numFramesAvailable=%d fb=%d pos=%d devPos=%lld nextPos=%d te=%d\n",
            numFramesAvailable, m_frameBytes,
            m_pcmData->posFrame,
            devicePosition,
            (m_pcmData->posFrame + numFramesAvailable),
            !!(flags & AUDCLNT_BUFFERFLAGS_TIMESTAMP_ERROR));

        memcpy(&m_pcmData->stream[m_pcmData->posFrame * m_frameBytes],
            pData, writeFrames * m_frameBytes);
    }
    m_pcmData->posFrame += writeFrames;

    HRG(m_captureClient->ReleaseBuffer(numFramesAvailable));

end:
    ReleaseMutex(m_mutex);
    return result;
}

/////////////////////////////////////////////////////////////////////////
// new features of Windows 7 

/*
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
    dprintf("IsFormatSupported=%08x\n", hr);

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

