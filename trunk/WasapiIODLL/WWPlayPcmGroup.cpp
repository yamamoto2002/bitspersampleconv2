// 日本語UTF-8

#include "WWPlayPcmGroup.h"
#include "WWUtil.h"
#include <assert.h>
#include <stdint.h>

WWPlayPcmGroup::WWPlayPcmGroup(void)
{
    m_repeat = false;
    Clear();
}

WWPlayPcmGroup::~WWPlayPcmGroup(void)
{
    assert(m_playPcmDataList.size() == 0);
}

void
WWPlayPcmGroup::Init(void)
{
}

void
WWPlayPcmGroup::Term(void)
{
    Clear();
}

bool
WWPlayPcmGroup::AddPlayPcmData(int id, BYTE *data, int64_t bytes)
{
    assert(1 <= m_numChannels);
    assert(1 <= m_sampleRate);
    assert(1 <= m_frameBytes);

    if (0 == bytes) {
        dprintf("E: %s(%d, %p, %lld) arg check failed\n", __FUNCTION__, id, data, bytes);
        return false;
    }

    WWPcmData pcmData;
    if (!pcmData.Init(id, m_format, m_numChannels,
            bytes/m_frameBytes,
            m_frameBytes, WWPcmDataContentPcmData)) {
        dprintf("E: %s(%d, %p, %lld) malloc failed\n", __FUNCTION__, id, data, bytes);
        return false;
    }

    if (NULL != data) {
        CopyMemory(pcmData.stream, data, (bytes/m_frameBytes) * m_frameBytes);
    }
    m_playPcmDataList.push_back(pcmData);
    return true;
}

bool
WWPlayPcmGroup::AddPlayPcmDataStart(
        int sampleRate,
        WWPcmDataFormatType format,
        int numChannels,
        int frameBytes)
{
    assert(m_playPcmDataList.size() == 0);
    assert(1 <= numChannels);
    assert(1 <= sampleRate);
    assert(1 <= frameBytes);

    m_sampleRate  = sampleRate;
    m_format      = format;
    m_numChannels = numChannels;
    m_frameBytes  = frameBytes;

    return true;
}

bool
WWPlayPcmGroup::AddPlayPcmDataEnd(void)
{
    PlayPcmDataListDebug();

    return true;
}

void
WWPlayPcmGroup::RemoveAt(int id)
{
    assert(0 <= id && id < m_playPcmDataList.size());

    WWPcmData *pcmData = &m_playPcmDataList[id];
    pcmData->Term();

    m_playPcmDataList.erase(m_playPcmDataList.begin()+id);

    // 連続再生のリンクリストをつなげ直す。
    SetPlayRepeat(m_repeat);
}

void
WWPlayPcmGroup::Clear(void)
{
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        m_playPcmDataList[i].Term();
    }
    m_playPcmDataList.clear();

    m_sampleRate  = 0;
    m_format      = WWPcmDataFormatUnknown;
    m_numChannels = 0;
    m_frameBytes  = 0;
}

void
WWPlayPcmGroup::SetPlayRepeat(bool repeat)
{
    dprintf("D: %s(%d)\n", __FUNCTION__, (int)repeat);
    m_repeat = repeat;

    if (m_playPcmDataList.size() < 1) {
        dprintf("D: %s(%d) pcmDataList.size() == %d nothing to do\n",
            __FUNCTION__, (int)repeat, m_playPcmDataList.size());
        return;
    }

    // 最初のpcmDataから、最後のpcmDataまでnextでつなげる。
    // リピートフラグが立っていたら最後のpcmDataのnextを最初のpcmDataにする。
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        if (i == m_playPcmDataList.size()-1) {
            // 最後→最初に接続。
            if (repeat) {
                m_playPcmDataList[i].next = 
                    &m_playPcmDataList[0];
            } else {
                // 最後→NULL
                m_playPcmDataList[i].next = NULL;
            }
        } else {
            // 最後のあたりの項目以外は、連続にnextをつなげる。
            m_playPcmDataList[i].next = 
                &m_playPcmDataList[i+1];
        }
    }
}

WWPcmData *
WWPlayPcmGroup::FindPcmDataById(int id)
{
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        if (m_playPcmDataList[i].id == id) {
            return &m_playPcmDataList[i];
        }
    }

    return NULL;
}

WWPcmData *
WWPlayPcmGroup::FirstPcmData(void)
{
    if (0 == m_playPcmDataList.size()) {
        return NULL;
    }

    return &m_playPcmDataList[0];
}

WWPcmData *
WWPlayPcmGroup::LastPcmData(void)
{
    if (0 == m_playPcmDataList.size()) {
        return NULL;
    }

    return &m_playPcmDataList[m_playPcmDataList.size()-1];
}

void
WWPlayPcmGroup::PlayPcmDataListDebug(void)
{
#ifdef _DEBUG
    dprintf("D: %s() count=%u\n", __FUNCTION__, m_playPcmDataList.size());
    for (size_t i=0; i<m_playPcmDataList.size(); ++i) {
        WWPcmData *p = &m_playPcmDataList[i];

        dprintf("  %p next=%p i=%d id=%d nFrames=%d posFrame=%d contentType=%s stream=%p\n",
            p, p->next, i, p->id, p->nFrames, p->posFrame,
            WWPcmDataContentTypeToStr(p->contentType), p->stream);
    }
#endif
}

