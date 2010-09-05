#include "stdafx.h"
#include "FlacDecodeDLL.h"
#include <string.h>
#include <stdlib.h>

int _tmain(int argc, _TCHAR* argv[])
{
    if (argc != 3) {
        printf("E: %s:%d inputFlacFilePath outputWavFilePath\n", __FILE__, __LINE__);
        return 1;
    }

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

    int  ercd    = 0;
    int  nFrames = 1048576;
    char *data   = (char *)malloc(nFrames * channels * bitsPerSample / 8);
    do {
        int rv = FlacDecodeDLL_GetNextPcmData(nFrames, data);
        ercd   = FlacDecodeDLL_GetLastResult();

        printf("D: GetNextPcmData get %d samples\n", rv);

        if (rv <= 0 || ercd == FDRT_Completed) {
            printf("D: GetNextPcmData rv=%d ercd=%d\n", rv, ercd);
            break;
        }
    } while (true);

    free(data);
    data = NULL;

    FlacDecodeDLL_DecodeEnd();

    if (ercd != 1) {
        printf("D: ERROR result=%d\n", ercd);
    }
	return 0;
}

