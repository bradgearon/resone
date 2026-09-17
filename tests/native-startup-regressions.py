from pathlib import Path
root = Path(__file__).resolve().parents[1]
dyn = (root/'native/include/DynamicLibrary.hpp').read_text()
audio = (root/'native/src/AudioEngine.cpp').read_text()
worker = (root/'src/wds.resone.launcher/WorkerHost.cs').read_text()
chat = (root/'src/wds.resone.api/NativeChatClient.cs').read_text()
assert 'LOAD_LIBRARY_SEARCH_USER_DIRS' in dyn
assert 'AddDllDirectory' in dyn
assert 'third_party/vcpkg/installed/x64-windows/bin' in audio
assert 'developmentTree' in audio
assert 'native-audio.log' in audio
assert 'StartupTrace' in worker
assert 'inference-load' in worker
assert 'WarmupAsync(settings, CancellationToken.None, m =>' in worker
assert 'GPU model load failed:' in chat
assert 'llama model/context loaded successfully' in chat
print('PASS native dependency search and early inference startup logging')
