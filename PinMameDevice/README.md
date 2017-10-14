# DmdDevice.dll

*A universal library for real DMDs*

## History

The API was originally created for Visual PinMAME. At the time, the goal was to
externalize DMD rendering to a separate DLL, so VPM wouldn't need a special
build process for every new display (there are four so far).

The DLL, called "DMDext" when installing, is shipped today with the Visual
Pinball installer. It supports multiple displays as well as a nice virtual
DMD that is fast due to a pixel shader doing most of the work on the graphics
card.

It also comes with a few more features, notably the coloring feature developed
originally by Lucky1 for PIN2DMD.

## API

The header to include into your C/C++ host application looks like this:

```c
#ifdef DMDDEVICE_DLL_EXPORTS
	#define DMDDEV __declspec(dllexport) 
#else
	#define DMDDEV __declspec(dllimport) 
#endif

typedef struct tPMoptions {
	int dmd_red, dmd_green, dmd_blue;
	int dmd_perc66, dmd_perc33, dmd_perc0;
	int dmd_only, dmd_compact, dmd_antialias;
	int dmd_colorize;
	int dmd_red66, dmd_green66, dmd_blue66;
	int dmd_red33, dmd_green33, dmd_blue33;
	int dmd_red0, dmd_green0, dmd_blue0;
} tPMoptions;

typedef struct rgb24 {
	unsigned char red;
	unsigned char green;
	unsigned char blue;
} rgb24;

UINT8 *OutputPacketBuffer;

typedef int(*Console_Input_t)(UINT8 *buf, int size);
Console_Input_t Console_Input = NULL;

typedef enum { 
	None,
	__2x16Alpha, 
	__2x20Alpha, 
	__2x7Alpha_2x7Num, 
	__2x7Alpha_2x7Num_4x1Num, 
	__2x7Num_2x7Num_4x1Num, 
	__2x7Num_2x7Num_10x1Num, 
	__2x7Num_2x7Num_4x1Num_gen7, 
	__2x7Num10_2x7Num10_4x1Num,
	__2x6Num_2x6Num_4x1Num,
	__2x6Num10_2x6Num10_4x1Num,
	__4x7Num10,
	__6x4Num_4x1Num,
	__2x7Num_4x1Num_1x16Alpha,
	__1x16Alpha_1x16Num_1x7Num
} layout_t;

#ifdef __cplusplus
extern "C"
{
#endif

DMDDEV int Open();
DMDDEV bool Close();
DMDDEV void Set_4_Colors_Palette(rgb24 color0, rgb24 color33, rgb24 color66, rgb24 color100);
DMDDEV void Set_16_Colors_Palette(rgb24 *color);
DMDDEV void PM_GameSettings(const char* GameName, UINT64 HardwareGeneration, const tPMoptions &Options);
DMDDEV void Render_4_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer);
DMDDEV void Render_16_Shades(UINT16 width, UINT16 height, UINT8 *currbuffer);
DMDDEV void Render_RGB24(UINT16 width, UINT16 height, rgb24 *currbuffer);
DMDDEV void Render_PM_Alphanumeric_Frame(layout_t, const UINT16 *const seg_data, const UINT16 *const seg_data2);
DMDDEV void Console_Data(UINT8 data);
DMDDEV void Console_Input_Ptr(Console_Input_t ptr);

#ifdef __cplusplus
}
#endif
```

## Building

Just load the parent solution into Visual Studio and you should be fine. Tested
on VS2015 and VS2017.

If you want the build process automatically copy `DmdDevice.dll` into your VPM
folder, point the `VPM_HOME` environment variable to your VPM folder.