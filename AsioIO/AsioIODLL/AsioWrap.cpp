// ASIO is a trademark and software of Steinberg Media Technologies GmbH

#include "targetver.h"
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "asiosys.h"
#include "asio.h"
#include "AsioWrap.h"
#include "asiodrivers.h"
#include <assert.h>
#include <stdio.h>

HANDLE hEvent;

struct WavData {
    int *data;
    int samples;
    int pos;
    int channel;
};

static WavData outputWavData;
static WavData inputWavData;

#if NATIVE_INT64
    #define ASIO64toDouble(a)  (a)
#else
    const double twoRaisedTo32 = 4294967296.;
    #define ASIO64toDouble(a)  ((a).lo + (a).hi * twoRaisedTo32)
#endif

double
AsioTimeStampToDouble(ASIOTimeStamp &a)
{
    return ASIO64toDouble(a);
}

double
AsioSamplesToDouble(ASIOSamples &a)
{
    return ASIO64toDouble(a);
}

extern AsioDrivers* asioDrivers;

static int
getAsioDriverNum(void)
{
    if(!asioDrivers) {
        asioDrivers = new AsioDrivers();
    }

    return (int)asioDrivers->asioGetNumDev();
}

static bool
getAsioDriverName(int n, char *name_return, int size)
{
    assert(name_return);

    name_return[0] = 0;

    if(!asioDrivers) {
        asioDrivers = new AsioDrivers();
    }

    if (asioDrivers->asioGetNumDev() <= n) {
        return false;
    }

    asioDrivers->asioGetDriverName(n, name_return, size);
    return true;
}

static bool
loadAsioDriver(int n)
{
    if(!asioDrivers) {
        asioDrivers = new AsioDrivers();
    }

    char name[64];
    name[0] = 0;
    asioDrivers->asioGetDriverName(n, name, 32);

    return asioDrivers->loadDriver(name);
}

static void
unloadAsioDriver(void)
{
    if (!asioDrivers) {
        return;
    }

    asioDrivers->removeCurrentDriver();
    delete asioDrivers;
    asioDrivers = NULL;
}

int AsioWrap_getDriverNum(void)
{
    return getAsioDriverNum();
}

bool AsioWrap_getDriverName(int n, char *name_return, int size)
{
    return getAsioDriverName(n, name_return, size);
}

bool AsioWrap_loadDriver(int n)
{
    return loadAsioDriver(n);
}

void AsioWrap_unloadDriver(void)
{
    unloadAsioDriver();
}

#include "asiosys.h"
#include "asio.h"
#include "AsioWrap.h"

#define TEST_RUN_TIME (3)

struct AsioPropertyInfo {
    ASIODriverInfo adi;
    long inputChannels;
    long outputChannels;
    long minSize;
    long maxSize;
    long preferredSize;
    long granularity;
    ASIOSampleRate sampleRate; /**< input param: 96000 or 44100 or whatever */
    bool postOutput;
    ASIOTime tInfo;
    ASIOBufferInfo  *bufferInfos;
    ASIOChannelInfo *channelInfos;
    long inputLatency;
    long outputLatency;
    double nanoSeconds;
    double samples;
    double tcSamples;
    long  sysRefTime;
};

static AsioPropertyInfo *
asioPropertyInstance(void)
{
    static AsioPropertyInfo ap;
    return &ap;
}

