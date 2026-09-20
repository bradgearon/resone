param(
  [string]$Version = '0.1.0',
  [string]$LicenseApiUrl = 'https://licenses.resone.io',
  [string]$RuntimeManifest = '',
  [string]$OutputDir = '',
  [string]$InnoCompiler = ''
)
$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($RuntimeManifest)) { $RuntimeManifest = Join-Path $Root 'config/runtime.release.json' }
if (!(Test-Path $RuntimeManifest -PathType Leaf)) { throw "Required release manifest not found: $RuntimeManifest" }

$Manifest = Get-Content $RuntimeManifest -Raw | ConvertFrom-Json
# Backward compatibility: older release manifests implied both protections were enabled.
$LicensingEnabled = $true
$EncryptInstructions = $true
if ($Manifest.PSObject.Properties['release']) {
  if ($Manifest.release.PSObject.Properties['licensingEnabled']) { $LicensingEnabled = [bool]$Manifest.release.licensingEnabled }
  if ($Manifest.release.PSObject.Properties['encryptInstructions']) { $EncryptInstructions = [bool]$Manifest.release.encryptInstructions }
}
if ($EncryptInstructions -and !$LicensingEnabled) {
  throw 'release.encryptInstructions=true currently requires release.licensingEnabled=true because the instruction key is delivered by the licensing service.'
}

$Secrets = Join-Path $Root 'cloud/licensing/.secrets'
$PublicKey = Join-Path $Secrets 'signing-public.jwk'
$InstructionKey = Join-Path $Secrets 'INSTRUCTION_KEY_BASE64'
if ($LicensingEnabled -and !(Test-Path $PublicKey -PathType Leaf)) { throw "Licensing is enabled but the signing public key was not found: $PublicKey" }
if ($EncryptInstructions -and !(Test-Path $InstructionKey -PathType Leaf)) { throw "Instruction encryption is enabled but the instruction key was not found: $InstructionKey" }

Write-Host "Building Resone customer release $Version" -ForegroundColor Cyan
Write-Host "Runtime manifest: $RuntimeManifest" -ForegroundColor DarkGray
Write-Host "Licensing: $LicensingEnabled" -ForegroundColor DarkGray
Write-Host "Instruction encryption: $EncryptInstructions" -ForegroundColor DarkGray
if ($LicensingEnabled) { Write-Host "License API: $LicenseApiUrl" -ForegroundColor DarkGray }
Write-Host 'Enabled release AI: Gemma LLM model + CPU/CUDA/Vulkan llama.cpp backends. TTS and ASR are disabled until their engine packs are published.' -ForegroundColor DarkGray

$Args = @{
  RuntimeManifest = $RuntimeManifest
  Version = $Version
  LicenseApiUrl = $LicenseApiUrl
}
if ($LicensingEnabled) { $Args.LicensePublicKeyFile = $PublicKey }
if ($EncryptInstructions) { $Args.InstructionKeyFile = $InstructionKey }
if (![string]::IsNullOrWhiteSpace($OutputDir)) { $Args.OutputDir = $OutputDir }
if (![string]::IsNullOrWhiteSpace($InnoCompiler)) { $Args.InnoCompiler = $InnoCompiler }

& (Join-Path $PSScriptRoot 'build-customer-release.ps1') @Args
if ($LASTEXITCODE -ne 0) { throw "Release build failed ($LASTEXITCODE)" }
