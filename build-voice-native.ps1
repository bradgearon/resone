param([switch]$Clean)

$ErrorActionPreference = 'Stop'
$BuildVoice = Join-Path $PSScriptRoot 'build-voice.ps1'

if ($Clean) {
    & $BuildVoice -NativeOnly -Clean
} else {
    & $BuildVoice -NativeOnly
}

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$Exe = Join-Path $PSScriptRoot 'build\voice\resone-voice.exe'
if (!(Test-Path $Exe)) {
    throw "Expected executable was not produced: $Exe"
}

Write-Host "Ready: $Exe" -ForegroundColor Green
exit 0
