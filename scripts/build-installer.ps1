param(
  [Parameter(Mandatory=$true)][string]$StageDir,
  [string]$Version = '0.0.1',
  [string]$OutputDir = '',
  [string]$InnoCompiler = '',
  [bool]$LicensingEnabled = $true,
  [switch]$LocalInstallerTest
)
$ErrorActionPreference='Stop'
$Root=Split-Path $PSScriptRoot -Parent
$StageDir=[IO.Path]::GetFullPath($StageDir)
if(!(Test-Path (Join-Path $StageDir 'wds.resone.launcher.exe'))){throw "Customer staging directory is incomplete: $StageDir"}
if(!$OutputDir){$OutputDir=Join-Path $Root 'build/installer'}
$OutputDir=[IO.Path]::GetFullPath($OutputDir);New-Item -ItemType Directory -Force $OutputDir|Out-Null
if(!$InnoCompiler){
  $Candidates=@("$env:ProgramFiles\Inno Setup 7\ISCC.exe","$env:ProgramFiles(x86)\Inno Setup 7\ISCC.exe","$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe","$env:ProgramFiles\Inno Setup 6\ISCC.exe")
  $InnoCompiler=$Candidates|Where-Object{Test-Path $_}|Select-Object -First 1
}
if(!$InnoCompiler -or !(Test-Path $InnoCompiler)){throw 'Inno Setup 6 or 7 (ISCC.exe) is required. Install Inno Setup or pass -InnoCompiler.'}
$LicensingDefine = if($LicensingEnabled){1}else{0}
$Defines=@("/DStageDir=$StageDir","/DOutputDir=$OutputDir","/DAppVersion=$Version","/DLicensingEnabled=$LicensingDefine")
if($LocalInstallerTest){$Defines += "/DLocalTestBuild=1"}
& $InnoCompiler @Defines (Join-Path $Root 'installer/Resone.iss')
if($LASTEXITCODE -ne 0){throw "Inno Setup failed ($LASTEXITCODE)"}
Write-Host "Installer built in $OutputDir" -ForegroundColor Green
