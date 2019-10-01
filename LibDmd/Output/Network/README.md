# Network Streaming

DMD Extensions comes with two drivers that stream DMD frames over the network.

`WebsocketServer` acts as a Websocket server to which clients can connect and 
receive frames. `WebsocketClient` connects to an existing server. Both are 
using the same protcol.

## Messages

Messages are sent as byte arrays. The first byte of every message defines the
type of the message. The following four bytes are a timestamp in milliseconds.

Timestamps are important to keep the frames in sync. What the consumer should
do is keep a time offset variable and adapt it as soon a frame gets delayed so
the next frames keep the same relative frame rate.

Note that there is not fixed frame rate though, frames are only sent if changed,
which can happen at irregular intervals.

All integers are little endian.

## Outgoing Messages

These are messages sent to the consumer.

### `0x01` - Dimensions

A dimension message indicates the resolution in which the following frames are
sent. Note that this can happen any time, the client should save this value and
adapt rendering depending on that value

#### Format

- tag: 1 byte, tag, always `0x01`
- timestamp: 4 bytes, uint32
- width: 2 bytes, ushort
- height: 2 bytes, ushort

### `0x02` - Color

Indicates in which color the frames should be tinted.

#### Format

- tag: 1 byte, tag, always `0x02`
- timestamp: 4 bytes, uint32
- color: 4 bytes, uint, e.g. `0xff00ff`

### `0x03` - Palette

Sets the color palette for the next frames. A color palette contains an array
of colors, where every color is mapped to the pixel value of its index. 

#### Format

- tag: 1 byte, tag, always `0x03`
- timestamp: 4 bytes, uint32
- number of colors: 2 bytes, ushort
- colors, 4 bytes uint each

### `0x10` - Bit Planes

Sends a frame of n bitplanes (or "sub frames"), where n is the number of bits 
and a bit plane is a top-left to bottom-right array of pixels for the given 
bit.

#### Format

- tag: 1 byte, tag, always `0x10`
- timestamp: 4 bytes, uint32
- number of bit planes: 1 byte
- bit planes, each width * height / 8

### `0x11` - Colored Bit Planes

Like the bit planes above, but additionaly includes a palette for each bit
combination. For example, a 2-bit frame would come with a palette of four
colors.

#### Format

- tag: 1 byte, tag, always `0x11`
- timestamp: 4 bytes, uint32
- number of bit planes: 1 byte
- colors, 4 bytes uint times 2^(number of bitplanes)
- bit planes, each width * height / 8

### `0x12` - RGB24 Frame

Sends a full RGB24 frame, i.e. three bytes per pixel for each red, blue and
green, from top left to bottom right. This is only used when sending 
bitmaps directly, all pinball simulators use either bit planes or colored
bit planes.

#### Format

- tag: 1 byte, tag, always `0x12`
- timestamp: 4 bytes, uint32
- RGB42 array, width * height * 3 bytes

### `0x20` - Clear Display

Clear the display, i.e. set all pixels black. This message has no 
additional parameters.

#### Format

- tag: 1 byte, tag, always `0x20`
- timestamp: 4 bytes, uint32

### `0x21` - Clear Color

Sets the current color back to the default color. This message has no 
additional parameters.

#### Format

- tag: 1 byte, tag, always `0x21`
- timestamp: 4 bytes, uint32

### `0x22` - Clear Palette

Sets the current palette back to the default palette. This message has no 
additional parameters.

#### Format

- tag: 1 byte, tag, always `0x22`
- timestamp: 4 bytes, uint32

## Incoming Messages

### `0x00` - Init

When the consumer send an init message, it will receive the color, dimensions
and the palette if available. This allows the consumer to the websocket before
actually being ready to receive frames.

#### Format

- tag: 1 byte, tag, always `0x00`
