# DMD Extensions

*A toolbox for digital pinball and dot matrix displays.*

This project aims to connect digital pinball games with the physical DMDs that are widespread in the community. It also
comes with pretty monitor output, supports frame-by-frame colorization, and can even stream over the network.

## Table of Contents

- [Table of Contents](#table-of-contents)
- [Features](#features)
  - [Supported Games](#supported-games)
  - [Supported Displays](#supported-displays)
  - [High-Resolution DMD for Monitors](#high-resolution-dmd-for-monitors)
  - [Segment Display Rendering](#segment-display-rendering)
  - [Frame Colorization](#frame-colorization)
  - [Network Streaming](#network-streaming)
  - [Scaling](#scaling)
- [Install Instructions](#install-instructions)
- [Usage](#usage)
  - [Test](#test)
  - [Pinball FX2](#pinball-fx2)
  - [Pinball FX3](#pinball-fx3)
  - [Pinball FX](#pinball-fx)
  - [The Pinball Arcade](#the-pinball-arcade)
  - [Pro Pinball Ultra](#pro-pinball-ultra)
  - [Visual PinMAME](#visual-pinmame)
  - [Future Pinball](#future-pinball)
  - [Media](#media)
  - [Pinup Player](#pinup-player)
  - [PinballX](#pinballx)
  - [Frame Dumping](#frame-dumping)
- [Configuration](#configuration)
  - [Output Configuration](#output-configuration)
  - [Command Line Configuration](#command-line-configuration)
  - [ZeDMD](#zedmd)
  - [Colorization](#colorization)
- [Breaking Changes](#breaking-changes)
  - [v2.2.2](#v222)
  - [v2.1.1](#v211)
  - [v1.8.0](#v180)
- [Troubleshooting](#troubleshooting)
  - [Flickering with PinDMDv3](#flickering-with-pindmdv3)
  - [Still flickering?](#still-flickering)
  - [DmdDevice.ini Ignored?](#dmddeviceini-ignored)
  - [Slow rendering on certain ROMs with VPM?](#slow-rendering-on-certain-roms-with-vpm)
  - [Weird positioning or no DMD visible at all?](#weird-positioning-or-no-dmd-visible-at-all)
  - [Unable to load DLL 'serum.dll'](#unable-to-load-dll-serumdll)
  - [Backglass covers segment displays](#backglass-covers-segment-displays)
- [Reporting Bugs](#reporting-bugs)
- [Manual Installation](#manual-installation)
- [Game Names](#game-names)
  - [x64 vs x86](#x64-vs-x86)
- [Build Instructions](#build-instructions)
- [Credits](#credits)
- [License](#license)

## Features

### Supported Games

DMD Extensions adds real DMD support to the following games:

- [**Pinball FX2**](#pinball-fx2) through frame grabbing from screen
- [**Pinball FX3**](#pinball-fx3) by reading the DMD texture data from memory
- [**Pinball FX**](#pinball-fx) by officially supporting `DmdDevice.dll` (thanks, Zen!) 
- [**The Pinball Arcade**](#the-pinball-arcade) by Farsight, through grabbing the DMD texture from memory
- [**Pro Pinball Timeshock**](#pro-pinball-ultra) through their message queue
- [**Visual PinMAME**](#visual-pinmame) through `DmdDevice.dll`
- [**Future Pinball**](#future-pinball) through using Macro's FP Intercept that sends DMD frames through a pipe.

The command line tool can also display image files on the DMD device and render
frames to bitmap files. Many features like this are described in the command
line options below, so have a close look at those as well.

### Supported Displays

Currently supported hardware displays:

- **PinDMD v3**, full RGB support
- **PinDMD v2**, 4 bit (16 shades) support
- **PinDMD v1**, 2 bit (4 shades) support
- **PIN2DMD**, full RGB support
- **Pixelcade**, full RGB support
- **ZeDMD** / **PinDMD v3.1***, full RGB support

_* The display from VirtuaPin marketed as [PinDMD v3.1](https://virtuapin.net/index.php?main_page=product_info&products_id=283) is
a [ZeDMD](https://github.com/PPUC/ZeDMD) under the hood._

### High-Resolution DMD for Monitors

DMD Extensions includes a complex shader with awesome effects for monitor users. For more info, find the documentation [here](https://github.com/freezy/dmd-extensions/tree/master/LibDmd/Output/Virtual/Dmd#readme).

<image src="https://user-images.githubusercontent.com/70426/109708090-3ee0cf80-7b9b-11eb-9fdd-83523aa265f9.png" width="350" />

### Segment Display Rendering

DMD Extensions supports high-resolution rendering of segmented alpha-numeric displays:

<image src="https://user-images.githubusercontent.com/70426/50459439-5f81bf00-096b-11e9-9f75-f70387f2c9cc.png" width="350"/>

Documentation how to enable and customize this feature can be found [here](https://github.com/freezy/dmd-extensions/tree/master/LibDmd/Output/Virtual/AlphaNumeric#readme).

### Frame Colorization

DMD Extensions includes support for Serum colorizations, as well as the VNI/PAL/PAC format, originally used on PIN2DMD 
devices.

Colorization is enabled for most games, including Pinball FX(3), The Pinball Arcade, and Visual PinMAME.

<image src="https://user-images.githubusercontent.com/57115343/151871089-5f958122-f9db-47d2-a133-f29e964eb8e4.jpg" width="350" />

### Network Streaming

DMD Extensions can also receive frames from the network and output them to any device. Likewise, it can stream frames
from any source over the network. Documentation of this feature can be found [here](Console/Server).

Additionally, DMD Extensions can act as a web server where any number of clients can connect to, and see the DMD in real
time in the web browser.

### Scaling

Most of the time, your DMD will match the resolution of the game, which is 128x32. There are however two cases where 
scaling comes into play.

1. The game has a different resolution than 128x32. If the resolution is higher, the frames will be **scaled down**, 
   using scaling algorithm defined by the `resize` option (*stretch*, *fill*, and *fit*). If the resolution is lower, the 
   frames will be centered on the DMD, with black borders around it.
2. You're using a high-resolution DMD, like a PIN2DMD XL or ZeDMD HD, running at 256x64. In this case, frames can be
   be **scaled up**, using either a "doubler" or "[scale2x](http://www.scale2x.it/)" algorithm. Note that your physical 
   display must be at least double the size of the source frame size.

The above mainly applies to hardware displays, which are limited to a fixed resolution. However, it's also possible to
upscale frames to the virtual DMD. In order to do that, set `scalermode` so to either `doubler` or `scale2x`. 

## Install Instructions

[Download the installer](https://github.com/freezy/dmd-extensions/releases) and
run it. A few notes:

- The installer will overwrite any existing `dmddevice.dll` in your VPM folder
  if you choose to install it (existing INI files are untouched).
- If you're on Windows x64, you probably want to install both 32 and 64-bit
  versions. They can live happily next to each other.
- The reason for installing both versions is that your VPM is probably 32-bit,
  so you'll need the 32-bit `DmdDevice.dll` and if you're using `dmdext.exe`
  with the 64-bit version of Pro Pinball, it will only work with the 64-bit
  version.
- However, if you don't have Pro Pinball, just 32-bit will do fine, and if you
  don't use VPM then just 64-bit will do fine as well. And if you're on a 32-bit
  Windows, then just take the 32-bit version.
- The installer will add the install folder of `dmdext.exe` to the `PATH`
  environment variable, but only if the platform is the same:
	- DMD Extensions (64-bit) on Windows x64 is added to `PATH`
	- DMD Extensions (32-bit) on Windows 32-bit is added to `PATH`
	- All other combinations aren't added to `PATH`
- The installer will set the `DMDDEVICE_CONFIG` environment variable to the
  location of your `DmdDevice.ini`. That means from wherever you launch
  `DmdDevice.dll` (or run `dmdext.exe` with `--use-ini`), the same
  `DmdDevice.ini` will be used.
- If you re-install and your VPM installation folder changed since the last
  installation, a new `DmdDevice.ini` will be copied to the new location (if it
  doesn't exist), and the `DMDDEVICE_CONFIG` environment variable will be updated
  accordingly.
- During installation, when a feature is deactivated, that means the installer
  couldn't find the host program. For VPM that means that you haven't registered
  `VPinMAME.dll`. For Pro Pinball it means it's not installed via Steam, and
  you'll need to select its installation folder manually.

If you don't trust the installer and want to do it manually, [see below](#manual-installation).

## Usage

### Test

1. Open a command prompt ([Windows]+[R], `cmd`, [enter])
2. Type `dmdext test -d auto` [enter]

You should see a test image on your DMD as well as on a virtual DMD.

### Pinball FX2

1. Enable cabinet options in Pinball FX2
2. Resize the DMD to:
	- Width: `520`
	- Height: `136`
3. Move the DMD to somewhere hidden like off-screen or behind the playfield
   (usually at `0`/`0`).
4. Open a command prompt ([Windows]+[R], `cmd`, [enter])
5. Type `dmdext mirror --source=pinballfx2 --no-virtual` [enter] (or
   `pinballfx3` for FX3)
6. Start Pinball FX2/3

For further tweaking, see options below.

### Pinball FX3

The DMD from Pinball FX3 is pulled directly from the memory.

1. Open a command line prompt ([Windows], type `cmd`, [enter])
2. Type `dmdext mirror --source=pinballfx3 --no-virtual` [enter]
3. Start Pinball FX3 and play a game.

It doesn't matter whether Pinball FX3 is started before or after `dmdext`, and
it works with or without cabinet mode.

Note that while the current memory grabber code should also work for future
Pinball FX3 versions, we obviously can't guarantee it. If a new version breaks
`dmdext`, you should still be able to fall back to the legacy screen grabber
that is used by Pinball FX2 by using the `--fx3-legacy` flag.

### Pinball FX

Since mid-June 2023, Pinball FX officially supports DMD Extensions through `DmdDevice.dll`. Note however that Pinball FX 
runs on 64 bit, so it's technically `DmdDevice64.dll`, which comes with the 64-bit version of DMD Extensions.

If you don't have Visual PinMAME installed, you'll have to set the `DMDDEVICE_CONFIG` environment variable to point to
`DmdDevice.ini`, given you keep `DmdDevice64.dll` in the same directory.

To do that:

1. Press the `[Windows]` key, type "env" and select *Edit the system environment variables*.
2. Click *Environment Variables...*.
3. Under *System variables*, click *New...*.
4. Enter `DMDDEVICE_CONFIG` as variable name and the full path to `DmdDevice.ini` as variable value.

If you have done that, or you do have VPM installed, simply enable the external DMD in Pinball FX:

1. Start Pinball FX
2. Open the settings menu (gears icon at the top right)
3. Choose *Cabinet Support*
4. Choose *Dotmatrix Window*
5. Under *Dotmatrix Window*, select *External DMD Device*.

For customizing games in `DmdDevice.ini`, see [this list of IDs](PinMameDevice/PinballFX_IDs.md) for all supported games.

### The Pinball Arcade

For TPA we're pulling frames off the DMD texture from TPA's memory.

1. Open a command-line prompt ([Windows], type `cmd`, [enter])
2. Type `dmdext mirror --source=pinballarcade --no-virtual` [enter]
3. Start the DX11 version of The Pinball Arcade.
4. Select and **start** a game (*only then* it starts mirroring, during attract
   mode it doesn't, even though TPA does display the DMD).

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
2. Copy `ProPinballSlave.bat` to the Pro Pinball installation folder
   (usually at `%PROGRAMFILES(X86)%\Steam\SteamLibrary\steamapps\common\Pro Pinball Ultra`)
3. Add the following command line parameters when launching `ProPinball.exe`:
   `m3 dProPinballSlave`

Note that this currently works only with the 64-bit version of Pro Pinball. 32
bit binaries will be provided with a later release. If you want to change the
options (e.g. show the virtual DMD), edit `ProPinballSlave.bat` and adopt
accordingly.

### Visual PinMAME

VPM introduced the possibility of delegating DMD rendering to an external DLL.
The main motivation is being able to ship a single build instead of having to
release multiple binaries for multiple DMDs.

Be sure to use at least PinMAME v3.4. You can download the latest version [here](https://github.com/vpinball/pinmame/releases).

### Future Pinball

Copy `Future Pinball/OpenGL32.dll` to your Future Pinball folder, i.e. where
`Future Pinball.exe` is located. Now, you can send frames to your DMD by running:

```bash
dmdext mirror -s futurepinball
```

Note that:
- The `-q` toggle works here, so instead of killing the process you could make it
  exit automatically when the table closes.
- With `-g <name>`, you can set the game's name, which will, when used along
  `--use-ini`, read the game-specific settings.

For more details check out TerryRed's guide [here](https://vpuniverse.com/forums/topic/4928-dmdext-freezy-and-future-pinball-real-and-virtual-dmd-support/).

### Media

DMD Extensions can also play videos and images. Here are a few examples:

```bash
dmdext play -f "ACDC (Stern 2012).gif"
dmdext play -f "Tron Legacy (Stern 2011).png"
```

### Pinup Player

DMD Extensions can send unprocessed frames and scene events detected by supported colorizers to Pinup Player, which
is then used to trigger videos.

To enable event triggering, you'll need a recent version of `dmddevicePUP.dll` and `dmddevicePUP64.dll`. You can download the latest version
[here](https://www.nailbuster.com/wikipinup/doku.php?id=release_notes).

Then, enable the Pinup Player output in `DmdDevice.ini`:

```ini
[pinup]
enabled = true
```

### PinballX

DMD Extensions can be used in [PinballX](https://www.pinballx.com/), the frontend for virtual pinball cabinets. In order 
to do so, copy `DmdDevice64.dll` to the PinballX's installation folder, and delete or rename `PinDMD.dll`. Then, activate
the DMD output through the Settings app, and you should be good to go.

### Frame Dumping

DMD Extensions can also dump frames to disk. This is needed when colorizing a new game, since both the Serum as well as
the Pin2Color editor use them as the basis for the colorization.

Visual PinMAME has a built-in frame dumper, which is enabled when the `dmddump` folder exists, and both *Use external DMD*
and *Show DMD Window* are active for a ROM. However, this doesn't work for the DMD rendering of alphanumeric games.

Dmdext's frame dumping not only works for those games, but can also be used to dump frames from Pinball FX and all the
other games where VPM isn't involved.

To enable it, update `DmdDevice.ini`:

```ini
[rawoutput]
enabled = true
```

For dumping frames through `dmdext.exe`, use the following command line:

```bash
dmdext mirror -s <source> --dump-frames
```

The frames will be dumped to the `dmddump` folder, which is located where PinMAME is installed. If PinMAME isn't found,
a `dmddump` folder is created in the current working directory. Existing dump files are appended to.

Note however that the frame dumper needs a source that provides a game name. Tested sources are:

- VPX/VPM
- Pinball FX
- Pinball FX3
- The Pinball Arcade
- Pro Pinball Ultra (add `--dump-frames` to `ProPinballSlave.bat`)

## Configuration

Since `DmdDevice.dll` is called by VPM, we can't pass any configuration
parameters to it. Instead, we use `DmdDevice.ini` which must be located
in the same folder as `VPinMAME.dll` or at the path where the `DMDDEVICE_CONFIG`
environment variable is pointing to.

Note however that it's also possible to make `dmdext.exe` use the ini instead of
command line parameters (using the `--use-ini` parameter).

### Output Configuration

You can also tell `dmdext.exe` to read the output configuration from `DmdDevice.ini`
instead of passing them as command line arguments. The options are described below.

The output are described by block below.

| Command Line                   | DmdDevice.ini                                  | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
|--------------------------------|------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `-r`, `--resize`               | [global]<br>resize                             | How to downscale SEGA 192x64 or other high-resolution games to smaller displays. Can have three values:<ul>  <li>`stretch` - Just fill the available space and ignore the aspect ratio</li>  <li>`fill` - Fill it up so the whole DMD is filled while keeping aspect ratio intact. Pixels will be cropped off.</li>  <li>`fit` - Scale it so the whole image fits on the DMD while keeping aspect ratio intact. There will be white space (uh, more like black space).</li> |
| `--flip-x`                     | [global]<br>fliphorizontally                   | Flips the image horizontally (left/right)                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `--flip-y`                     | [global]<br>flipvertically                     | Flips the image vertically (top/down)                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `--colorize`                   | [global]<br>colorize                           | Enable or disable frame-by-frame colorization (inactive in VPX bundle)                                                                                                                                                                                                                                                                                                                                                                                                      |
| `--plugin`                     | [global]<br>plugin{n}.path[64]                 | Enable a third party plugin. See [Plugin Configuration](#plugin-configuration)                                                                                                                                                                                                                                                                                                                                                                                              |
| `--plugin-passthrough`         | [global]<br>plugin{n}.passthrough              | Enable the plugin to always receive frames. See [Plugin Configuration](#plugin-configuration)                                                                                                                                                                                                                                                                                                                                                                               |
| `-d virtual`<br>`--no-virtual` | [virtualdmd]<br>enabled                        | Shows a virtual DMD that renders on the computer screen somewhat nicely                                                                                                                                                                                                                                                                                                                                                                                                     |
| `--virtual-stay-on-top`        | [virtualdmd]<br>stayontop                      | Virtual DMD stays on top of most other windows                                                                                                                                                                                                                                                                                                                                                                                                                              |
| *n/a*                          | [virtualdmd]<br>ignorear                       | If true, allow free resizing of the virtual DMD, otherwise the aspect ratio is locked to the DMD's.                                                                                                                                                                                                                                                                                                                                                                         |
| *n/a*                          | [virtualdmd]<br>useregistry                    | If true, read the virtual DMD position from VPM's registry.                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `--virtual-position`           | [virtualdmd]<br>left<br>top<br>width<br>height | Position of the virtual DMD. Command line takes in all four in the shown order, where the last (height) is optional.                                                                                                                                                                                                                                                                                                                                                        |
| `--virtual-dotsize`            | [virtualdmd]<br>dotsize                        | Scale dot size. Use `0.8` for previous default setting.                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `-d pindmdv1`                  | [pindmd1]<br>enabled                           | Enables the 2-bit pinDMD display.                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `-d pindmdv2`                  | [pindmd2]<br>enabled                           | Enables the 4-bit pinDMD2 display.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `-d pindmdv3`                  | [pindmd3]<br>enabled                           | Enables the RGB24 pinDMDv3 display.                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `--port`                       | [pindmd3]<br>port                              | COM port, e.g. `COM3`.                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `-d pin2dmd`                   | [pin2dmd]<br>enabled                           | Enables the RGB24 PIN2DMD display.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| *n/a*                          | [pin2dmd]<br>delay                             | Delay in milliseconds to wait after loading a palette.                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `-d zedmd`                     | [zedmd]<br>enabled                             | Enables the RGB24 ZeDMD display.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `-d zedmdhd`                   | [zedmdhd]<br>enabled                           | Enables the RGB24 ZeDMD HD display.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `-d zedmdwifi`                 | [zedmdwifi]<br>enabled                         | Enables the RGB24 ZeDMD WiFi display.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `-d zedmdhdwifi`               | [zedmdhdwifi]<br>enabled                       | Enables the RGB24 ZeDMD HD WiFi display.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `--zedmd-debug`                | [zedmd]<br>debug                               | Let any ZeDMD show its debug informations.                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `--zedmd-brightness`           | [zedmd]<br>brightness                          | Change any ZeDMD brightness between 0 and 15.                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `--zedmd-rgborder`             | [zedmd]<br>rgborder                            | Change any ZeDMD RGB order between 0 and 5.                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `--zedmd-wifi-address`         | [zedmdwifi]<br>wifi.address                    | Connect to ZeDMD (HD) WiFi using this IP address. If not provided, a connection to zedmd-wifi.local will be established.                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| *n/a*                          | [video]<br>enabled                             | Enables creating an .avi video from the DMD frames.                                                                                                                                                                                                                                                                                                                                                                                                                         |
| *n/a*                          | [video]<br>path                                | Path to folder or .avi file. If a folder is given, it will create a file named after the current game.                                                                                                                                                                                                                                                                                                                                                                      |
| *n/a*                          | [browserstream]<br>enabled                     | Enables streaming the DMD in real time to your browser in your LAN.                                                                                                                                                                                                                                                                                                                                                                                                         |
| *n/a*                          | [browserstream]<br>port                        | Port of the web server to listen on                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `--pinup`                      | [pinup]<br>enable                              | Enables output to PinUP.                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `--scaler-mode`                | [global]<br>scalermode                         | Use to upscale <strong>all</strong> frames.<br><br>Can have three values:<ul><li>`none` - No upscaling.</li> <li>`doubler` - Double all pixels.</li>  <li>`scale2x` - Use Scale2x algorithm.</li>                                                                                                                                                                                                                                                                           |
| `--scaler-mode`                | [global]<br>vni.scalermode                     | Scaler mode for VNI/PAC colorizations. <strong>Note:</strong> This only applies to 256x64 colorized content files.<br><br>Can have two scaling modes:<ul><li>`doubler` - Double all pixels.</li>  <li>`scale2x` - Use Scale2x algorithm.</li>                                                                                                                                                                                                                               |
| `--skip-analytics`             | [global]<br>skipanalytics                      | If true, Don't send anonymous usage statistics to the developer. More info [here](https://github.com/freezy/dmd-extensions/wiki/Analytics).                                                                                                                                                                                                                                                                                                                                 |

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

### Command Line Configuration

Input options only apply to dmdext.exe, because the DLL is called externally
from VPM where no configuration is necessary (apart from enabling it in VPM).

The following parameters are valid for all of dmdext's commands:

| Parameter              | Description                                                                                                                                                                                                                                             | Default |
|------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------|
| `--use-ini`            | Path to `DmdDevice.ini` to use instead of command line options. If no path is provided, the `DMDDEVICE_CONFIG` environment variable is used as fallback. If just `DmdDevice.ini` is provided, dmdext will look for it in the current working directory. | *none*  |
| `-c, --color`          | Sets the color of a grayscale source that is rendered on an RGB destination.                                                                                                                                                                            | ff3000  |
| `-q, --quit-when-done` | Exit the program when finished, e.g. when Pinball FX2 doesn't receive any frames anymore.                                                                                                                                                               | false   |
| `--quit-after`         | Exit after n milliseconds. If set to -1, waits indefinitely or until source finishes when -q used.                                                                                                                                                      | -1      |
| `--no-clear`           | Don't clear screen when quitting.                                                                                                                                                                                                                       | false   |
| `-o, --output-to-file` | If set, writes all frames as PNG bitmaps to the provided folder.                                                                                                                                                                                        |         |

Note that all options of dmdext.exe are also available via command line:

```
dmdext --help
dmdext mirror --help
dmdext play --help
dmdext test --help
```

#### Mirror Command

The `mirror` command has the following additional parameters:

| Parameter           | Description                                                                                                                                                                     | Default    |
|---------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------|
| `-s, --source`      | Required. The source you want to retrieve DMD data from. One of: [ `pinballfx2`, `pinballfx3`, `pinballarcade`, `propinball`, `futurepinball`, `screen` ].                      | *n/a*      |
| `-f, --fps`         | How many frames per second should be mirrored.                                                                                                                                  | 25         |
| `--idle-after`      | Wait for number of milliseconds until clearing the screen. Disable with 0.                                                                                                      | 0          |
| `--idle-play`       | Play this file while idleing instead of blank screen. Supported formats: JPG, PNG, GIF. Animated GIFs are supported.                                                            | *none*     |
| `--position`        | *screen* - Position and size of screen grabber source. Four values: `<Left> <Top> <Right> <Bottom>`.                                                                            | 0 0 128 32 |
| `--resize-to`       | *screen* - Resize captured screen to this size. Two values: <Width> <Height>.                                                                                                   | 128 32     |
| `--grid-spacing`    | *screen* - How much of the white space around the dot should be cut off (grid size is defined by --resize-to). 1 means same size as the dot, 0.5 half size, etc. 0 for disable. | 0          |
| `--propinball-args` | *propinball* - Arguments send from the Pro Pinball master process. Usually something like: `ndmd w0_0_0_0_w m392`. Will be set automatically when called through Pro Pinball.   |            |
| `--fx3-legacy`      | *pinballfx3* - If set, don't use the memory grabber but the legacy screen grabber, like Pinball FX2.                                                                            | false      |
| `--colorize`        | Enable or disable frame-by-frame colorization. Supported on `pinballfx3` (memory grabber) and `pinballarcade`.                                                                  | false      |

#### Play Command

The `play` command has the following additional parameters:

| Parameter           | Description                                                                    | Default |
|---------------------|--------------------------------------------------------------------------------|---------|
| `-f, --file`        | Path to the file to play. Currently supported file types: PNG, JPG, BIN (raw). | *none*  |

#### Test Command

The `test` command just outputs a single image. This allows you to test different modes and resolutions without having
to start a game.

| Parameter  | Description                                                                                         | Default  |
|------------|-----------------------------------------------------------------------------------------------------|----------|
| `--format` | The frame format. One of `rgb24`, `gray2`, `gray4`, `coloredgray2`, `coloredgray4`, `coloredgray6`. | `rgb24`  |

Examples:

```
dmdext test
dmdext test --format gray2
```

### ZeDMD

The [ZeDMD library](https://github.com/PPUC/libzedmd) (`zedmd.dll`) is bundled with `DmdDevice.dll` and `dmdext.exe`.
However, you can also replace this library with a newer version if you wish.
To do that, download the latest DLL from [here](https://github.com/PPUC/libzedmd/releases) and place it alongside
with `DmdDevice.dll` or `dmdext.exe`. Note that this might break compatibility, so check your log if ZeDMD suddenly stops
working.

#### USB mode

This is the default mode for ZeDMD. You don't need to configure anything except setting `enabled` to `true` in
`DmdDevice.ini` for `[zedmd]` or `[zedmdhd]`.
But in case you have multiple devices or run into issues with the auto-detection and other USB devices attached,
you can set a concrete COM port to use in `DmdDevice.ini` using `port`.

#### WiFi mode

Alternatively ZeDMD could be set to WiFi mode in its settings menu.
In `DmdDevice.ini` there are special devices named `[zedmdwifi]` and `[zedmdhdwifi]` to use it.

To run the ZeDMD in WiFi mode it needs WiFi credentials to establish the connection to your local network.
To configure these for the first time, ZeDMD opens it's own WiFi access point.
You can connect to it using `ZeDMD-WiFi` as SSID and `zedmd1234` as password.
Then open a connection to `zedmd-wifi.local` in your browser and configure the device.
ZeDMD will remain reachable under this name even it becomes part of your local network.
Therefore, a MDNS service is running on ZeDMD.
If you run into issues with that, you can directly connect to the IP address that is shown on top of the logo when ZeDMD gets powered. 
That IP address has to be added as `wifi.address` to `DmdDevice.ini`.  
You can test with `dmdext.exe test -d zedmdwifi --zedmd-wifi-address=x.x.x.x` (replace `x.x.x.x` with the IP address shown on ZeDMD).

> [!WARNING]  
> ZeDMD will store the WiFi credentials internally! So ensure to erase them using one of the processes described
> above before giving your ZeDMD to someone else!

### Colorization

If you are a PC monitor user or have an RGB display (PinDMDv3, PIN2DMD, Pixelcade or ZeDMD), you can enable frame-by-frame
colorization for games that are supported by the creators of the virtual pinball community. This means that the DMD will
be rendered in color, with up to 64 colors per frame.

There are two natively supported formats: Serum and VNI/PAL/PAC (originally only available for PIN2DMD displays). 
Additionally, dmdext supports colorization plugins from third parties. 

#### Colorization File Location

All formats keep their files in the `altcolor` folder, which is located in the same folder as VPM (usually 
`Visual Pinball\VPinMAME\altcolor`). In this folder, every game has its separate folder, which contains the colorization
files.

Depending on the source, the game folders and colorization files are named differently:

- In VPM, the game folders are named after the ROM name (e.g. `cp_16` for Champion Pub).
- Serum files can be named anything, but must have the `.cRZ` extension.
- VNI/PAL/PAC files can be named anything, but must have the `.vni`/`.pal`/`.pac` extension.
- In Pinball Arcade and Pinball FX3, the games have different names. Scroll down for [a list of game names](#game-names)
  at the time of writing.

For example, a structure for Champion Pub for all games and all color formats, could look like this (note that if both
a `.cRZ` and VNI/PAL/PAC files are present in the same game folder, the `.cRZ` file will be used):
```
altcolor
├── cp_16
│   ├── ChampionPub.cRZ
│   ├── pin2dmd.pal
│   └── pin2dmd.vni
├── ChampionPub
│   ├── ChampionPub.cRZ
│   ├── pin2dmd.pal
│   └── pin2dmd.vni
├── BALLY_Champion_Pub
│   ├── ChampionPub.cRZ
│   ├── pin2dmd.pal
│   ├── pin2dmd.vni
└── 121
    ├── ChampionPub.cRZ
    ├── pin2dmd.pal
    └── pin2dmd.vni
```

*Not about Serum*: The [library that does Serum colorization](https://github.com/zesinger/libserum) (`serum.dll`) is 
bundled with `DmdDevice.dll` and `dmdext.exe`. However, you can also replace this library with a newer version if you wish.
To do that, download the latest DLL from [here](https://github.com/zesinger/libserum/releases) and place it alongside
with `DmdDevice.dll` or `dmdext.exe`. Note that this might break compatibility, so check your log if Serum suddenly stops
working.

#### Plugin Configuration

Enabling colorization plugins is done through the `plugin.path` setting in `DmdDevice.ini`, or through the `--plugin` 
command line option in `dmdext.exe`. Additionally, you can specify the `plugin.passthrough` option, which will send 
frames to the plugin independently of the whether a colorization file is present or not. This is useful for frame 
dumping.

`DmdDevice.ini` example:
```ini
; load plugins
plugin.0.path = C:\Visual Pinball\VPinMAME\pin2color.dll
plugin.0.path64 = C:\Visual Pinball\VPinMAME\pin2color64.dll
plugin.0.passthrough = false
```
You can add up to 10 plugins. The first plugin which has a colorization file present or passthrough enabled will be used.

Command line example:
```bash
dmdext.exe mirror --source pinballfx3 --colorize --plugin "C:\Visual Pinball\VPinMAME\pin2color.dll" --plugin-passthrough
```

Note that the `--plugin` parameter needs to point to a DLL with the correct bitness, i.e. 64-bit `dmdext.exe` would need
the 64-bit version of the plugin.

Also note that without `passthrough` enabled, the plugin is disabled if no colorization file is present. 

#### Usage

Depending on the source, enabling colorization is different:

- In VPM, enter setup, open the ROM's game options and enable *Use external DMD (dll)* as well as *Colorize DMD*.
- In Pinball Arcade and Pinball FX3, use the `--colorize` option when running `dmdext.exe`.

Colorizations in the VNI/PAL format or the plugin are able to emit selected frames at 256x64, which is twice the normal 
size. In this case, all frames will be rendered at this resolution. Frames that aren't provided at 256x64 will be 
upscaled. In the native VNI/PAL/PAC colorizer, you can choose which upscale algorithm to use.

- For the command line, use the `--scaler-mode` option. The default is `double`.
- In DmdDevice.ini, set the `vni.scalermode` option.

## Breaking Changes

### v2.3.1

- ZeDMD firmware versions before 5.0.0 aren't supported anymore.

### v2.2.2

- In DmdDevice.ini, the `[zedmd]` has been split into `[zedmd]`, `[zedmdhd]`, `[zedmdwifi]`, `[zedmdhdwifi]`.
- ZeDMD HD devices don't use their built-in scaler anymore when attached to DMD Extension. They now leverage the common `scalermode` setting.
- ZeDMD firmware versions before 3.6.0 aren't supported anymore.
- Removed --scale-to-hd in favor of --scaler-mode. Set scaler mode to `none` if you want to disable scaling.

### v2.1.1

- In DmdDevice.ini, the `[ZeDMD]` has changed to `[zedmd]`.
- ZeDMD firmware versions before 3.2.0 aren't supported anymore.

### v1.8.0

- Data types in configuration files are now culture invariant. Meaning if you are running under a Windows UI Culture such as German
  which normally represents decimals with commas instead of periods you will need to change your configuration file (such as
  DMDDevice.ini) to use periods for decimal data. The most common example would be those users who are using `dotsize` setting
  in your DMDDevice.ini. This was done to standardize the format of numeric data accross different languages.
- Hardware DMDs aren't probed per default anymore, unless you specifically set the destination. For most people that means that
  you'll need to provide the `-d` option when running `dmdext.exe`.
- If you are a PinDMDv3 user and are using colored ROMs, you'll want to [upgrade your firmware](https://github.com/freezy/dmd-extensions/wiki/PinDMD3:-How-to-flash-a-new-firmware).


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

DMD Extensions comes bundled in two versions: A library which is loaded by VPM
called `DmdDevice.dll`, and an executable, `dmdext.exe` for all the other
applications.

The executable ignores `DmdDevice.ini` because it's configured through command
line options (that is, unless you specifically tell it to use a config file
with the `--use-ini` option).

### Slow rendering on certain ROMs with VPM?

Use VPM 3.1. If you can't wait there's a beta build [here](http://vpuniverse.com/forums/topic/3461-sambuild31-beta-thread/?page=2&tab=comments#comment-39309).

*Thanks to djrobx for the fix an all others at [#52](https://github.com/freezy/dmd-extensions/issues/52) for reporting.*

### Weird positioning or no DMD visible at all?

When you override *High DPI scaling* in the host app (e.g. `vpinballx.exe`),
dmdext is put into a different coordinate system, so your `DmdDevice.ini`'s
position settings are applied differently. This can lead to wrong positioning
or complete off-screen rendering.

*Thanks to outhere for the tip!*

### Unable to load DLL 'serum.dll'

This can happen if your system doesn't have the needed Visual C++ Redistributable installed. You can
download it [here](https://aka.ms/vs/17/release/vc_redist.x86.exe).

*Thanks Jewer76!*

### Backglass covers segment displays

Try unchecking "backglass bring to front" by right clicking the backglass ([source](https://github.com/freezy/dmd-extensions/issues/332)).

*Thanks wiesshund!*

## Reporting Bugs

Make sure you include the application log, in its entirety. You can *usually* find
it at the same place you copied your `DmdDevice.dll` or `dmdext.exe`. However, there
are two premises for the log to be created:

1. You need a log config file. If there's no log config file, no log is created.
   The log config file is called `DmdDevice.log.config` for both `DmdDevice.dll` and 
   `DmdDevice64.dll`, and `dmdext.log.config` for `dmdext.exe`. They must be at
   the same place as the DLL and EXE respectively.
2. The user running the host app must have write permissions to the log folder.
   Per default that's the current working directory. If you have copied the DLL to the
   `Windows\SysWOW64` folder, you don't have write access. In this case you need
   to write the log elsewhere. You can do that by editing the log config file.
   The format of the log file is XML. To change the log file location, find this line:

       <target xsi:type="File" name="file" fileName="DmdDevice.log"

   And change the `fileName` attribute to somewhere you can write, for example:

       <target xsi:type="File" name="file" fileName="C:\Users\youruser\DmdDevice.log"

For problems with DmdDevice.dll, specially with coloring, set the log level to
`Trace` and reproduce the problem. You can do that by editing `DmdDevice.log.config`
and changing:

```xml
<logger name="*" minlevel="Info" writeTo="file" />
```
to
```xml
<logger name="*" minlevel="Trace" writeTo="file" />
```

If you have a crash, please also include which OS/bitness you're using. If it's
about the DLL, let us know where you copied the DLL and which host application
you're using (VPM's `setup.exe` or`vpinball.exe`, also which version).

You can post bugs at [VPUniverse](https://vpuniverse.com/forums/forum/175-dmd-extensions/), 
or preferably [here](https://github.com/freezy/dmd-extensions/issues).

## Manual Installation

1. [Download the .zip archive](https://github.com/freezy/dmd-extensions/releases),
   copy its content to your hard drive, preferably in your `PATH`,
   and unblock the `.exe` file (right-click, properties, unblock).
2. Download and install the [Visual C++ Redistributables for Visual Studio 2017](https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads).
3. If you haven't already, download and install the [.NET Framework 4.7.2](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472).

If you want to use DMD Extensions with VisualPinMAME:

1. Copy `DmdDevice.dll`, `DmdDevice64.dll`, `DmdDevice.ini` and `DmdDevice.log.config` 
   into your VPM folder (usually at `Visual Pinball\VPinMAME`).
2. Run the VPM setup
3. Click on *Test*, select the game and click on *Game Options*.
4. Enable *Use External DMD* and optionally disable *Show DMD*.

You can also set this as the default by selecting *Default Options* in the VPM
setup, however note that games that you've already configured won't be affected.

## Game Names

| Title                                | Pinball Arcade      | Pinball FX3                            | Pinball FX |
|--------------------------------------|---------------------|----------------------------------------|------------|
| AC/DC                                | `ACDC`              |                                        |            |
| Attack From Mars                     | `AttackFromMars`    | `BALLY_Attack_from_Mars`               | `119`      |
| Big Buck Hunter PRO                  | `BuckHunter`        |                                        |            | 
| Big Hurt                             | `BigHurt`           |                                        |            |
| Black Rose                           | `BlackRose`         | `BALLY_BlackRose`                      | `118`      |
| Bram Stoker's Dracula                | `Dracula`           |                                        |            |
| Cactus Canyon                        | `CactusCanyon`      |                                        |            |
| Champion Pub                         | `ChampionPub`       | `BALLY_Champion_Pub`                   | `121`      |
| Cirqus Voltaire                      | `Cirqus`            | `BALLY_Cirqus_Voltaire`                | `127`      |
| Creature of the Black Lagoon         | `Creature`          | `BALLY_Creature_from_the_Black_Lagoon` | `131`      |
| Cue Ball Wizard                      | `CueBallWizard`     |                                        |            |
| Doctor Who                           | `DoctorWho`         |                                        |            |
| Dr. Dude and his Excellent Ray       | `DrDude`            | `BALLY_Dr_Dude`                        | `135`      |
| Fish Tales                           | `FishTales`         | `WMS_Fish_Tales`                       | `108`      |
| Funhouse                             | `FnHouse`           | `WMS_Funhouse`                         | `134`      |
| Ghostbusters                         | `GhostBustersStern` |                                        |            |
| Gladiators                           | `Gladiators`        |                                        |            |
| Harley Davidson                      | `HarleyDavidson`    |                                        |            |
| High Roller Casino                   | `HighRollerCasino`  |                                        |            |
| Hurricane                            | `Hurricane`         | `WMS_Hurricane`                        | `126`      |
| Indiana Jones: The Pinball Adventure |                     | `WMS_Indiana_Jones`                    | `133`      |
| Indianapolis 500                     | `Indy500`           |                                        |            |
| Jack·Bot                             | `JackBot`           |                                        |            |
| Judge Dredd                          | `JudgeDredd`        |                                        |            |
| Junk Yard                            | `JunkYard`          | `WMS_Junkyard`                         | `110`      |
| Last Action Hero                     | `LastActionHero`    |                                        |            |
| Mary Shelley's Frankenstein          | `Frankenstein`      |                                        |            |
| Medieval Madness                     | `Mediev`            | `WMS_Medieval_Madness`                 | `109`      |
| Monster Bash                         | `Monster`           | `WMS_Monster_Bash`                     | `130`      |
| Mustang                              | `Mustang`           |                                        |            |
| No Fear: Dangerous Sports            | `NoFear`            |                                        |            |
| No Good Gofers                       | `Gofers`            | `WMS_No_Good_Gofers`                   | `128`      |
| Party Zone                           | `PartyZone`         | `BALLY_Party_Zone`                     | `120`      |
| Pistol Poker                         | `PistolPoker`       |                                        |            |
| Red and Ted's Road Show              | `RoadShow`          | `WMS_Roadshow`                         | `125`      |
| Rescue 911                           | `Rescue911`         |                                        |            |
| Ripley's Believe it Or Not           | `Ripleys`           |                                        |            |
| Safe Cracker                         | `SafeCracker`       | `BALLY_Safe_Cracker`                   | `122`      |
| Scared Stiff                         | `ScaredStiff`       |                                        |            |
| Space Station                        |                     | `WMS_Space_Station`                    | `129`      |
| Starship Troopers                    | `StarshipTroopers`  |                                        |            |
| StarTrek                             | `StarTrek`          |                                        |            |
| StarTrek: The Next Generation        | `StarTrekTNG`       |                                        | `163`      |
| Swords of Fury                       | `SwordsOfFury`      |                                        | `149`      |
| Tales of the Arabian Nights          | `Totan`             | `WMS_Tales_of_the_Arabian_Nights`      | `132`      |
| Teed Off                             | `TeedOff`           |                                        |            |
| Terminator 2: Judgment Day           | `Terminator2`       |                                        |            |
| The Addams Family                    | `AddamsFamily`      |                                        | `156`      |
| The Getaway: High Speed 2            | `HighSpeed2`        | `WMS_Getaway`                          | `111`      |
| The Machine: Bride of Pin·Bot        | `BridePB`           |                                        | `148`      |
| Theatre of Magic                     | `TheaterOfMagic`    | `BALLY_TheatreOfMagic`                 | `117`      |
| Twilight Zone                        | `TwilightZone`      |                                        | `162`      |
| Whirlwind                            | `WhirlWind`         |                                        | `168`      |
| White Water                          | `WhiteWater`        | `WMS_White_Water`                      | `124`      |
| WHO dunnit                           | `WhoDunnit`         |                                        |            |
| Whoa Nellie! Big Juicy Melons        | `WhoaNellie`        |                                        |            |
| Wipeout                              | `Wipeout`           |                                        |            |
| World Cup Soccer                     | `WorldCup`          |                                        | `157`      |
| World Tour                           | `WorldTour`         |                                        |            |

### x64 vs x86

- For `dmdext.exe`, take the bitness of your OS. This is important for Pro
  Pinball, where the bitness of the binary must be the same as dmdext's.
- For `DmdDevice.dll`, we now recommend using the 64-bit version 
  (i.e. `DmdDevice64.dll`).


## Build Instructions

1. Download and install [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)
2. The unmanaged exports library needs MS Build tools, which come with .NET 3.5. [Install Instructions](https://docs.microsoft.com/en-us/dotnet/framework/install/dotnet-35-windows-10)
3. *Recommended:* If you want `DmdDevice.dll` automatically copied to your VPM folder after build, point the `VPM_HOME` environment variable to your VPM installation folder.
4. Clone the repo: `git clone https://github.com/freezy/dmd-extensions.git`
5. After cloning the repo, you'll need to restore the [DllExport](https://github.com/3F/DllExport)
   dependency. There is a script that does it for you. Go into the cloned repo and run:
   ```cmd
   DllExport -action Restore -sln-file DmdExtensions.sln
   ```
6. Open the `.sln` file in Visual Studio and build the solution.

If you want to build the installer, you'll need the [WiX Toolset v3](https://wixtoolset.org/docs/wix3/)
and its [Visual Studio Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension).

Then there is an issue with Fody third party addin that may give you an error
ith the $(IntermediateOutputPath). If this occurs, simply close Visual Studio
and re-launch the DMD Extensions project and the issue will go away. [Reference](https://github.com/Fody/Fody/issues/629)


## Credits

- NoEx for the excellent TPA grabber code, as well as the Pinball FX3 memory grabber.
- Tom Speirs, Lucky1, CarnyPriest and Russdx for their help on the DMD code
- Lucky1 for instructions and details about the coloring features and Pin2DMD support.
- DJRobX for helping with the dynamic coloring, better performance and much more.
- mjrgh for debugging and optimizing stuff.
- Adrian Page from Barnstorm for his help setting up the slave correctly.
- Funkyman for 64 color support, 256x64 scaling options, bug fixes and code cleanup.
- To Zen Studios for their support and for making Pinball FX!

<a title="IntelliJ IDEA" href="https://www.jetbrains.com/resharper/"><img src="https://raw.githubusercontent.com/freezy/dmd-extensions/master/resharper.svg?sanitize=true" alt="IntelliJ Resharper" width="250"></a>

Finally, thanks to JetBrains for their awesome tools and support of the Open Source Community!


## License

GPLv2, see [LICENSE](LICENSE).
