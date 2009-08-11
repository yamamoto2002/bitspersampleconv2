#ifndef H_AsioWrap
#define H_AsioWrap

int  AsioWrap_getDriverNum(void);
bool AsioWrap_getDriverName(int n, char *name_return, int size);
bool AsioWrap_loadDriver(int n);
void AsioWrap_unloadDriver(void);

struct ASIOTimeStamp;
struct ASIOSamples;
double AsioTimeStampToDouble(ASIOTimeStamp &a);
double AsioSamplesToDouble(ASIOSamples &a);

int
AsioWrap_setup(int sampleRate);

void
AsioWrap_unsetup(void);


int
AsioWrap_getInputChannelsNum(void);
int
AsioWrap_getOutputChannelsNum(void);

bool
AsioWrap_getInputChannelName(int n, char *name_return, int size);

bool
AsioWrap_getOutputChannelName(int n, char *name_return, int size);

void
AsioWrap_setOutputData(int outputChannel, int *data, int length);

void
AsioWrap_run(void);

#endif /* H_AsioWrap */
