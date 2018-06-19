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

- **Pinball FX2/3** through frame grabbing from screen 
- Farsight's **The Pinball Arcade** through grabbing the DMD texture from memory
- **Pro Pinball Timeshock** through their message queue
- **Visual PinMAME** through `DmdDevice.dll` 

The command line tool can also display image files on the DMD device and render
frames to bitmap files. Many features like this are described in the command 
line options below, so have a close look at those as well.

With v1.7, DMD Extensions also supports dynamic DMD coloring previously only
available for PIN2DMD displays.

### Colors

If you have an RGB display, you can override the color tone in which the output
is rendered. Note that this only works for non-colored games. Use the `--color`
parameter for that.

## Install Instructions

1. [Get the latest release](https://github.com/freezy/dmd-extensions/releases),
   copy the content of the zip file to your hard drive, preferable in your `PATH`,
   and unblock the `.exe` file (right-click, properties, unblock).
2. Download and install the [Visual C++ Redistributable for Visual Studio 2015](https://www.microsoft.com/en-us/download/details.aspx?id=48145).
3. If you haven't already, download and install the [.NET Framework 4.5.2](https://www.microsoft.com/en-us/download/details.aspx?id=42643).

### Test

1. Open a command prompt ([Windows]+[R], `cmd`, [enter])
2. Type `dmdext test` [enter]

You should see a test image on your DMD as well as on a virtual DMD.

### Pinball FX2/3

1. Enable cabinet options in Pinball FX2
2. Resize the DMD to:
   - Width: `1040`
   - Height: `272`
3. Move the DMD to somewhere hidden like off-screen or behind the playfield
   (usually at `0`/`0`).
4. Open a command prompt ([Windows]+[R], `cmd`, [enter])
5. Type `dmdext mirror --source=pinballfx2 --no-virtual` [enter] (or `pinballfx3` for FX3)
6. Start Pinball FX2/3

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

If you happen switch tables often and the last DMD frame gets stuck on your DMD
when changing to an EM game, you can use the `--idle-after` option to clear the
screen.

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
advantage of dmdext is that Lucky1's coloring features are fully supported. You
can set this up as you would for a PIN2DMD:

1. Create an `altcolor` folder where VPM is installed (usually `Visual Pinball\VPinMAME`)
2. In that folder, create another one named after the ROM (e.g. `simpprty`)
3. Download the `.pal` and `.vni` file from your favorite virtual pinball
   site, put them into that folder and name them `pin2dmd.pal` and `pin2dmd.vni`
4. Run VPM setup, open the ROM's game options and enable *Use external DMD (dll)*
   as well as *Colorize DMD*.

#### Configuration

Since `DmdDevice.dll` is called by VPM, we can't pass any configuration 
parameters to it. Instead, we use `DmdDevice.ini` which must be located
in the same folder as `VPinMAME.dll`. The options are described by block below.

- `[global]` - Global options that are applied to all display types
  - `resize`- How to downscale SEGA 192x64 pixel games to smaller displays. Can 
    have three values:
    - `stretch` - Just fill the available space and ignore the aspect ratio
    - `fill` - Fill it up so the whole DMD is filled while keeping aspect ratio
      intact. Pixels will be cropped off.
    - `fit` - Scale it so the whole image fits on the DMD while keeping aspect
       ratio intact. There will be white space (uh, more like black space).
  - `fliphorizontally` - Flips the image horizontally (left/right)
  - `flipvertically` - Flips the image vertically (top/down)
  - `colorize` - enable or disable frame-by-frame colorization (inactive in 
    VPX bundle)
- `[virtualdmd]` - A virtual DMD that renders on the computer screen somewhat
   nicely
  - `enabled` - If false, don't show it
  - `stayontop` - Virtual dmd stays on top of most other windows 
  - `hidegrip` - Hide the resize grip
  - `ignorear` - If true, allow free resizing of the virtual DMD.
  - `useregistry` - If true, read the virtual DMD position from VPM's registry.
  - `left` - X-axis of the window position
  - `top` - Y-axis of the window position
  - `width` - Width of the DMD in monitor pixels
  - `height` - Height of the dmd in monitor pixels
  - `dotsize` - Scale dot size. Use `0.8` for previous default setting
- `[pindmd1]` Options for the 2-bit pinDMD display
  - `enabled` - If false, doesn't bother looking for a pinDMD1
- `[pindmd2]` Options for the 4-bit pinDMD2 display
  - `enabled` - If false, doesn't bother looking for a pinDMD2
- `[pindmd3]` Options for the RGB24 pinDMDv3 display
  - `enabled` - If false, doesn't bother looking for a pinDMD3
  - `port` - COM port, e.g. `COM3`
- `[pin2dmd]` Options for the RGB24 PIN2DMD display
  - `enabled` - If false, doesn't bother looking for a PIN2DMD
- `[video]` - Allows creating an .avi video from the DMD frames.
  - `enabled` - If enabled, write to an .avi file.   
  - `path` - Path to folder or .avi file. If a folder is given, it will create
    a file named after the current game.
- `[browserstream]` - Streams the DMD in real time to your browser in your LAN
  - `enabled` - If true, start a small web server
  - `port` - Port of the web server

You can also override all options per game by using the game's name as section 
name and pre-fixing options with the name of the section (apart from `[global]`
options, which aren't prefixed). For example if you have a PinDMD2 and don't
want to use it for Baywatch which has a different resolution, you would create
a section like this:

```
[baywatch]
pindmd2 enabled = false
virtualdmd enabled = true
virtualdmd left = 2500
virtualdmd top = 800
virtualdmd width = 1024
virtualdmd height = 256
```

This is also useful if you just want to fit the virtual DMD onto different
backglasses which have slightly different positions. 

Also note that dmdext can retrieve the DMD's position from VPM's registry 
setting. That means you can use VPM to position the DMD and dmdext will take 
the same setting for each game. This behavior can be enabled using the 
`useregistry` option.

Finally, be aware that `DmdDevice.ini` is exclusively read by `DmdDevice.dll`.
Options for `dmdext.exe` are passed as command line options as documented below.
    

## Documentation

All options are documented in the tool.

```
C:\>dmdext

DMD Extensions v1.6.0
USAGE: dmdext <command> [<options>]

  mirror    Mirrors pixel data from the screen or memory to all available
            devices.

  play      Plays any media on all available devices (currently only images).

  test      Displays a test image on all available devices.
```

### Mirror

```
C:\>dmdext mirror --help

DMD Extensions v1.6.0
USAGE: dmdext mirror --source=<source> [--destination=<destination>]

  -s, --source             Required. The source you want to retrieve DMD data
                           from. One of: [ pinballfx2, pinballfx3,
                           pinballarcade, propinball, screen ].

  -f, --fps                How many frames per second should be mirrored.
                           Default: 25

  --idle-after             Wait for number of milliseconds until clearing the
                           screen. Disable with 0. Default: 0.

  --idle-play              Play this file while idleing instead of blank
                           screen. Supported formats: JPG, PNG, GIF. Animated
                           GIFs are supported.

  --position               [screen] Position and size of screen grabber source.
                           Four values: <Left> <Top> <Width> <Height>. Default:
                           "0 0 128 32".

  --resize-to              [screen] Resize captured screen to this size. Two
                           values: <Width> <Height>. Default: "128 32".

  --grid-spacing           [screen] How much of the white space around the dot
                           should be cut off (grid size is defined by
                           --resize-to). 1 means same size as the dot, 0.5 half
                           size, etc. 0 for disable. Default: 0.

  --propinball-args        [propinball] Arguments send from the Pro Pinball
                           master process. Usually something like: "ndmd
                           w0_0_0_0_w m392". Will be set autmatically when
                           called through Pro Pinball.

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

  --virtual-dotsize        Scale the dot size of the virtual DMD. Default: 1

  -c, --color              Sets the color of a grayscale source that is
                           rendered on an RGB destination. Default: ff3000

  --flip-x                 Flips the image horizontally (left/right). Default:
                           false.

  --flip-y                 Flips the image vertically (top/down). Default:
                           false.

  -p, --port               Force COM port for PinDMDv3 devices. Example:
                           "COM3".

  -q, --quit-when-done     Exit the program when finished, e.g. when Pinball
                           FX2 doesn't receive any frames anymore. Default:
                           false

  --quit-after             Exit after n milliseconds. If set to -1, waits
                           indefinitely or until source finishes when -q used.
                           Default: -1

  --no-clear               Don't clear screen when quitting. Default: false.

  -o, --output-to-file     If set, writes all frames as PNG bitmaps to the
                           provided folder.
```

### Media

```
C:\>dmdext play --help

DMD Extensions v1.6.0
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

  --virtual-dotsize        Scale the dot size of the virtual DMD. Default: 1

  -c, --color              Sets the color of a grayscale source that is
                           rendered on an RGB destination. Default: ff3000

  --flip-x                 Flips the image horizontally (left/right). Default:
                           false.

  --flip-y                 Flips the image vertically (top/down). Default:
                           false.

  -p, --port               Force COM port for PinDMDv3 devices. Example:
                           "COM3".

  -q, --quit-when-done     Exit the program when finished, e.g. when Pinball
                           FX2 doesn't receive any frames anymore. Default:
                           false

  --quit-after             Exit after n milliseconds. If set to -1, waits
                           indefinitely or until source finishes when -q used.
                           Default: -1

  --no-clear               Don't clear screen when quitting. Default: false.

  -o, --output-to-file     If set, writes all frames as PNG bitmaps to the
                           provided folder.
```
### Test

```
C:\>dmdext test --help

DMD Extensions v1.6.0
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

  --virtual-dotsize        Scale the dot size of the virtual DMD. Default: 1

  -c, --color              Sets the color of a grayscale source that is
                           rendered on an RGB destination. Default: ff3000

  --flip-x                 Flips the image horizontally (left/right). Default:
                           false.

  --flip-y                 Flips the image vertically (top/down). Default:
                           false.

  -p, --port               Force COM port for PinDMDv3 devices. Example:
                           "COM3".

  -q, --quit-when-done     Exit the program when finished, e.g. when Pinball
                           FX2 doesn't receive any frames anymore. Default:
                           false

  --quit-after             Exit after n milliseconds. If set to -1, waits
                           indefinitely or until source finishes when -q used.
                           Default: -1

  --no-clear               Don't clear screen when quitting. Default: false.

  -o, --output-to-file     If set, writes all frames as PNG bitmaps to the
                           provided folder.
```

## Compatibility

This application is based on .NET 4.5, which only runs on Windows 7 or later.
Probably on Vista too, but who cares. If you're still running WinXP then you
have my sincerest sympathy, but that's as far as I go. ;)


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

*Thanks to xman72!*

### Still flickering?

Try boosting how fast dmdext grabs the frames. For example, for Pinball FX2, try:

    dmdext mirror --source=pinballfx2 --no-virtual -q --fps 60

Default is 25, which seems too slow for some games.

*Thanks smoke007 for the tip!*

### DmdDevice.ini Ignored?

This project comes bundled in two versions: A library which is loaded by VPM
called `DmdDevice.dll`, and an executable, `dmdext.exe` for all the other
applications.

The executable ignores `DmdDevice.ini` because it's configured through command
line options.

### Slow rendering on certain ROMs with VPM?

Use VPM 3.1. If you can't wait there's a beta build [here](http://vpuniverse.com/forums/topic/3461-sambuild31-beta-thread/?page=2&tab=comments#comment-39309).

*Thanks to djrobx for the fix an all others at [#52](https://github.com/freezy/dmd-extensions/issues/52) for reporting.*

## Reporting Bugs

Make sure you include the application log. You can *usually* find it at the same 
place you copied your `DmdDevice.dll` or `dmdext.exe`. However, there are two
premises for the log to be created:

1. You need a log config file. If there's no log config file, no log is created.
   The log config file is called `DmdDevice.log.config` for `DmdDevice.dll` and
   `dmdext.log.config` for `dmdext.exe`. They must be at the same place as the DLL
   and EXE respectively.
2. The user running the host app must have write permissions to the log folder.
   Per default that's the current directory. If you have copied the DLL to the
   `Windows\SysWOW64` folder, you don't have write access. In this case you need
   to write the log elsewhere. You can do that by editing the log config file.
   The format of the log file is XML. To change the log file location, find this line:
   
       `<target xsi:type="File" name="file" fileName="DmdDevice.log"`

   And change the `fileName` attribute to somewhere you can write, for example:

       `<target xsi:type="File" name="file" fileName="C:\Users\youruser\DmdDevice.log"`

If you have a crash, please also include which OS/bitness you're using. If it's
about the DLL, let us know where you copied the DLL and which host application
you're using (VPM's `setup.exe` or`vpinball.exe`, also which version).

You can post bugs at [VPF](http://www.vpforums.org/index.php?showtopic=36915) or 
[VPU](http://vpuniverse.com/forums/forums/topic/2728-sam-build-with-modular-dmd-drivers-for-pindmd123-and-pin2dmd/),
but preferably [here](https://github.com/freezy/dmd-extensions/issues).

## Credits

- NoEx for the excellent TPA grabber code.
- Tom Speirs, Lucky1, CarnyPriest and Russdx for their help on the DMD code
- Lucky1 for instructions and details about the coloring feature
- DJRobX for helping with the dynamic coloring, better performance and much more.
- Adrian Page from Barnstorm for his help setting up the slave correctly.


## License

GPLv2, see [LICENSE](LICENSE).
