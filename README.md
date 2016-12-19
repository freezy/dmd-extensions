# DMD Extensions

*Features for DMD owners that aren't officially supported*

Real DMDs for virtual pinball cabinets are pretty cool but they need to be
supported by the software running the games. This is an attempt to fill some
gaps.


## Supported Displays

Currently supported displays:

- **PinDMD v3**, full RGB support
- **PinDMD v2**, 4 bit (16 shades) support
- **PinDMD v1**, 2 bit (4 shades) support
- **PIN2DMD**, full RGB support
- **Virtual DMD** on a computer monitor, renders nice dots and is useful for debugging.

## Features

The primary goal of this project was to add support real DMDs for games that
don't provide support out of the box. Supported games are:

- **Pinball FX2** through frame grabbing from screen 
- Farsight's **The Pinball Arcade** through grabbing the DMD texture from memory
- **Pro Pinball Timeshock** through their message queue
- **Visual PinMAME** through `dmddevice.dll` 

The command line tool can also display image files on the DMD device and render
frames to bitmap files.

### Colors

If you have an RGB display, you can override the color tone in which the output
is rendered. However, in order to do this, you must force the source to produce
grayscale frames. You can do this by using the `--render-as` parameter. Then
use the `--color` parameter to specify the color. Example:

    dmdext test -d virtual --render-as gray4 --color=8080ff

## Install Instructions

