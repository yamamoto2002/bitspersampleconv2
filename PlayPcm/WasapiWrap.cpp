#include "WasapiWrap.h"
#include "WWUtil.h"
#include <assert.h>
#include <functiondiscoverykeys.h>
#include <strsafe.h>

WWDeviceInfo::WWDeviceInfo(int id, const wchar_t * name)
{
    this->id = id;
    wcsncpy(this->name, name, WW_DEVICE_NAME_COUNT-1);
}


WasapiWrap::WasapiWrap(void)
{
    m_deviceCollection = NULL;
    m_deviceToUse      = NULL;
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
}

void
WasapiWrap::Term(void)
{
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);

    CoUninitialize();
}


static HRESULT
DeviceNameGet(IMMDeviceCollection *dc, UINT id, wchar_t *name, size_t nameBytes)
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

    HRR(CoCreateInstance(__uuidof(MMDeviceEnumerator), NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&deviceEnumerator)));
    
    HRR(deviceEnumerator->EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, &m_deviceCollection));

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



end:
    SafeRelease(&m_deviceCollection);
    return hr;
}

