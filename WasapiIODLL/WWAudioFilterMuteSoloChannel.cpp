// 日本語 UTF-8

#include "WWAudioFilterMuteSoloChannel.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
#include <stdint.h>
#include "WWWasapiIOUtil.h"

WWAudioFilterMuteSoloChannel::WWAudioFilterMuteSoloChannel(WWAFMSModeType mode, PCWSTR args)
{
    mMode = mode;

    // mEnableFlagsをargsから作る。
    WWCommaSeparatedIdxToFlagArray(args, mEnableFlags, sizeof mEnableFlags);
}

bool
WWAudioFilterMuteSoloChannel::IsMuteChannel(int ch) const
{
    assert(0 <= ch && ch < WW_CHANNEL_NUM);

    switch (mMode) {
    case WWAFMSMode_Mute:
        return mEnableFlags[ch] == true;
    case WWAFMSMode_Solo:
        return mEnableFlags[ch] != true;
    default:
        assert(0);
        return false;
    }
}

void
WWAudioFilterMuteSoloChannel::UpdateSampleFormat(
        int sampleRate,
        WWPcmDataSampleFormatType format,
        WWStreamType streamType, int numChannels)
{
    (void)sampleRate;
    mManip.UpdateFormat(format, streamType, numChannels);

    assert(mManip.NumChannels() <= WW_CHANNEL_NUM);
}

void
WWAudioFilterMuteSoloChannel::Filter(unsigned char *buff, int bytes)
{
    if (mManip.StreamType() == WWStreamDop) {
        FilterDoP(buff, bytes);
    } else {
        FilterPcm(buff, bytes);
    }
}

void
WWAudioFilterMuteSoloChannel::FilterDoP(unsigned char *buff, int bytes)
{
    // DoP DSD。
    int bytesPerSample = 3;
    int dsdOffs = 0;

    switch (mManip.SampleFormat()) {
    case WWPcmDataSampleFormatSint24:
        // b0 b1 marker
        bytesPerSample = 3;
        dsdOffs = 0;
        break;
    case WWPcmDataSampleFormatSint32V24:
        // 0 b0 b1 marker
        bytesPerSample = 4;
        dsdOffs = 1;
        break;
    default:
        assert(0);
        break;
    }

    for (int pos=0; pos < bytes; pos += bytesPerSample * mManip.NumChannels()) {
        for (int ch=0; ch<mManip.NumChannels(); ++ch) {
            if (IsMuteChannel(ch)) {
                buff[pos + ch*bytesPerSample + dsdOffs + 0] = 0x69;
                buff[pos + ch*bytesPerSample + dsdOffs + 1] = 0x69;
            }
        }
    }
}

void
WWAudioFilterMuteSoloChannel::FilterPcm(unsigned char *buff, int bytes)
{
    // PCM。
    int bytesPerSample = WWPcmDataSampleFormatTypeToBytesPerSample(mManip.SampleFormat());
    for (int pos=0; pos < bytes; pos += bytesPerSample * mManip.NumChannels()) {
        for (int ch=0; ch<mManip.NumChannels(); ++ch) {
            if (IsMuteChannel(ch)) {
                for (int b=0; b<bytesPerSample; ++b) {
                    buff[pos+ch*bytesPerSample+b] = 0;
                }
            }
        }
    }
}

