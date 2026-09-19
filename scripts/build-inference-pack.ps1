param()
$ErrorActionPreference='Stop'
$Root=Split-Path $PSScriptRoot -Parent
$Manifest = Get-Content (Join-Path $Root 'config/runtime.json') -Raw | ConvertFrom-Json
$LlamaPin = $Manifest.dependencyPins.'llama.cpp'
if (-not $LlamaPin -or [string]::IsNullOrWhiteSpace($LlamaPin.gitRef)) { throw 'config/runtime.json must define dependencyPins.llama.cpp.gitRef.' }
$AbiHeader = Get-Content (Join-Path $Root 'native/inference/llama_dynamic_abi.hpp') -Raw
if ($AbiHeader -notmatch [regex]::Escape([string]$LlamaPin.gitRef)) { throw "llama_dynamic_abi.hpp is not pinned to the runtime manifest llama.cpp gitRef $($LlamaPin.gitRef)." }
function Run([string]$Exe,[string[]]$Args){& $Exe @Args;if($LASTEXITCODE-ne 0){throw "$Exe failed ($LASTEXITCODE)"}}

# This builds only Resone's tiny dynamic-loader bridge. It does NOT download,
# clone, configure, or build llama.cpp. The real llama runtime is a prebuilt ZIP
# provisioned by the launcher from config/runtime.json (or the release manifest).
$Build="$Root/build/llama-bridge"
$Stage="$Root/build/llama-bridge-stage"
Remove-Item $Build,$Stage -Recurse -Force -ErrorAction SilentlyContinue
Run cmake @('-S',"$Root/native/inference",'-B',$Build,'-G','Visual Studio 17 2022','-A','x64')
Run cmake @('--build',$Build,'--config','Release','--target','resone_llama_bridge','--parallel')
New-Item -ItemType Directory -Force $Stage|Out-Null
Copy-Item "$Build/Release/resone_llama_bridge.dll" $Stage -Force
Write-Host "Built Resone's loader bridge at $Stage. Supply llama.cpp itself as a prebuilt engine ZIP; no llama source checkout is used."
