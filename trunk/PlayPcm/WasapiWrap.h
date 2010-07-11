#pragma once

#include <Windows.h>
#include <mmdeviceapi.h>
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

private:
    IMMDeviceCollection       *m_deviceCollection;
    IMMDevice                 *m_deviceToUse;
    std::vector<WWDeviceInfo> m_deviceInfo;
};

