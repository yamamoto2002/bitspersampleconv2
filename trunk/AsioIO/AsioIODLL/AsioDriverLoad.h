#ifndef H_AsioDriverLoad
#define H_AsioDriverLoad

int  AsioDriverLoad_getDriverNum(void);
bool AsioDriverLoad_getDriverName(int n, char *name_return, int size);
bool AsioDriverLoad_loadDriver(int n);
void AsioDriverLoad_unloadDriver(void);

struct ASIOTimeStamp;
double AsioTimeStampToDouble(ASIOTimeStamp &a);
double AsioSamplesToDouble(ASIOSamples &a);

#endif /* H_AsioDriverLoad */
