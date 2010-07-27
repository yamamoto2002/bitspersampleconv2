#include "WWUtil.h"

BYTE*
WWStereo24ToStereo32(BYTE *data, int bytes)
{
    int nData = bytes / 3; // 3==24bit

    BYTE *p = (BYTE *)malloc(nData * 4);
    int fromPos = 0;
    int toPos = 0;
    for (int i=0; i<nData; ++i) {
        p[toPos++] = 0;
        p[toPos++] = data[fromPos++];
        p[toPos++] = data[fromPos++];
        p[toPos++] = data[fromPos++];
    }

    return p;
}

void
WWWaveFormatDebug(WAVEFORMATEX *v)
{
    dprintf(
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

void
WWWFEXDebug(WAVEFORMATEXTENSIBLE *v)
{
    dprintf(
        "  dwChannelMask=0x%x\n"
        "  Samples.wValidBitsPerSample=%d\n"
        "  SubFormat=0x%x\n",
        v->dwChannelMask,
        v->Samples.wValidBitsPerSample,
        v->SubFormat);
}

