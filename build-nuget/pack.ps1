<#
.SYNOPSIS
  Packs the LibDmd.Core NuGet packages: the managed package plus the per-platform native
  packages (libserum / libzedmd), mirroring the vpinball/pinmame-dotnet layout.

.DESCRIPTION
  Produces, in ./artifacts:
    - LibDmd.Core.<version>.nupkg                 (managed, multi-targets netstandard2.1 + net8.0)
    - LibDmd.Core.Native.<version>.nupkg          (meta package -> all per-RID packages)
    - LibDmd.Core.Native.<rid>.<version>.nupkg    (one per RID: win-x64, osx-x64, osx-arm64, linux-x64)

  Native binaries must be staged first (build-nuget/fetch-natives.sh); pass -SkipFetch to reuse
  an existing native/nuget/runtimes/ tree.

.EXAMPLE
  pwsh build-nuget/pack.ps1 -Version 0.1.0
  pwsh build-nuget/pack.ps1 -Version 0.1.0 -Push -ApiKey $env:NUGET_API_KEY
#>
[CmdletBinding()]
param(
  [string]$Version = "0.1.0-dev",
  [string]$SerumVersion = "2.5.2",
  [string]$ZedmdVersion = "0.11.0",
  [switch]$SkipFetch,
  [switch]$Push,
  [string]$ApiKey,
  [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path "$PSScriptRoot/..").Path
$nugetDir = Join-Path $repo "native/nuget"
$artifacts = Join-Path $repo "artifacts"
$rids = @("win-x64", "osx-x64", "osx-arm64", "linux-x64")

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# 1. Stage native binaries -----------------------------------------------------------------
if (-not $SkipFetch) {
  Write-Host "==> Fetching native binaries (libserum $SerumVersion, libzedmd $ZedmdVersion)" -ForegroundColor Cyan
  $env:SERUM_VERSION = $SerumVersion
  $env:ZEDMD_VERSION = $ZedmdVersion
  & bash "$repo/build-nuget/fetch-natives.sh"
  if ($LASTEXITCODE -ne 0) { throw "fetch-natives.sh failed" }
}

# 2. Ensure nuget.exe (for packing the native .nuspec files) -------------------------------
$nuget = Get-Command nuget -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (-not $nuget) {
  $nuget = Join-Path $PSScriptRoot "nuget.exe"
  if (-not (Test-Path $nuget)) {
    Write-Host "==> Downloading nuget.exe" -ForegroundColor Cyan
    Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nuget
  }
}

# 3. Pack native packages (meta + per-RID) from the .nuspec files --------------------------
#    __VERSION__ is replaced in a temp copy; -BasePath keeps the relative <file src> paths valid.
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("libdmdcore-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
try {
  $availRids = @($rids | Where-Object {
    $d = Join-Path $nugetDir "runtimes/$_/native"
    (Test-Path $d) -and @(Get-ChildItem $d -File -ErrorAction SilentlyContinue).Count -gt 0
  })
  $skipped = @($rids | Where-Object { $availRids -notcontains $_ })
  if ($skipped.Count -gt 0) {
    Write-Warning "No staged native binaries for: $($skipped -join ', '). Skipping those packages (run build-nuget/fetch-natives.sh on the matching OS, e.g. in CI)."
  }
  $nuspecs = @("LibDmd.Core.Native") + ($availRids | ForEach-Object { "LibDmd.Core.Native.$_" })
  foreach ($id in $nuspecs) {
    $src = Join-Path $nugetDir "$id.nuspec"
    $dst = Join-Path $tmp "$id.nuspec"
    (Get-Content $src -Raw).Replace("__VERSION__", $Version) | Set-Content $dst -Encoding UTF8
    Write-Host "==> Packing $id $Version" -ForegroundColor Cyan
    & $nuget pack $dst -BasePath $nugetDir -OutputDirectory $artifacts -Version $Version -NoDefaultExcludes
    if ($LASTEXITCODE -ne 0) { throw "nuget pack failed for $id" }
  }
} finally {
  Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

# 4. Pack the managed package (depends on LibDmd.Core.Native, resolved from ./artifacts) ----
Write-Host "==> Packing LibDmd.Core $Version (managed)" -ForegroundColor Cyan
& dotnet pack (Join-Path $repo "LibDmd.Core/LibDmd.Core.csproj") `
  -c Release `
  -p:Version=$Version `
  -p:LibDmdCoreNativeVersion=$Version `
  -p:RestoreSources="$Source;$artifacts" `
  -o $artifacts
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for LibDmd.Core" }

Write-Host "`nPackages in $artifacts :" -ForegroundColor Green
Get-ChildItem $artifacts -Filter *.nupkg | ForEach-Object { Write-Host "  $($_.Name)" }

# 5. Optionally publish --------------------------------------------------------------------
if ($Push) {
  if (-not $ApiKey) { throw "-Push requires -ApiKey" }
  Write-Host "==> Pushing to $Source" -ForegroundColor Cyan
  Get-ChildItem $artifacts -Filter *.nupkg | ForEach-Object {
    & dotnet nuget push $_.FullName --api-key $ApiKey --source $Source --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "push failed for $($_.Name)" }
  }
}
