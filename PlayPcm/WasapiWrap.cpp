#include "WasapiWrap.h"
#include "WWUtil.h"
#include <assert.h>


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

    HRF(CoInitializeEx(NULL, COINIT_MULTITHREADED));
}

void
WasapiWrap::Term(void)
{
    assert(!m_deviceCollection);
    assert(!m_deviceToUse);

    CoUninitialize();
}

