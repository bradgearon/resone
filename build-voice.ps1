param(
    [switch]$Clean,
    [switch]$NativeOnly
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$VoiceOut = Join-Path $Root 'build\voice'
$NativeBuild = Join-Path $Root 'build\voice-native'
$NativeSource = Join-Path $Root 'tools\resone-voice\native'
$CliProject = Join-Path $Root 'tools\resone-voice\ResoneVoice.csproj'
$CliExe = Join-Path $VoiceOut 'resone-voice.exe'

if ($Clean) {
    if (Test-Path $NativeBuild) { Remove-Item $NativeBuild -Recurse -Force }
    if (Test-Path $VoiceOut) { Remove-Item $VoiceOut -Recurse -Force }
}

New-Item -ItemType Directory -Force -Path $VoiceOut | Out-Null

if (!(Test-Path (Join-Path $NativeBuild 'CMakeCache.txt'))) {
    cmake -S $NativeSource -B $NativeBuild -G 'Visual Studio 17 2022' -A x64
    if ($LASTEXITCODE -ne 0) { throw 'CMake configure for resone-voice failed.' }
}

cmake --build $NativeBuild --config Release --target wds_resone_vocals --parallel
if ($LASTEXITCODE -ne 0) { throw 'Native vocal renderer build failed.' }

$VocalDll = Get-ChildItem $NativeBuild -Recurse -File -Filter 'wds.resone.vocals.dll' | Select-Object -First 1
if (!$VocalDll) { throw 'Native build completed but wds.resone.vocals.dll was not found.' }

# NativeOnly means "do not rebuild the managed CLI when it already exists".
# On a fresh tree, however, build-voice-native.ps1 must still leave a runnable
# resone-voice.exe behind, so bootstrap the CLI once if it is missing.
$BootstrapCli = $NativeOnly -and !(Test-Path $CliExe)
$BuildCli = !$NativeOnly -or $BootstrapCli

if ($BuildCli) {
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "resone-voice.exe does not exist yet and dotnet was not found. Install/use the same .NET SDK used by Resone, then run .\build-voice-native.ps1 again."
    }

    if ($BootstrapCli) {
        Write-Host 'resone-voice.exe is missing; bootstrapping the CLI once...' -ForegroundColor Yellow
    }

    dotnet build $CliProject -c Release -r win-x64 -o $VoiceOut --nologo
    if ($LASTEXITCODE -ne 0) { throw 'resone-voice CLI build failed.' }
}

# dotnet build may clean/copy files into the output. Always make the just-built
# native renderer the final DLL in build\voice.
Copy-Item $VocalDll.FullName (Join-Path $VoiceOut 'wds.resone.vocals.dll') -Force

if (!(Test-Path $CliExe)) {
    throw "Build completed but the runnable CLI was not found at '$CliExe'."
}

Write-Host ''
Write-Host "Executable: $CliExe" -ForegroundColor Green
Write-Host "Vocal DLL : $(Join-Path $VoiceOut 'wds.resone.vocals.dll')" -ForegroundColor Green
Write-Host 'Run       : .\build\voice\resone-voice.exe peppy amazing' -ForegroundColor Cyan
if ($NativeOnly -and !$BootstrapCli) {
    Write-Host 'Fast path: only the native vocal DLL was rebuilt; the existing CLI executable was reused.' -ForegroundColor DarkGray
}
