from pathlib import Path
import json, re
root=Path(__file__).resolve().parents[1]
llama=(root/'native/inference/llama_dynamic_abi.hpp').read_text()
bridge=(root/'native/inference/resone_llama_bridge.cpp').read_text()
qwen=(root/'native/vendor/qwen/qwen_resone_abi.h').read_text()
qmgr=(root/'src/wds.resone.api/VocalSinging/QwenTtsServiceManager.cs').read_text()
contracts=(root/'src/wds.resone.api/Contracts.cs').read_text()
ui=(root/'src/wds.resone.ui/resources/web/app.js').read_text()
worker=(root/'src/wds.resone.launcher/WorkerHost.cs').read_text()
native_client=(root/'src/wds.resone.api/NativeChatClient.cs').read_text()
build=(root/'scripts/build-windows.ps1').read_text()
app=json.loads((root/'config/appsettings.json').read_text())
assert 'release b9870' in llama
assert '2d973636e292ee6f75fadcf08d29cb33511f509f' in llama
for bad in ['llama_load_mode load_mode','llama_lazy_mode lazy_mode','n_outputs_max_per_seq','bool load_mtp']:
    assert bad not in llama, bad
assert 'bool use_mmap;' in llama and 'bool use_direct_io;' in llama and 'bool use_mlock;' in llama
assert 'llama_version() was not part of the b9870 public header contract' in bridge
assert 'resone_llama_bridge_abi() { return 3; }' in bridge
assert 'resone-llama-bridge/3 llama.cpp-b9870 unlimited-output' in bridge
assert (root/'native/inference/llama-commit.txt').read_text().splitlines()[0].strip()=='b9870'
assert '#define QT_ABI_VERSION 4' in qwen
assert 'bool do_sample;' in qwen and 'bool subtalker_do_sample;' in qwen
assert 'a8a7716b530e49fed537c57711247c12fbbb903c' in qwen
assert (root/'native/vendor/qwen/qwen-commit.txt').read_text().splitlines()[0].strip()=='a8a7716b530e49fed537c57711247c12fbbb903c'
assert 'qwen-server.exe' in qmgr and '/v1/audio/speech' in qmgr
assert '["instructions"] = instruction.Trim()' in qmgr
assert '["instruct"] = instruction.Trim()' not in qmgr
assert '/v1/audio/voices' in qmgr and 'ScheduleIdleStopLocked' in qmgr
assert app['qwenTtsServerName']=='qwen-server.exe'
assert app['serviceDelays']=={'voice-design':3000,'custom-voice':3000}
assert app['contextTokens']==16384 and app['reasoningEnabled'] is False
assert "'Vocals'" in ui and 'vocals : i === 6' in ui and 'includeInAi : i === 0' in ui
assert 'renderVocals' in worker
assert 'bridgeAbi != 3' in native_client and 'resone_llama_bridge_build_id' in native_client
assert "--clean-first" in build and "resone_llama_bridge" in build
assert 'List<VocalGuidanceEvent>' in contracts
print('PASS pinned llama b9870 + qwentts a8a7716 server contract, vocals lane, and local render contract')
