#include "targetver.h"
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "asiosys.h"
#include "asio.h"
#include "AsioDriverLoad.h"
#include "asiodrivers.h"
#include <assert.h>

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

int AsioDriverLoad_getDriverNum(void)
{
    return getAsioDriverNum();
}

bool AsioDriverLoad_getDriverName(int n, char *name_return, int size)
{
    return getAsioDriverName(n, name_return, size);
}

bool AsioDriverLoad_loadDriver(int n)
{
    return loadAsioDriver(n);
}

void AsioDriverLoad_unloadDriver(void)
{
    unloadAsioDriver();
}