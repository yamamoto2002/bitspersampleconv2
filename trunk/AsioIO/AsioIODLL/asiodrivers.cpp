#include "asiodrivers.h"
#include <string.h>
#include "ginclude.h"

#include <windows.h>
#include "asiolist.h"
#include <assert.h>

class AsioDrivers : public AsioDriverList
{
public:
    AsioDrivers();
    ~AsioDrivers();
    
    bool getCurrentDriverName(char *name);
    long getDriverNames(char **names, long maxDrivers);
    bool loadDriver(char *name);
    void removeCurrentDriver();
    long getCurrentDriverIndex() {return curIndex;}
protected:
    unsigned long connID;
    long curIndex;
};

static AsioDrivers* g_pAsioDrivers = 0;

#include "iasiodrv.h"

extern IASIO* theAsioDriver;

AsioDrivers::AsioDrivers() : AsioDriverList()
{
    curIndex = -1;
}

AsioDrivers::~AsioDrivers()
{
}

bool AsioDrivers::getCurrentDriverName(char *name)
{
    if(curIndex >= 0)
        return asioGetDriverName(curIndex, name, 32) == 0 ? true : false;
    name[0] = 0;
    return false;
}

long AsioDrivers::getDriverNames(char **names, long maxDrivers)
{
    for(long i = 0; i < asioGetNumDev() && i < maxDrivers; i++)
        asioGetDriverName(i, names[i], 32);
    return asioGetNumDev() < maxDrivers ? asioGetNumDev() : maxDrivers;
}

bool AsioDrivers::loadDriver(char *name)
{
    char dname[64];
    char curName[64];

    for(long i = 0; i < asioGetNumDev(); i++)
    {
        if(!asioGetDriverName(i, dname, 32) && !strcmp(name, dname))
        {
            curName[0] = 0;
            getCurrentDriverName(curName);  // in case we fail...
            removeCurrentDriver();

            if(!asioOpenDriver(i, (void **)&theAsioDriver))
            {
                curIndex = i;
                return true;
            }
            else
            {
                theAsioDriver = 0;
                if(curName[0] && strcmp(dname, curName))
                    loadDriver(curName);    // try restore
            }
            break;
        }
    }
    return false;
}

void AsioDrivers::removeCurrentDriver()
{
    if(curIndex != -1)
        asioCloseDriver(curIndex);
    curIndex = -1;
}

////////////////////////////////////////////////////////////////////
// API

void AsioDrvInit(void)
{
    if(!g_pAsioDrivers) {
        g_pAsioDrivers = new AsioDrivers();
    }
}

void AsioDrvTerm(void)
{
    if (g_pAsioDrivers) {
        delete g_pAsioDrivers;
        g_pAsioDrivers = NULL;
    }
}

long AsioDrvGetCurrentDriverIndex(void)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->getCurrentDriverIndex();
}

bool AsioDrvGetCurrentDriverName(char *name)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->getCurrentDriverName(name);
}

long AsioDrvGetDriverNames(char **names, long maxDrivers)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->getDriverNames(names, maxDrivers);
}

bool AsioDrvLoadDriver(char *name)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->loadDriver(name);
}

void AsioDrvRemoveCurrentDriver(void)
{
    assert(g_pAsioDrivers);
    g_pAsioDrivers->removeCurrentDriver();
}

long AsioDrvOpenDriver(int a, void **b)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->asioOpenDriver(a, b);
}

long AsioDrvCloseDriver(int a)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->asioCloseDriver(a);
}

long AsioDrvGetNumDev(void)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->asioGetNumDev();
}

long AsioDrvGetDriverName(int a, char *b, int c)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->asioGetDriverName(a, b, c);
}

long AsioDrvGetDriverPath(int a, char *b, int c)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->asioGetDriverPath(a, b, c);
}

long AsioDrvGetDriverCLSID(int a, CLSID *b)
{
    assert(g_pAsioDrivers);
    return g_pAsioDrivers->asioGetDriverCLSID(a, b);
}
