# LibDmd.Core

The cross-platform (WPF-free) core of [dmd-extensions](https://github.com/freezy/dmd-extensions) /
`LibDmd`, targeting `netstandard2.1` (Unity / Mono / CoreCLR) and `net8.0`.

It contains:

- the reactive **frame pipeline** (`RenderGraph`),
- **Serum** (`.cRZ`) and pure-C# **VNI/PAL** colorization,
- **ZeDMD**, **PinDMD3** and **Pixelcade** output devices.

## Native binaries

The Serum and ZeDMD output paths call native libraries (`libserum`, `libzedmd`). Those are
**not** in this package — install the matching native package as well:

```
dotnet add package LibDmd.Core
dotnet add package LibDmd.Core.Native
```

`LibDmd.Core.Native` is a meta-package that pulls the per-platform native packages
(`LibDmd.Core.Native.win-x64`, `…win-x86`, `…osx-x64`, `…osx-arm64`, `…linux-x64`) and copies the
right binaries to your output directory. If you only target one platform you can reference just
that RID package instead of the meta-package.

> In Unity, native libraries are delivered through the per-OS `Plugins/` folders instead; the
> managed assembly resolves them by name via `NativeLibraryLoader`.

## Frame ownership and steady-state allocations

Core render graphs and VNI/plugin/fallback converters enable `ReuseFrameBuffers` by default.
Their callbacks receive **read-only borrowed frames**, valid until that callback returns.
Treat frame objects, pixel arrays, palettes, and conversion results made within a callback as
borrowed. `Clone()` is shallow and does not retain the pixels. To retain a frame, copy it into
separate storage before returning:

```csharp
var retained = new DmdFrame();
// In a grayscale/RGB callback; safe to reuse only after the previous copy is consumed:
retained.CopyFrom(frame);

var retainedColored = new ColoredFrame();
// In a colored callback; copies both pixels and palette:
retainedColored.Update(coloredFrame);
```

Use one snapshot per queued/in-flight frame. Alphanumeric segments, palette-only updates and
frame events follow the same callback lifetime. The library's asynchronous render queues and
clocked converters copy their inputs into owned snapshots before returning. Palette rotation
also keeps a private snapshot; transformations and colorization use separate output buffers,
so grayscale data remains available to other destinations. SDL copies pixels into its own
back buffer before returning; device encoders pack into their own transmission buffers.

Set `RenderGraph.ReuseFrameBuffers = false` before starting a graph if a custom destination
needs the previous allocating conversion behavior. VNI, plugin and switching converters have
the same opt-out for their direct output subscriptions. Serum output has always used reusable
native/managed storage and must be copied when retained. Legacy `LibDmd` keeps reuse disabled
by default. Direct utility calls outside a borrowed callback still return ordinary arrays.

Release-build regression tests measure zero managed allocations after warm-up for the covered
processing paths, including synchronous/asynchronous delivery, conversion/scaling, VNI
animations, Serum frame publication and hardware packing. Initialization, new dimensions or
formats, a new maximum queue depth, explicit owning copies, diagnostic logging, and allocations
inside native libraries or transport implementations are outside that guarantee. Queues retain
storage up to their high-water mark and preserve all frames; sustained producer overload can
grow the queue. Debug builds include an allocating profiler.

Serum reuses its rotation timer across animations and polls at approximately 60 Hz.
This also corrects the former fractional-millisecond interval, which rounded to
zero on timer-backed schedulers instead of providing a recurring 60 Hz poll.

Run the regression suite with:

```sh
dotnet test LibDmd.Core.Tests/LibDmd.Core.Tests.csproj -c Release
```

## License

GPL-2.0 — see the bundled `LICENSE`.
