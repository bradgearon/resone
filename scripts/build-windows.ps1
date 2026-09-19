param([string]$InstallDir = "$env:LOCALAPPDATA\Wds\Resone", [string]$SixStarsRuntimeRoot = "", [switch]$CustomerRelease, [switch]$ForceDeploy, [string]$LicensePublicKeyFile = "", [string]$LicenseApiUrl = "", [string]$RuntimeManifest = "")
$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
$PublishFlags = @()
$RunningLauncher = @(Get-Process -Name 'wds.resone.launcher' -ErrorAction SilentlyContinue)
$DeployInstall = $CustomerRelease -or $ForceDeploy -or $RunningLauncher.Count -eq 0
if (!$DeployInstall) {
  Write-Host 'Resone launcher is running. Building into build\ only and leaving the live installation untouched.' -ForegroundColor Yellow
  Write-Host 'Close the launcher and run the build again to deploy, or pass -ForceDeploy if you intentionally want a live deploy.' -ForegroundColor DarkGray
}
if ($CustomerRelease) {
  if (!(Test-Path $LicensePublicKeyFile)) { throw 'Supply -LicensePublicKeyFile with your Cloudflare signing public JWK.' }
  $PublicKey = Get-Content $LicensePublicKeyFile -Raw | ConvertFrom-Json
  if ($PublicKey.d -or $PublicKey.kty -ne 'EC' -or $PublicKey.crv -ne 'P-256') { throw 'Only a PUBLIC P-256 JWK may be embedded.' }
  if (!$LicenseApiUrl.StartsWith('https://')) { throw 'Supply -LicenseApiUrl with the deployed HTTPS licensing endpoint.' }
  if (!(Test-Path $RuntimeManifest)) { throw 'Supply -RuntimeManifest with configured, hashed engine packs and models.' }
  $Manifest = Get-Content $RuntimeManifest -Raw | ConvertFrom-Json
  if ($Manifest.useExistingStack -or !$Manifest.requireHashes) { throw 'Customer manifest must own its stack and require hashes.' }
  foreach ($Item in @($Manifest.enginePacks) + @($Manifest.models | Where-Object enabled)) {
    if ($Item.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or !$Item.url.StartsWith('https://')) { throw 'Every download needs HTTPS and its actual SHA256.' }
  }
  if (!($Manifest.enginePacks | Where-Object { $_.enabled -ne $false -and $_.rid -eq 'win-x64' -and $_.directory -eq 'engines/llm/llama-cpp-dynamic-win-x64' })) { throw 'Supply a Windows x64 prebuilt llama.cpp pack targeting engines/llm/llama-cpp-dynamic-win-x64.' }
  if (Test-Path $InstallDir) { if (Get-ChildItem $InstallDir -Force) { throw 'Use an empty staging directory for CustomerRelease so old instructions/logs cannot ship.' } }
  if ($SixStarsRuntimeRoot) { throw 'Customer releases use downloadable engine packs; do not bundle Six Stars runtime folders.' }
  $PublishFlags = @('-p:CustomerRelease=true',"-p:LicensePublicKeyFile=$([System.IO.Path]::GetFullPath($LicensePublicKeyFile))")
  foreach ($Dir in @('build/api','build/launcher')) { if (Test-Path "$Root/$Dir") { Remove-Item "$Root/$Dir" -Recurse -Force } }
}
function Run([string]$Exe, [string[]]$Arguments) { & $Exe @Arguments; if ($LASTEXITCODE -ne 0) { throw "$Exe failed ($LASTEXITCODE)" } }
function Test-WindowsIconResource([string]$BinaryPath, [int]$ResourceId = 40003) {
  if (!(Test-Path $BinaryPath -PathType Leaf)) { return $false }
  if (-not ('Resone.NativeResourceCheckV2' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
namespace Resone {
  public static class NativeResourceCheckV2 {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, uint dwFlags);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr FindResourceW(IntPtr hModule, IntPtr lpName, IntPtr lpType);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeLibrary(IntPtr hModule);
  }
}
'@
  }
  # LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE
  $module = [Resone.NativeResourceCheckV2]::LoadLibraryExW($BinaryPath, [IntPtr]::Zero, 0x22)
  if ($module -eq [IntPtr]::Zero) { return $false }
  try {
    # RT_GROUP_ICON = 14. IDI_ICON1 in resources/resource.h is 40003.
    return [Resone.NativeResourceCheckV2]::FindResourceW($module, [IntPtr]$ResourceId, [IntPtr]14) -ne [IntPtr]::Zero
  } finally {
    [void][Resone.NativeResourceCheckV2]::FreeLibrary($module)
  }
}

