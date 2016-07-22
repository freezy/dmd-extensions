# Bridge Library for Pro Pinball

Pro Pinball uses Boost's [message queue](http://www.boost.org/doc/libs/1_60_0/doc/html/boost/interprocess/message_queue.html)
for sending DMD frames to a separate process. Pro Pinball can launch a custom process for supporting
real DMDs instead of the standard virtual DMD by using the `d<executable>` option when launching the
game. The game must also be launched with the `m3` option, specifying a three monitor setup so the DMD gets frames through IPC.

An example of a PinDMDv2 implementation can be seen [here](https://www.pro-pinball.com/forum/viewtopic.php?f=22&t=778).
The goal of this bridge is to provide a DLL which can be easily used by any language able to import
COM libraries.