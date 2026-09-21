param([string]$InstallDir = "$env:LOCALAPPDATA\Wds\Resone", [string]$SixStarsRuntimeRoot = "", [switch]$CustomerRelease, [switch]$ForceDeploy, [switch]$LocalInstallerTest, [string]$LocalTestLicenseKeyFile = "", [string]$LicensePublicKeyFile = "", [string]$InstructionKeyFile = "", [string]$LicenseApiUrl = "", [string]$RuntimeManifest = "")
$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'clean-instruction-assets.ps1') -Root $Root
$SourceRuntimeManifest = Get-Content (Join-Path $Root 'config/runtime.json') -Raw | ConvertFrom-Json
$SourceSettings = Get-Content (Join-Path $Root 'config/appsettings.json') -Raw | ConvertFrom-Json
function DependencyPin([string]$Name) {
  $Pin = $SourceRuntimeManifest.dependencyPins.$Name
  if (-not $Pin -or [string]::IsNullOrWhiteSpace($Pin.repository) -or [string]::IsNullOrWhiteSpace($Pin.gitRef)) { throw "config/runtime.json is missing dependency pin: $Name" }
  return $Pin
}
$PublishFlags = @()
if ($LocalInstallerTest) {
  if ([string]::IsNullOrWhiteSpace($LocalTestLicenseKeyFile)) { $LocalTestLicenseKeyFile = Join-Path $Root 'installer/local-test-license.txt' }
  if (!(Test-Path $LocalTestLicenseKeyFile)) { throw "Local installer test key file not found: $LocalTestLicenseKeyFile" }
  $PublishFlags += @('-p:LocalInstallerTest=true',"-p:LocalTestLicenseKeyFile=$([System.IO.Path]::GetFullPath($LocalTestLicenseKeyFile))")
}
$RunningLauncher = @(Get-Process -Name 'wds.resone.launcher' -ErrorAction SilentlyContinue)
$DeployInstall = $CustomerRelease -or $ForceDeploy -or $RunningLauncher.Count -eq 0
if (!$DeployInstall) {
  Write-Host 'Resone launcher is running. Building into build\ only and leaving the live installation untouched.' -ForegroundColor Yellow
  Write-Host 'Close the launcher and run the build again to deploy, or pass -ForceDeploy if you intentionally want a live deploy.' -ForegroundColor DarkGray
}
$ReleaseLicensingEnabled = $false
$ReleaseEncryptInstructions = $false
if ($CustomerRelease) {
  if (!(Test-Path $RuntimeManifest)) { throw 'Supply -RuntimeManifest with configured, hashed engine packs and models.' }
  $Manifest = Get-Content $RuntimeManifest -Raw | ConvertFrom-Json

  # Older release manifests implied both protections were enabled. New manifests make
  # licensing and instruction encryption independently explicit.
  $ReleaseLicensingEnabled = $true
  $ReleaseEncryptInstructions = $true
  if ($Manifest.PSObject.Properties['release']) {
    if ($Manifest.release.PSObject.Properties['licensingEnabled']) { $ReleaseLicensingEnabled = [bool]$Manifest.release.licensingEnabled }
    if ($Manifest.release.PSObject.Properties['encryptInstructions']) { $ReleaseEncryptInstructions = [bool]$Manifest.release.encryptInstructions }
  }
  if ($ReleaseEncryptInstructions -and !$ReleaseLicensingEnabled) {
    throw 'release.encryptInstructions=true requires release.licensingEnabled=true because the protected-instruction key is delivered by the licensing service.'
  }

  if ($ReleaseLicensingEnabled) {
    if (!(Test-Path $LicensePublicKeyFile)) { throw 'Licensing is enabled; supply -LicensePublicKeyFile with your Cloudflare signing public JWK.' }
    $PublicKey = Get-Content $LicensePublicKeyFile -Raw | ConvertFrom-Json
    if ($PublicKey.d -or $PublicKey.kty -ne 'EC' -or $PublicKey.crv -ne 'P-256') { throw 'Only a PUBLIC P-256 JWK may be embedded.' }
    if ([string]::IsNullOrWhiteSpace($LicenseApiUrl) -or !$LicenseApiUrl.StartsWith('https://')) { throw 'Licensing is enabled; supply -LicenseApiUrl with the deployed HTTPS licensing endpoint.' }
  }
  if ($ReleaseEncryptInstructions) {
    if (!(Test-Path $InstructionKeyFile)) { throw 'Instruction encryption is enabled; supply -InstructionKeyFile with the same 32-byte base64 key stored by the licensing service.' }
    try { $InstructionKeyBytes = [Convert]::FromBase64String((Get-Content $InstructionKeyFile -Raw).Trim()) } catch { throw 'InstructionKeyFile must contain valid base64.' }
    if ($InstructionKeyBytes.Length -ne 32) { throw 'InstructionKeyFile must decode to exactly 32 bytes.' }
    [Array]::Clear($InstructionKeyBytes,0,$InstructionKeyBytes.Length)
  }
  if ($Manifest.useExistingStack -or $Manifest.provisionAiRuntime -eq $false -or !$Manifest.requireHashes) { throw 'Customer manifest must own/provision its stack and require hashes.' }
  $EngineDownloads = @()
  $HasWindowsLlm = $false
  $ExpectedLlmDirectory = [string]$SourceSettings.llamaEngineDirectories.'win-x64'
  $ExpectedLlmModelPath = [string]$SourceSettings.nativeModelPath
  foreach ($PackEntry in @($Manifest.enginePacks)) {
    $IsCompact = $null -ne $PackEntry.cpu -or $null -ne $PackEntry.cuda -or $null -ne $PackEntry.vulkan
    if ($IsCompact) {
      foreach ($Backend in @('cpu','cuda','vulkan')) {
        $BackendPack = $PackEntry.$Backend
        if ($null -eq $BackendPack) { continue }
        foreach ($Component in @('llm','tts','asr')) {
          $Item = $BackendPack.$Component
          if ($null -eq $Item -or $Item.enabled -eq $false) { continue }
          $EngineDownloads += $Item
          if ($Component -eq 'llm') {
            $HasWindowsLlm = $true
            if ([string]::IsNullOrWhiteSpace($Item.directory)) { throw "Enabled $Backend LLM pack must declare directory explicitly." }
            if ($Item.directory.Replace('\','/') -ne $ExpectedLlmDirectory.Replace('\','/')) { throw "Enabled $Backend LLM pack directory '$($Item.directory)' does not match appsettings llamaEngineDirectories win-x64 '$ExpectedLlmDirectory'." }
          }
        }
      }
    } elseif ($PackEntry.enabled -ne $false) {
      $EngineDownloads += $PackEntry
      if ($PackEntry.rid -eq 'win-x64' -and $PackEntry.component -eq 'llm') { $HasWindowsLlm = $true }
    }
  }
  foreach ($Item in $EngineDownloads) {
    if ($Item.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or [string]::IsNullOrWhiteSpace($Item.url) -or !$Item.url.StartsWith('https://')) { throw 'Every enabled AI engine download needs HTTPS and its actual SHA256.' }
    foreach ($Extra in @($Item.additionalArchives)) {
      if ($null -eq $Extra) { continue }
      if ($Extra.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or [string]::IsNullOrWhiteSpace($Extra.url) -or !$Extra.url.StartsWith('https://')) { throw 'Every enabled AI engine companion archive needs HTTPS and its actual SHA256.' }
    }
  }
  $HasConfiguredLlmModel = $false
  foreach ($Item in @($Manifest.models | Where-Object { $_.enabled -ne $false })) {
    if ([string]::IsNullOrWhiteSpace($Item.id) -or [string]::IsNullOrWhiteSpace($Item.version)) { throw 'Every enabled AI model needs id and version.' }
    if ($Item.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or [string]::IsNullOrWhiteSpace($Item.url) -or !$Item.url.StartsWith('https://')) { throw 'Every enabled AI model download needs HTTPS and its actual SHA256.' }
    if ($Item.path.Replace('\','/') -eq $ExpectedLlmModelPath.Replace('\','/')) { $HasConfiguredLlmModel = $true }
  }
  if (!$HasWindowsLlm) { throw 'Supply at least one enabled Windows x64 LLM engine pack.' }
  if (!$HasConfiguredLlmModel) { throw "Enable the configured LLM model '$ExpectedLlmModelPath' in the release manifest." }
  if (Test-Path $InstallDir) { if (Get-ChildItem $InstallDir -Force) { throw 'Use an empty staging directory for CustomerRelease so old instructions/logs cannot ship.' } }
  if ($SixStarsRuntimeRoot) { throw 'Customer releases use downloadable engine packs; do not bundle Six Stars runtime folders.' }
  $PublishFlags += @('-p:CustomerRelease=true',"-p:ResoneLicensingEnabled=$($ReleaseLicensingEnabled.ToString().ToLowerInvariant())","-p:ResoneEncryptInstructions=$($ReleaseEncryptInstructions.ToString().ToLowerInvariant())")
  if ($ReleaseLicensingEnabled) { $PublishFlags += "-p:LicensePublicKeyFile=$([System.IO.Path]::GetFullPath($LicensePublicKeyFile))" }
  if ($ReleaseEncryptInstructions) { $PublishFlags += "-p:InstructionKeyFile=$([System.IO.Path]::GetFullPath($InstructionKeyFile))" }
  foreach ($Dir in @('build/api','build/launcher','build/updater')) { if (Test-Path "$Root/$Dir") { Remove-Item "$Root/$Dir" -Recurse -Force } }
}
function Run([string]$Exe, [string[]]$Arguments) { & $Exe @Arguments; if ($LASTEXITCODE -ne 0) { throw "$Exe failed ($LASTEXITCODE)" } }
function Get-LauncherSourceFingerprint {
  $Files = @()
  foreach ($Dir in @("$Root/src/wds.resone.launcher", "$Root/src/wds.resone.api")) {
    if (Test-Path $Dir) {
      $Files += Get-ChildItem $Dir -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.props','.targets')
      }
    }
  }
  foreach ($Path in @("$Root/Directory.Build.props", "$Root/global.json", "$Root/src/wds.resone.ui/resources/Resone.ico")) {
    if (Test-Path $Path -PathType Leaf) { $Files += Get-Item $Path }
  }
  $Records = foreach ($File in ($Files | Sort-Object FullName -Unique)) {
    $Relative = $File.FullName.Substring($Root.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    $Hash = (Get-FileHash $File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$Relative=$Hash"
  }
  $Bytes = [System.Text.Encoding]::UTF8.GetBytes(($Records -join "`n"))
  $Sha = [System.Security.Cryptography.SHA256]::Create()
  try { return -join ($Sha.ComputeHash($Bytes) | ForEach-Object { $_.ToString('x2') }) }
  finally { $Sha.Dispose() }
}
function Get-BridgeSourceFingerprint {
  $Files = @()
  foreach ($Dir in @("$Root/native/inference", "$Root/native/vendor")) {
    if (Test-Path $Dir) {
      $Files += Get-ChildItem $Dir -Recurse -File | Where-Object {
        $_.Extension -in @('.c','.cc','.cpp','.cxx','.h','.hpp','.inl')
      }
    }
  }
  foreach ($Path in @("$Root/src/wds.resone.ui/CMakeLists.txt", "$Root/native/inference/CMakeLists.txt")) {
    if (Test-Path $Path -PathType Leaf) { $Files += Get-Item $Path }
  }
  $Records = foreach ($File in ($Files | Sort-Object FullName -Unique)) {
    $Relative = $File.FullName.Substring($Root.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    $Hash = (Get-FileHash $File.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$Relative=$Hash"
  }
  $Bytes = [System.Text.Encoding]::UTF8.GetBytes(($Records -join "`n"))
  $Sha = [System.Security.Cryptography.SHA256]::Create()
  try { return -join ($Sha.ComputeHash($Bytes) | ForEach-Object { $_.ToString('x2') }) }
  finally { $Sha.Dispose() }
}

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
$IPlug2Pin = DependencyPin 'iPlug2'
$Vst3Pin = DependencyPin 'vst3sdk'
$VcpkgPin = DependencyPin 'vcpkg'
Checkout $IPlug2Pin.repository $IPlug2Pin.gitRef "$Root/third_party/iPlug2"
Checkout $Vst3Pin.repository $Vst3Pin.gitRef "$Root/third_party/iPlug2/Dependencies/IPlug/VST3_SDK"
Run git @('-C',"$Root/third_party/iPlug2/Dependencies/IPlug/VST3_SDK",'submodule','update','--init','base','pluginterfaces','public.sdk','cmake')
Checkout $VcpkgPin.repository $VcpkgPin.gitRef "$Root/third_party/vcpkg"
Run "$Root/third_party/vcpkg/bootstrap-vcpkg.bat" @('-disableMetrics')
Run "$Root/third_party/vcpkg/vcpkg.exe" @('install','fluidsynth:x64-windows','--classic')
Run dotnet (@('publish',"$Root/src/wds.resone.api/wds.resone.api.csproj",'-c','Release','-r','win-x64','--self-contained','true','-o',"$Root/build/api") + $PublishFlags)

$LauncherOutputDir = "$Root/build/launcher"
$LauncherPrimaryDir = $LauncherOutputDir
$LauncherExe = Join-Path $LauncherPrimaryDir 'wds.resone.launcher.exe'
$LauncherFingerprint = Get-LauncherSourceFingerprint
$LauncherWorkerDir = Join-Path "$Root/build/launcher-workers" $LauncherFingerprint.Substring(0,16)
$LauncherTargetLocked = $false
$LauncherTargetFullPath = [System.IO.Path]::GetFullPath($LauncherExe)
foreach ($Process in $RunningLauncher) {
  try {
    if ($Process.Path -and [System.IO.Path]::GetFullPath($Process.Path) -eq $LauncherTargetFullPath) { $LauncherTargetLocked = $true; break }
  }
  catch { }
}

$SkipLauncherPublish = $false
if ($RunningLauncher.Count -gt 0 -and !$CustomerRelease -and !$LocalInstallerTest) {
  $LauncherCandidates = @($LauncherPrimaryDir)
  if ($LauncherTargetLocked) { $LauncherCandidates += $LauncherWorkerDir }
  foreach ($Candidate in $LauncherCandidates) {
    $CandidateExe = Join-Path $Candidate 'wds.resone.launcher.exe'
    $CandidateFingerprint = Join-Path $Candidate '.source-fingerprint'
    if ((Test-Path $CandidateExe) -and (Test-Path $CandidateFingerprint)) {
      $PreviousLauncherFingerprint = (Get-Content $CandidateFingerprint -Raw).Trim()
      if ($PreviousLauncherFingerprint -eq $LauncherFingerprint) {
        $LauncherOutputDir = $Candidate
        $SkipLauncherPublish = $true
        break
      }
    }
  }
}

if ($SkipLauncherPublish) {
  Write-Host "The current launcher/API build already exists; skipping launcher publish ($LauncherOutputDir)." -ForegroundColor Cyan
}
else {
  if ($LauncherTargetLocked) {
    $LauncherOutputDir = $LauncherWorkerDir
    New-Item -ItemType Directory -Force $LauncherOutputDir | Out-Null
    Write-Host "The tray launcher is staying resident. Publishing the changed API/worker to $LauncherOutputDir." -ForegroundColor Yellow
  }
  Run dotnet (@('publish',"$Root/src/wds.resone.launcher/wds.resone.launcher.csproj",'-c','Release','-r','win-x64','--self-contained','true','-o',$LauncherOutputDir) + $PublishFlags)
  Set-Content (Join-Path $LauncherOutputDir '.source-fingerprint') $LauncherFingerprint -Encoding ASCII
}
Run dotnet (@('publish',"$Root/src/wds.resone.updater/wds.resone.updater.csproj",'-c','Release','-r','win-x64','--self-contained','true','-o',"$Root/build/updater") + $PublishFlags)
# Clean shell-decoration leftovers from older builds before CMake/iPlug2 deploys.
# This also repairs an existing installation once, so no manual deletion is needed.
$AutoVst3Bundle = Join-Path $env:LOCALAPPDATA 'Programs\Common\VST3\Resone.vst3'
Remove-LegacyVst3ShellIconArtifacts "$Root/build/ui/out/Resone.vst3"
if ($DeployInstall) { Remove-LegacyVst3ShellIconArtifacts (Join-Path $InstallDir 'Resone.vst3') }
Remove-LegacyVst3ShellIconArtifacts $AutoVst3Bundle

$BridgeFingerprint = Get-BridgeSourceFingerprint
$BridgeBuildId = $BridgeFingerprint.Substring(0, 16)
$BridgeOutputName = "resone_llama_bridge_$BridgeBuildId.dll"
$BridgeDll = "$Root/build/ui/out/Release/$BridgeOutputName"
Run cmake @('-S',"$Root/src/wds.resone.ui",'-B',"$Root/build/ui",'-G','Visual Studio 17 2022','-A','x64',"-DRESONE_LLAMA_BRIDGE_BUILD_ID=$BridgeBuildId")
# The llama bridge is loaded in-process and therefore locked by Windows while the
# AI stack is running. Build it under a source-hashed immutable filename instead
# of cleaning/overwriting the loaded DLL. A changed source hash creates a new DLL;
# an unchanged hash reuses the existing build without touching it.
if (Test-Path $BridgeDll) {
  Write-Host "Llama bridge source is unchanged; reusing $BridgeOutputName." -ForegroundColor Cyan
}
else {
  Write-Host "Building immutable llama bridge $BridgeOutputName..."
  Run cmake @('--build',"$Root/build/ui",'--config','Release','--target','resone_llama_bridge','--parallel')
}
if (!(Test-Path $BridgeDll)) { throw "Expected llama bridge was not produced: $BridgeDll" }
Set-Content "$Root/build/ui/bridge-current.txt" $BridgeOutputName -Encoding ASCII
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
Copy-Item $VocalDll "$LauncherOutputDir/wds.resone.vocals.dll" -Force
Copy-Item $VocalDll "$Root/build/api/wds.resone.vocals.dll" -Force

if (!$DeployInstall) {
  if ($LauncherTargetLocked) {
    $ReloadWorkerExe = Join-Path $LauncherOutputDir 'wds.resone.launcher.exe'
    Write-Host "Reloading the API/AI worker from $ReloadWorkerExe while leaving the tray launcher running..." -ForegroundColor Cyan
    Run $ReloadWorkerExe @('--reload-worker',$ReloadWorkerExe)
  }
  Write-Host "Build complete. Live tray launcher stayed running; the API/AI worker was refreshed from the current build." -ForegroundColor Green
  Write-Host "Build outputs: $LauncherOutputDir, $Root/build/updater, $Root/build/api, $Root/build/ui" -ForegroundColor Cyan
  exit 0
}

# Do not add desktop.ini/Plugin.ico shell decoration to the .vst3 directory.
# The icon that matters is embedded in the VST3 module and assigned to the native
# editor window; decorating the bundle folder caused repeat-build AccessDenied errors.
Copy-Item "$Root/build/api/wds.resone.api.dll" $InstallDir -Force
Copy-Item "$LauncherOutputDir/wds.resone.launcher.exe" $InstallDir -Force
Copy-Item "$Root/build/updater/wds.resone.updater.exe" $InstallDir -Force
Copy-Item "$Root/src/wds.resone.ui/resources/Resone.ico" $InstallDir -Force
Copy-Item "$Root/build/ui/out/Resone.exe" "$InstallDir/wds.resone.ui.exe" -Force
Copy-Item $BridgeDll "$InstallDir/resone_llama_bridge.dll" -Force
Copy-Item $VocalDll $InstallDir -Force
# Keep FluidSynth and its dynamically-linked runtime dependencies isolated from the
# Resone application root. This also keeps the LGPL component visibly replaceable.
$FluidRuntimeDir = Join-Path $InstallDir 'third_party/libfluidsynth'
New-Item -ItemType Directory -Force $FluidRuntimeDir | Out-Null
Copy-Item "$Root/third_party/vcpkg/installed/x64-windows/bin/*.dll" $FluidRuntimeDir -Force
# FluidSynth uses different DLL basenames across distributions. Keep our ABI loader's name stable.
$Fluid = Get-ChildItem "$FluidRuntimeDir/*fluidsynth*.dll" | Select-Object -First 1
if (!$Fluid) { throw 'FluidSynth runtime DLL missing' }
if ($Fluid.Name -ne 'libfluidsynth-3.dll') { Copy-Item $Fluid.FullName "$FluidRuntimeDir/libfluidsynth-3.dll" -Force }
# Copy directory contents explicitly so repeated installs cannot nest assets/assets.
New-Item -ItemType Directory -Force "$InstallDir/assets" | Out-Null
Copy-Item "$LauncherOutputDir/assets/*" "$InstallDir/assets" -Recurse -Force
# Reused staging directories can retain obsolete instruction files from older builds.
& (Join-Path $PSScriptRoot 'clean-instruction-assets.ps1') -Root $InstallDir
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
# Legacy LLM log settings point at the same daily-log directory used by the launcher/updater.
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
$LogDirectory = Join-Path $env:LOCALAPPDATA 'Wds\Logs\Resone'
$Settings | Add-Member -NotePropertyName logLlmRequests -NotePropertyValue $Enabled -Force
$Settings | Add-Member -NotePropertyName llmLogDirectory -NotePropertyValue $LogDirectory -Force
$Settings | ConvertTo-Json -Depth 32 | Set-Content $SettingsPath -Encoding UTF8
$RuntimePath = "$InstallDir/config/runtime.json"
$Runtime = Get-Content $RuntimePath -Raw | ConvertFrom-Json
$SourceRuntime = Get-Content "$Root/config/runtime.json" -Raw | ConvertFrom-Json
# Provisioning configuration is source-controlled for development. Synchronize
# engine/model download settings so editing config/runtime.json cannot leave an
# older AppData installation silently using stale engine-pack URLs.
foreach ($RuntimeField in @('manifestVersion','provisionAiRuntime','useExistingStack','requireHashes','logging','updates','dependencyPins','enginePacks','models','services')) {
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
Copy-Item "$Root/docs/third-party-dependencies.md" "$InstallDir/licenses" -Force
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
  # Never ship raw LLM request/response logging in customer releases.
  $Settings.logLlmRequests = $false
  $Settings | ConvertTo-Json -Depth 32 | Set-Content "$InstallDir/config/appsettings.json" -Encoding UTF8
  $ReleaseLicenseUrl = if ($ReleaseLicensingEnabled) { $LicenseApiUrl } else { '' }
  @{ enabled=$ReleaseLicensingEnabled; url=$ReleaseLicenseUrl } | ConvertTo-Json | Set-Content "$InstallDir/config/licensing.json" -Encoding UTF8
  Remove-Item "$InstallDir/config/runtime.release.example.json" -ErrorAction SilentlyContinue
  $InstructionRoot = "$InstallDir/assets/Instructions/Music"
  if ($ReleaseEncryptInstructions) {
    if (Test-Path "$InstallDir/assets/Instructions") { throw 'Loose instructions detected while release.encryptInstructions=true.' }
  } elseif (!(Test-Path "$InstructionRoot/music-composition.json")) {
    throw 'Plain production instructions were not staged while release.encryptInstructions=false.'
  }
}
