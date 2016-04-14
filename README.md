# DMD Extensions

*Features for DMD owners that aren't officially supported yet*

Real DMDs for virtual pinball cabinets are pretty cool but they need to be
supported by the software running the games. This is an attempt to fill some
gaps.

## Supported Displays

Currently supported displays are the brand new **PinDMD v3**, as well as 
**PIN2DMD** and soon **PinDMD v2**.  
For development there is also a virtual DMD that renders on the computer
screen, so you can fiddle with it without any display at all.

## Features

The primary goal of this project was to add support real DMD support for games
that don't provide any support. Currently, only Pinball FX2 is supported.

### Pinball FX2

Pinball FX2 supports basic cabinet features such as portrait mode and multi
monitors. These options must be enabled. The application will then grab pixels
off the DMD rendered by Pinball FX2, parse the dots and their luminosity and
send the data to the physical DMD.

The grabber should work even if the DMD is behind the playfield, so under
settings, move it to position `0`/`0`. Size should be the following:

- Width: `1036`
- Height: `272`

Then launch both the application and Pinball FX2 and you should see frames 
appearing on the real DMD.

## TODO

Since Pinball Arcade axed the promised cabinet features, we won't see any
official support soon. Using the FreeCam mod, the same method as for Pinball
FX2 might be possible.

This is still considered proof of concept. A lot will change until the v1.0
tag. Report bugs either at VPF or here on the bug tracker.


## Credits

- [Cropper](http://cropper.codeplex.com/) for the neat screen capture code
- Tom Speirs, Lucky1, CarnyPriest and Russdx for their help on the DMD code

## License

GPLv2, see [LICENSE](LICENSE).