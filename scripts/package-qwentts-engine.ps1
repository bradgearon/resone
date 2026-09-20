param(
    [ValidateSet('cpu', 'cuda', 'vulkan', 'all')]
    [string]$Backend = 'cuda',

    # Optional override when the staged Qwen binaries are somewhere else.
    [string]$SourceDirectory = '',

    [string]$Version = '1',

    # By default the archives are written next to the staged engine folders.
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $Root 'engines/tts'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

function Get-DefaultSourceDirectory([string]$SelectedBackend) {
    $candidates = switch ($SelectedBackend) {
        'cuda' {
            @(
                (Join-Path $Root 'engines/tts/qwenttscpp-nvidia-win-x64'),
                (Join-Path $Root 'engines/tts/qwenttscpp-cuda-win-x64')
            )
        }
        'cpu' {
            @(
                (Join-Path $Root 'engines/tts/qwenttscpp-cpu-win-x64')
            )
        }
        'vulkan' {
            @(
                (Join-Path $Root 'engines/tts/qwenttscpp-vulkan-win-x64')
            )
        }
        default { @() }
    }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate -PathType Container) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }

    return $null
}

function Copy-DirectoryContents([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Force $Destination | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        # Build/debug artifacts are not part of the runtime engine pack.
        if ($item.Name -match '\.(pdb|lib|exp|ilk|zip)$') { continue }
        if ($item.Name -in @('.git', '.vs')) { continue }
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

function Assert-QwenRuntime([string]$Directory, [string]$SelectedBackend) {
    $server = Join-Path $Directory 'qwen-server.exe'
    $legacyServer = Join-Path $Directory 'tts-server.exe'

    if (-not (Test-Path $server -PathType Leaf)) {
        if (Test-Path $legacyServer -PathType Leaf) {
            Copy-Item -LiteralPath $legacyServer -Destination $server -Force
        } else {
            throw "Qwen runtime does not contain qwen-server.exe or tts-server.exe: $Directory"
        }
    }

    foreach ($required in @('ggml.dll', 'ggml-base.dll', 'ggml-cpu.dll')) {
        if (-not (Test-Path (Join-Path $Directory $required) -PathType Leaf)) {
            throw "Qwen runtime is missing required file '$required': $Directory"
        }
    }

    if ($SelectedBackend -eq 'cuda') {
        if (-not (Get-ChildItem -LiteralPath $Directory -File -Filter 'ggml-cuda*.dll' -ErrorAction SilentlyContinue)) {
            throw "CUDA Qwen runtime does not contain a ggml-cuda*.dll backend: $Directory"
        }
    }

    if ($SelectedBackend -eq 'vulkan') {
        if (-not (Get-ChildItem -LiteralPath $Directory -File -Filter 'ggml-vulkan*.dll' -ErrorAction SilentlyContinue)) {
            throw "Vulkan Qwen runtime does not contain a ggml-vulkan*.dll backend: $Directory"
        }
    }
}

function New-QwenArchive([string]$SelectedBackend, [string]$ExplicitSource = '') {
    $source = $ExplicitSource
    if ([string]::IsNullOrWhiteSpace($source)) {
        $source = Get-DefaultSourceDirectory $SelectedBackend
    }

    if ([string]::IsNullOrWhiteSpace($source)) {
        throw "No staged Qwen $SelectedBackend engine directory was found. Pass -SourceDirectory explicitly or stage it under engines/tts first."
    }

    $source = [IO.Path]::GetFullPath($source)
    if (-not (Test-Path $source -PathType Container)) {
        throw "Qwen engine directory not found: $source"
    }

    $archiveBackendName = if ($SelectedBackend -eq 'cuda') { 'cuda' } else { $SelectedBackend }
    $archiveName = "qwenttscpp-$archiveBackendName-win-x64-v$Version.zip"
    $archivePath = Join-Path $OutputDirectory $archiveName
    $stage = Join-Path ([IO.Path]::GetTempPath()) ("resone-qwen-pack-" + [Guid]::NewGuid().ToString('N'))

    try {
        Copy-DirectoryContents $source $stage
        Assert-QwenRuntime $stage $SelectedBackend

        if (Test-Path $archivePath) {
            Remove-Item -LiteralPath $archivePath -Force
        }

        # Compress the CONTENTS of the staged engine folder. The archive must not
        # contain an extra qwenttscpp-* parent directory because the launcher
        # extracts it directly into the configured engine directory.
        Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archivePath -CompressionLevel Optimal

        $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToUpperInvariant()
        $size = (Get-Item -LiteralPath $archivePath).Length

        Write-Host ''
        Write-Host "Qwen $SelectedBackend engine pack created."
        Write-Host "Source : $source"
        Write-Host "Archive: $archivePath"
        Write-Host "Bytes  : $size"
        Write-Host "SHA256 : $hash"
        Write-Host ''
        Write-Host 'runtime.release.json values:'
        Write-Host "  file   = $archiveName"
        Write-Host "  sha256 = $hash"

        return [pscustomobject]@{
            Backend = $SelectedBackend
            Source = $source
            Archive = $archivePath
            FileName = $archiveName
            Sha256 = $hash
            Bytes = $size
        }
    }
    finally {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$results = @()
if ($Backend -eq 'all') {
    if (-not [string]::IsNullOrWhiteSpace($SourceDirectory)) {
        throw '-SourceDirectory can only be used when packaging one backend. Use the conventional engines/tts folders with -Backend all.'
    }

    foreach ($candidateBackend in @('cpu', 'cuda', 'vulkan')) {
        $candidateSource = Get-DefaultSourceDirectory $candidateBackend
        if ([string]::IsNullOrWhiteSpace($candidateSource)) {
            Write-Warning "Skipping ${candidateBackend}: no staged Qwen engine directory was found."
            continue
        }
        $results += New-QwenArchive $candidateBackend $candidateSource
    }

    if ($results.Count -eq 0) {
        throw 'No staged Qwen engine folders were found to package.'
    }
} else {
    $results += New-QwenArchive $Backend $SourceDirectory
}

Write-Host ''
Write-Host 'Done. Upload the generated ZIP(s) to your download host and copy the printed SHA-256 value(s) into the matching TTS engine entry in config/runtime.release.json.'
