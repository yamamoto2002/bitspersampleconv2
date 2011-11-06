#include "FlacDecodeDLL.h"
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <assert.h>

int wmain(int argc, wchar_t* argv[])
{
    if (argc != 3) {
        printf("E: %s:%d inputFlacFilePath outputBinFilePath\n", __FILE__, __LINE__);
        return 1;
    }

    printf("D: %s:%d FlacDecodeDLL_DecodeStart %S\n", __FILE__, __LINE__, argv[1]);
    int id = FlacDecodeDLL_DecodeStart(argv[1]);
    if (id < 0) {
        printf("E: %s:%d FlacDecodeDLL_DecodeStart %d\n", __FILE__, __LINE__, id);
        return 1;
    }

    int bitsPerSample  = FlacDecodeDLL_GetBitsPerSample(id);
    int channels       = FlacDecodeDLL_GetNumOfChannels(id);
    int sampleRate     = FlacDecodeDLL_GetSampleRate(id);
    int64_t numSamples = FlacDecodeDLL_GetNumSamples(id);
    int numFramesPerBlock = FlacDecodeDLL_GetNumFramesPerBlock(id);

    wchar_t titleStr[16];
    wchar_t albumStr[16];
    wchar_t artistStr[16];
    FlacDecodeDLL_GetTitleStr(id, titleStr, sizeof titleStr);
    FlacDecodeDLL_GetAlbumStr(id, albumStr, sizeof albumStr);
    FlacDecodeDLL_GetArtistStr(id, artistStr, sizeof artistStr);

    {
        int pictureBytes = FlacDecodeDLL_GetPictureBytes(id);
        if (0 < pictureBytes) {
            char *pictureData = (char*)malloc(pictureBytes);
            assert(pictureData);
            int rv = FlacDecodeDLL_GetPictureData(id, 0, pictureBytes, pictureData);
            if (0 < rv) {
                FILE *fp = NULL;
                errno_t erno = _wfopen_s(&fp, L"image.bin", L"wb");
                assert(erno == 0);
                assert(fp);

                fwrite(pictureData, 1, pictureBytes, fp);

                fclose(fp);
                fp = NULL;

            }

            free(pictureData);
            pictureData = NULL;
        }
    }

    printf("D: decodeId=%d bitsPerSample=%d sampleRate=%d numSamples=%lld channels=%d numFramesPerBlock=%d\n",
        id,
        bitsPerSample,
        sampleRate,
        numSamples,
        channels,
        numFramesPerBlock);

    printf("D: title=%S\n", titleStr);
    printf("D: album=%S\n", albumStr);
    printf("D: artist=%S\n", artistStr);

    {
        FILE *fp = NULL;
        errno_t erno = _wfopen_s(&fp, argv[2], L"wb");
        assert(erno == 0);
        assert(fp);

        int     ercd    = 0;
        int     nFrames = (1048576 / numFramesPerBlock) * numFramesPerBlock;
        int     bytesPerFrame = channels * bitsPerSample / 8;
        int64_t pcmPos  = 0;
        char *data   = (char *)malloc(nFrames * bytesPerFrame);
        do {
            memset(data, 0xee, nFrames * bytesPerFrame);

            int rv = FlacDecodeDLL_GetNextPcmData(id, nFrames, data);
            ercd   = FlacDecodeDLL_GetLastResult(id);

            if (0 < rv) {
                fwrite(data, 1, rv * bytesPerFrame, fp);
                pcmPos += rv;
            }
            printf("D: GetNextPcmData get %d samples. total %lld\n", rv, pcmPos);

            if (rv <= 0 || ercd == FDRT_Completed) {
                printf("D: GetNextPcmData rv=%d ercd=%d\n", rv, ercd);
                break;
            }
        } while (true);

        fclose(fp);
        fp = NULL;

        free(data);
        data = NULL;

        if (ercd != 1) {
            printf("D: ERROR result=%d\n", ercd);
        }
    }

    FlacDecodeDLL_DecodeEnd(id);

    return 0;
}

