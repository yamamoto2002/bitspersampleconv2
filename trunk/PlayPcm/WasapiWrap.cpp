#include "WasapiWrap.h"
#include "WWUtil.h"
#include <avrt.h>
#include <assert.h>
#include <functiondiscoverykeys.h>
#include <strsafe.h>


WWDeviceInfo::WWDeviceInfo(int id, const wchar_t * name)
{
    this->id = id;
    wcsncpy(this->name, name, WW_DEVICE_NAME_COUNT-1);
}

/*
void
WWPcmData::Init(int samples)
{
    bitsPerSample = 16;
    nChannels = 2;
    int nSamplesPerSec = 96000;
    nFrames = samples;
    posFrame = 0;
    
    stream = (BYTE*)malloc(samples * 4);
    for (int i=0; i<samples*4; ++i) {
        stream[i] = rand();
    }
}
*/

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


WasapiWrap::WasapiWrap(void)
{
    m_deviceCollection = NULL;
    m_deviceToUse      = NULL;
    m_shutdownEvent    = NULL;
    m_audioSamplesReadyEvent = NULL;
    m_audioClient      = NULL;
    m_mixFormat        = NULL;
    m_frameBytes       = 0;
    m_bufferSamples    = 0;
    m_renderClient     = NULL;
    m_renderThread     = NULL;
    m_pcmData          = NULL;
    m_mutex            = NULL;
}


WasapiWrap::~WasapiWrap(void)
{
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);
}


HRESULT
WasapiWrap::Init(void)
{
    HRESULT hr;
    
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);

    HRR(CoInitializeEx(NULL, COINIT_MULTITHREADED));

    assert(!m_mutex);
    m_mutex = CreateMutex(
        NULL, FALSE, NULL);
}

void
WasapiWrap::Term(void)
{
    SafeRelease(&m_deviceCollection);

    assert(!m_deviceToUse);

    if (m_mutex) {
        CloseHandle(m_mutex);
        m_mutex = NULL;
    }

    CoUninitialize();
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
WasapiWrap::DoDeviceEnumeration(void)
{
    HRESULT hr = 0;
    IMMDeviceEnumerator *deviceEnumerator = NULL;

    m_deviceInfo.clear();

    HRR(CoCreateInstance(__uuidof(MMDeviceEnumerator),
        NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&deviceEnumerator)));
    
    HRR(deviceEnumerator->EnumAudioEndpoints(
        eRender, DEVICE_STATE_ACTIVE, &m_deviceCollection));

    UINT nDevices = 0;
    HRG(m_deviceCollection->GetCount(&nDevices));

    for (UINT i=0; i<nDevices; ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];
        HRG(DeviceNameGet(m_deviceCollection, i, name, sizeof name));

        for (int j=0; j<wcslen(name); ++j) {
            if (name[j] < 0x20 || 127 <= name[j]) {
                name[j] = L'?';
            }
        }

        m_deviceInfo.push_back(WWDeviceInfo(i, name));
    }

end:
    SafeRelease(&deviceEnumerator);
    return hr;
}

int
WasapiWrap::GetDeviceCount(void)
{
    assert(m_deviceCollection);
    return (int)m_deviceInfo.size();
}

bool
WasapiWrap::GetDeviceName(int id, LPWSTR name, size_t nameBytes)
{
    assert(0 <= id && id < (int)m_deviceInfo.size());

    wcsncpy(name, m_deviceInfo[id].name, nameBytes/sizeof name[0] -1);
    return true;
}

HRESULT
WasapiWrap::ChooseDevice(int id)
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

static void
MixFormatDebug(WAVEFORMATEX *v)
{
    printf(
        "  cbSize=%d\n"
        "  nAvgBytesPerSec=%d\n"
        "  nBlockAlign=%d\n"
        "  nChannels=%d\n"
        "  nSamplesPerSec=%d\n"
        "  wBitsPerSample=%d\n"
        "  wFormatTag=0x%x\n",
        v->cbSize,
        v->nAvgBytesPerSec,
        v->nBlockAlign,
        v->nChannels,
        v->nSamplesPerSec,
        v->wBitsPerSample,
        v->wFormatTag);
}

static void
WFEXDebug(WAVEFORMATEXTENSIBLE *v)
{
    printf(
        "  dwChannelMask=0x%x\n"
        "  Samples.wValidBitsPerSample=%d\n"
        "  SubFormat=0x%x\n",
        v->dwChannelMask,
        v->Samples.wValidBitsPerSample,
        v->SubFormat);
}

