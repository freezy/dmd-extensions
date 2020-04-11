# Network Streaming

DMD Extensions allow receiving frames through the network. Both ends are 
implemented, i.e. you can receive and send frames.

This can be useful if you want to test the DMD but your physical display
is connected to a different machine, or if you have a display that is already
driven by a network daemon and you want it to receive data from dmdext.

The frames are sent in binary data through a WebSocket and are thus somewhat
effecient.

## Server

*Run this where your physical display is connected.*

The console application now has another mode, `server`. This spawns a web 
server and listens for incoming frames. The other options are the same, i.e.
you can pass it the usual destination you want the server to output frames
to.

For example, with a PinDMD3, you would run the server like so:

```
dmdext server -d pindmdv3 --ip=127.0.0.1 --port=80 --path=/dmd
```

Now you have an open WebSocket at `ws://127.0.0.1/dmd` that listens to incoming
frames.

## Client

*Run this where your game is running.*

Dmdext now has another destination called `network` (or `[networkstream]` in
DmdDevice.ini). Enabling it will send the frames of your game to the given
WebSocket.

For example, if you want to send the DMD of Pinball FX3 to the server you've 
started above, you would run dmdext like this:

```
dmdext mirror -s pinballfx3 -d network --url=ws://127.0.0.1/dmd
```

Or, if you want to use it with VPM, add this to your DmdDevice.ini:

```ini
[networkstream]
; if enabled, stream to your DMD connected to another computer
enabled = true
url = ws://127.0.0.1/dmd
```

Now, the display connected to where your server is running will render frames
from where you're playing!