#include <Windows.h>
#include <ddraw.h>
#include "WWUtil.h"
#include <assert.h>

extern "C" __declspec(dllexport)
int __stdcall
WWDirectDrawTest_Test(void)
{
    HRESULT              hr = E_FAIL;
    LPDIRECTDRAW7        pDirectDraw = NULL;
    LPDIRECTDRAWSURFACE7 pDDSPrimary = NULL;
    DDSURFACEDESC2 ddsd;

    HINSTANCE ddrawLib = LoadLibraryW( L"ddraw.dll" );
    if(NULL == ddrawLib) {
        dprintf("LoadLibrary ddraw.dll failed!\n");
        goto end;
    }
    HRESULT (WINAPI* fDirectDrawCreateEx)( GUID* lpGUID, void** lplpDD, REFIID iid, IUnknown* pUnkOuter ) =
        (HRESULT (WINAPI*)( GUID* lpGUID, void** lplpDD, REFIID iid, IUnknown* pUnkOuter ))GetProcAddress(ddrawLib, "DirectDrawCreateEx");
    if (NULL == fDirectDrawCreateEx) {
        dprintf("GetProcAddress DirectDrawCreateEx failed!\n");
        goto end;
    }

    HRG(fDirectDrawCreateEx(NULL, (LPVOID*)&pDirectDraw,
            IID_IDirectDraw7, NULL));
    assert(pDirectDraw);

    HRG(pDirectDraw->SetCooperativeLevel(NULL, DDSCL_NORMAL));

    ZeroMemory(&ddsd, sizeof ddsd);
    ddsd.dwSize = sizeof ddsd;
    ddsd.dwFlags        = DDSD_CAPS;
    ddsd.ddsCaps.dwCaps = DDSCAPS_PRIMARYSURFACE;

    HRG(pDirectDraw->CreateSurface(&ddsd, &pDDSPrimary, NULL));
    assert(pDDSPrimary);

    HRG(pDDSPrimary->Lock(NULL, &ddsd, 0, NULL));

    HRG(pDDSPrimary->Unlock(NULL));

end:
    SAFE_RELEASE(pDDSPrimary);
    SAFE_RELEASE(pDirectDraw);
    if (ddrawLib) {
        FreeLibrary(ddrawLib);
        ddrawLib = NULL;
    }

    return hr;
}
