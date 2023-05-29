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
// Copyright 2023 PinMAME Team
// 
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
// 
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
// 
// 2. Redistributions in binary form must reproduce the above copyright notice, 
//    this list of conditions and the following disclaimer in the documentation 
//    and/or other materials provided with the distribution.
// 
// 3. Neither the name of the copyright holder nor the names of its contributors may
//    be used to endorse or promote products derived from this software without 
//    specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS “AS IS” AND 
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
// IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, 
// INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
// NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
// POSSIBILITY OF SUCH DAMAGE.

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

Just load the parent solution into Visual Studio and you should be fine. Set up
for VS2022, but you should able to compile it with other versions as well.

If you want the build process automatically copy `DmdDevice.dll` into your VPM
folder, point the `VPM_HOME` environment variable to your VPM folder.

## Testing

This is a list of particular games that should be tested after major changes.

| Title                           | ROM Name | Output                          | Comments               |
|---------------------------------|----------|---------------------------------|------------------------|
| Riverboat Gambler (L-3)         | rvrbt    | 1x16Alpha 1x16Num 1x7Num 1x4Num | Support recently added |
| Police Force (LA-4)             | polic_l4 | 2x7Num 2x7Num 4x1Num            | Support recently added |
| Jokerz (L-6)                    | jokrz_l6 | 2x7Num 2x16Alpha                |                        |
| Bad Girls                       | badgirls | 2x20Alpha                       |                        |
| High Speed (L-3)                | hs_l3    | 2x7Alpha 2x7Num 4x1Num          |                        |
| Supersonic (7-digit conversion) | sstb     | 2x7Num 2x7Num 4x1Num            |                        |
| Black Knight (L-4)              | bk_l4    | 2x7Num 2x7Num 4x1Num_gen7       |                        |
| Space Shuttle (L-3)             | sshtl_l3 | 2x7Num10 2x7Num10 4x1Num        |                        |
| Topaz (Shuffle) (L-1)           | topaz_l1 | 2x6Num 2x6Num 4x1Num            |                        |
| Atlantis (rev. 3)               | atlantis | 4x7Num10                        |                        |
| Alley Cats (Shuffle) (L-7)      | alcat_l7 | 6x4Num_4x1Num                   |                        |