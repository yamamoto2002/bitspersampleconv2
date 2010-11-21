// 日本語UTF-8

#include "stdafx.h"
#include <stdio.h>
#include <stdlib.h>
#include "FLAC/stream_decoder.h"
#include "FlacDecodeDLL.h"
#include <assert.h>
#include <map>

// x86 CPUにしか対応してない。
// x64やビッグエンディアンには対応してない。

#define FLACDECODE_MAXPATH (1024)

#ifdef _DEBUG
#  define dprintf1(fp, x, ...) { \
    if (NULL == fp) { \
        printf(x, __VA_ARGS__); \
    } else { \
        fprintf(fp, x, __VA_ARGS__); fflush(fp); \
    } \
}
#  define dprintf(fp, x, ...) { \
    if (NULL == fp) { \
        printf(x, __VA_ARGS__); \
    } else { \
        fprintf(fp, x, __VA_ARGS__); fflush(fp); \
    } \
}
//#  define dprintf1(fp, x, ...) printf(x, __VA_ARGS__)
//#  define dprintf(fp, x, ...) printf(x, __VA_ARGS__)
//#  define dprintf1(fp, x, ...)
//#  define dprintf(fp, x, ...)
#else
#  define dprintf1(fp, x, ...)
#  define dprintf(fp, x, ...)
#endif

