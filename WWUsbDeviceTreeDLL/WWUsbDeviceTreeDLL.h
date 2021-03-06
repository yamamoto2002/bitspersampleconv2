// 日本語。

#include <stdint.h>

#pragma once


extern "C" {

#define WWUSB_STRING_COUNT (256)

#pragma pack(push, 4)
    struct WWUsbHostControllerCs {
        int idx;
        int numberOfRootPorts;
        uint32_t deviceCount;
        uint32_t currentUsbFrame;

        uint32_t bulkBytes;
        uint32_t isoBytes;
        uint32_t interruptBytes;
        uint32_t controlDataBytes;

        uint32_t pciInterruptCount;
        uint32_t hardResetCount;

        uint64_t totalBusBandwidth;     //< bits/sec
        uint64_t total32secBandwidth;   //< bits/32sec
        uint64_t allocedBulkAndControl; //< bits/32sec
        uint64_t allocedIso;            //< bits/32sec
        wchar_t name[WWUSB_STRING_COUNT];
        wchar_t desc[WWUSB_STRING_COUNT];
        wchar_t vendor[WWUSB_STRING_COUNT];

    };

    struct WWUsbHubCs {
        int idx;
        int parentIdx;
        int numPorts;
        int isBusPowered; //< TRUE: Bus powered, FALSE: Self powered
        int isRoot;
        int speed; //< WWUsbDeviceBusSpeed
        wchar_t name[WWUSB_STRING_COUNT];
    };

    struct WWUsbHubPortCs {
        int idx;
        int parentIdx;
        int deviceIsHub;
        int bmAttributes; //< USB config descriptor bmAttributes. 0x80: BUS_POWERED flag
        int powerMilliW;

        int speed; //< WWUsbDeviceBusSpeed
        int usbVersion; //< WWUsbDeviceBusSpeed
        int portConnectorType; //< WWUsbPortConnectorType
        int confDescBytes;
        int numStringDesc;

        unsigned char *confDesc;
        wchar_t name[WWUSB_STRING_COUNT];
        wchar_t product[WWUSB_STRING_COUNT];
        wchar_t vendor[WWUSB_STRING_COUNT];
    };

    struct WWUsbStringDescCs {
        int descIdx;
        int langId;
        int descType;
        wchar_t name[WWUSB_STRING_COUNT];
    };

#pragma pack(pop)

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_Init(void);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_Refresh(void);

__declspec(dllexport)
void __stdcall
WWUsbDeviceTreeDLL_Term(void);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_GetNumOfHostControllers(void);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_GetHostControllerInf(int nth, WWUsbHostControllerCs &hc_r);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_GetNumOfHubs(void);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_GetHubInf(int nth, WWUsbHubCs &hub_r);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_GetNumOfHubPorts(void);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_GetHubPortInf(int nth, WWUsbHubPortCs &hp_r);

__declspec(dllexport)
int __stdcall
WWUsbDeviceTreeDLL_GetStringDesc(int nth, int idx, WWUsbStringDescCs &sd_r);

}; // Extern "C"

