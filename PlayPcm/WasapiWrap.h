#pragma once

#include <Windows.h>
#include <mmdeviceapi.h>
#include <vector>

class WasapiWrap {
public:
    WasapiWrap(void);
    ~WasapiWrap(void);

    HRESULT Init(void);
    void Term(void);

    // device enumeration
    HRESULT DoDeviceEnumeration(void);
    int GetDeviceCount(void);
    bool GetDeviceName(int id, LPWSTR name, int nameCount);
    HRESULT ChooseDevice(int id);

private:
    IMMDeviceCollection *m_deviceCollection;
    IMMDevice           *m_deviceToUse;
};

