param([ValidateSet('cpu','cuda','metal')][string]$Backend='cpu',[string]$LlamaSource='')
$ErrorActionPreference='Stop'
$Root=Split-Path $PSScriptRoot -Parent
function Run([string]$Exe,[string[]]$Arguments){ & $Exe @Arguments; if($LASTEXITCODE -ne 0){throw "$Exe failed"} }
$Commit=(Get-Content "$Root/native/inference/llama-commit.txt" -Raw).Trim()
if(!$LlamaSource){$LlamaSource="$Root/third_party/llama.cpp";if(!(Test-Path "$LlamaSource/.git")){Run git @('clone','https://github.com/ggml-org/llama.cpp.git',$LlamaSource)};Run git @('-C',$LlamaSource,'checkout','--detach',$Commit)}
$Actual=(& git -C $LlamaSource rev-parse HEAD).Trim();if($Actual -ne $Commit){throw 'Inference adapter must be built with its pinned llama.cpp revision.'}
$Platform=if($IsMacOS){'osx'}elseif($IsLinux){'linux'}else{'win'}
$Architecture=[System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
if($Architecture -notin @('x64','arm64')){throw "Unsupported build architecture: $Architecture"}
$Rid="$Platform-$Architecture"
if($Backend -eq 'metal' -and $Platform -ne 'osx'){throw 'Metal packs require macOS.'}
$Build="$Root/build/inference-$Rid-$Backend";$Stage="$Root/build/pack-$Rid-$Backend"
if(Test-Path $Stage){Remove-Item $Stage -Recurse -Force}
$Options=@('-S',"$Root/native/inference",'-B',$Build,"-DLLAMA_CPP_DIR=$LlamaSource",'-DCMAKE_BUILD_TYPE=Release','-DGGML_NATIVE=OFF')
if($Backend -eq 'cpu'){$Options+=@('-DGGML_AVX=OFF','-DGGML_AVX2=OFF','-DGGML_FMA=OFF','-DGGML_F16C=OFF')}
if($Backend -eq 'cuda'){$Options+='-DGGML_CUDA=ON'}
if($Backend -eq 'metal'){$Options+='-DGGML_METAL=ON'}
Run cmake $Options
Run cmake @('--build',$Build,'--config','Release','--target','resone_inference','--parallel')
Run cmake @('--install',$Build,'--config','Release','--prefix',$Stage,'--component','ResoneInference')
Copy-Item "$LlamaSource/LICENSE" "$Stage/llama-LICENSE.txt"
if(Test-Path "$LlamaSource/licenses"){Copy-Item "$LlamaSource/licenses" "$Stage/licenses" -Recurse}
Get-ChildItem "$LlamaSource/vendor" -Recurse -File | Where-Object { $_.Name -match '^(LICENSE|COPYING|LICENCE)' } | ForEach-Object { Copy-Item $_.FullName "$Stage/$($_.Directory.Name)-$($_.Name)" }
$Zip="$Root/build/resone-llm-$Rid-$Backend.zip"
Compress-Archive -Path "$Stage/*" -DestinationPath $Zip -Force
Write-Host "Pack: $Zip"
Get-FileHash $Zip -Algorithm SHA256
Write-Host 'For GPU packs, verify required driver/toolkit runtime dependencies on a clean target before publishing.'
