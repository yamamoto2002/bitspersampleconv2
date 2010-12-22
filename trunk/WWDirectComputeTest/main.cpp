// 日本語UTF-8

#include "WWDirectComputeUser.h"
#include "WWUtil.h"
#include <assert.h>
#include <crtdbg.h>

int main(void)
{
#if defined(DEBUG) || defined(_DEBUG)
    _CrtSetDbgFlag( _CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF );
#endif

    HRESULT             hr    = S_OK;
    WWDirectComputeUser *pDCU = NULL;
    ID3D11ComputeShader *pCS  = NULL;

    pDCU = new WWDirectComputeUser();
    assert(pDCU);

    HRG(pDCU->Init());

    HRG(pDCU->CreateComputeShader(L"SincConvolution.hlsl", "CSMain", &pCS));

end:
    if (pCS) {
        pDCU->DestroyComputeShader(pCS);
        pCS = NULL;
    }

    if (pDCU) {
        pDCU->Term();
    }

    SAFE_DELETE(pDCU);
    return 0;
}
