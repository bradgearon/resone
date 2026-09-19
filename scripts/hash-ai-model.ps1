param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference='Stop'
$Full=[IO.Path]::GetFullPath($Path)
if (!(Test-Path $Full -PathType Leaf)) { throw "File not found: $Full" }
$Hash=(Get-FileHash -Algorithm SHA256 $Full).Hash.ToUpperInvariant()
Write-Host "File   : $Full"
Write-Host "SHA256 : $Hash"
