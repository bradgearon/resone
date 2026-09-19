from pathlib import Path
root=Path(__file__).resolve().parents[1]
build=(root/'scripts/build-windows.ps1').read_text()
cmake=(root/'src/wds.resone.ui/CMakeLists.txt').read_text()
resolver=(root/'src/wds.resone.api/LlamaEngineResolver.cs').read_text()
bridge=(root/'native/inference/resone_llama_bridge.cpp').read_text()

# A loaded bridge is immutable: changed source produces a new hash-named DLL instead of overwriting it.
assert 'Get-BridgeSourceFingerprint' in build
assert '$BridgeBuildId = $BridgeFingerprint.Substring(0, 16)' in build
assert '$BridgeOutputName = "resone_llama_bridge_$BridgeBuildId.dll"' in build
assert '--clean-first' not in build
assert '-DRESONE_LLAMA_BRIDGE_BUILD_ID=$BridgeBuildId' in build
assert 'if (Test-Path $BridgeDll)' in build
assert 'bridge-current.txt' in build
assert 'Copy-Item $BridgeDll "$InstallDir/resone_llama_bridge.dll" -Force' in build

assert 'RESONE_LLAMA_BRIDGE_BUILD_ID' in cmake
assert 'resone_llama_bridge_${RESONE_LLAMA_BRIDGE_SAFE_ID}' in cmake
assert 'RESONE_LLAMA_BRIDGE_BUILD_ID' in bridge and 'source=' in bridge

# Dev runtime follows the marker/hash build; installed runtime still uses canonical bridge filename.
assert 'bridge-current.txt' in resolver
assert 'Directory.GetFiles(devDir, stem + "_*" + ext)' in resolver
assert 'ResolveUnderRoot(name)' in resolver
print('llama bridge live-build regression: PASS')
