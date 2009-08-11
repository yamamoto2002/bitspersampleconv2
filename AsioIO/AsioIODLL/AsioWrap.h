#ifndef H_AsioWrap
#define H_AsioWrap

int
AsioWrap_getDriverNum(void);

bool
AsioWrap_getDriverName(int n, char *name_return, int size);

bool
AsioWrap_loadDriver(int n);

void
AsioWrap_unloadDriver(void);

struct ASIOTimeStamp;

double
AsioTimeStampToDouble(ASIOTimeStamp &a);

struct ASIOSamples;
double
AsioSamplesToDouble(ASIOSamples &a);

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
AsioWrap_setOutput(int outputChannel, int *data, int samples);

void
AsioWrap_setInput(int inputChannel, int samples);

int
AsioWrap_start(void);

bool
AsioWrap_run(void);

void
AsioWrap_stop(void);

void
AsioWrap_getRecordedData(int inputChannel, int recordedData_return[], int samples);

#endif /* H_AsioWrap */
