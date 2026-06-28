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

## License

GPL-2.0 — see the bundled `LICENSE`.
