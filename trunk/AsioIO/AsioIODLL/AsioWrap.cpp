#include "targetver.h"
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "asiosys.h"
#include "asio.h"
#include "AsioWrap.h"
#include "asiodrivers.h"
#include <assert.h>
#include <stdio.h>

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
    bool stopped;
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
        if (ap->bufferInfos[i].isInput == false) {
            switch (ap->channelInfos[i].type) {
            case ASIOSTInt16LSB:
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 2);
                break;
            case ASIOSTInt24LSB:                // used for 20 bits as well
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 3);
                break;
            case ASIOSTInt32LSB:
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 4);
                break;
            case ASIOSTFloat32LSB:      // IEEE 754 32 bit float, as found on Intel x86 architecture
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 4);
                break;
            case ASIOSTFloat64LSB:      // IEEE 754 64 bit double float, as found on Intel x86 architecture
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 8);
                break;

                // these are used for 32 bit data buffer, with different alignment of the data inside
                // 32 bit PCI bus systems can more easily used with these
            case ASIOSTInt32LSB16:      // 32 bit data with 18 bit alignment
            case ASIOSTInt32LSB18:      // 32 bit data with 18 bit alignment
            case ASIOSTInt32LSB20:      // 32 bit data with 20 bit alignment
            case ASIOSTInt32LSB24:      // 32 bit data with 24 bit alignment
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 4);
                break;

            case ASIOSTInt16MSB:
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 2);
                break;
            case ASIOSTInt24MSB:        // used for 20 bits as well
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 3);
                break;
            case ASIOSTInt32MSB:
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 4);
                break;
            case ASIOSTFloat32MSB:      // IEEE 754 32 bit float, as found on Intel x86 architecture
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 4);
                break;
            case ASIOSTFloat64MSB:      // IEEE 754 64 bit double float, as found on Intel x86 architecture
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 8);
                break;

                // these are used for 32 bit data buffer, with different alignment of the data inside
                // 32 bit PCI bus systems can more easily used with these
            case ASIOSTInt32MSB16:      // 32 bit data with 18 bit alignment
            case ASIOSTInt32MSB18:      // 32 bit data with 18 bit alignment
            case ASIOSTInt32MSB20:      // 32 bit data with 20 bit alignment
            case ASIOSTInt32MSB24:      // 32 bit data with 24 bit alignment
                memset (ap->bufferInfos[i].buffers[index], 0, buffSize * 4);
                break;
            }
        }
    }

    if (ap->postOutput) {
        ASIOOutputReady();
    }

    if (processedSamples >= ap->sampleRate * TEST_RUN_TIME) {
        ap->stopped = true;
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
        ap->stopped = true;
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
}

