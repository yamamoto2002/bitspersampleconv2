#include "WWPcmData.h"
#include "WWUtil.h"
#include <assert.h>
#include <malloc.h>
#include <stdint.h>

// 日本語 UTF-8

const char *
WWPcmDataContentTypeToStr(WWPcmDataContentType w)
{
    switch (w) {
    case WWPcmDataContentSilence: return "Silence";
    case WWPcmDataContentPcmData: return "PcmData";
    case WWPcmDataContentSplice:  return "Splice";
    default: return "unknown";
    }
}

const char *
WWPcmDataFormatTypeToStr(WWPcmDataFormatType w)
{
    switch (w) {
    case WWPcmDataFormatSint16: return "Sint16";
    case WWPcmDataFormatSint24: return "Sint24";
    case WWPcmDataFormatSint32V24: return "Sint32V24";
    case WWPcmDataFormatSint32: return "Sint32";
    case WWPcmDataFormatSfloat: return "Sfloat";
    default: return "unknown";
    }
}

int WWPcmDataFormatTypeToBitsPerSample(WWPcmDataFormatType t)
{
    static const int result[WWPcmDataFormatNUM]
        = { 16, 24, 32, 32, 32 };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return -1;
    }
    return result[t];
}

int WWPcmDataFormatTypeToValidBitsPerSample(WWPcmDataFormatType t)
{
    static const int result[WWPcmDataFormatNUM]
        = { 16, 24, 24, 32, 32 };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return -1;
    }
    return result[t];
}

bool WWPcmDataFormatTypeIsFloat(WWPcmDataFormatType t)
{
    static const bool result[WWPcmDataFormatNUM]
        = { false, false, false, false, true };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return false;
    }
    return result[t];
}

bool WWPcmDataFormatTypeIsInt(WWPcmDataFormatType t)
{
    static const bool result[WWPcmDataFormatNUM]
        = { true, true, true, true, false };

    if (t < 0 || WWPcmDataFormatNUM <= t) {
        assert(0);
        return false;
    }
    return result[t];
}

void
WWPcmData::Term(void)
{
    dprintf("D: %s() stream=%p\n", __FUNCTION__, stream);

    free(stream);
    stream = NULL;
}

WWPcmData::~WWPcmData(void)
{
}

void
WWPcmData::CopyFrom(WWPcmData *rhs)
{
    *this = *rhs;

    next = NULL;

    int bytes = nFrames * 4;

    stream = (BYTE*)malloc(bytes);
    CopyMemory(stream, rhs->stream, bytes);
}

bool
WWPcmData::Init(
        int aId, WWPcmDataFormatType aFormat, int anChannels,
        int anFrames, int aframeBytes, WWPcmDataContentType dataType)
{
    id       = aId;
    format   = aFormat;
    contentType = dataType;
    next     = NULL;
    posFrame = 0;
    nChannels = anChannels;
    // メモリ確保に成功してからフレーム数をセットする。
    nFrames  = 0;
    stream   = NULL;

    long bytes = anFrames * aframeBytes;
    if (0xffffffff < bytes || bytes < 0) {
        return false;
    }

    BYTE *p = (BYTE *)malloc((size_t)bytes);
    if (NULL == p) {
        // 失敗…
        return false;
    }

    ZeroMemory(p, (size_t)bytes);
    nFrames = anFrames;
    stream = p;
    return true;
}

int
WWPcmData::GetSampleValueInt(int ch, int posFrame)
{
    assert(format != WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return 0;
    }

    int result = 0;
    switch (format) {
    case WWPcmDataFormatSint16:
        {
            short *p = (short*)(&stream[2 * (nChannels * posFrame + ch)]);
            result = *p;
        }
        break;
    case WWPcmDataFormatSint24:
        {
            // bus error回避。x86にはbus error無いけど一応。
            unsigned char *p =
                (unsigned char*)(&stream[3 * (nChannels * posFrame + ch)]);

            result =
                (((unsigned int)p[0])<<8) +
                (((unsigned int)p[1])<<16) +
                (((unsigned int)p[2])<<24);
            result /= 256;
        }
        break;
    case WWPcmDataFormatSint32V24:
        {
            int *p = (int*)(&stream[4 * (nChannels * posFrame + ch)]);
            result = ((*p)/256);
        }
        break;
    case WWPcmDataFormatSint32:
        {
            // mallocで確保したバッファーなので、bus errorは起きない。
            int *p = (int*)(&stream[4 * (nChannels * posFrame + ch)]);
            result = *p;
        }
        break;
    default:
        assert(0);
        break;
    }

    return result;
}

float
WWPcmData::GetSampleValueFloat(int ch, int posFrame)
{
    assert(format == WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return 0;
    }

    float *p = (float *)(&stream[4 * (nChannels * posFrame + ch)]);
    return *p;
}

