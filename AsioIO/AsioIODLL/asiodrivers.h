#ifndef AsioDrivers__
#define AsioDrivers__

#include <windows.h>

void AsioDrvInit(void);
void AsioDrvTerm(void);

bool AsioDrvGetCurrentDriverName(char *name);
long AsioDrvGetDriverNames(char **names, long maxDrivers);
bool AsioDrvLoadDriver(char *name);
void AsioDrvRemoveCurrentDriver(void);
long AsioDrvGetCurrentDriverIndex(void);

long AsioDrvOpenDriver(int,void **);
long AsioDrvCloseDriver(int);
long AsioDrvGetNumDev(void);
long AsioDrvGetDriverName(int, char *, int);		
long AsioDrvGetDriverPath(int, char *, int);
long AsioDrvGetDriverCLSID(int, CLSID *);

#endif