ASIOTime *
bufferSwitchTimeInfo(ASIOTime *timeInfo, long index, ASIOBool processNow)
{
    AsioPropertyInfo *ap = asioPropertyInstance();

    static long processedSamples = 0;

    ap->tInfo = *timeInfo;

    if (timeInfo->timeInfo.flags & kSystemTimeValid) {
        ap->nanoSeconds = AsioTimeStampToDouble(timeInfo->timeInfo.systemTime);
    } else {
        ap->nanoSeconds = 0;
    }

    if (timeInfo->timeInfo.flags & kSamplePositionValid) {
        ap->samples = AsioSamplesToDouble(timeInfo->timeInfo.samplePosition);
    } else {
        ap->samples = 0;
    }

    if (timeInfo->timeCode.flags & kTcValid) {
        ap->tcSamples = AsioSamplesToDouble(timeInfo->timeCode.timeCodeSamples);
    } else {
        ap->tcSamples = 0;
    }

    ap->sysRefTime = GetTickCount();

    long buffSize = ap->preferredSize;

    for (int i = 0; i <ap->inputChannels + ap->outputChannels; i++) {
        if (ap->bufferInfos[i].isInput == true &&
            ap->channelInfos[i].channel == inputWavData.channel) {
            assert(ASIOSTInt32LSB == ap->channelInfos[i].type);
            memcpy(&inputWavData.data[inputWavData.pos], ap->bufferInfos[i].buffers[index], buffSize * 4);
            inputWavData.pos += buffSize;
        }
        if (ap->bufferInfos[i].isInput == false &&
            ap->channelInfos[i].channel == outputWavData.channel) {
            assert(ASIOSTInt32LSB == ap->channelInfos[i].type);

            memcpy(ap->bufferInfos[i].buffers[index], &outputWavData.data[outputWavData.pos], buffSize * 4);
            outputWavData.pos += buffSize;
        }
    }

    if (ap->postOutput) {
        ASIOOutputReady();
    }

    if (outputWavData.samples <= outputWavData.pos ||
        inputWavData.samples <= inputWavData.pos) {
        SetEvent(hEvent);
    } else {
        processedSamples += buffSize;
    }

    return 0L;
}

//----------------------------------------------------------------------------------
static void
bufferSwitch(long index, ASIOBool processNow)
{
    ASIOTime  timeInfo;
    memset (&timeInfo, 0, sizeof (timeInfo));

    if(ASIOGetSamplePosition(&timeInfo.timeInfo.samplePosition, &timeInfo.timeInfo.systemTime) == ASE_OK) {
        timeInfo.timeInfo.flags = kSystemTimeValid | kSamplePositionValid;
    }

    bufferSwitchTimeInfo (&timeInfo, index, processNow);
}


//----------------------------------------------------------------------------------
static void
sampleRateChanged(ASIOSampleRate sRate)
{
    printf("sampleRateChanged(%f)\n", sRate);
}

//----------------------------------------------------------------------------------
long asioMessages(long selector, long value, void* message, double* opt)
{
    AsioPropertyInfo *ap = asioPropertyInstance();
    
    long ret = 0;
    switch(selector) {
    case kAsioSelectorSupported:
        if(value == kAsioResetRequest
        || value == kAsioEngineVersion
        || value == kAsioResyncRequest
        || value == kAsioLatenciesChanged
        || value == kAsioSupportsTimeInfo
        || value == kAsioSupportsTimeCode
        || value == kAsioSupportsInputMonitor)
            ret = 1L;
        break;
    case kAsioResetRequest:
        SetEvent(hEvent);
        ret = 1L;
        break;
    case kAsioResyncRequest:
        ret = 1L;
        break;
    case kAsioLatenciesChanged:
        ret = 1L;
        break;
    case kAsioEngineVersion:
        ret = 2L;
        break;
    case kAsioSupportsTimeInfo:
        ret = 1;
        break;
    case kAsioSupportsTimeCode:
        ret = 0;
        break;
    }
    return ret;
}

