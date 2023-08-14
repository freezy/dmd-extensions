$buildDir = "_build"
$releaseDir = "_release"

function getBuildConfiguration()
{
    $sln = Get-Content "DmdExtensions.sln"
    $regex = [regex]"GlobalSection\(SolutionConfigurationPlatforms\)\s*=\s*preSolution\s*(.*?)\s*EndGlobalSection"

    $match = [System.Text.RegularExpressions.Regex]::Match($sln, $regex)
    if (!$match.Success) {
        Write-Host "Could not parse global section from .sln."
        Exit 1
    }
    $match = Select-String "([^|]+)\|([^\s]+)\s*=\s*[^|]+\|[^\s]+" -input $match.Groups[1].Value -AllMatches
    return $match.Matches | ForEach-Object { [System.Tuple]::Create($_.Groups[1].Value.Trim(),$_.Groups[2].Value.Trim()) }
}

function getShortName($name)
{
    $names = @{
        "Baller Installer" = "BALLER";
        "DMD Extensions Installer" = "INSTALLER";
        "Pixelcade" = "PIXELCADE";
        "VPX Installer" = "NOCOLOR";
    }
    return $names[$name]
}

function getVersion
{
    $asi = Get-Content "VersionAssemblyInfo.cs"
    $regex = [regex]"[^/]*\[assembly:\s*AssemblyInformationalVersion\(\""((\d)\.(\d)\.(\d)(-([\w\W]+))?)\""\)"
    $match = [System.Text.RegularExpressions.Regex]::Match($asi, $regex)
    if (!$match.Success) {
        Write-Host "Could not parse version from assembly info."
        Exit 1
    }

    return $match.Groups[1].Value
}


$version = getVersion
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
Write-Host "Building DMD Extensions version $version..."
foreach ($buildConfig in getBuildConfiguration) {

    $config = $buildConfig.Item1
    $platform = $buildConfig.Item2
    $n = $config -split " - "
    if ($n[0] -ne "Release") {
        continue
    }

    if ($null -eq $n[1]) {
        $suffix = ""
    } else {
        $suffix = "_$( getShortName($n[1].Trim()) )"
    }

    Write-Host "Building $config for $platform"

    & $msbuild .\DmdExtensions.sln -t:rebuild /p:Configuration="$config" /p:Platform=$platform

    if ($LastExitCode -ne 0) {
        Write-Host "Build of $config for $platform failed."
        Exit
    }

    if ($platform -eq "x64") {
        $dllSuffix = "64"
    } else {
        $dllSuffix = ""
    }

    if (Test-Path $buildDir) {
        Remove-Item $buildDir -Recurse -Force -Confirm:$false
    }

    New-Item -Name $buildDir -ItemType "directory"
    New-Item -Name "$buildDir\dmdext" -ItemType "directory"
    New-Item -Name "$buildDir\Future Pinball" -ItemType "directory"

    Copy-Item "PinMameDevice\data\textures" -Destination "$buildDir\dmdext" -Force -Recurse
    Copy-Item "LibDmd\Input\FutureDmd\OpenGL32.dll" -Destination "$buildDir\Future Pinball" -Force

#    Copy-Item "Console\bin\$platform\$config\dmdext.exe" -Destination $buildDir -Force
#    Copy-Item "Console\bin\$platform\$config\dmdext.log.config" -Destination $buildDir -Force
#    Copy-Item "Console\ProPinballSlave.bat" -Destination $buildDir -Force
#    Copy-Item "PinMameDevice\bin\$platform\$config\DmdDevice$dllSuffix.dll" -Destination $buildDir -Force
#    Copy-Item "PinMameDevice\bin\$platform\$config\DmdDevice.log.config" -Destination $buildDir -Force
#    Copy-Item "PinMameDevice\DmdDevice.ini" -Destination $buildDir -Force

    New-Item -ItemType Directory -Force -Path $releaseDir

    $zipArchive = "$releaseDir\dmdext-v$version-$platform$suffix.zip"

    if (Test-Path $zipArchive) {
        Remove-Item $zipArchive -Force -Confirm:$false
    }

    Compress-Archive -Path "Console\bin\$platform\$config\dmdext.exe" -Update -DestinationPath $zipArchive
    Compress-Archive -Path "Console\bin\$platform\$config\dmdext.log.config"  -Update -DestinationPath $zipArchive
    Compress-Archive -Path "Console\ProPinballSlave.bat" -Update -DestinationPath $zipArchive
    Compress-Archive -Path "PinMameDevice\bin\$platform\$config\DmdDevice$dllSuffix.dll" -Update -DestinationPath $zipArchive
    Compress-Archive -Path "PinMameDevice\bin\$platform\$config\DmdDevice.log.config" -Update -DestinationPath $zipArchive
    Compress-Archive -Path "PinMameDevice\DmdDevice.ini" -Update -DestinationPath $zipArchive
    Compress-Archive -Path "$buildDir\dmdext" -Update -DestinationPath $zipArchive
    Compress-Archive -Path "$buildDir\Future Pinball" -Update -DestinationPath $zipArchive

    Move-Item -Path "Installer\Builds\dmdext-v$version-$platform.msi" -Destination "$releaseDir\dmdext-v$version-$platform$suffix.msi"

}
