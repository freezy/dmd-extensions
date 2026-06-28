# Testing the DMD window on macOS (no Unity, no SDK)

This lets someone validate the cross-platform **SDL / GL-ES (ANGLE)** virtual-DMD window — the
macOS go/no-go for the VPE DMD work — by running one self-contained console app. It opens a
borderless DMD window and animates a test pattern through the real shader pipeline
(`GlesDmdPipeline`: dots + two-stage glow + glass), exactly as VPE will on macOS.

> **Scope.** This proves the window + ANGLE GL-ES context + the shader **standalone**. It does *not*
> prove ANGLE coexisting in-process with Unity's Metal/HDRP device — that still needs the Unity
> macOS player. Getting this working is the high-risk part and the right first step.

## Build it (developer, on Windows or macOS)

```bash
LibDmd.Core.Harness/publish-macos.sh osx-arm64   # or osx-x64 for Intel Macs
```

Produces `artifacts/macos-harness/<rid>/` and a `.tar.gz`. The app is self-contained (no .NET SDK
needed on the test Mac).

### The one thing not bundled: SDL2 + ANGLE

macOS has no native OpenGL ES, so the ES context comes from **ANGLE**. These three dylibs must sit
next to the executable; the script bundles them automatically if you drop them into
`native/vendor/<rid>/` first:

| File | Where to get it |
|---|---|
| `libSDL2.dylib` | `brew install sdl2` → copy `$(brew --prefix sdl2)/lib/libSDL2-2.0.0.dylib` as `libSDL2.dylib`; or libsdl.org |
| `libEGL.dylib` | ANGLE build — e.g. from an Electron release (`Electron.app/Contents/Frameworks/`) or a prebuilt ANGLE |
| `libGLESv2.dylib` | same ANGLE source as `libEGL.dylib` |

Match the architecture to the RID (`arm64` dylibs for `osx-arm64`, `x86_64` for `osx-x64`;
`lipo -archs <dylib>` to check).

## Run it (the tester, on the Mac)

```bash
tar -xzf libdmd-macos-harness-osx-arm64.tar.gz
cd osx-arm64
xattr -dr com.apple.quarantine .        # clear Gatekeeper so the unsigned app/dylibs run
./LibDmd.Core.Harness --native-window
```

**Success:** a borderless ~1024×256 window appears showing animated colored bars with a dot-matrix
look and glow, for ~10 seconds. Dragging it with the mouse should reposition it. The console prints
`Window up: Native DMD Window (SDL/GL ES) (RequiresHostPump=True)`.

## If it doesn't work

- **`Native window unavailable ... SDL2 / ANGLE runtime is missing`** — the three dylibs aren't next
  to the exe, are the wrong architecture, or are still quarantined. Re-check the table above and the
  `xattr` step.
- **Window opens but is black / the app crashes on the GL context** — ANGLE isn't providing the
  GL-ES context. This is the core macOS go/no-go; capture the console output (the
  `SDL_GL_CreateContext` / pipeline error line) and send it back.
- **Image is upside-down** — known one-liner: flip in `GetUploadBuffer` or invert `uv.y` in the
  vertex shader (see `GlesDmdPipeline` remarks).

Send back the full console output either way — it logs OS/arch, whether the window came up, and any
GL error, which is what we need to settle the spike.