//----------------------------------------------------------------------------------
int
AsioWrap_setup(int sampleRate)
{
    AsioPropertyInfo *ap = asioPropertyInstance();
    ASIOError rv;

    ap->sampleRate = sampleRate;

    memset(&ap->adi, 0, sizeof ap->adi);
    rv = ASIOInit(&ap->adi);
    if (ASE_OK != rv) {
        printf ("ASIOGetChannels err %d\n", rv);
        return rv;
    }
    printf ("ASIOInit()\n"
        "  asioVersion:   %d\n"
        "  driverVersion: %d\n"
        "  Name:          %s\n"
        "  ErrorMessage:  %s\n",
        ap->adi.asioVersion, ap->adi.driverVersion,
        ap->adi.name, ap->adi.errorMessage);

    rv = ASIOGetChannels(&ap->inputChannels, &ap->outputChannels);
    if (ASE_OK != rv) {
        printf ("ASIOGetChannels err %d\n", rv);
        return rv;
    }
    printf ("ASIOGetChannels (inputs: %d, outputs: %d);\n",
        ap->inputChannels, ap->outputChannels);

    int totalChannels = ap->inputChannels + ap->outputChannels;

    ap->bufferInfos  = new ASIOBufferInfo[totalChannels];
    ap->channelInfos = new ASIOChannelInfo[totalChannels];

    rv = ASIOGetBufferSize(&ap->minSize, &ap->maxSize, &ap->preferredSize, &ap->granularity);
    if (ASE_OK != rv) {
        printf ("ASIOGetBufferSize err %d\n", rv);
        return rv;
    }
    printf ("ASIOGetBufferSize (min: %d, max: %d, preferred: %d, granularity: %d);\n",
             ap->minSize, ap->maxSize,
             ap->preferredSize, ap->granularity);

    rv = ASIOCanSampleRate(ap->sampleRate);
    if (ASE_OK != rv) {
        printf ("ASIOCanSampleRate (sampleRate: %f) failed %d\n", ap->sampleRate, rv);
        return rv;
    }

    rv = ASIOSetSampleRate(ap->sampleRate);
    if (ASE_OK != rv) {
        printf ("ASIOSetSampleRate (sampleRate: %f) failed %d\n", ap->sampleRate, rv);
        return rv;
    }
    printf ("ASIOSetSampleRate (sampleRate: %f)\n", ap->sampleRate);

    ap->postOutput = true;
    rv = ASIOOutputReady();
    if (ASE_OK != rv) {
        ap->postOutput = false;
    }
    printf ("ASIOOutputReady(); - %s\n", ap->postOutput ? "Supported" : "Not supported");

    ASIOBufferInfo *info = ap->bufferInfos;

    for (int i=0; i<ap->inputChannels; ++i) {
        info->isInput    = ASIOTrue;
        info->channelNum = i;
        info->buffers[0] = 0;
        info->buffers[1] = 0;
        ++info;
    }

    for (int i=0; i<ap->outputChannels; ++i) {
        info->isInput    = ASIOFalse;
        info->channelNum = i;
        info->buffers[0] = 0;
        info->buffers[1] = 0;
        ++info;
    }

    static ASIOCallbacks asioCallbacks;
    asioCallbacks.bufferSwitch         = &bufferSwitch;
    asioCallbacks.sampleRateDidChange  = &sampleRateChanged;
    asioCallbacks.asioMessage          = &asioMessages;
    asioCallbacks.bufferSwitchTimeInfo = &bufferSwitchTimeInfo;

    rv = ASIOCreateBuffers(ap->bufferInfos,
        totalChannels, ap->preferredSize, &asioCallbacks);
    if (ASE_OK != rv) {
        printf ("ASIOCreateBuffers() failed %d\n", rv);
        return rv;
    }
    printf ("ASIOCreateBuffers() success.\n");

    for (int i=0; i<totalChannels; i++) {
        ap->channelInfos[i].channel = ap->bufferInfos[i].channelNum;
        ap->channelInfos[i].isInput = ap->bufferInfos[i].isInput;
        rv = ASIOGetChannelInfo(&ap->channelInfos[i]);
        if (ASE_OK != rv) {
            printf ("ASIOGetChannelInfo() failed %d\n", rv);
            return rv;
        }
        printf("i=%2d ch=%2d isInput=%d chGroup=%08x type=%2d name=%s\n",
            i,
            ap->channelInfos[i].channel,
            ap->channelInfos[i].isInput,
            ap->channelInfos[i].channelGroup,
            ap->channelInfos[i].type,
            ap->channelInfos[i].name);
    }

    rv = ASIOGetLatencies(&ap->inputLatency, &ap->outputLatency);
    if (ASE_OK != rv) {
        printf ("ASIOGetLatencies() failed %d\n", rv);
        return rv;
    }
    printf ("ASIOGetLatencies (input: %d, output: %d);\n",
        ap->inputLatency, ap->outputLatency);

    return ASE_OK;
}

