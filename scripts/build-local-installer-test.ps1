param(
  [string]$Version = '0.0.1-test',
  [string]$StageDir = '',
  [string]$OutputDir = '',
  [string]$InnoCompiler = ''
)
$ErrorActionPreference='Stop'
$Root=Split-Path $PSScriptRoot -Parent
if(!$StageDir){$StageDir=Join-Path $Root 'build/local-installer-test-staging'}
if(!$OutputDir){$OutputDir=Join-Path $Root 'build/installer'}
$KeyFile=Join-Path $Root 'installer/local-test-license.txt'
if(!(Test-Path $KeyFile)){throw "Missing local installer test key: $KeyFile"}
if(Test-Path $StageDir){Remove-Item $StageDir -Recurse -Force}
New-Item -ItemType Directory -Force $StageDir|Out-Null

& (Join-Path $PSScriptRoot 'build-windows.ps1') -InstallDir $StageDir -ForceDeploy -LocalInstallerTest -LocalTestLicenseKeyFile $KeyFile
if($LASTEXITCODE -ne 0){throw "Local installer test build failed ($LASTEXITCODE)"}
& (Join-Path $PSScriptRoot 'build-installer.ps1') -StageDir $StageDir -Version $Version -OutputDir $OutputDir -InnoCompiler $InnoCompiler -LocalInstallerTest
if($LASTEXITCODE -ne 0){throw "Local installer packaging failed ($LASTEXITCODE)"}
Write-Host "Local installer test key: $((Get-Content $KeyFile -Raw).Trim())" -ForegroundColor Cyan
