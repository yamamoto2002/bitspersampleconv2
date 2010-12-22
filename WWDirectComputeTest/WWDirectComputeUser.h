#pragma once

// 日本語UTF-8

#include <d3d11.h>

class WWDirectComputeUser {
public:
    WWDirectComputeUser(void);
    ~WWDirectComputeUser(void);

    HRESULT Init(void);
    void    Term(void);

    /* ComputeShaderをコンパイルしてGPUに送る */
    HRESULT CreateComputeShader(
        LPCWSTR path, LPCSTR entryPoint, ID3D11ComputeShader **ppCS);

    void DestroyComputeShader(ID3D11ComputeShader *pCS);

    /* 入出力データ領域をGPUメモリに作成 */
    HRESULT CreateStructuredBuffer(
        unsigned int uElementSize,
        unsigned int uCount,
        void * pInitData,
        ID3D11Buffer ** ppBufOut);

    void DestroyStructuredBuffer(ID3D11Buffer * pBuf);
private:
    ID3D11Device*               m_pDevice;
    ID3D11DeviceContext*        m_pContext;

    ID3D11Buffer*               m_pBufIn;
    ID3D11Buffer*               m_pBufOut;

    ID3D11ShaderResourceView*   m_pBufInSRV;
    ID3D11UnorderedAccessView*  m_pBufOutUAV;

    HRESULT CreateComputeDevice(void);
};
