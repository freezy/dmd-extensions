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

It's also possible to render to a virtual DMD on the computer screen if you 
want to test or debug or you don't have a real DMD.


## Features

The primary goal of this project was to add support real DMDs for games that
don't provide support out of the box. The first game is Pinball FX2 and we're
looking into support for the Pinball Arcade as well.


## Install Instructions

[Get the latest release](https://github.com/freezy/dmd-extensions/releases),
copy the content of the zip file to your hard drive, preferable in your `PATH`,
and unblock the `.exe` file (right-click, properties, unblock).

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

## Documentation

All options are documented in the tool.

```
C:\>dmdext

DMD Extensions v1.1.1
USAGE: dmdext <command> [<options>]

  mirror    Mirrors pixel data from the screen to one or more other
            destinations.

  test      Displays a test image on all available devices.
```

### Mirror

```
C:\>dmdext mirror --help

DMD Extensions v1.1.1
USAGE: dmdext mirror --source=<source> [--destination=<destination>]

  -s, --source             Required. The source you want to retrieve DMD data
                           from. One of: [ pinballfx2, screen ].

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

  --no-shading             [pinballfx2] Disable shading, i.e. artificial
                           downsampling for RGB displays. Default: false.

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

  -d, --destination        The destination where the DMD data is sent to. One
                           of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd,
                           virtual ]. Default: "auto", which outputs to all
                           available devices.

  --no-virtual             Explicitly disables the virtual DMD when destination
                           is "auto". Default: false.

  --virtual-stay-on-top    Makes the virtual DMD stay on top of other
                           application windows. Default: false.

  --virtual-hide-grip      Hides the resize grip of the virtual DMD. Default:
                           false.

  --virtual-position       Position and size of virtual DMD. Three values:
                           <Left> <Top> <Width>. Default: "0 0 1024".

  --use-gray4              Sends frames in 4-bit grayscale to the display if
                           supported. Default: false

  --flip-x                 Flips the image horizontally. Default: false.

  --flip-y                 Flips the image vertically. Default: false.

  -q, --quit-when-done     Exit the program when finished, e.g. when Pinball
                           FX2 doesn't receive any frames anymore. Default:
                           false
```

### Test

```
C:\>dmdext test --help

DMD Extensions v1.1.1
USAGE: dmdext test [--destination=<destination>]

  -d, --destination       The destination where the DMD data is sent to. One
                          of: [ auto, pindmdv1, pindmdv2, pindmdv3, pin2dmd,
                          virtual ]. Default: "auto", which outputs to all
                          available devices.

  --no-virtual            Explicitly disables the virtual DMD when destination
                          is "auto". Default: false.

  --use-gray4             Sends frames in 4-bit grayscale to the display if
                          supported.

  --flip-x                Flips the image horizontally. Default: false.

  --flip-y                Flips the image vertically. Default: false.

  -q, --quit-when-done    Exit the program when finished, e.g. when Pinball FX2
                          doesn't receive any frames anymore.
```

## Compatibility

This application is based on .NET 4.5, which only runs on Windows 7 or later.
Probably on Vista too, but who cares. If you're still running WinXP then you
have my sincerest sympathy, but that's as far as I go. ;)


## Credits

- [Cropper](http://cropper.codeplex.com/) for some of the neat screen capture 
  code
- Tom Speirs, Lucky1, CarnyPriest and Russdx for their help on the DMD code


## License

GPLv2, see [LICENSE](LICENSE).