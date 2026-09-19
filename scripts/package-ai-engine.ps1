param(
  [Parameter(Mandatory=$true)][string]$SourceDirectory,
  [Parameter(Mandatory=$true)][string]$OutputZip
)
$ErrorActionPreference='Stop'
$SourceDirectory=[IO.Path]::GetFullPath($SourceDirectory)
$OutputZip=[IO.Path]::GetFullPath($OutputZip)
if (!(Test-Path $SourceDirectory -PathType Container)) { throw "Engine directory not found: $SourceDirectory" }
New-Item -ItemType Directory -Force (Split-Path $OutputZip -Parent) | Out-Null
Remove-Item $OutputZip -Force -ErrorAction SilentlyContinue
# Archive the directory contents, not an extra parent directory. This matches stripComponents=0.
Compress-Archive -Path (Join-Path $SourceDirectory '*') -DestinationPath $OutputZip -CompressionLevel Optimal
$Hash=(Get-FileHash -Algorithm SHA256 $OutputZip).Hash.ToUpperInvariant()
Write-Host "Archive: $OutputZip"
Write-Host "SHA256 : $Hash"
Write-Host 'Paste this hash into config/runtime.json (or your release manifest) for the matching engine pack.'
