from pathlib import Path
root = Path(__file__).resolve().parents[1]
bridge = (root/'native/inference/resone_llama_bridge.cpp').read_text()
client = (root/'src/wds.resone.api/NativeChatClient.cs').read_text()
build = (root/'scripts/build-windows.ps1').read_text()

# b9870 does NOT expose llama_version in its public include/llama.h. It may be
# absent from official release DLLs, so it must never be loaded as a required symbol.
assert 'LLAMA_FN(version, "llama_version")' not in bridge
assert 'load_symbol<decltype(api->version)>' not in bridge
assert 'GetProcAddress(api->llama, "llama_version")' in bridge
assert 'dlsym(api->llama, "llama_version")' in bridge

# A stale bridge from an older source package must be detected immediately.
assert 'resone_llama_bridge_abi() { return 3; }' in bridge
assert 'resone_llama_bridge_build_id' in bridge
assert 'bridgeAbi != 3' in client
assert 'expected 3' in client

# Rebuilding over an existing extracted tree must not reuse stale native objects.
assert '--clean-first' in build
assert "'--target','resone_llama_bridge'" in build

print('PASS llama b9870 loader: optional llama_version + stale-bridge clean rebuild guard')