#define CHK(x)                           \
{   if (!x) {                            \
        dprintf(fdi->logFP, "E: %s:%d %s is NULL\n", \
            __FILE__, __LINE__, #x);     \
        return FDRT_OtherError;          \
    }                                    \
}

/// FlacDecodeスレッドへのコマンド。
enum FlacDecodeCommand {
    /// コマンドなし。(コマンド実行後にFlacDecodeがセットする)
    FDC_None,

    /// シャットダウンイベント。
    FDC_Shutdown,

    /// フレーム(サンプルデータ)取得。
    /// 取得するフレーム数
    FDC_GetFrames,
};

/// FlacDecodeの物置。
struct FlacDecodeInfo {
    int          id;
    int          sampleRate;
    int          channels;
    int          bitsPerSample;
    FLAC__uint64 totalSamples;

    FLAC__StreamDecoder *decoder;

    /// 1個のブロックに何サンプル(frame)データが入っているか。
    int          numFramesPerBlock;

    HANDLE       thread;

    FlacDecodeResultType errorCode;

    FlacDecodeCommand command;
    HANDLE            commandEvent;
    HANDLE            commandCompleteEvent;
    /// コマンドを投入する部分を囲むミューテックス。
    HANDLE            commandMutex;

    char              *buff;
    int               buffFrames;
    int               retrievedFrames;
    FILE              *logFP;

    char         fromFlacPath[FLACDECODE_MAXPATH];

    void Clear(void) {
        totalSamples  = 0;
        sampleRate    = 0;
        channels      = 0;
        bitsPerSample = 0;

        decoder = NULL;

        numFramesPerBlock     = 0;

        thread        = NULL;

        errorCode     = FDRT_DataNotReady;

        command              = FDC_None;
        commandEvent         = NULL;
        commandCompleteEvent = NULL;
        commandMutex         = NULL;

        buff            = NULL;
        buffFrames      = 0;
        retrievedFrames = 0;
        logFP           = NULL;

        fromFlacPath[0] = 0;
    }

    FlacDecodeInfo(void) {
        Clear();
    }
};

#define RG(x,v)                                   \
{                                                 \
    rv = x;                                       \
    if (v != rv) {                                \
        goto end;                                 \
    }                                             \
}                                                 \

////////////////////////////////////////////////////////////////////////
// FLACデコーダーコールバック

static FLAC__StreamDecoderWriteStatus
WriteCallback1(const FLAC__StreamDecoder *decoder,
    const FLAC__Frame *frame, const FLAC__int32 * const buffer[],
    void *clientData)
{
    FlacDecodeInfo *fdi = (FlacDecodeInfo*)clientData;

    dprintf(fdi->logFP, "%s fdi->totalSamples=%lld errorCode=%d\n", __FUNCTION__,
        fdi->totalSamples, fdi->errorCode);

    if(fdi->totalSamples == 0) {
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

    dprintf(fdi->logFP, "%s frame->header.number.sample_number=%d\n", __FUNCTION__,
        frame->header.number.sample_number);
    if(frame->header.number.sample_number == 0) {
        fdi->numFramesPerBlock = frame->header.blocksize;

        // 最初のデータが来た。ここでいったん待ち状態になる。
        dprintf(fdi->logFP, "%s first data come. numFramesPerBlock=%d. set commandCompleteEvent\n",
            __FUNCTION__, fdi->numFramesPerBlock);
        SetEvent(fdi->commandCompleteEvent);
        WaitForSingleObject(fdi->commandEvent, INFINITE);

        // 起きた。要因をチェックする。
        dprintf(fdi->logFP, "%s event received1. %d\n", __FUNCTION__, fdi->command);
        if (fdi->command == FDC_Shutdown) {
            return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
        }
    }

    if (fdi->errorCode != FDRT_Success) {
        // デコードエラーが起きた。ここでいったん待ち状態になる。
        dprintf(fdi->logFP, "%s decode error %d. set commandCompleteEvent\n",
            __FUNCTION__, fdi->errorCode);
        SetEvent(fdi->commandCompleteEvent);
        WaitForSingleObject(fdi->commandEvent, INFINITE);

        // 起きた。要因をチェックする。どちらにしても続行はできない。
        dprintf(fdi->logFP, "%s event received2. %d\n", __FUNCTION__, fdi->command);
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

    // データが来た。ブロック数は frame->header.blocksize
    dprintf(fdi->logFP, "%s fdi->numFramesPerBlock=%d frame->header.blocksize=%d\n", __FUNCTION__,
        fdi->numFramesPerBlock, frame->header.blocksize);
    if (fdi->numFramesPerBlock != frame->header.blocksize) {
        dprintf(fdi->logFP, "%s fdi->numFramesPerBlock changed %d to %d\n",
            __FUNCTION__, fdi->numFramesPerBlock, frame->header.blocksize);
        fdi->numFramesPerBlock = frame->header.blocksize;
    }

    dprintf(fdi->logFP, "%s fdi->buffFrames=%d fdi->retrievedFrames=%d fdi->numFramesPerBlock=%d\n", __FUNCTION__,
        fdi->buffFrames, fdi->retrievedFrames, fdi->numFramesPerBlock);
    if ((fdi->buffFrames - fdi->retrievedFrames) < fdi->numFramesPerBlock) {
        // このブロックを収容する場所がない。データ詰め終わり。
        fdi->errorCode       = FDRT_Success;
        SetEvent(fdi->commandCompleteEvent);
        WaitForSingleObject(fdi->commandEvent, INFINITE);

        // 起きた。要因をチェックする。
        dprintf(fdi->logFP, "%s event received3. %d fdi->errorCode=%d\n",
            __FUNCTION__, fdi->command, fdi->errorCode);
        if (fdi->command == FDC_Shutdown) {
            return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
        }

        assert(fdi->retrievedFrames == 0);
        // いったんバッファをフラッシュしたのに、まだ足りない場合はデータが詰められない。
        if ((fdi->buffFrames - fdi->retrievedFrames) < fdi->numFramesPerBlock) {
            fdi->errorCode = FDRT_RecvBufferSizeInsufficient;
            dprintf(fdi->logFP, "D: bufferSize insufficient %d < %d\n",
                fdi->buffFrames, fdi->numFramesPerBlock);
            return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
        }
        dprintf(fdi->logFP, "%s fdi->buffFrames=%d fdi->retrievedFrames=%d fdi->numFramesPerBlock=%d\n", __FUNCTION__,
            fdi->buffFrames, fdi->retrievedFrames, fdi->numFramesPerBlock);
    }

    {
        int bytesPerSample = fdi->bitsPerSample / 8;
        int bytesPerFrame  = bytesPerSample * fdi->channels;

        for(int i = 0; i < fdi->numFramesPerBlock; i++) {
            for (int ch = 0; ch < fdi->channels; ++ch) {
                memcpy(&fdi->buff[(fdi->retrievedFrames + i) * bytesPerFrame + ch * bytesPerSample],
                    &buffer[ch][i], bytesPerSample);
            }
        }
    }

    dprintf(fdi->logFP, "%s set %d frame. fdi->errorCode=%d set commandCompleteEvent\n",
        __FUNCTION__, fdi->numFramesPerBlock, fdi->errorCode);

    fdi->retrievedFrames += fdi->numFramesPerBlock;

    return FLAC__STREAM_DECODER_WRITE_STATUS_CONTINUE;
}

static FLAC__StreamDecoderWriteStatus
WriteCallback(const FLAC__StreamDecoder *decoder,
    const FLAC__Frame *frame, const FLAC__int32 * const buffer[],
    void *clientData)
{
    FLAC__StreamDecoderWriteStatus rv =
        WriteCallback1(decoder, frame, buffer, clientData);

    if (rv == FLAC__STREAM_DECODER_WRITE_STATUS_ABORT) {
        /* デコード終了 */
    }
    return rv;
}

static void
MetadataCallback(const FLAC__StreamDecoder *decoder,
    const FLAC__StreamMetadata *metadata, void *clientData)
{
    FlacDecodeInfo *fdi = (FlacDecodeInfo*)clientData;

    dprintf(fdi->logFP, "%s type=%d\n", __FUNCTION__, metadata->type);

    if(metadata->type == FLAC__METADATA_TYPE_STREAMINFO) {
        fdi->totalSamples  = metadata->data.stream_info.total_samples;
        fdi->sampleRate    = metadata->data.stream_info.sample_rate;
        fdi->channels      = metadata->data.stream_info.channels;
        fdi->bitsPerSample = metadata->data.stream_info.bits_per_sample;
    }
}

static void
ErrorCallback(const FLAC__StreamDecoder *decoder,
    FLAC__StreamDecoderErrorStatus status, void *clientData)
{
    FlacDecodeInfo *fdi = (FlacDecodeInfo*)clientData;

    dprintf(fdi->logFP, "%s status=%d\n", __FUNCTION__, status);

    switch (status) {
    case FLAC__STREAM_DECODER_ERROR_STATUS_LOST_SYNC:
        fdi->errorCode = FDRT_LostSync;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_BAD_HEADER:
        fdi->errorCode = FDRT_BadHeader;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_FRAME_CRC_MISMATCH:
        fdi->errorCode = FDRT_FrameCrcMismatch;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_UNPARSEABLE_STREAM:
        fdi->errorCode = FDRT_Unparseable;
        break;
    default:
        fdi->errorCode = FDRT_OtherError;
        break;
    }

    if (fdi->errorCode != FDRT_Success) {
        /* エラーが起きた。 */
    }
};

///////////////////////////////////////////////////////////////

// デコードスレッド
static int
DecodeMain(FlacDecodeInfo *fdi)
{
    assert(fdi);

    FLAC__bool                    ok = true;
    FLAC__StreamDecoderInitStatus init_status;

    fdi->decoder = FLAC__stream_decoder_new();
    if(fdi->decoder == NULL) {
        fdi->errorCode = FDRT_FlacStreamDecoderNewFailed;
        dprintf(fdi->logFP, "%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, fdi->errorCode);
        goto end;
    }

    dprintf(fdi->logFP, "%s FLAC_stream_decoder=%p\n", __FUNCTION__, fdi->decoder);

    FLAC__stream_decoder_set_md5_checking(fdi->decoder, true);

    init_status = FLAC__stream_decoder_init_file(
        fdi->decoder, fdi->fromFlacPath,
        WriteCallback, MetadataCallback, ErrorCallback, fdi);
    if(init_status != FLAC__STREAM_DECODER_INIT_STATUS_OK) {
        fdi->errorCode = FDRT_FlacStreamDecoderInitFailed;
        dprintf(fdi->logFP, "%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, fdi->errorCode);
        goto end;
    }

    ok = FLAC__stream_decoder_process_until_end_of_stream(fdi->decoder);
    if (!ok) {
        dprintf(fdi->logFP, "%s Flac decode error fdi->errorCode=%d\n",
            __FUNCTION__, fdi->errorCode);

        if (fdi->errorCode == FDRT_Success) {
            fdi->errorCode = FDRT_DecorderProcessFailed;
        }
        dprintf(fdi->logFP, "%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, fdi->errorCode);
        goto end;
    } else {
        // OK。データがバッファに溜まっていたら最後のイベントを出す。

        if (0 < fdi->retrievedFrames) {
            SetEvent(fdi->commandCompleteEvent);
            WaitForSingleObject(fdi->commandEvent, INFINITE);

            // 起きた。要因をチェックする。
            dprintf(fdi->logFP, "%s event received. %d fdi->errorCode=%d\n",
                __FUNCTION__, fdi->command, fdi->errorCode);
            if (fdi->command == FDC_Shutdown) {
                fdi->errorCode = FDRT_DecorderProcessFailed;
                goto end;
            }
        }
    }

    fdi->errorCode = FDRT_Completed;
end:
    if (NULL != fdi->decoder) {
        FLAC__stream_decoder_delete(fdi->decoder);
        fdi->decoder = NULL;
    }

    SetEvent(fdi->commandCompleteEvent);

    dprintf(fdi->logFP, "%s end ercd=%d\n", __FUNCTION__, fdi->errorCode);
    return fdi->errorCode;
}

static DWORD WINAPI
DecodeEntry(LPVOID param)
{
    dprintf(NULL, "%s\n", __FUNCTION__);

    FlacDecodeInfo *fdi = (FlacDecodeInfo*)param;
    DecodeMain(fdi);

    dprintf(NULL, "%s end\n", __FUNCTION__);
    return 0;
}

///////////////////////////////////////////////////////////////

/// 物置の実体。グローバル変数。
static std::map<int, FlacDecodeInfo*> g_flacDecodeInfoMap;

static int g_nextDecoderId = 0;

static FlacDecodeInfo *
FlacDecodeInfoNew(void)
{
    FlacDecodeInfo * fdi = new FlacDecodeInfo();
    if (NULL == fdi) {
        return NULL;
    }

    fdi->id = g_nextDecoderId;
    g_flacDecodeInfoMap[g_nextDecoderId] = fdi;

    ++g_nextDecoderId;
    return fdi;
}

static void
FlacDecodeInfoDelete(FlacDecodeInfo *fdi)
{
    if (NULL == fdi) {
        return;
    }

    g_flacDecodeInfoMap.erase(fdi->id);
    delete fdi;
    fdi = NULL; // あんまり意味ないが、一応
}

static FlacDecodeInfo *
FlacDecodeInfoFindById(int id)
{
    std::map<int, FlacDecodeInfo*>::iterator ite
        = g_flacDecodeInfoMap.find(id);
    if (ite == g_flacDecodeInfoMap.end()) {
        return NULL;
    }
    return ite->second;
}

///////////////////////////////////////////////////////////////

/// チャンネル数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNumOfChannels(int id)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    assert(fdi);

    if (fdi->errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return fdi->channels;
}

/// 量子化ビット数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetBitsPerSample(int id)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    assert(fdi);

    if (fdi->errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return fdi->bitsPerSample;
}

/// サンプルレート。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetSampleRate(int id)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    assert(fdi);

    if (fdi->errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return fdi->sampleRate;
}

/// サンプル(==frame)総数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int64_t __stdcall
FlacDecodeDLL_GetNumSamples(int id)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    assert(fdi);

    if (fdi->errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return fdi->totalSamples;
}

extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetLastResult(int id)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    assert(fdi);

    return fdi->errorCode;
}

extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNumFramesPerBlock(int id)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    assert(fdi);

    return fdi->numFramesPerBlock;
}

#ifdef _DEBUG
static FILE *g_fp = NULL;
static void
LogOpen(FlacDecodeInfo *fdi)
{
    assert(fdi);

    LARGE_INTEGER performanceCount;
    QueryPerformanceCounter(&performanceCount);

    char s[256];
    sprintf_s(s, "log%d_%lld.txt", fdi->id, performanceCount.QuadPart);

    errno_t result = fopen_s(&fdi->logFP, s, "wb");
    if (result != 0) {
        fdi->logFP = NULL;
    }
}
static void
LogClose(FlacDecodeInfo *fdi)
{
    assert(fdi);
    if (fdi->logFP) {
        fclose(fdi->logFP);
        fdi->logFP = NULL;
    }
}
#else
#define LogOpen(fdi)
#define LogClose(fdi)
#endif

/// FLACヘッダーを読み込んで、フォーマット情報を取得する。
/// 中のグローバル変数に貯める。APIの設計がスレッドセーフになってないので注意。
/// @return 0 成功。1以上: エラー。FlacDecodeResultType参照。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_DecodeStart(const char *fromFlacPath)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoNew();
    if (NULL == fdi) {
        dprintf1(NULL, "%s out of memory\n", __FUNCTION__);
        return FDRT_OtherError;
    }

    LogOpen(fdi);
    dprintf1(fdi->logFP, "%s started\n", __FUNCTION__);
    dprintf1(fdi->logFP, "%s path=\"%s\"\n", __FUNCTION__, fromFlacPath);

    assert(NULL == fdi->commandMutex);
    fdi->commandMutex = CreateMutex(NULL, FALSE, NULL);
    CHK(fdi->commandMutex);

    assert(NULL == fdi->commandEvent);
    fdi->commandEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(fdi->commandEvent);

    assert(NULL == fdi->commandCompleteEvent);
    fdi->commandCompleteEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(fdi->commandCompleteEvent);

    fdi->errorCode = FDRT_Success;
    strncpy_s(fdi->fromFlacPath, fromFlacPath,
        sizeof fdi->fromFlacPath-1);

    fdi->thread
        = CreateThread(NULL, 0, DecodeEntry, fdi, 0, NULL);
    assert(fdi->thread);

    dprintf(fdi->logFP, "%s createThread\n", __FUNCTION__);

    // FlacDecodeスレが動き始める。commandCompleteEventを待つ。
    // FlacDecodeスレは、途中でエラーが起きるか、
    // データの準備ができたらcommandCompleteEventを発行し、commandEventをWaitする。
    WaitForSingleObject(fdi->commandCompleteEvent, INFINITE);
    
    dprintf1(fdi->logFP, "%s commandCompleteEvent. ercd=%d fdi->id=%d\n",
        __FUNCTION__, fdi->errorCode, fdi->id);
    if (fdi->errorCode < 0) {
        FlacDecodeInfoDelete(fdi);
        return fdi->errorCode;
    }

    return fdi->id;
}

#define CLOSE_SET_NULL(p) \
if (NULL != p) {          \
    CloseHandle(p);       \
    p = NULL;             \
}

/// FlacDecodeを終了する。(DecodeStartで立てたスレを止めたりする)
extern "C" __declspec(dllexport)
void __stdcall
FlacDecodeDLL_DecodeEnd(int id)
{
    if (id < 0) {
        dprintf1(NULL, "%s id=%d done.\n", __FUNCTION__, id);
        return;
    }

    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    if (NULL == fdi) {
        dprintf1(NULL, "%s id=%d not found!\n", __FUNCTION__, id);
        assert(0);
        return;
    }
    dprintf1(fdi->logFP, "%s started. id=%d\n", __FUNCTION__, id);


    if (fdi->thread) {
        assert(fdi->commandMutex);
        assert(fdi->commandEvent);
        assert(fdi->commandCompleteEvent);

        WaitForSingleObject(fdi->commandMutex, INFINITE);
        fdi->command = FDC_Shutdown;

        dprintf(fdi->logFP, "%s SetEvent and wait to complete FlacDecodeThead\n",
            __FUNCTION__);

        SetEvent(fdi->commandEvent);
        ReleaseMutex(fdi->commandMutex);

        // スレッドが終わるはず。
        WaitForSingleObject(fdi->thread, INFINITE);

        dprintf(fdi->logFP, "%s thread stopped. delete FlacDecodeThead\n",
            __FUNCTION__);
        CLOSE_SET_NULL(fdi->thread);
    }

    CLOSE_SET_NULL(fdi->commandEvent);
    CLOSE_SET_NULL(fdi->commandCompleteEvent);
    CLOSE_SET_NULL(fdi->commandMutex);

    fdi->Clear();

    dprintf1(fdi->logFP, "%s id=%d done.\n", __FUNCTION__, id);
    LogClose(fdi);

    FlacDecodeInfoDelete(fdi);
    fdi = NULL;
}

/// 次のPCMデータをnumFrameサンプルだけbuff_returnに詰める
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNextPcmData(int id, int numFrame, char *buff_return)
{
    FlacDecodeInfo *fdi = FlacDecodeInfoFindById(id);
    assert(fdi);

    if (NULL == fdi->thread) {
        dprintf(fdi->logFP, "%s FlacDecodeThread is not ready.\n",
            __FUNCTION__);
        return FDRT_OtherError;
    }

    assert(fdi->commandMutex);
    assert(fdi->commandEvent);
    assert(fdi->commandCompleteEvent);

    const int bytesPerFrame
        = fdi->channels * fdi->bitsPerSample/8;

    {   // FlacDecodeThreadにGetFramesコマンドを伝える
        WaitForSingleObject(fdi->commandMutex, INFINITE);

        fdi->errorCode    = FDRT_Success;
        fdi->command      = FDC_GetFrames;
        fdi->buff         = buff_return;
        fdi->buffFrames    = numFrame;
        fdi->retrievedFrames = 0;

        dprintf(fdi->logFP, "%s set command.\n", __FUNCTION__);
        SetEvent(fdi->commandEvent);

        ReleaseMutex(fdi->commandMutex);
    }

    dprintf(fdi->logFP, "%s wait for commandCompleteEvent.\n", __FUNCTION__);
    WaitForSingleObject(fdi->commandCompleteEvent, INFINITE);

    dprintf1(fdi->logFP, "%s numFrame=%d retrieved=%d ercd=%d\n",
            __FUNCTION__, numFrame,
            fdi->retrievedFrames, fdi->errorCode);

    if (FDRT_Success   != fdi->errorCode &&
        FDRT_Completed != fdi->errorCode) {
        // エラー終了。
        return -1;
    }
    return fdi->retrievedFrames;
}

