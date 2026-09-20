from pathlib import Path
root=Path(__file__).resolve().parents[1]
supervisor=(root/'src/wds.resone.launcher/ServiceSupervisor.cs').read_text()
installer=(root/'src/wds.resone.launcher/EnginePackInstaller.cs').read_text()
program=(root/'src/wds.resone.launcher/Program.cs').read_text()
background=(root/'src/wds.resone.launcher/BackgroundController.cs').read_text()
bootstrap=(root/'src/wds.resone.api/LauncherBootstrap.cs').read_text()
whisper=(root/'src/wds.resone.api/WhisperCppRuntime.cs').read_text()
qwen=(root/'src/wds.resone.api/VocalSinging/QwenTtsServiceManager.cs').read_text()

assert 'EnsureComponentCoreAsync("llm"' in supervisor
assert 'EnsureComponentAsync(string component' in supervisor
assert 'IsModelForComponent' in supervisor
assert 'SelectPacks(config, hardware, component)' in supervisor
assert 'string? component = null' in installer
assert 'ensure-component ' in program
assert 'EnsureRuntimeComponentAsync' in background
assert 'EnsureRuntimeComponentAsync(string component' in bootstrap
assert 'EnsureRuntimeComponentAsync("asr"' in whisper
assert 'EnsureRuntimeComponentAsync("tts"' in qwen
assert 'if (active && runtimeEnsured)' in qwen
assert 'if (runtimeEnsured)' in qwen
print('lazy voice runtime provisioning regression: PASS')
