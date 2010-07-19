#include "WasapiWrap.h"
#include "WWUtil.h"

#include <stdio.h>
#include <Windows.h>

static HRESULT
GetIntValueFromConsole(const char *prompt, int from, int to, int *value_return)
{
    *value_return = 0;

    printf("%s (%d to %d) ? ", prompt, from, to);
    fflush(stdout);

    char s[32];
    char *result = fgets(s, 31, stdin);
    if (NULL == result) {
        return E_INVALIDARG;
    }

    char *p = NULL;
    errno = 0;
    int v = (int)strtol(s, &p, 10);
    if (errno != 0 || p == s) {
        printf("E: malformed input...\n");
        return E_INVALIDARG;
    }
    if (v < from || to < v) {
        printf("E: value is out of range.\n");
        return E_INVALIDARG;
    }

    *value_return = v;

    return NOERROR;
}

static HRESULT
Run(int deviceId, WWPcmData *pcm)
{
    HRESULT hr;
    WasapiWrap ww;

    HRR(ww.Init());
    HRG(ww.DoDeviceEnumeration());

    printf("Device list:\n");
    for (int i=0; i<ww.GetDeviceCount(); ++i) {
        wchar_t name[WW_DEVICE_NAME_COUNT];

        ww.GetDeviceName(i, name, sizeof name);
        printf("    deviceId=%d: %S\n", i, name);
    }
    printf("\n");

    ww.ChooseDevice(deviceId);
    if (deviceId < 0) {
        goto end;
    }

    HRG(ww.Setup(pcm->nSamplesPerSec, pcm->bitsPerSample, 10));

    ww.Start(pcm);

    while (!ww.Run(1000)) {
        printf("%d / %d\n", ww.GetPosFrame(), ww.GetTotalFrameNum());
    }

end:
    ww.Stop();
    ww.Unsetup();
    ww.Term();
    return NOERROR;
}

static void
PrintUsage(void)
{
    printf(
        "Usage:\n"
        "    PlayPcm\n"
        "        Enumerate all available devices\n"
        "    PlayPcm -d [deviceId] input_wave_file_name\n"
        "        Play wav file on deviceId device\n"
        "        Example:\n"
        "            PlayPcm -d 1 C:\\wav\\music.wav\n\n"
        );
}

/*
static int
ReadAllFileContents(const char *path, unsigned char **buff_return)
{
    int bytes = 0;
    unsigned char *buff = *buff_return;

    buff = NULL;

    FILE *fp = fopen(path, "rb");
    if (NULL == fp) {
        return NULL;
    }

    fseek(fp, 0, SEEK_END);
    bytes = ftell(fp);
    if (bytes <= 0) {
        goto end;
    }

    rewind(fp);

    buff = (unsigned char*)malloc(bytes);
    if (NULL == buff) {
        goto end;
    }

    int r = fread(buff, 1, bytes, fp);
    if (r != bytes) {
        free(buff);
        buff = NULL;
        goto end;
    }

end:
    fclose(fp);
    return bytes;
}
*/

struct WaveFormatInfo {
    int bitsPerSample;
    int nChannels;
    int nSamplesPerSec;
    int nFrames;
    unsigned char *data;
};

static bool
ReadWaveChunk(FILE *fp, WaveFormatInfo &wfi)
{
    bool result = false;
    unsigned char header[8];
    UINT32 bytes;
    
    bytes = fread(header, 1, 8, fp);
    if (bytes != 8) {
        return false;
    }

    UINT32 chunkSize = *((UINT32*)(&header[4]));
    if (chunkSize == 0) {
        return false;
    }
    unsigned char *buff = (unsigned char*)malloc(chunkSize);
    if (NULL == buff) {
        return false;
    }
    bytes = fread(buff, 1, chunkSize, fp);
    if (bytes != chunkSize) {
        goto end;
    }

    if (0 == strncmp("fmt ", (const char *)header, 4)) {
        if (chunkSize < 16) {
            goto end;
        }

        int wFormatTag = *((short*)(&buff[0]));
        if (wFormatTag != 1) { /* PCM */
            printf("E: wave fmt %d is not supported\n", wFormatTag);
            goto end;
        }
        wfi.nChannels = *((short*)(&buff[2]));
        wfi.nSamplesPerSec = *((int*)(&buff[4]));
        wfi.bitsPerSample = *((short*)(&buff[14]));
        if (wfi.nChannels != 2) {
            printf("E: nChannels=%d is not supported\n", wfi.nChannels);
            goto end;
        }
    } else if (0 == strncmp("data", (const char*)header, 4)) {
        int bytesPerFrame = wfi.nChannels * (wfi.bitsPerSample/8);
        if (bytesPerFrame == 0) {
            goto end;
        }

        wfi.data = buff;
        wfi.nFrames = chunkSize / bytesPerFrame;
        buff = NULL;
    } else {
        fseek(fp, chunkSize, SEEK_CUR);
    }

    result = true;

end:
    free(buff);
    return result;
}

static WWPcmData *
LoadAndCreatePcmData(const char *path)
{
    unsigned char buff[12];
    WWPcmData *result = NULL;
    WaveFormatInfo wfi;

    memset(&wfi, 0, sizeof wfi);

    FILE *fp = fopen(path, "rb");
    if (NULL == fp) {
        return NULL;
    }

    int rv = fread(buff, 1, 12, fp);
    if (rv != 12) {
        goto end;
    }
    if (0 != (strncmp("RIFF", (const char*)buff, 4))) {
        return NULL;
    }
    if (0 != (strncmp("WAVE", (const char*)&buff[8], 4))) {
        return NULL;
    }

    while (true) {
        if (!ReadWaveChunk(fp, wfi)) {
            break;
        }
    }

    if (wfi.data) {
        result = new WWPcmData();
        result->bitsPerSample  = wfi.bitsPerSample;
        result->nChannels      = wfi.nChannels;
        result->nSamplesPerSec = wfi.nSamplesPerSec;
        result->nFrames        = wfi.nFrames;
        result->posFrame = 0;
        result->stream = wfi.data;
    }
    
end:
    fclose(fp);
    return result;
}

int
main(int argc, char *argv[])
{
    WWPcmData *pcmData = NULL;
    int deviceId = -1;

    if (argc == 4) {
        if (0 != strcmp("-d", argv[1])) {
            PrintUsage();
            return 1;
        }
        deviceId = atoi(argv[2]);

        pcmData = LoadAndCreatePcmData(argv[3]);
        if (NULL == pcmData) {
            printf("E: read error %s\n",
                argv[3]);
            return 1;
        }
    } else {
        PrintUsage();
    }

    HRESULT hr = Run(deviceId, pcmData);
    if (FAILED(hr)) {
        printf("Run failed (%08x)\n", hr);
    }

    if (NULL != pcmData) {
        pcmData->Term();
        delete pcmData;
    }

    return 0;
}

