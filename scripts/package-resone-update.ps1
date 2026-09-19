param(
  [Parameter(Mandatory=$true)][string]$Version,
  [Parameter(Mandatory=$true)][string]$PackageUrl,
  [string]$InstallDirectory = "$env:LOCALAPPDATA\Wds\Resone",
  [string]$OutputZip = "",
  [string]$ManifestOutput = "",
  [string]$Rid = "win-x64",
  [string]$Channel = "stable",
  [string]$ReleaseNotesUrl = ""
)
$ErrorActionPreference='Stop'
$Root = Split-Path $PSScriptRoot -Parent
if (!(Test-Path $InstallDirectory -PathType Container)) { throw "Install directory not found: $InstallDirectory" }
if (!$PackageUrl.StartsWith('https://')) { throw 'PackageUrl must use HTTPS.' }
if ([string]::IsNullOrWhiteSpace($OutputZip)) { $OutputZip = Join-Path $Root "artifacts/resone-$Version-$Rid.zip" }
if ([string]::IsNullOrWhiteSpace($ManifestOutput)) { $ManifestOutput = Join-Path $Root "artifacts/resone-$Channel.json" }
New-Item -ItemType Directory -Force (Split-Path $OutputZip -Parent),(Split-Path $ManifestOutput -Parent) | Out-Null

$Stage = Join-Path ([IO.Path]::GetTempPath()) ("resone-update-package-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $Stage | Out-Null
try {
  $ExcludeTop = @('user','logs','updates')
  foreach ($Item in Get-ChildItem $InstallDirectory -Force) {
    if ($ExcludeTop -contains $Item.Name) { continue }
    $Dest = Join-Path $Stage $Item.Name
    if ($Item.PSIsContainer) { Copy-Item $Item.FullName $Dest -Recurse -Force }
    else { Copy-Item $Item.FullName $Dest -Force }
  }

  foreach ($Required in @('wds.resone.launcher.exe','wds.resone.updater.exe','wds.resone.ui.exe','config/runtime.json')) {
    if (!(Test-Path (Join-Path $Stage $Required))) { throw "Update staging is missing $Required" }
  }

  $RuntimePath = Join-Path $Stage 'config/runtime.json'
  $Runtime = Get-Content $RuntimePath -Raw | ConvertFrom-Json
  if (!$Runtime.updates) { throw 'config/runtime.json has no updates section.' }
  $Runtime.updates.currentVersion = $Version
  $Runtime.updates.channel = $Channel
  $Runtime.updates.enabled = $true
  $Runtime | ConvertTo-Json -Depth 64 | Set-Content $RuntimePath -Encoding UTF8

  if (Test-Path $OutputZip) { Remove-Item $OutputZip -Force }
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [IO.Compression.ZipFile]::CreateFromDirectory($Stage,$OutputZip,[IO.Compression.CompressionLevel]::Optimal,$false)
  $ArchiveSha = (Get-FileHash $OutputZip -Algorithm SHA256).Hash

  $Critical = @(
    'wds.resone.launcher.exe',
    'wds.resone.updater.exe',
    'wds.resone.ui.exe',
    'wds.resone.api.dll',
    'resone_llama_bridge.dll',
    'wds.resone.vocals.dll',
    'config/runtime.json'
  ) | Where-Object { Test-Path (Join-Path $Stage $_) }
  $Files = @($Critical | ForEach-Object {
    [ordered]@{ path = ($_ -replace '\\','/'); sha256 = (Get-FileHash (Join-Path $Stage $_) -Algorithm SHA256).Hash }
  })

  $Manifest = [ordered]@{
    schemaVersion = 1
    product = 'resone'
    channel = $Channel
    version = $Version
    publishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    releaseNotesUrl = $ReleaseNotesUrl
    artifacts = @(
      [ordered]@{
        enabled = $true
        id = "resone-$Rid"
        kind = 'app'
        rid = $Rid
        installMode = 'archive'
        url = $PackageUrl
        sha256 = $ArchiveSha
        maxBytes = [Math]::Max((Get-Item $OutputZip).Length * 2, 104857600)
        maxExtractedBytes = [Math]::Max((Get-ChildItem $Stage -Recurse -File | Measure-Object Length -Sum).Sum * 2, 209715200)
        stripComponents = 0
        required = $true
        autoInstall = $true
        requiredFiles = @('wds.resone.launcher.exe','wds.resone.updater.exe','wds.resone.ui.exe','config/runtime.json')
        files = $Files
        arguments = @()
      }
    )
  }
  $Manifest | ConvertTo-Json -Depth 64 | Set-Content $ManifestOutput -Encoding UTF8
  Write-Host "Update archive: $OutputZip"
  Write-Host "Archive SHA256: $ArchiveSha"
  Write-Host "Manifest: $ManifestOutput"
  Write-Host "Upload the archive to $PackageUrl and publish the manifest at the URL in config/runtime.json -> updates.manifestUrl."
}
finally { if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force } }
