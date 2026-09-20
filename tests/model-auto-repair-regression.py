from pathlib import Path

root = Path(__file__).resolve().parents[1]
supervisor = (root/'src/wds.resone.launcher/ServiceSupervisor.cs').read_text()
installer = (root/'src/wds.resone.launcher/EnginePackInstaller.cs').read_text()
background = (root/'src/wds.resone.launcher/BackgroundController.cs').read_text()
whisper = (root/'src/wds.resone.api/WhisperCppRuntime.cs').read_text()
qwen = (root/'src/wds.resone.api/VocalSinging/QwenTtsServiceManager.cs').read_text()

# Every explicit ensure refreshes runtime.json so a long-lived launcher cannot hold stale release metadata.
assert '_config = await LoadRuntimeConfigAsync(token)' in supervisor
assert 'private async Task<RuntimeConfig> LoadRuntimeConfigAsync' in supervisor

# Enabled models are checked/restored independently before engine-pack selection.
models_pos = supervisor.index('foreach (ModelFile model in config.Models.Where')
packs_pos = supervisor.index('IReadOnlyList<EnginePack> packs = EnginePackInstaller.SelectPacks')
assert models_pos < packs_pos
assert 'await DownloadModelAsync(model' in supervisor

# If a stale manifest temporarily lacks a downloadable pack, an already verified active engine can keep working.
assert 'HasUsableActiveEngine(aiRoot, component, hardware.Platform)' in supervisor
assert 'internal static bool HasUsableActiveEngine' in installer
assert '.wds-ai-engine.json' in installer

# Reconnecting/opening the UI performs the cheap LLM existence/receipt check again.
assert 'if(supervisor is not null)await supervisor.EnsureComponentAsync("llm",token);' in background

# Voice runtimes remain lazy and invoke provisioning only when their capability is used.
assert 'EnsureRuntimeComponentAsync("asr"' in whisper
assert 'EnsureRuntimeComponentAsync("tts"' in qwen

print('model/engine auto-repair regression: PASS')