1. [Get the latest release](https://github.com/freezy/dmd-extensions/releases),
   copy the content of the zip file to your hard drive, preferable in your `PATH`,
   and unblock the `.exe` file (right-click, properties, unblock).
2. Download and install the [Visual C++ Redistributable for Visual Studio 2015](https://www.microsoft.com/en-us/download/details.aspx?id=48145).

### Test

1. Open a command prompt ([Windows]+[R], `cmd`, [enter])
2. Type `dmdext test` [enter]

You should see a test image on your DMD as well as on a virtual DMD.

### Pinball FX2

1. Enable cabinet options in Pinball FX2
2. Resize the DMD to:
   - Width: `1040`
   - Height: `272`
3. Move the DMD to somewhere hidden like off-screen or behind the playfield
   (usually at `0`/`0`).
4. Open a command prompt ([Windows]+[R], `cmd`, [enter])
5. Type `dmdext mirror --source=pinballfx2 --no-virtual` [enter]
6. Start Pinball FX2

For further tweaking, see options below.

### The Pinball Arcade

For TPA we're pulling frames off the DMD texture from TPA's memory. Thus, you
must be running the tool as administrator.

1. Open an administrator command prompt ([Windows], type `cmd`, right-click on `cmd.exe`, click on "Run as administrator")
2. Type `dmdext mirror --source=pinballarcade --no-virtual` [enter]
3. Start the DX11 version of The Pinball Arcade.
4. Select and **start** a game (*only then* it starts mirroring, during attract mode it doesn't, even though TPA does display the DMD).

Currently re-running `dmdext` while TPA is running doesn't work and will be 
addressed soon.

Also note that currently only 128x32 DMD games are supported, that means no
EM games neither Frankenstein which has a different resolution.

### Pro Pinball Ultra

In version 1.2.1, Barnstorm Games added support for external hardware through
a message queue. To get it running, do the following:

1. Make sure you have `dmdext.exe` in your `PATH`
2. Copy `ProPinballSlave.bat` to the Pro Pinball installation folder (usually at `%PROGRAMFILES(X86)%\Steam\SteamLibrary\steamapps\common\Pro Pinball Ultra`)
3. Add the following command line parameters when launching `ProPinball.exe`: `m3 dProPinballSlave`

Note that this currently works only with the 64-bit version of Pro Pinball. 32 bit binaries will
be provided with a later release. If you want to change the options (e.g. show the virtual DMD), 
edit `ProPinballSlave.bat` and adopt accordingly.

### Visual PinMAME

VPM recently introduced the possibility of delegating DMD rendering to an external
DLL. The main motivation is being able to ship a single build instead of having to
release multiple binaries for multiple DMDs. 

You can enable this by copying the included `DmdDevice.dll` to the location of
`VPinMAME.dll`. In VPM you then need to enable *Use External DMD* and then 
rendering should go through dmdext.

If you are a PC monitor user or have a RGB display (PinDMDv3 or PIN2DMD), the
advantage of dmdext is that Lucky1's coloring features are fully supported.

## Documentation

All options are documented in the tool.

```
C:\>dmdext

DMD Extensions v1.3.0-beta3
USAGE: dmdext <command> [<options>]

  mirror    Mirrors pixel data from the screen or memory to all available
            devices.

  play      Plays any media on all available devices (currently only images).

  test      Displays a test image on all available devices.
```

### Mirror

```
C:\>dmdext mirror --help

DMD Extensions v1.3.0-beta3
USAGE: dmdext mirror --source=<source> [--destination=<destination>]

  -s, --source             Required. The source you want to retrieve DMD data
                           from. One of: [ pinballfx2, pinballarcade, screen ].

  -f, --fps                How many frames per second should be mirrored.
                           Default: 25

  -p, --position           [screen] Position and size of screen grabber source.
                           Four values: <Left> <Top> <Width> <Height>. Default:
                           "0 0 128 32".

  --grid-spacing           [pinballfx2] How much of the white space around the
                           dot should be cut off. 1 means same size as the dot,
                           0.5 half size, etc. 0 for disable. Default: 1.

  --grid-size              [pinballfx2] Number of horizontal and vertical dots
                           when removing grid spacing. Two values: <Width>
                           <Height>. Default: "128 32".

  --no-shading             [pinballfx2, pinballarcade] Disable shading, i.e.
                           artificial downsampling for RGB displays. Default:
                           false.

  --shading-numshades      [pinballfx2] Number of shades for artifical
                           downsampling for RGB displays. Default: 4

  --shading-intensity      [pinballfx2] Multiplies luminosity of the parsed dot
                           so it covers the whole spectrum before downsampling.
                           Default: 2.5.

  --shading-brightness     [pinballfx2] Adds luminosity to the parsed dot after
                           being multiplied. Useful if even black dots should
                           be slightly illuminated. Default: 0.1.

  --dmd-crop               [pinballfx2] How many pixels should be cropped off
                           the DMD window. Four values: <Left> <Top> <Right>
                           <Bottom>. Default: "12 8 8 12".

  --propinball-args        [propinball] Arguments send from the Pro Pinball
                           master process. Usually something like: "ndmd
                           w0_0_0_0_w m392".

  -d, --destination        The destination where the DMD data is sent to. One
                           of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd,
                           virtual ]. Default: "auto", which outputs to all
                           available devices.

  -r, --resize             How the source image is resized. One of: [ stretch,
                           fill, fit ]. Default: "stretch".

  --no-virtual             Explicitly disables the virtual DMD when destination
                           is "auto". Default: false.

  --virtual-stay-on-top    Makes the virtual DMD stay on top of other
                           application windows. Default: false.

  --virtual-hide-grip      Hides the resize grip of the virtual DMD. Default:
                           false.

  --virtual-position       Position and size of virtual DMD. Four values:
                           <Left> <Top> <Width> [<Height>]. Height is optional
                           and can be used for custom aspect ratio. Default: "0
                           0 1024".

  --render-as              Internally process at a given bit length. One of: [
                           gray2, gray4, rgb24, bitmap ]. Default: "bitmap"

  -c, --color              Sets the color of a grayscale source that is
                           rendered on an RGB destination. Default: ff3000

  --flip-x                 Flips the image horizontally. Default: false.

  --flip-y                 Flips the image vertically. Default: false.

  -p, --port               Force COM port for PinDMDv3 devices. Example:
                           "COM3".

  -q, --quit-when-done     Exit the program when finished, e.g. when Pinball
                           FX2 doesn't receive any frames anymore. Default:
                           false

  -o, --output-to-file     If set, writes all frames as PNG bitmaps to the
                           provided folder.
```

### Media

```
C:\>dmdext play --help

DMD Extensions v1.3.0-beta3
USAGE: dmdext play --file=<image path> [--destination=<destination>]

  -f, --file               Required. Path to the file to play. Currently
                           supported file types: PNG, JPG, BIN (raw).

  -d, --destination        The destination where the DMD data is sent to. One
                           of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd,
                           virtual ]. Default: "auto", which outputs to all
                           available devices.

  -r, --resize             How the source image is resized. One of: [ stretch,
                           fill, fit ]. Default: "stretch".

  --no-virtual             Explicitly disables the virtual DMD when destination
                           is "auto". Default: false.

  --virtual-stay-on-top    Makes the virtual DMD stay on top of other
                           application windows. Default: false.

  --virtual-hide-grip      Hides the resize grip of the virtual DMD. Default:
                           false.

  --virtual-position       Position and size of virtual DMD. Four values:
                           <Left> <Top> <Width> [<Height>]. Height is optional
                           and can be used for custom aspect ratio. Default: "0
                           0 1024".

  --render-as              Internally process at a given bit length. One of: [
                           gray2, gray4, rgb24, bitmap ]. Default: "bitmap"

  -c, --color              Sets the color of a grayscale source that is
                           rendered on an RGB destination. Default: ff3000

  --flip-x                 Flips the image horizontally. Default: false.

  --flip-y                 Flips the image vertically. Default: false.

  -p, --port               Force COM port for PinDMDv3 devices. Example:
                           "COM3".

  -q, --quit-when-done     Exit the program when finished, e.g. when Pinball
                           FX2 doesn't receive any frames anymore. Default:
                           false

  -o, --output-to-file     If set, writes all frames as PNG bitmaps to the
                           provided folder.
```
### Test

```
C:\>dmdext test --help

DMD Extensions v1.3.0-beta3
USAGE: dmdext test [--destination=<destination>]

  -d, --destination        The destination where the DMD data is sent to. One
                           of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd,
                           virtual ]. Default: "auto", which outputs to all
                           available devices.

  -r, --resize             How the source image is resized. One of: [ stretch,
                           fill, fit ]. Default: "stretch".

  --no-virtual             Explicitly disables the virtual DMD when destination
                           is "auto". Default: false.

  --virtual-stay-on-top    Makes the virtual DMD stay on top of other
                           application windows. Default: false.

  --virtual-hide-grip      Hides the resize grip of the virtual DMD. Default:
                           false.

  --virtual-position       Position and size of virtual DMD. Four values:
                           <Left> <Top> <Width> [<Height>]. Height is optional
                           and can be used for custom aspect ratio. Default: "0
                           0 1024".

  --render-as              Internally process at a given bit length. One of: [
                           gray2, gray4, rgb24, bitmap ]. Default: "bitmap"

  -c, --color              Sets the color of a grayscale source that is
                           rendered on an RGB destination. Default: ff3000

  --flip-x                 Flips the image horizontally. Default: false.

  --flip-y                 Flips the image vertically. Default: false.

  -p, --port               Force COM port for PinDMDv3 devices. Example:
                           "COM3".

  -q, --quit-when-done     Exit the program when finished, e.g. when Pinball
                           FX2 doesn't receive any frames anymore. Default:
                           false

  -o, --output-to-file     If set, writes all frames as PNG bitmaps to the
                           provided folder.
```

## Troubleshooting

### Flickering with PinDMDv3

Some users reported heavy flickering when running via PinballX. Seems that this
is linked to the desktop settings. This seems to help:

- Go to **My Computer**
- Click on **Performance Information and Tools** on the bottom left
- Click on **Adjust Visual Effects** at the top left.
- When the Performance Window pops up, click on **Adjust for Best Appearance**. 
  All the boxes should automatically get check marked.
- Click on **Apply**, and then OK to get out.

*Thanks xman72*

## Compatibility

This application is based on .NET 4.5, which only runs on Windows 7 or later.
Probably on Vista too, but who cares. If you're still running WinXP then you
have my sincerest sympathy, but that's as far as I go. ;)


## Credits

- NoEx for the excellent TPA grabber code.
- [Cropper](http://cropper.codeplex.com/) for some of the neat screen capture 
  code
- Tom Speirs, Lucky1, CarnyPriest and Russdx for their help on the DMD code
- Adrian Page from Barnstorm for his help setting up the slave correctly.


## License

GPLv2, see [LICENSE](LICENSE).