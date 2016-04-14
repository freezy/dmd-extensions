#ifdef PIN2DMD_EXPORTS
	#define PIN2DMD_API __declspec(dllexport) 
#else
	#define PIN2DMD_API __declspec(dllimport) 
#endif

typedef struct rgb24 {
	UINT8 red;
	UINT8 green;
	UINT8 blue;
} rgb24;

//define vendor id and product id
#define MY_VID 0x0314
#define MY_PID 0xe457

//endpoints for communication
#define EP_IN 0x81
#define EP_OUT 0x01

unsigned char *OutputPacketBuffer;

extern "C"
{
PIN2DMD_API bool pin2dmdInit();
PIN2DMD_API bool pin2dmdDeInit();
PIN2DMD_API void pin2dmdRenderRGB24(rgb24 *currbuffer);
}