HRESULT
WasapiWrap::Setup(int sampleRate, int latencyMillisec)
{
    HRESULT hr = 0;
    UINT32  renderBufferBytes;

    m_shutdownEvent = CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_shutdownEvent);

    m_audioSamplesReadyEvent =
        CreateEventEx(NULL, NULL, 0, EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(m_audioSamplesReadyEvent);

    assert(m_deviceToUse);
    assert(!m_audioClient);
    HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

    assert(!m_mixFormat);
    HRG(m_audioClient->GetMixFormat(&m_mixFormat));
    assert(m_mixFormat);

    if (m_mixFormat->wFormatTag != WAVE_FORMAT_EXTENSIBLE) {
        printf("E: unsupported device ! mixformat == 0x%08x\n", m_mixFormat->wFormatTag);
        hr = E_FAIL;
        goto end;
    }

    WAVEFORMATEXTENSIBLE * wfex = (WAVEFORMATEXTENSIBLE*)m_mixFormat;
    wfex->SubFormat = KSDATAFORMAT_SUBTYPE_PCM;
    wfex->Format.wBitsPerSample = 16;
    wfex->Format.nSamplesPerSec = sampleRate;

    wfex->Format.nBlockAlign = (m_mixFormat->wBitsPerSample / 8) * m_mixFormat->nChannels;
    wfex->Format.nAvgBytesPerSec = wfex->Format.nSamplesPerSec*wfex->Format.nBlockAlign;
    wfex->Samples.wValidBitsPerSample = 16;

    MixFormatDebug(m_mixFormat);
    WFEXDebug(wfex);
    
    HRG(m_audioClient->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE,m_mixFormat,NULL));

    m_frameBytes = m_mixFormat->nBlockAlign;
    
    REFERENCE_TIME bufferDuration = latencyMillisec*10000;

    hr = m_audioClient->Initialize(
        AUDCLNT_SHAREMODE_EXCLUSIVE,
        AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST, 
        bufferDuration, bufferDuration, m_mixFormat, NULL);
    if (hr == AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED) {
        HRG(m_audioClient->GetBufferSize(&m_bufferSamples));

        SafeRelease(&m_audioClient);

        bufferDuration = (REFERENCE_TIME)(
            10000.0 *                         // (REFERENCE_TIME / ms) *
            1000 *                            // (ms / s) *
            m_bufferSamples /                 // frames /
            m_mixFormat->nSamplesPerSec +     // (frames / s)
            0.5);

        HRG(m_deviceToUse->Activate(
        __uuidof(IAudioClient), CLSCTX_INPROC_SERVER, NULL, (void**)&m_audioClient));

        hr = m_audioClient->Initialize(
            AUDCLNT_SHAREMODE_EXCLUSIVE, 
            AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST, 
            bufferDuration, 
            bufferDuration, 
            m_mixFormat, 
            NULL);
    }
    if (FAILED(hr)) {
        printf("E: audioClient->Initialize failed 0x%08x\n", hr);
        goto end;
    }

    HRG(m_audioClient->GetBufferSize(&m_bufferSamples));
    HRG(m_audioClient->SetEventHandle(m_audioSamplesReadyEvent));
    HRG(m_audioClient->GetService(IID_PPV_ARGS(&m_renderClient)));

    renderBufferBytes = m_bufferSamples * m_frameBytes;


end:
    return hr;
}

void
WasapiWrap::Unsetup(void)
{
    if (m_renderThread) {
        SetEvent(m_renderThread);
        WaitForSingleObject(m_renderThread, INFINITE);
        CloseHandle(m_renderThread);
        m_renderThread = NULL;
    }

    if (m_shutdownEvent) {
        CloseHandle(m_shutdownEvent);
        m_shutdownEvent = NULL;
    }
    if (m_audioSamplesReadyEvent) {
        CloseHandle(m_audioSamplesReadyEvent);
        m_audioSamplesReadyEvent = NULL;
    }

    SafeRelease(&m_deviceToUse);
    SafeRelease(&m_audioClient);
    SafeRelease(&m_renderClient);

    if (m_mixFormat) {
        CoTaskMemFree(m_mixFormat);
        m_mixFormat = NULL;
    }
}

HRESULT
WasapiWrap::Start(WWPcmData *pcm)
{
    BYTE *pData = NULL;
    HRESULT hr = 0;

    m_renderThread = CreateThread(NULL, 0, RenderEntry, this, 0, NULL);
    assert(m_renderThread);

    assert(m_renderClient);
    HRG(m_renderClient->GetBuffer(m_bufferSamples, &pData));

    assert(m_bufferSamples <= pcm->nFrames);
    CopyMemory(pData, pcm->stream, m_bufferSamples * m_frameBytes);
    HRG(m_renderClient->ReleaseBuffer(m_bufferSamples, 0));

    pcm->posFrame = m_bufferSamples;
    m_pcmData = pcm;

    assert(m_audioClient);
    HRG(m_audioClient->Start());

end:
    return hr;
}

