// 日本語UTF-8

#include "stdafx.h"
#include <stdio.h>
#include <stdlib.h>
#include "FLAC/stream_decoder.h"
#include "FlacDecodeDLL.h"
#include <assert.h>

// x86 CPUにしか対応してない。
// x64やビッグエンディアンには対応してない。

#define FLACDECODE_MAXPATH (1024)

#ifdef _DEBUG
static FILE *g_fp = NULL;
static void
LogOpen(void)
{
    g_fp = fopen("log.txt", "wb");
}
static void
LogClose(void)
{
    fclose(g_fp);
    g_fp = NULL;
}
#else
#define LogOpen()
#define LogClose()
#endif

#ifdef _DEBUG
#  define dprintf1(x, ...) fprintf(g_fp, x, __VA_ARGS__); fflush(g_fp);
#  define dprintf(x, ...)
//#  define dprintf1(x, ...) printf(x, __VA_ARGS__)
//#  define dprintf(x, ...) printf(x, __VA_ARGS__)
#else
#  define dprintf1(x, ...)
#  define dprintf(x, ...)
#endif

#define CHK(x)                           \
{   if (!x) {                            \
        dprintf("E: %s:%d %s is NULL\n", \
            __FILE__, __LINE__, #x);     \
        return E_FAIL;                   \
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
    FLAC__uint64 totalSamples;
    int          sampleRate;
    int          channels;
    int          bitsPerSample;

    /// 1個のブロックに何サンプルデータが入っているか。
    int          blockSize;

    HANDLE       thread;

    FlacDecodeResultType errorCode;

    FlacDecodeCommand command;
    HANDLE            commandEvent;
    HANDLE            commandCompleteEvent;
    /// コマンドを投入する部分を囲むミューテックス。
    HANDLE            commandMutex;

    char              *buff;
    int               numFrames;
    int               retrievedFrames;

    char         fromFlacPath[FLACDECODE_MAXPATH];

    void Clear(void) {
        totalSamples  = 0;
        sampleRate    = 0;
        channels      = 0;
        bitsPerSample = 0;
        blockSize     = 0;

        thread        = NULL;

        errorCode     = FDRT_DataNotReady;

        command              = FDC_None;
        commandEvent         = NULL;
        commandCompleteEvent = NULL;
        commandMutex         = NULL;

        buff            = NULL;
        numFrames       = 0;
        retrievedFrames = 0;

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
    FlacDecodeInfo *args = (FlacDecodeInfo*)clientData;
    size_t i;

    dprintf("%s args->totalSamples=%lld errorCode=%d\n", __FUNCTION__,
        args->totalSamples, args->errorCode);

    if(args->totalSamples == 0) {
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }
    if(args->channels != 2
        || (args->bitsPerSample != 16
         && args->bitsPerSample != 24)) {
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

    if(frame->header.number.sample_number == 0) {
        args->blockSize = frame->header.blocksize;

        // 最初のデータが来た。ここでいったん待ち状態になる。
        dprintf("%s first data come. blockSize=%d. set commandCompleteEvent\n",
            __FUNCTION__, args->blockSize);
        SetEvent(args->commandCompleteEvent);
        WaitForSingleObject(args->commandEvent, INFINITE);

        // 起きた。要因をチェックする。
        dprintf("%s event received. %d\n", __FUNCTION__, args->command);
        if (args->command == FDC_Shutdown) {
            return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
        }
    }

    if (args->errorCode != FDRT_Success) {
        // デコードエラーが起きた。ここでいったん待ち状態になる。
        dprintf("%s decode error %d. set commandCompleteEvent\n",
            __FUNCTION__, args->errorCode);
        SetEvent(args->commandCompleteEvent);
        WaitForSingleObject(args->commandEvent, INFINITE);

        // 起きた。要因をチェックする。どちらにしても続行はできない。
        dprintf("%s event received. %d\n", __FUNCTION__, args->command);
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

    // データが来た。ブロック数は frame->header.blocksize
    if (args->blockSize != frame->header.blocksize) {
        args->blockSize = frame->header.blocksize;
        if (args->blockSize < (int)frame->header.blocksize) {
            // ブロックサイズが途中で増加した。びっくりする。
            // なお、ブロックサイズが最終フレームで小さい値になることは普通にある。
            dprintf("D: block size changed !!! %d to %d\n",
                args->blockSize, frame->header.blocksize);
            assert(0);
        }
    }

    if (args->bitsPerSample == 16) {
        assert((int)frame->header.blocksize <= args->numFrames);

        for(i = 0; i < frame->header.blocksize; i++) {
            memcpy(&args->buff[i*4+0], &buffer[0][i], 2);
            memcpy(&args->buff[i*4+2], &buffer[1][i], 2);
        }
    }

    if (args->bitsPerSample == 24) {
        assert((int)frame->header.blocksize <= args->numFrames);

        for(i = 0; i < frame->header.blocksize; i++) {
            memcpy(&args->buff[i*6+0], &buffer[0][i], 3);
            memcpy(&args->buff[i*6+3], &buffer[1][i], 3);
        }
    }

    dprintf("%s set %d frame. args->errorCode=%d set commandCompleteEvent\n",
        __FUNCTION__, frame->header.blocksize, args->errorCode);

    args->retrievedFrames = frame->header.blocksize;
    args->errorCode       = FDRT_Success;
    SetEvent(args->commandCompleteEvent);
    WaitForSingleObject(args->commandEvent, INFINITE);

    // 起きた。要因をチェックする。
    dprintf("%s event received. %d args->errorCode=%d\n",
        __FUNCTION__, args->command, args->errorCode);
    if (args->command == FDC_Shutdown) {
        return FLAC__STREAM_DECODER_WRITE_STATUS_ABORT;
    }

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
    FlacDecodeInfo *args = (FlacDecodeInfo*)clientData;

    dprintf("%s type=%d\n", __FUNCTION__, metadata->type);

    if(metadata->type == FLAC__METADATA_TYPE_STREAMINFO) {
        args->totalSamples  = metadata->data.stream_info.total_samples;
        args->sampleRate    = metadata->data.stream_info.sample_rate;
        args->channels      = metadata->data.stream_info.channels;
        args->bitsPerSample = metadata->data.stream_info.bits_per_sample;
    }
}

static void
ErrorCallback(const FLAC__StreamDecoder *decoder,
    FLAC__StreamDecoderErrorStatus status, void *clientData)
{
    FlacDecodeInfo *args = (FlacDecodeInfo*)clientData;

    dprintf("%s status=%d\n", __FUNCTION__, status);

    switch (status) {
    case FLAC__STREAM_DECODER_ERROR_STATUS_LOST_SYNC:
        args->errorCode = FDRT_LostSync;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_BAD_HEADER:
        args->errorCode = FDRT_BadHeader;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_FRAME_CRC_MISMATCH:
        args->errorCode = FDRT_FrameCrcMismatch;
        break;
    case FLAC__STREAM_DECODER_ERROR_STATUS_UNPARSEABLE_STREAM:
        args->errorCode = FDRT_Unparseable;
        break;
    default:
        args->errorCode = FDRT_OtherError;
        break;
    }

    if (args->errorCode != FDRT_Success) {
        /* エラーが起きた。 */
    }
};

///////////////////////////////////////////////////////////////

// デコードスレッド
static int
DecodeMain(FlacDecodeInfo *args)
{
    FLAC__bool                    ok       = true;
    FLAC__StreamDecoder           *decoder = NULL;
    FLAC__StreamDecoderInitStatus init_status;

    dprintf("%s\n", __FUNCTION__);

    decoder = FLAC__stream_decoder_new();
    if(decoder == NULL) {
        args->errorCode = FDRT_FlacStreamDecoderNewFailed;
        dprintf("%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, args->errorCode);
        goto end;
    }

    FLAC__stream_decoder_set_md5_checking(decoder, true);

    init_status = FLAC__stream_decoder_init_file(
        decoder, args->fromFlacPath,
        WriteCallback, MetadataCallback, ErrorCallback, args);
    if(init_status != FLAC__STREAM_DECODER_INIT_STATUS_OK) {
        args->errorCode = FDRT_FlacStreamDecoderInitFailed;
        dprintf("%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, args->errorCode);
        goto end;
    }

    ok = FLAC__stream_decoder_process_until_end_of_stream(decoder);
    if (!ok) {
        if (args->errorCode == FDRT_Success) {
            args->errorCode = FDRT_DecorderProcessFailed;
        }
        dprintf("%s Flac decode error %d. set complete event.\n",
            __FUNCTION__, args->errorCode);
        goto end;
    }

    args->errorCode = FDRT_Completed;
end:
    if (NULL != decoder) {
        FLAC__stream_decoder_delete(decoder);
        decoder = NULL;
    }

    SetEvent(args->commandCompleteEvent);

    dprintf("%s end ercd=%d\n", __FUNCTION__, args->errorCode);
    return args->errorCode;
}

static DWORD WINAPI
DecodeEntry(LPVOID param)
{
    dprintf("%s\n", __FUNCTION__);

    FlacDecodeInfo *args = (FlacDecodeInfo*)param;
    DecodeMain(args);

    dprintf("%s end\n", __FUNCTION__);
    return 0;
}

///////////////////////////////////////////////////////////////

/// 物置の実体。グローバル変数。
static FlacDecodeInfo g_flacDecodeInfo;

/// チャンネル数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNumOfChannels(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.channels;
}

/// 量子化ビット数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetBitsPerSample(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.bitsPerSample;
}

/// サンプルレート。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetSampleRate(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.sampleRate;
}

/// サンプル(==frame)総数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int64_t __stdcall
FlacDecodeDLL_GetNumSamples(void)
{
    if (g_flacDecodeInfo.errorCode != FDRT_Success) {
        assert(!"please call FlacDecodeDLL_DecodeStart()");
        return 0;
    }

    return g_flacDecodeInfo.totalSamples;
}

extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetLastResult(void)
{
    return g_flacDecodeInfo.errorCode;
}

extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetBlockSize(void)
{
    return g_flacDecodeInfo.blockSize;
}

/// FLACヘッダーを読み込んで、フォーマット情報を取得する。
/// 中のグローバル変数に貯める。APIの設計がスレッドセーフになってないので注意。
/// @return 0 成功。1以上: エラー。FlacDecodeResultType参照。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_DecodeStart(const char *fromFlacPath)
{
    LogOpen();

    dprintf1("%s started\n", __FUNCTION__);
    dprintf1("%s path=\"%s\"\n", __FUNCTION__, fromFlacPath);

    assert(NULL == g_flacDecodeInfo.commandMutex);
    g_flacDecodeInfo.commandMutex = CreateMutex(NULL, FALSE, NULL);
    CHK(g_flacDecodeInfo.commandMutex);

    assert(NULL == g_flacDecodeInfo.commandEvent);
    g_flacDecodeInfo.commandEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(g_flacDecodeInfo.commandEvent);

    assert(NULL == g_flacDecodeInfo.commandCompleteEvent);
    g_flacDecodeInfo.commandCompleteEvent = CreateEventEx(NULL, NULL, 0,
        EVENT_MODIFY_STATE | SYNCHRONIZE);
    CHK(g_flacDecodeInfo.commandCompleteEvent);

    g_flacDecodeInfo.errorCode = FDRT_Success;
    strncpy_s(g_flacDecodeInfo.fromFlacPath, fromFlacPath,
        sizeof g_flacDecodeInfo.fromFlacPath-1);

    g_flacDecodeInfo.thread
        = CreateThread(NULL, 0, DecodeEntry, &g_flacDecodeInfo, 0, NULL);
    assert(g_flacDecodeInfo.thread);

    dprintf("%s createThread\n", __FUNCTION__);

    // FlacDecodeスレが動き始める。commandCompleteEventを待つ。
    // FlacDecodeスレは、途中でエラーが起きるか、
    // データの準備ができたらcommandCompleteEventを発行し、commandEventをWaitする。
    WaitForSingleObject(g_flacDecodeInfo.commandCompleteEvent, INFINITE);
    
    dprintf1("%s commandCompleteEvent. ercd=%d\n",
        __FUNCTION__, g_flacDecodeInfo.errorCode);
    return g_flacDecodeInfo.errorCode;
}

#define CLOSE_SET_NULL(p) \
if (NULL != p) {          \
    CloseHandle(p);       \
    p = NULL;             \
}

/// FlacDecodeを終了する。(DecodeStartで立てたスレを止めたりする)
extern "C" __declspec(dllexport)
void __stdcall
FlacDecodeDLL_DecodeEnd(void)
{
    dprintf1("%s started.\n", __FUNCTION__);

    if (g_flacDecodeInfo.thread) {
        assert(g_flacDecodeInfo.commandMutex);
        assert(g_flacDecodeInfo.commandEvent);
        assert(g_flacDecodeInfo.commandCompleteEvent);

        WaitForSingleObject(g_flacDecodeInfo.commandMutex, INFINITE);
        g_flacDecodeInfo.command = FDC_Shutdown;

        dprintf("%s SetEvent and wait to complete FlacDecodeThead\n",
            __FUNCTION__);

        SetEvent(g_flacDecodeInfo.commandEvent);
        ReleaseMutex(g_flacDecodeInfo.commandMutex);

        // スレッドが終わるはず。
        WaitForSingleObject(g_flacDecodeInfo.thread, INFINITE);

        dprintf("%s thread stopped. delete FlacDecodeThead\n",
            __FUNCTION__);
        CLOSE_SET_NULL(g_flacDecodeInfo.thread);
    }

    CLOSE_SET_NULL(g_flacDecodeInfo.commandEvent);
    CLOSE_SET_NULL(g_flacDecodeInfo.commandCompleteEvent);
    CLOSE_SET_NULL(g_flacDecodeInfo.commandMutex);

    g_flacDecodeInfo.Clear();

    dprintf1("%s done.\n", __FUNCTION__);
    LogClose();
}

/// 次のPCMデータをnumFrameサンプルだけbuff_returnに詰める
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNextPcmData(int numFrame, char *buff_return)
{
    if (NULL == g_flacDecodeInfo.thread) {
        dprintf("%s FlacDecodeThread is not ready.\n",
            __FUNCTION__);
        return E_FAIL;
    }

    assert(g_flacDecodeInfo.commandMutex);
    assert(g_flacDecodeInfo.commandEvent);
    assert(g_flacDecodeInfo.commandCompleteEvent);

    const int bytesPerFrame
        = g_flacDecodeInfo.channels * g_flacDecodeInfo.bitsPerSample/8;

    int pos = 0;

    while (pos < numFrame) {
        dprintf("%s pos=%d numFrame=%d\n",
            __FUNCTION__, pos, numFrame);

        {   // FlacDecodeThreadにGetFramesコマンドを伝える
            WaitForSingleObject(g_flacDecodeInfo.commandMutex, INFINITE);

            g_flacDecodeInfo.errorCode    = FDRT_Success;
            g_flacDecodeInfo.command      = FDC_GetFrames;
            g_flacDecodeInfo.buff         = &buff_return[bytesPerFrame * pos];
            g_flacDecodeInfo.numFrames    = numFrame;
            g_flacDecodeInfo.retrievedFrames = 0;

            dprintf("%s set command.\n", __FUNCTION__);
            SetEvent(g_flacDecodeInfo.commandEvent);

            ReleaseMutex(g_flacDecodeInfo.commandMutex);
        }

        dprintf("%s wait for commandCompleteEvent.\n", __FUNCTION__);
        WaitForSingleObject(g_flacDecodeInfo.commandCompleteEvent, INFINITE);

        dprintf("%s command completed. ercd=%d retrievedFrames=%d\n",
            __FUNCTION__, g_flacDecodeInfo.errorCode,
            g_flacDecodeInfo.retrievedFrames);

        pos += g_flacDecodeInfo.retrievedFrames;

        if (g_flacDecodeInfo.errorCode != FDRT_Success) {
            break;
        }
    }

    dprintf1("%s numFrame=%d retrieved=%d ercd=%d\n",
            __FUNCTION__, numFrame, pos, g_flacDecodeInfo.errorCode);

    if (FDRT_Success   != g_flacDecodeInfo.errorCode &&
        FDRT_Completed != g_flacDecodeInfo.errorCode) {
        // エラー終了。
        return -1;
    }
    return pos;
}

