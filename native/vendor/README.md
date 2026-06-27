# Vendored native binaries

## Currently staged (in-repo)

- **libusb-1.0** — ✅ all four RIDs (`win-x64`, `osx-x64`, `osx-arm64`, `linux-x64`). win from the
  official libusb 1.0.27 release; the others from the PyPI `libusb_package` 1.0.26 wheels.
- **libftd2xx** — `win-x64/ftd2xx.dll` only (reused from the repo's existing copy). **macOS/Linux
  `libftd2xx` is NOT staged**: FTDI gates automated downloads, so grab `libftd2xx.{dylib,so}` from
  ftdichip.com manually and drop them under `osx-*/` and `linux-x64/` here (and in the Unity
  `Plugins/`). PinDMD1 is unaffected on Windows; on macOS/Linux it stays unavailable until then.

The same binaries are also placed in the VPE DMD package's Unity `Plugins/<rid>/` folders.

---


`serum`/`zedmd` are downloaded from their upstream GitHub releases by `build-nuget/fetch-natives.sh`.
The USB/FTDI libraries used by the PIN2DMD and PinDMD families don't have a single canonical
cross-platform release, so they are **vendored** here and picked up automatically by the same script
(and then packaged via the per-RID `runtimes/<rid>/native/*` glob).

## Layout

Drop the per-OS binaries under the matching RID folder, named exactly as the
`NativeLibraryLoader` resolver expects:

```
native/vendor/
  win-x64/      libusb-1.0.dll      ftd2xx.dll
  osx-x64/      libusb-1.0.0.dylib  libftd2xx.dylib
  osx-arm64/    libusb-1.0.0.dylib  libftd2xx.dylib
  linux-x64/    libusb-1.0.so.0     libftd2xx.so
```

- **libusb-1.0** — needed by PIN2DMD and PinDMD2. Sources: Windows → libusb.info / the
  `libusb/libusb` GitHub releases (`VS2022/MS64/dll`); macOS → Homebrew (`brew install libusb`,
  copy `libusb-1.0.0.dylib`); Linux → distro package `libusb-1.0-0` (`libusb-1.0.so.0`).
- **libftd2xx** — needed by PinDMD1. FTDI ships per-OS D2XX drivers (Windows/macOS/Linux/ARM) at
  ftdichip.com. `ftd2xx.dll` on Windows; `libftd2xx.{dylib,so}` elsewhere.

## Unity / VPE consumption

The NuGet path above is for general .NET consumers. For VPE, place the same binaries in the DMD
package's Unity `Plugins/<platform>/` folders (next to `libserum`/`libzedmd`). On **Linux**, add a
`udev` rule so the device is accessible without root, e.g. for PIN2DMD (VID `0314`):

```
SUBSYSTEM=="usb", ATTRS{idVendor}=="0314", MODE="0660", GROUP="plugdev"
```

This folder is intentionally empty in the repo (binaries are not committed); it documents the
expected drop location and names.
