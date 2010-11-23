#pragma once

// 日本語 UTF-8

#include <Windows.h>

/// PCMデータの用途。
enum WWPcmDataContentType {
    WWPcmDataContentPcmData,
    WWPcmDataContentSilence,
    WWPcmDataContentSplice,
};
const char *
WWPcmDataContentTypeToStr(WWPcmDataContentType w);

/// データフォーマット。
enum WWPcmDataFormatType {
    WWPcmDataFormatUnknown = -1,
    WWPcmDataFormatSint16,
    WWPcmDataFormatSint24,
    WWPcmDataFormatSint32V24,
    WWPcmDataFormatSint32,
    WWPcmDataFormatSfloat,

    WWPcmDataFormatNUM
};
const char *
WWPcmDataFormatTypeToStr(WWPcmDataFormatType w);
int WWPcmDataFormatTypeToBitsPerSample(WWPcmDataFormatType t);
int WWPcmDataFormatTypeToValidBitsPerSample(WWPcmDataFormatType t);
bool WWPcmDataFormatTypeIsFloat(WWPcmDataFormatType t);
bool WWPcmDataFormatTypeIsInt(WWPcmDataFormatType t);

/*
 * play
 *   pcmData->posFrame: playing position
 *   pcmData->nFrames: total frames to play (frame == sample point)
 * record
 *   pcmData->posFrame: available recorded frame num
 *   pcmData->nFrames: recording buffer size
 */
struct WWPcmData {
    int       id;
    WWPcmData *next;
    WWPcmDataFormatType format;
    WWPcmDataContentType contentType;
    int       nChannels;
    int       nFrames;
    int       posFrame;
    BYTE      *stream;

    WWPcmData(void) {
        id       = 0;
        next     = NULL;
        contentType = WWPcmDataContentPcmData;
        nChannels = 0;
        nFrames  = 0;
        posFrame = 0;
        stream   = NULL;
    }

    ~WWPcmData(void);

    bool Init(int id, WWPcmDataFormatType format, int nChannels,
        int nFrames, int frameBytes, WWPcmDataContentType dataType);
    void Term(void);

    void CopyFrom(WWPcmData *rhs);

    /** create splice data from the two adjacent sample data */
    void UpdateSpliceDataWithStraightLine(
        WWPcmData *fromPcmData, int fromPosFrame,
        WWPcmData *toPcmData,   int toPosFrame);

private:
    /** get sample value on posFrame.
     * 24 bit signed int value is returned when Sint32V24
     */
    int GetSampleValueInt(int ch, int posFrame);
    float GetSampleValueFloat(int ch, int posFrame);

    bool SetSampleValueInt(int ch, int posFrame, int value);
    bool SetSampleValueFloat(int ch, int posFrame, float value);
};