int
AsioWrap_getInputChannelsNum(void)
{
    AsioPropertyInfo *ap = asioPropertyInstance();
    return ap->inputChannels;
}

int
AsioWrap_getOutputChannelsNum(void)
{
    AsioPropertyInfo *ap = asioPropertyInstance();
    return ap->outputChannels;
}

bool
AsioWrap_getInputChannelName(int n, char *name_return, int size)
{
    AsioPropertyInfo *ap = asioPropertyInstance();

    if (n < 0 || ap->inputChannels <= n) {
        assert(0);
    }

    memcpy_s(name_return, size,
        ap->channelInfos[n].name, sizeof ap->channelInfos[0].name);
    return true;
}

bool
AsioWrap_getOutputChannelName(int n, char *name_return, int size)
{
    AsioPropertyInfo *ap = asioPropertyInstance();

    if (n < 0 || ap->outputChannels <= n) {
        assert(0);
    }

    memcpy_s(name_return, size,
        ap->channelInfos[n + ap->inputChannels].name, sizeof ap->channelInfos[0].name);
    return true;
}

void
AsioWrap_unsetup(void)
{
    ASIODisposeBuffers();
    printf("ASIODisposeBuffers()\n");
    ASIOExit();
    printf("ASIOExit()\n");

    delete[] inputWavData.data;
    inputWavData.data = NULL;

    delete[] outputWavData.data;
    outputWavData.data = NULL;
}

void
AsioWrap_setOutput(int outputChannel, int *data, int samples)
{
    delete[] outputWavData.data;
    outputWavData.data = NULL;

    outputWavData.data = new int[samples];
    memcpy(outputWavData.data, data, samples * 4);
    outputWavData.samples = samples;
    outputWavData.pos = 0;
    outputWavData.channel = outputChannel;
}

void
AsioWrap_setInput(int inputChannel, int samples)
{
    delete[] inputWavData.data;
    inputWavData.data = NULL;

    inputWavData.data = new int[samples];
    inputWavData.samples = samples;
    inputWavData.pos = 0;
    inputWavData.channel = inputChannel;
}

void
AsioWrap_getRecordedData(int inputChannel, int recordedData_return[], int samples)
{
    assert(inputWavData.data);
    memcpy_s(recordedData_return, samples * 4, inputWavData.data, inputWavData.pos *4);
}

int
AsioWrap_start(void)
{
    AsioPropertyInfo *ap = asioPropertyInstance();

    assert(!hEvent);
    hEvent = CreateEvent(NULL,FALSE,FALSE,"Test");
    printf("CreateEvent()\n");

    ASIOError rv = ASIOStart();
    if (rv == ASE_OK) {
        printf("ASIOStart() success.\n\n");
    } else {
        printf("ASIOStart() failed %d\n", rv);
    }
    return rv;
}

bool
AsioWrap_run(void)
{
    AsioPropertyInfo *ap = asioPropertyInstance();

    assert(hEvent);

    printf("WaitForSingleObject() start\n");
    DWORD rv = WaitForSingleObject(hEvent, 1000);
    printf("WaitForSingleObject() %x\n", rv);
    if (rv == WAIT_TIMEOUT) {
        return false;
    }

    ASIOStop();
    printf("ASIOStop()\n");

    CloseHandle(hEvent);
    hEvent = NULL;
    printf("CloseHandle()\n");
    return true;
}

void
AsioWrap_stop(void)
{
    printf("AsioWrap_stop\n");
    if (hEvent) {
        printf("AsioWrap_stop calling SetEvent()\n");
        SetEvent(hEvent);
    }
}
