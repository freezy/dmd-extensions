# LibDmd

*The core library for DMD Extensions*

This project contains all the logic for DMD Extensions. The [Console](https://github.com/freezy/dmd-extensions/tree/master/Console) and [PinMameDevice](https://github.com/freezy/dmd-extensions/tree/master/PinMameDevice) projects are wrappers for an executable and a VPM compatible DLL.

## Render Graph

DMD Extensions supports a number of *input* sources like Pinball FX2, the Pinball Arcade and Pro Pinball. DMD frames from these sources are routed to a bunch of supported *output* destinations such as PinDMD hardware displays, a virtual DMD, or the file system.

The [`RenderGraph`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/RenderGraph.cs) class is the class that hooks them together in an intelligent way. It does also the conversion of different formats, subscribes and disposes sources, manages converters and deals with DMD dimension changes. Short, it's the core class and the public API of the library.

### Frame Formats

Since we don't want to do unnecessary convertions, every source and destination has a preferred data format in which frames are sent and received respectively. There are currently six different formats:

#### 2-Bit Grayscale

Pixels with up to four different gray tones.

- Data Type: `byte[]`. Upper-left to lower-right pixel array. Every value between 0 and 3.
- Interfaces: [`IGray2Source`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Input/IGray2Source.cs), [`IGray2Destination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IGray2Destination.cs)
- Sources: Visual PinMAME
- Destinations: PinDMD1-3, PIN2DMD, all streaming destinations

#### 4-Bit Grayscale

Up to 16 different gray tones.

- Data Type: `byte[]`. Upper-left to lower-right pixel array. Every value between 0 and 15.
- Interfaces: [`IGray4Source`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Input/IGray4Source.cs), [`IGray4Destination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IGray4Destination.cs)
- Sources: Visual PinMAME, Pro Pinball, Pinball Arcade
- Destinations: PinDMD2-3, PIN2DMD, all streaming destinations

#### 2-Bit Colored

Every frame comes with a separate palette of four RGB colors. There are no native sources but the colorizer feature provides sources that generate frames in that format.

- Data Type: `Tuple<byte[][], Color[]>`. The byte table are upper-left to lower-right bit planes for every bit (four of them), and the color array contains the four RGB colors for every shade.
- Interfaces: [`IColoredGray2Source`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Input/IColoredGray2Source.cs), [`IColoredGray2Destination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IColoredGray2Destination.cs)
- Destinations: PinDMD3, PIN2DMD, streaming destinations

#### 4-Bit Colored

Same as the 2-bit colored format, but with 16 colors instead of four.

- Data Type: `Tuple<byte[][], Color[]>`. The byte table are upper-left to lower-right bit planes for every bit (16 of them), and the color array contains the 16 RGB colors for every shade.
- Interfaces: [`IColoredGray4Source`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Input/IColoredGray4Source.cs), [`IColoredGray4Destination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IColoredGray4Destination.cs)
- Destinations: PIN2DMD, streaming destinations

#### RGB24

These are full true color RGB frames. This format has no native sources.

- Data Type: `byte[]`. Upper-left to lower-right pixel array with three bytes per pixel containing the red, blue and green component of the color.
- Interfaces: [`IRgb24Source`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Input/IRgb24Source.cs), [`IRgb24Destination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IRgb24Destination.cs)
- Destinations: PinDMD3, PIN2DMD, virtual DMD

#### Bitmap

The bitmap format of .NET. 

- Data Type: `BitmapSource`. A bitmap as used in .NET
- Interfaces: [`IBitmapSource`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Input/IBitmapSource.cs), [`IBitmapDestination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IBitmapDestination.cs)
- Sources: Screen grabber, file sources
- Destinations: Virtual DMD, file destinations


### Routing

A render graph always has one frame source and one or more render destinations. It's up to the host application to define those, but it's the render graph's job to link them in an intelligent way. For that, it looks at which interfaces the source and destination implement and based on that decides which format to request from the source and which format to send to the destination.

That decision is based on three cases (in that order):

1. If the source format is the same as destination format (fastest): Just pass the data from source to destination.
2. If source contains less data than the destination (upscale): Convert up to the richer format (e.g. convert 2-bit grayscale to RGB24).
3. If source contains more data than the destination (downscale): Convert down to poorer format (e.g. convert RGB24 down to 16 shades grayscale).

All but the first case come with frame conversion. Good news is that they are relatively cheap and don't reflect in the frame rate. However, what's not cheap due to bus limitations is the data sent to the hardware displays. Thus, it's important that for example a PinDMD3 gets 2-bit frames with a palette (1kb per frame) rather than RGB24 frames (96kb per frame) if the source doesn't produce anything better than 2-bit grayscale anyway.

### DMD Pixel Resolution

In principle, any source can change the pixel dimension of the frame at any time. For example, VPM usually produces 128x32, but depending on the game that can also become 192x64 or even 256x64. Since VPM doesn't reinitialize between games, the source will switch dimension randomly.

For the destinations we support both cases: Displays like the virtual DMD that are able to adapt the resolution implement [`IResizableDestination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IResizableDestination.cs), while displays with fixed dimensions (hardware displays) implement [`IFixedSizeDestination`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Output/IFixedSizeDestination.cs).

Resizable displays will then be notified when the source dimension changes, while fixed displays will get a resized frame fitting their dimension.

### Colorization

It's possible to do frame-by-frame colorization similar to [ColorDMD](http://www.colordmd.com/). In such a case, an [`AbstractColorizer`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Converter/AbstractColorizer.cs) receives the source frames and acts as source at the same time. This is done by passing it as an [`IConverter`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/Converter/IConverter.cs) to the render graph.

### Multiple Graphs

Sometimes, a game can produce frames at multiple formats indeterminately. For example, VPM can produce 2-bit, 4-bit or RGB frames at any time. Since a render graph only has one source, multiple graphs can in this case created. The destination instances can be reused. Using [`RenderGraphCollection`](https://github.com/freezy/dmd-extensions/blob/master/LibDmd/RenderGraphCollection.cs) is recommended because it implements part of `RenderGraph`'s API and makes dealing with multiple render graphs easier.