param(
    [string]$Source = "",
    [string]$Output = "",
    [string]$CudaArchitectures = ""
)
$ErrorActionPreference = 'Stop'
$Root = Split-Path $PSScriptRoot -Parent
$RuntimeManifest = Get-Content (Join-Path $Root 'config/runtime.json') -Raw | ConvertFrom-Json
$Pin = $RuntimeManifest.dependencyPins.'qwentts.cpp'
if (-not $Pin -or [string]::IsNullOrWhiteSpace($Pin.gitRef) -or [string]::IsNullOrWhiteSpace($Pin.repository)) { throw 'config/runtime.json must define dependencyPins.qwentts.cpp.repository and gitRef.' }
$Commit = [string]$Pin.gitRef
$Repository = [string]$Pin.repository
if ([string]::IsNullOrWhiteSpace($Source)) { $Source = Join-Path $Root 'work/qwentts.cpp-pinned' }
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $Root 'engines/tts/qwenttscpp-nvidia-win-x64' }
$Build = Join-Path $Source 'build-resone-win-x64'
$Stage = "$Output.stage-$([Guid]::NewGuid().ToString('N'))"

function Run([string]$Exe, [string[]]$Args) {
    & $Exe @Args
    if ($LASTEXITCODE -ne 0) { throw "$Exe failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path (Join-Path $Source '.git'))) {
    New-Item -ItemType Directory -Force (Split-Path $Source -Parent) | Out-Null
    Run git @('clone','--recursive',$Repository,$Source)
}

Run git @('-C',$Source,'fetch','origin',$Commit)
Run git @('-C',$Source,'checkout','--detach',$Commit)
Run git @('-C',$Source,'submodule','sync','--recursive')
Run git @('-C',$Source,'submodule','update','--init','--recursive','--force')
$Actual = (& git -C $Source rev-parse HEAD).Trim()
if ($Actual -ne $Commit) { throw "qwentts.cpp checkout mismatch: $Actual" }

Remove-Item $Build,$Stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $Stage | Out-Null
$configure = @('-S',$Source,'-B',$Build,'-G','Visual Studio 17 2022','-A','x64','-DGGML_CUDA=ON')
if (-not [string]::IsNullOrWhiteSpace($CudaArchitectures)) { $configure += "-DCMAKE_CUDA_ARCHITECTURES=$CudaArchitectures" }
Run cmake $configure
Run cmake @('--build',$Build,'--config','Release','--target','tts-server','--parallel')

$server = Get-ChildItem $Build -Recurse -File | Where-Object { $_.Name -eq 'tts-server.exe' } | Select-Object -First 1
if (-not $server) { throw 'Build did not produce tts-server.exe.' }
Copy-Item $server.FullName (Join-Path $Stage 'qwen-server.exe') -Force

# qwentts.cpp itself is statically linked into the server. Stage only the runtime
# backend DLLs that the executable actually needs; there is intentionally no qwen.dll.
$backend = Get-ChildItem $Build -Recurse -File | Where-Object { $_.Name -like 'ggml*.dll' }
foreach ($required in @('ggml.dll','ggml-base.dll','ggml-cpu.dll')) {
    if (-not ($backend | Where-Object Name -eq $required)) { throw "Build did not produce required $required" }
}
foreach ($f in $backend) { Copy-Item $f.FullName (Join-Path $Stage $f.Name) -Force }

# Replace, never overlay, so the server and its GGML/CUDA backend DLLs always come
# from the same pinned checkout/submodule build.
$backup = "$Output.old-$([Guid]::NewGuid().ToString('N'))"
try {
    if (Test-Path $Output) { Move-Item $Output $backup }
    Move-Item $Stage $Output
    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
} catch {
    if ((Test-Path $backup) -and -not (Test-Path $Output)) { Move-Item $backup $Output }
    throw
}
Set-Content -Path (Join-Path $Output '.qwentts-commit') -Value $Commit -NoNewline
Write-Host "Staged pinned Qwen NVIDIA server pack: $Output"
