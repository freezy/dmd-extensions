# LibDmd.Core.Sdl — macOS / Linux validation guide

This assembly is the **cross-platform, host-pumped** native DMD window (SDL2 + OpenGL ES 2.0).
It is a **code-complete draft that has never run on a Mac**. The architecture (no background
thread, host drives `Pump()` on the main thread) is the thing to validate; the rendering details
below are the likely first-run tweaks.

## How it differs from the Windows backend
- `LibDmd.Core.Windows` (`NativeWindowDestination` → `Win32NativeWindowDestination`) owns its own
  Win32 thread + message loop (self-driven). `INativeDmdWindow.RequiresHostPump == false`.
- `LibDmd.Core.Sdl` (`SdlNativeDmdWindow`) creates nothing in the background: the VPE bridge calls
  `Pump()` once per frame from Unity's **main thread** (`DmdBridgePlayer.Update` →
  `DmdPipeline.PumpMainThread`). `RequiresHostPump == true`. This is required because AppKit/SDL
  windowing on macOS is main-thread-only.
- Frames arrive on the DMD **worker** thread (`RenderGray*/RenderRgb*`) and are only written into a
  locked back-buffer; all GL happens in `Pump()` on the main thread.

## Native runtime the consumer must ship (NOT bundled here)
Drop these next to the managed plugins (`Plugins/osx-arm64`, `osx-x64`, `linux-x64`):
- **SDL2** — `libSDL2-2.0.0.dylib` (macOS) / `libSDL2-2.0.so.0` (Linux). The P/Invoke library name
  is `SDL2`.
- **ANGLE** (macOS, and Windows if ever used): `libEGL.dylib` + `libGLESv2.dylib`. We request a GL ES
  profile (`SDL_GL_CONTEXT_PROFILE_ES`, v2.0) and set `SDL_OPENGL_ES_DRIVER=1`. On Linux, native GL ES
  (Mesa) works without ANGLE.

## Build
```
dotnet build LibDmd.Core.slnx -c Release          # builds Core + Windows + Sdl
# or, to (re)deploy into the VPE package Plugins for a mac RID:
dotnet build VisualPinball.Engine.DMD/VisualPinball.Engine.DMD.csproj -c Release -r osx-arm64
```
The VPE proxy references this project for all non-`win-x64` RIDs and copies `LibDmd.Core.Sdl.dll`
into `Plugins/<rid>/` via its `PluginsDeploy` target.

## Go/No-Go checklist (in priority order)
1. **GL context coexistence with Unity (THE risk).** `Pump()` saves the current GL context, makes the
   SDL context current, renders, then best-effort restores. Restoring a *non-SDL* (Unity) context via
   `SDL_GL_MakeCurrent` is not portable, so watch for Unity's own view going black or the DMD window
   not painting. If broken, the fix lives entirely in `SdlNativeDmdWindow.Pump`/`Initialize`
   (e.g. platform-native save/restore, or confirm Unity renders on a separate thread so there's no
   conflict). Validate a borderless, repositionable window alongside Unity HDRP — this settles plan
   spike **S1**.
2. **Window orientation.** The source texture is row-flipped on upload (matching the desktop renderer)
   and the quad maps uv directly. If the DMD is upside-down, remove the flip in
   `GlesDmdPipeline.GetUploadBuffer` *or* invert `uv.y` in the vertex shader — one line.
3. **Shader compile.** Shaders are `#version 100` + `precision highp float;`. If ANGLE rejects them,
   check the `#define`/`const` preamble in `GlesDmdPipeline.BuildDmdFragment` and the blur prelude.
4. **Letterboxing.** `ComputeLetterbox` centers the DMD aspect in the drawable; verify on HiDPI
   (we use `SDL_GL_GetDrawableSize`, so the viewport should be in pixels, not points).
5. **Borderless drag.** Left-drag moves the window (manual, via `SDL_GetGlobalMouseState`); `stayontop`
   and live reposition/resize from `dmddevice.ini` flow through `ConfigureWindow`.

## Not yet ported
- **Glass overlay** (`GLASS` shader path + glass texture) — the dot + dot-glow + back-glow passes are
  ported; glass is a follow-on. `DmdWindowStyle` already carries the glass fields.
- Vsync is on (`SDL_GL_SetSwapInterval(1)`); revisit if it ever stalls the host pump.
