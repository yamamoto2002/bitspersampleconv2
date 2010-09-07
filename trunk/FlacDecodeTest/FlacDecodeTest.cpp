#include "stdafx.h"
#include "FlacDecodeDLL.h"
#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <assert.h>

int _tmain(int argc, _TCHAR* argv[])
{
    if (argc != 3) {
        printf("E: %s:%d inputFlacFilePath outputBinFilePath\n", __FILE__, __LINE__);
        return 1;
    }

    printf("D: %s:%d FlacDecodeDLL_DecodeStart %s\n", __FILE__, __LINE__, argv[1]);
    int rv = FlacDecodeDLL_DecodeStart(argv[1]);
    if (rv != 0) {
        printf("E: %s:%d FlacDecodeDLL_DecodeStart %d\n", __FILE__, __LINE__, rv);
        FlacDecodeDLL_DecodeEnd();
        return 1;
    }

    int bitsPerSample  = FlacDecodeDLL_GetBitsPerSample();
    int channels       = FlacDecodeDLL_GetNumOfChannels();
    int sampleRate     = FlacDecodeDLL_GetSampleRate();
    int64_t numSamples = FlacDecodeDLL_GetNumSamples();

    printf("D: bitsPerSample=%d sampleRate=%d numSamples=%lld channels=%d\n",
        bitsPerSample,
        sampleRate,
        numSamples,
        channels);

    FILE *fp = fopen(argv[2], "wb");
    assert(fp);

    int     ercd    = 0;
    int     nFrames = 1048576;
    int     bytesPerFrame = channels * bitsPerSample / 8;
    int64_t pcmPos  = 0;
    char *data   = (char *)malloc(nFrames * bytesPerFrame);
    do {
        memset(data, 0xee, nFrames * bytesPerFrame);

        int rv = FlacDecodeDLL_GetNextPcmData(nFrames, data);
        ercd   = FlacDecodeDLL_GetLastResult();

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

    FlacDecodeDLL_DecodeEnd();

    if (ercd != 1) {
        printf("D: ERROR result=%d\n", ercd);
    }
	return 0;
}

