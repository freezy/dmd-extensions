# LibDmd.Core NuGet packaging

Produces the `LibDmd.Core` NuGet package plus per-platform native binary packages, modelled on
[vpinball/pinmame-dotnet](https://github.com/vpinball/pinmame-dotnet).

## Packages produced

| Package | Contents |
|---|---|
| `LibDmd.Core` | The managed assembly (`netstandard2.1` + `net8.0`). Depends on `LibDmd.Core.Native`. |
| `LibDmd.Core.Native` | Meta-package; depends on all per-RID native packages below (`include="native"`). |
| `LibDmd.Core.Native.win-x64` | `runtimes/win-x64/native/` → `serum64.dll`, `zedmd64.dll` |
| `LibDmd.Core.Native.osx-x64` | `runtimes/osx-x64/native/` → `libserum`/`libzedmd` + serial deps (`.dylib`) |
| `LibDmd.Core.Native.osx-arm64` | `runtimes/osx-arm64/native/` → same, arm64 |
| `LibDmd.Core.Native.linux-x64` | `runtimes/linux-x64/native/` → `libserum`/`libzedmd` + serial deps (`.so`) |

A consumer just installs `LibDmd.Core` (+ `LibDmd.Core.Native`); NuGet then deploys the matching
`runtimes/<rid>/native/` binaries and the .NET host resolves them at runtime via `deps.json`.

> Windows is **x64 only** — `LibDmd.Core` is built with `PLATFORM_X64` (so the P/Invokes use the
> `*64.dll` names). 32-bit Windows is served by the legacy WPF build, not this package.

## Native binary source & versions

Binaries are downloaded from the upstream GitHub releases:

- **libserum** — https://github.com/PPUC/libserum/releases (default `SERUM_VERSION=2.5.2`)
- **libzedmd** — https://github.com/PPUC/libzedmd/releases (default `ZEDMD_VERSION=0.11.0`)

> Keep these in sync with the C# wrappers (`Converter/Serum`, `Output/ZeDMD`). The Windows DLLs
> committed under `LibDmd/Costura64` (used by the legacy WPF product) may be a different build —
> bump the versions here when you update those.

## Build locally

```pwsh
# 1) stage native binaries  (bash; Git Bash on Windows. Linux symlinks need Linux/macOS or CI)
./build-nuget/fetch-natives.sh

# 2) pack everything into ./artifacts
pwsh ./build-nuget/pack.ps1 -Version 0.1.0 -SkipFetch
```

`pack.ps1` packs the native `.nuspec` files with `nuget.exe` (downloaded automatically if absent)
and the managed package with `dotnet pack`, resolving the native dependency from `./artifacts`.

To publish: `pwsh ./build-nuget/pack.ps1 -Version 0.1.0 -Push -ApiKey <key>`.

## CI

`.github/workflows/build-core-nuget.yml` runs on `windows-latest`: stages natives, packs, uploads
the `.nupkg`s as a build artifact, and — on a `core-v*` tag — pushes them to NuGet.org (needs the
`NUGET_API_KEY` secret). Linux symlink handling is correct on the runner's Git Bash because the
fetch dereferences real files; the per-OS `runtimes/` payloads come straight from the upstream
release archives.

## Files

- `native/nuget/*.nuspec` — package definitions (`__VERSION__` is substituted at pack time)
- `native/nuget/targets/*.targets` — copy the native libs next to the consumer output (best-effort
  for non-RID builds; RID publish & Unity resolve them on their own)
- `native/nuget/runtimes/` — staging dir populated by `fetch-natives.sh` (git-ignored)