void
WasapiWrap::Stop(void)
{
    HRESULT hr = 0;

    if (m_shutdownEvent) {
        SetEvent(m_shutdownEvent);
    }

    if (m_audioClient) {
        m_audioClient->Stop();
    }

    if (m_renderThread) {
        WaitForSingleObject(m_renderThread, INFINITE);

        CloseHandle(m_renderThread);
        m_renderThread = NULL;
    }

    if (m_pcmData) {
        m_pcmData = NULL;
    }
}

bool
WasapiWrap::Run(int millisec)
{
    DWORD rv = WaitForSingleObject(m_renderThread, millisec);
    if (rv == WAIT_TIMEOUT) {
        return false;
    }
    return true;
}

/////////////////////////////////////////////////////////////////////////////////
// callbacks

DWORD
WasapiWrap::RenderEntry(LPVOID lpThreadParameter)
{
    WasapiWrap* self = (WasapiWrap*)lpThreadParameter;
    return self->RenderMain();
}

int
WasapiWrap::GetTotalFrameNum(void)
{
    if (!m_pcmData) {
        return 0;
    }

    return m_pcmData->nFrames;
}

int
WasapiWrap::GetPosFrame(void)
{
    int result = 0;

    assert(m_mutex);

    WaitForSingleObject(m_mutex, INFINITE);
    if (m_pcmData) {
        result = m_pcmData->posFrame;
    }
    ReleaseMutex(m_mutex);

    return result;
}

bool
WasapiWrap::AudioSamplesReadyProc(void)
{
    bool result = true;
    UINT32 *pFrames = NULL;
    BYTE *pData = NULL;
    HRESULT hr = 0;
    int copyBytes = 0;

    WaitForSingleObject(m_mutex, INFINITE);

    copyBytes = m_bufferSamples;
    if (m_pcmData->nFrames < m_pcmData->posFrame + copyBytes) {
        copyBytes = m_pcmData->nFrames - m_pcmData->posFrame;
    }

    if (copyBytes <= 0) {
        result = false;
        goto end;
    }

    pFrames = (UINT32 *)m_pcmData->stream;
    pFrames += m_pcmData->posFrame;

    assert(m_renderClient);
    hr = m_renderClient->GetBuffer(m_bufferSamples, &pData);
    if (FAILED(hr)) {
        result = false;
        goto end;
    }

    CopyMemory(pData, pFrames, copyBytes * m_frameBytes);
    if (0 < m_bufferSamples - copyBytes) {
        memset(&pData[copyBytes*m_frameBytes], 0,
            (m_bufferSamples - copyBytes)*m_frameBytes);
    }

    hr = m_renderClient->ReleaseBuffer(m_bufferSamples, 0);
    if (FAILED(hr)) {
        result = false;
        goto end;
    }

    m_pcmData->posFrame += copyBytes;

end:
    ReleaseMutex(m_mutex);
    return result;
}

DWORD
WasapiWrap::RenderMain(void)
{
    bool stillPlaying = true;
    HANDLE waitArray[2] = {m_shutdownEvent, m_audioSamplesReadyEvent};
    HANDLE mmcssHandle = NULL;
    DWORD mmcssTaskIndex = 0;
    DWORD waitResult;
    HRESULT hr = 0;
    
    HRG(CoInitializeEx(NULL, COINIT_MULTITHREADED));

    mmcssHandle = AvSetMmThreadCharacteristics(L"Audio", &mmcssTaskIndex);
    if (NULL == mmcssHandle) {
        printf("Unable to enable MMCSS on render thread: %d\n", GetLastError());
    }

    while (stillPlaying) {
        waitResult = WaitForMultipleObjects(2, waitArray, FALSE, INFINITE);
        switch (waitResult) {
        case WAIT_OBJECT_0 + 0:     // m_shutdownEvent
            stillPlaying = false;
            break;
        case WAIT_OBJECT_0 + 1:     // m_audioSamplesReadyEvent
            stillPlaying = AudioSamplesReadyProc();
            break;
        }
    }

end:
    if (NULL != mmcssHandle) {
        AvRevertMmThreadCharacteristics(mmcssHandle);
        mmcssHandle = NULL;
    }

    CoUninitialize();
    return hr;
}



