#include <stdio.h>
#include <string.h>
#include <windows.h>
#include "AsioWrap.h"

int main(void)
{
    printf("main() started\n");
    int i=0;
    for (i=0; i<AsioWrap_getDriverNum(); ++i) {
        char name[64];
        AsioWrap_getDriverName(i, name, sizeof name);
        printf("%d %s\n", i, name);

    }
    printf("count=%d\n", i);

    AsioWrap_loadDriver(0);

    ASIOError rv;
    
    rv = AsioWrap_setup(96000);

    if (ASIOStart() == ASE_OK) {
        printf("ASIOStart() success.\n\n");
        while (!ap->stopped) {
            Sleep(100);
        }
        ASIOStop();
        printf("ASIOStop()\n");
    }

    AsioWrap_finalize();
    AsioWrap_unloadDriver();

    return 0;
}
