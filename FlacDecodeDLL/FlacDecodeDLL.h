// 日本語UTF-8
#pragma once

#include <stdint.h>

enum FlacDecodeResultType {
    /// ヘッダの取得やデータの取得に成功。
    FDRT_Success = 0,

    /// ファイルの最後まで行き、デコードを完了した。もうデータはない。
    FDRT_Completed,

    // 以下、FLACデコードエラー。
    FDRT_DataNotReady,
    FDRT_WriteOpenFailed,
    FDRT_FlacStreamDecoderNewFailed,
    FDRT_FlacStreamDecoderInitFailed,
    FDRT_DecorderProcessFailed,
    FDRT_LostSync,
    FDRT_BadHeader,
    FDRT_FrameCrcMismatch,
    FDRT_Unparseable,
    FDRT_NumFrameIsNotAligned,
    FDRT_OtherError
};

/// FLACヘッダーを読み込んで、フォーマット情報を取得する。
/// 中のグローバル変数に貯める。APIの設計がスレッドセーフになってないので注意。
/// @return 0 成功。0以外: エラー。1以上: FlacDecodeResultType参照。0未満: Windowsのエラー。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_DecodeStart(const char *fromFlacPath);

/// FlacDecodeを終了する。(DecodeStartで立てたスレを止めたりする)
/// DecodeStartが失敗を戻しても、成功を戻しても、呼ぶ必要がある。
extern "C" __declspec(dllexport)
void __stdcall
FlacDecodeDLL_DecodeEnd(void);

/// チャンネル数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNumOfChannels(void);

/// 量子化ビット数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetBitsPerSample(void);

/// サンプルレート。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetSampleRate(void);

/// サンプル(==frame)総数。
/// DecodeStart成功後に呼ぶことができる。
extern "C" __declspec(dllexport)
int64_t __stdcall
FlacDecodeDLL_GetNumSamples(void);

/// リザルトコード FlacDecodeResultType を取得。
/// ファイルの最後まで行った場合
///   GetLastError==FDRT_Completedで、GetNextPcmDataの戻り値は取得できたフレーム数となる。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetLastResult(void);

/// ブロックサイズを取得。
/// FlacDecodeDLL_GetNextPcmData()のnumFrameはこのサイズの倍数である必要がある。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetBlockSize(void);


/// 次のPCMデータをnumFrameサンプルだけbuff_returnに詰める
/// @return エラーの場合、-1が戻る。0以上の場合、取得できたサンプル数。FDRT_Completedは、正常終了に分類されている。
/// @retval 0 0が戻った場合、取得できたデータが0サンプルであった(成功)。
extern "C" __declspec(dllexport)
int __stdcall
FlacDecodeDLL_GetNextPcmData(int numFrame, char *buff_return);