bool
WWPcmData::SetSampleValueInt(int ch, int posFrame, int value)
{
    assert(format != WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return false;
    }

    switch (format) {
    case WWPcmDataFormatSint16:
        {
            short *p =
                (short*)(&stream[2 * (nChannels * posFrame + ch)]);
            *p = (short)value;
        }
        break;
    case WWPcmDataFormatSint24:
        {
            // bus error回避。x86にはbus error無いけど一応。
            unsigned char *p =
                (unsigned char*)(&stream[3 * (nChannels * posFrame + ch)]);
            p[0] = (unsigned char)(value & 0xff);
            p[1] = (unsigned char)((value>>8) & 0xff);
            p[2] = (unsigned char)((value>>16) & 0xff);
        }
        break;
    case WWPcmDataFormatSint32V24:
        {
            unsigned char *p =
                (unsigned char*)(&stream[4 * (nChannels * posFrame + ch)]);
            p[0] = 0;
            p[1] = (unsigned char)(value & 0xff);
            p[2] = (unsigned char)((value>>8) & 0xff);
            p[3] = (unsigned char)((value>>16) & 0xff);
        }
        break;
    case WWPcmDataFormatSint32:
        {
            // mallocで確保したバッファーなので、bus errorは起きない。
            int *p = (int*)(&stream[4 * (nChannels * posFrame + ch)]);
            *p = value;
        }
        break;
    default:
        assert(0);
        break;
    }

    return true;
}

bool
WWPcmData::SetSampleValueFloat(int ch, int posFrame, float value)
{
    assert(format == WWPcmDataFormatSfloat);
    assert(0 <= ch && ch < nChannels);

    if (posFrame < 0 ||
        nFrames <= posFrame) {
        return false;
    }

    float *p = (float *)(&stream[4 * (nChannels * posFrame + ch)]);
    *p = value;
    return true;
}

struct PcmSpliceInfoFloat {
    float dydx;
    float y;
};

struct PcmSpliceInfoInt {
    int deltaX;
    int error;
    int ystep;
    int deltaError;
    int deltaErrorDirection;
    int y;
};

void
WWPcmData::UpdateSpliceDataWithStraightLine(
        WWPcmData *fromPcmData, int fromPosFrame,
        WWPcmData *toPcmData,   int toPosFrame)
{
    assert(0 < nFrames);

    switch (fromPcmData->format) {
    case WWPcmDataFormatSfloat:
        {
            // floatは、簡単。
            PcmSpliceInfoFloat *p = (PcmSpliceInfoFloat*)_malloca(nChannels * sizeof(PcmSpliceInfoFloat));
            assert(p);

            for (int ch=0; ch<nChannels; ++ch) {
                float y0 = fromPcmData->GetSampleValueFloat(ch, fromPosFrame);
                float y1 = toPcmData->GetSampleValueFloat(ch, toPosFrame);
                p[ch].dydx = (y1 - y0)/(nFrames);
                p[ch].y = y0;
            }

            for (int x=0; x<nFrames; ++x) {
                for (int ch=0; ch<nChannels; ++ch) {
                    SetSampleValueFloat(ch, x, p[ch].y);
                    p[ch].y += p[ch].dydx;
                }
            }

            _freea(p);
            p = NULL;
        }
        break;
    default:
        {
            // Bresenham's line algorithm的な物
            PcmSpliceInfoInt *p = (PcmSpliceInfoInt*)_malloca(nChannels * sizeof(PcmSpliceInfoInt));
            assert(p);

            for (int ch=0; ch<nChannels; ++ch) {
                int y0 = fromPcmData->GetSampleValueInt(ch, fromPosFrame);
                int y1 = toPcmData->GetSampleValueInt(ch, toPosFrame);
                p[ch].deltaX = nFrames;
                p[ch].error  = p[ch].deltaX/2;
                p[ch].ystep  = ((int64_t)y1 - y0)/p[ch].deltaX;
                p[ch].deltaError = abs(y1 - y0) - abs(p[ch].ystep * p[ch].deltaX);
                p[ch].deltaErrorDirection = (y1-y0) >= 0 ? 1 : -1;
                p[ch].y = y0;
            }

            for (int x=0; x<nFrames; ++x) {
                for (int ch=0; ch<nChannels; ++ch) {
                    SetSampleValueInt(ch, x, p[ch].y);
                    // printf("(%d %d)", x, y);
                    p[ch].y += p[ch].ystep;
                    p[ch].error -= p[ch].deltaError;
                    if (p[ch].error < 0) {
                        p[ch].y += p[ch].deltaErrorDirection;
                        p[ch].error += p[ch].deltaX;
                    }
                }
            }
            // printf("\n");

            _freea(p);
            p = NULL;
        }
        break;
    }
}