param(
  [string]$LicensePublicKeyFile = '',
  [string]$InstructionKeyFile = '',
  [string]$LicenseApiUrl = '',
  [Parameter(Mandatory=$true)][string]$RuntimeManifest,
  [string]$Version='0.0.1',
  [string]$StageDir='',
  [string]$OutputDir='',
  [string]$InnoCompiler=''
)
$ErrorActionPreference='Stop'
$Root=Split-Path $PSScriptRoot -Parent
if(!(Test-Path $RuntimeManifest -PathType Leaf)){throw "Runtime manifest not found: $RuntimeManifest"}
$Manifest = Get-Content $RuntimeManifest -Raw | ConvertFrom-Json
$LicensingEnabled = $true
$EncryptInstructions = $true
if($Manifest.PSObject.Properties['release']){
  if($Manifest.release.PSObject.Properties['licensingEnabled']){$LicensingEnabled=[bool]$Manifest.release.licensingEnabled}
  if($Manifest.release.PSObject.Properties['encryptInstructions']){$EncryptInstructions=[bool]$Manifest.release.encryptInstructions}
}
if($EncryptInstructions -and !$LicensingEnabled){throw 'release.encryptInstructions=true requires release.licensingEnabled=true.'}
if(!$StageDir){$StageDir=Join-Path $Root 'build/customer-staging'}
$StageDir=[IO.Path]::GetFullPath($StageDir)
if(Test-Path $StageDir){Remove-Item $StageDir -Recurse -Force}
New-Item -ItemType Directory -Force $StageDir|Out-Null
& (Join-Path $PSScriptRoot 'build-windows.ps1') -CustomerRelease -InstallDir $StageDir -LicensePublicKeyFile $LicensePublicKeyFile -InstructionKeyFile $InstructionKeyFile -LicenseApiUrl $LicenseApiUrl -RuntimeManifest $RuntimeManifest
if($LASTEXITCODE -ne 0){throw "Customer release build failed ($LASTEXITCODE)"}
& (Join-Path $PSScriptRoot 'build-installer.ps1') -StageDir $StageDir -Version $Version -OutputDir $OutputDir -InnoCompiler $InnoCompiler -LicensingEnabled $LicensingEnabled
if($LASTEXITCODE -ne 0){throw "Installer build failed ($LASTEXITCODE)"}
