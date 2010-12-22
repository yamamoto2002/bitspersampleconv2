#pragma once

// 日本語UTF-8

#include <d3d11.h>
#include <map>

/// 読み出し専用GPUメモリ管理情報
struct WWReadOnlyGpuBufferInfo {
    ID3D11Buffer             *pBuf;
    ID3D11ShaderResourceView *pSrv;
};

/// 読み書き可能GPUメモリ管理情報
struct WWReadWriteGpuBufferInfo {
    ID3D11Buffer              *pBuf;
    ID3D11UnorderedAccessView *pUav;
};

class WWDirectComputeUser {
public:
    WWDirectComputeUser(void);
    ~WWDirectComputeUser(void);

    HRESULT Init(void);
    void    Term(void);

    // ComputeShaderをコンパイルしてGPUに送る
    HRESULT CreateComputeShader(
        LPCWSTR path, LPCSTR entryPoint, ID3D11ComputeShader **ppCS);

    void DestroyComputeShader(ID3D11ComputeShader *pCS);

    // 入力データ(読み出し専用)をGPUメモリに送る
    HRESULT SendReadOnlyDataAndCreateShaderResourceView(
        unsigned int uElementSize,
        unsigned int uCount,
        void * pSendData,
        const char *name,
        ID3D11ShaderResourceView **ppSrv);

    void DestroyDataAndShaderResourceView(
        ID3D11ShaderResourceView * pSrv);

    /// 入出力可能データをGPUメモリに作成。
    /// @param pSendData NULLでも可。
    HRESULT CreateBufferAndUnorderedAccessView(
        unsigned int uElementSize,
        unsigned int uCount,
        void *pSendData,
        const char *name,
        ID3D11UnorderedAccessView **ppUav);

    void DestroyDataAndUnorderedAccessView(
        ID3D11UnorderedAccessView * pUav);

    // 実行。ブロックするらしいｗｗ
    HRESULT Run(
        ID3D11ComputeShader * pComputeShader,
        UINT nNumViews,
        ID3D11ShaderResourceView ** pShaderResourceViews,
        ID3D11Buffer * pCBCS,
        void * pCSData,
        DWORD dwNumDataBytes,
        ID3D11UnorderedAccessView * pUnorderedAccessView,
        UINT X,
        UINT Y,
        UINT Z);

    // 計算結果をGPUから取り出す。
    HRESULT RecvResultToCpuMemory(
            ID3D11UnorderedAccessView * pUav,
            void *dest,
            int bytes);

private:
    ID3D11Device*               m_pDevice;
    ID3D11DeviceContext*        m_pContext;

    HRESULT CreateComputeDevice(void);

    HRESULT CreateStructuredBuffer(
        unsigned int uElementSize,
        unsigned int uCount,
        void * pInitData,
        const char *name,
        ID3D11Buffer ** ppBufOut);

    HRESULT CreateBufferShaderResourceView(
        ID3D11Buffer * pBuffer,
        const char *name,
        ID3D11ShaderResourceView ** ppSrvOut);

    HRESULT CreateBufferUnorderedAccessView(
        ID3D11Buffer * pBuffer,
        const char *name,
        ID3D11UnorderedAccessView ** ppUavOut);

    std::map<ID3D11ShaderResourceView *, WWReadOnlyGpuBufferInfo> m_readGpuBufInfo;
    std::map<ID3D11UnorderedAccessView *, WWReadWriteGpuBufferInfo> m_rwGpuBufInfo;

};