function Remove-LegacyVst3ShellIconArtifacts([string]$BundlePath) {
  if (!(Test-Path -LiteralPath $BundlePath -PathType Container)) { return }

  # Older Resone builds decorated the .vst3 directory with desktop.ini and
  # marked the directory ReadOnly plus desktop.ini/Plugin.ico Hidden+System.
  # Those attributes make a later Copy-Item deployment fail with AccessDenied.
  # The shell decoration is not needed for the embedded VST icon or the native
  # editor/taskbar icon, so clean it up permanently before every deployment.
  $Protected = [IO.FileAttributes]::ReadOnly -bor [IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::System
  $Bundle = Get-Item -LiteralPath $BundlePath -Force
  $Bundle.Attributes = $Bundle.Attributes -band (-bnot $Protected)

  foreach ($Name in @('desktop.ini','Plugin.ico')) {
    $Path = Join-Path $BundlePath $Name
    if (Test-Path -LiteralPath $Path) {
      $Item = Get-Item -LiteralPath $Path -Force
      $Item.Attributes = $Item.Attributes -band (-bnot $Protected)
      Remove-Item -LiteralPath $Path -Force
    }
  }
}

function Checkout([string]$Url, [string]$Commit, [string]$Path) {
  if (!(Test-Path "$Path/.git")) {
    if (Test-Path $Path) {
      # iPlug2 ships a README-only SDK placeholder. Preserve it before cloning.
      if ((Get-ChildItem $Path -Force | Where-Object { $_.Name -ne 'README.md' }).Count -ne 0) { throw "Refusing to replace nonempty dependency directory: $Path" }
      Move-Item $Path "$Path.placeholder" -Force
    }
    New-Item -ItemType Directory -Force $Path | Out-Null
    Run git @('-C',$Path,'init')
    Run git @('-C',$Path,'remote','add','origin',$Url)
    Run git @('-C',$Path,'fetch','--depth','1','origin',$Commit)
  }
  Run git @('-C',$Path,'checkout','--detach',$Commit)
}
New-Item -ItemType Directory -Force "$Root/third_party" | Out-Null
if ($DeployInstall) { New-Item -ItemType Directory -Force $InstallDir | Out-Null }
# Never mutate the running installation during a build-only pass.
if ($DeployInstall) { Remove-Item "$InstallDir/resone_inference.dll" -Force -ErrorAction SilentlyContinue }
Checkout 'https://github.com/iPlug2/iPlug2.git' 'd54f69050f517e43b941d88c2a170f0a840b9ee4' "$Root/third_party/iPlug2"
Checkout 'https://github.com/steinbergmedia/vst3sdk.git' '9fad9770f2ae8542ab1a548a68c1ad1ac690abe0' "$Root/third_party/iPlug2/Dependencies/IPlug/VST3_SDK"
Run git @('-C',"$Root/third_party/iPlug2/Dependencies/IPlug/VST3_SDK",'submodule','update','--init','base','pluginterfaces','public.sdk','cmake')
Checkout 'https://github.com/microsoft/vcpkg.git' '1577f17ee57f42a0ef6d75bbb82cb37d0b76d7e8' "$Root/third_party/vcpkg"
Run "$Root/third_party/vcpkg/bootstrap-vcpkg.bat" @('-disableMetrics')
Run "$Root/third_party/vcpkg/vcpkg.exe" @('install','fluidsynth:x64-windows','--classic')
Run dotnet (@('publish',"$Root/src/wds.resone.api/wds.resone.api.csproj",'-c','Release','-r','win-x64','--self-contained','true','-o',"$Root/build/api") + $PublishFlags)
Run dotnet (@('publish',"$Root/src/wds.resone.launcher/wds.resone.launcher.csproj",'-c','Release','-r','win-x64','--self-contained','true','-o',"$Root/build/launcher") + $PublishFlags)
# Clean shell-decoration leftovers from older builds before CMake/iPlug2 deploys.
# This also repairs an existing installation once, so no manual deletion is needed.
$AutoVst3Bundle = Join-Path $env:LOCALAPPDATA 'Programs\Common\VST3\Resone.vst3'
Remove-LegacyVst3ShellIconArtifacts "$Root/build/ui/out/Resone.vst3"
if ($DeployInstall) { Remove-LegacyVst3ShellIconArtifacts (Join-Path $InstallDir 'Resone.vst3') }
Remove-LegacyVst3ShellIconArtifacts $AutoVst3Bundle

Run cmake @('-S',"$Root/src/wds.resone.ui",'-B',"$Root/build/ui",'-G','Visual Studio 17 2022','-A','x64')
# Source archives are commonly extracted over an existing development tree. The
# extracted source timestamps can be older than an already-built .obj/.dll, which
# previously allowed an obsolete resone_llama_bridge.dll to survive a rebuild.
# Clean the native build first, then build the bridge explicitly before the app.
Write-Host 'Cleaning native UI build to prevent stale native bridge/object files...'
Run cmake @('--build',"$Root/build/ui",'--config','Release','--target','resone_llama_bridge','--clean-first','--parallel')
Run cmake @('--build',"$Root/build/ui",'--config','Release','--target','Resone-app','Resone-vst3','wds_resone_vocals','--parallel')

$StandaloneBinary = "$Root/build/ui/out/Resone.exe"
$Vst3Module = Get-ChildItem "$Root/build/ui/out/Resone.vst3" -Recurse -File -Filter 'Resone.vst3' -ErrorAction SilentlyContinue | Select-Object -First 1
if (!(Test-WindowsIconResource $StandaloneBinary)) {
  throw 'Standalone Resone.exe is missing IDI_ICON1 (resource 40003).'
}
if (!$Vst3Module -or !(Test-WindowsIconResource $Vst3Module.FullName)) {
  throw 'Resone VST3 module is missing IDI_ICON1 (resource 40003). resources/main.rc must be compiled into Resone-vst3.'
}
Write-Host 'Verified Resone icon resource in standalone and VST3 binaries.'

# Keep build outputs complete even when the live launcher remains running.
$VocalDll = "$Root/build/ui/out/Release/wds.resone.vocals.dll"
if (!(Test-Path $VocalDll)) { $VocalDll = "$Root/build/ui/out/wds.resone.vocals.dll" }
if (!(Test-Path $VocalDll)) { throw 'wds.resone.vocals.dll was not produced by the native build.' }
Copy-Item $VocalDll "$Root/build/launcher/wds.resone.vocals.dll" -Force
Copy-Item $VocalDll "$Root/build/api/wds.resone.vocals.dll" -Force

if (!$DeployInstall) {
  Write-Host "Build complete. Live launcher was left running; install directory was not modified." -ForegroundColor Green
  Write-Host "Build outputs: $Root/build/launcher, $Root/build/api, $Root/build/ui" -ForegroundColor Cyan
  exit 0
}

# Do not add desktop.ini/Plugin.ico shell decoration to the .vst3 directory.
# The icon that matters is embedded in the VST3 module and assigned to the native
# editor window; decorating the bundle folder caused repeat-build AccessDenied errors.
Copy-Item "$Root/build/api/wds.resone.api.dll" $InstallDir -Force
Copy-Item "$Root/build/launcher/wds.resone.launcher.exe" $InstallDir -Force
Copy-Item "$Root/src/wds.resone.ui/resources/Resone.ico" $InstallDir -Force
Copy-Item "$Root/build/ui/out/Resone.exe" "$InstallDir/wds.resone.ui.exe" -Force
Copy-Item "$Root/build/ui/out/Release/resone_llama_bridge.dll" $InstallDir -Force
Copy-Item $VocalDll $InstallDir -Force
Copy-Item "$Root/third_party/vcpkg/installed/x64-windows/bin/*.dll" $InstallDir -Force
# FluidSynth uses different DLL basenames across distributions. Keep our ABI loader's name stable.
$Fluid = Get-ChildItem "$InstallDir/*fluidsynth*.dll" | Select-Object -First 1
if (!$Fluid) { throw 'FluidSynth runtime DLL missing' }
if ($Fluid.Name -ne 'libfluidsynth-3.dll') { Copy-Item $Fluid.FullName "$InstallDir/libfluidsynth-3.dll" -Force }
# Copy directory contents explicitly so repeated installs cannot nest assets/assets.
New-Item -ItemType Directory -Force "$InstallDir/assets" | Out-Null
Copy-Item "$Root/build/launcher/assets/*" "$InstallDir/assets" -Recurse -Force
if (!$CustomerRelease -and !(Test-Path "$InstallDir/assets/Instructions/Music/music-composition.json")) {
  throw 'Music instructions were not staged into the installation directory'
}
New-Item -ItemType Directory -Force "$InstallDir/web", "$InstallDir/config", "$InstallDir/licenses" | Out-Null
Copy-Item "$Root/src/wds.resone.ui/resources/web/*" "$InstallDir/web" -Force
foreach ($File in Get-ChildItem "$Root/config/*.json") { if (!(Test-Path "$InstallDir/config/$($File.Name)")) { Copy-Item $File.FullName "$InstallDir/config" } }
# Migrate the old Resone defaults while preserving custom service addresses.
$SettingsPath = "$InstallDir/config/appsettings.json"
$Settings = Get-Content $SettingsPath -Raw | ConvertFrom-Json
if ($Settings.sttUrl -in @('http://127.0.0.1:8100/inference','http://localhost:8100/inference')) {
  $Settings.sttUrl = 'http://127.0.0.1:8000/inference'
  $Settings | ConvertTo-Json -Depth 32 | Set-Content $SettingsPath -Encoding UTF8
}
# Keep diagnostic logs at the source solution even when the app is installed elsewhere.
$SourceSettings = Get-Content "$Root/config/appsettings.json" -Raw | ConvertFrom-Json
# Local inference mode belongs to the source build configuration. Older installers
# preserved appsettings.json wholesale, which meant changing nativeInference in the
# source could leave the installed launcher silently using llmUrl/port 8080.
$UseLocalInference = $false
if ($SourceSettings.PSObject.Properties['nativeInference']) { $UseLocalInference = $UseLocalInference -or [bool]$SourceSettings.nativeInference }
if ($SourceSettings.PSObject.Properties['useLocalInference']) { $UseLocalInference = $UseLocalInference -or [bool]$SourceSettings.useLocalInference }
$Settings | Add-Member -NotePropertyName nativeInference -NotePropertyValue $UseLocalInference -Force
$Settings | Add-Member -NotePropertyName useLocalInference -NotePropertyValue $UseLocalInference -Force
foreach ($NativeField in @('llamaEngineDirectories','nativeModelPath','contextTokens','gpuLayers','allowCpuFallback','flashAttention','reasoningEnabled','warmModelOnStackStart','temperature','topK','topP','useLocalWhisper','whisperEngineDirectories','whisperServerName','whisperModelPath','whisperLanguage','qwenTtsEngineDirectories','qwenTtsServerName','qwenTtsStartupTimeoutSeconds','serviceDelays','qwenTtsTalkerPath','qwenTtsVoiceDesignTalkerPath','qwenTtsCodecPath','qwenTtsLanguage','qwenTtsFlashAttention','qwenTtsClampFp16','qwenTtsMaxBatch','qwenTtsCodecChunkSeconds')) {
  if ($SourceSettings.PSObject.Properties[$NativeField]) {
    $Settings | Add-Member -NotePropertyName $NativeField -NotePropertyValue $SourceSettings.PSObject.Properties[$NativeField].Value -Force
  }
}
$Enabled = $true
if ($SourceSettings.PSObject.Properties['logLlmRequests']) { $Enabled = [bool]$SourceSettings.logLlmRequests }
$LogDirectory = "$Root/logs"
if ($SourceSettings.llmLogDirectory) {
  if ([System.IO.Path]::IsPathRooted($SourceSettings.llmLogDirectory)) {
    $LogDirectory = $SourceSettings.llmLogDirectory
  } else {
    $LogDirectory = [System.IO.Path]::GetFullPath((Join-Path $Root $SourceSettings.llmLogDirectory))
  }
}
$Settings | Add-Member -NotePropertyName logLlmRequests -NotePropertyValue $Enabled -Force
$Settings | Add-Member -NotePropertyName llmLogDirectory -NotePropertyValue $LogDirectory -Force
$Settings | ConvertTo-Json -Depth 32 | Set-Content $SettingsPath -Encoding UTF8
$RuntimePath = "$InstallDir/config/runtime.json"
$Runtime = Get-Content $RuntimePath -Raw | ConvertFrom-Json
$SourceRuntime = Get-Content "$Root/config/runtime.json" -Raw | ConvertFrom-Json
# Provisioning configuration is source-controlled for development. Synchronize
# engine/model download settings so editing config/runtime.json cannot leave an
# older AppData installation silently using stale engine-pack URLs.
foreach ($RuntimeField in @('useExistingStack','backend','requireHashes','enginePacks','models','services')) {
  if ($SourceRuntime.PSObject.Properties[$RuntimeField]) {
    $Runtime | Add-Member -NotePropertyName $RuntimeField -NotePropertyValue $SourceRuntime.PSObject.Properties[$RuntimeField].Value -Force
  }
}
foreach ($Service in $Runtime.services) {
  if ($Service.name -eq 'Whisper') {
    for ($i = 1; $i -lt $Service.arguments.Count; $i++) {
      if ($Service.arguments[$i - 1] -eq '--port' -and $Service.arguments[$i] -eq '8100') {
        $Service.arguments[$i] = '8000'
      }
    }
  }
}
$Runtime | ConvertTo-Json -Depth 32 | Set-Content $RuntimePath -Encoding UTF8
Copy-Item "$Root/docs/THIRD-PARTY.md" "$InstallDir/licenses" -Force
Get-ChildItem "$Root/third_party/vcpkg/installed/x64-windows/share" -Filter copyright -Recurse | ForEach-Object { Copy-Item $_.FullName "$InstallDir/licenses/$($_.Directory.Name)-copyright.txt" -Force }
Copy-Item "$Root/third_party/iPlug2/LICENSE.txt" "$InstallDir/licenses/iPlug2.txt" -Force
Copy-Item "$Root/third_party/iPlug2/Dependencies/IPlug/VST3_SDK/LICENSE.txt" "$InstallDir/licenses/VST3.txt" -Force
# Replace stale legacy shell artifacts one more time immediately before copying in
# case this install directory was touched while the native build was running.
Remove-LegacyVst3ShellIconArtifacts (Join-Path $InstallDir 'Resone.vst3')
Copy-Item "$Root/build/ui/out/Resone.vst3" $InstallDir -Recurse -Force
# Optional: reuse the exact, already-working native runtimes and models from Six Stars.
if ($SixStarsRuntimeRoot) {
  foreach ($Folder in @('engines','models')) { if (Test-Path "$SixStarsRuntimeRoot/$Folder") { Copy-Item "$SixStarsRuntimeRoot/$Folder" $InstallDir -Recurse -Force } }
}
Write-Host "Built Resone at $InstallDir"
Write-Host "Run wds.resone.launcher.exe or the standalone app. Opening the VST editor starts the shared launcher automatically."

if ($CustomerRelease) {
  Copy-Item $RuntimeManifest "$InstallDir/config/runtime.json" -Force
  $Settings.nativeInference = $true
  $Settings | Add-Member -NotePropertyName useLocalInference -NotePropertyValue $true -Force
  $Settings.logLlmRequests = $false
  $Settings | ConvertTo-Json -Depth 32 | Set-Content "$InstallDir/config/appsettings.json" -Encoding UTF8
  @{ enabled=$true; url=$LicenseApiUrl } | ConvertTo-Json | Set-Content "$InstallDir/config/licensing.json" -Encoding UTF8
  Remove-Item "$InstallDir/config/runtime.release.example.json" -ErrorAction SilentlyContinue
  if (Test-Path "$InstallDir/assets/Instructions") { throw 'Loose instructions detected in release staging.' }
}
