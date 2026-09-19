from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root/'config/runtime.json').read_text())
assert manifest['manifestVersion'] >= 2
assert manifest['provisionAiRuntime'] is True
assert manifest['requireHashes'] is True
for dep in ('llama.cpp','qwentts.cpp','iPlug2','vst3sdk','vcpkg'):
    pin=manifest['dependencyPins'][dep]
    assert pin['repository'].startswith('https://')
    assert len(pin['gitRef']) >= 7

program=(root/'src/wds.resone.launcher/Program.cs').read_text()
location=(root/'src/wds.resone.launcher/AiRuntimeLocation.cs').read_text()
installer=(root/'src/wds.resone.launcher/EnginePackInstaller.cs').read_text()
supervisor=(root/'src/wds.resone.launcher/ServiceSupervisor.cs').read_text()
apiroot=(root/'src/wds.resone.api/AiRuntimeRoot.cs').read_text()
hardware=(root/'src/wds.resone.launcher/AiHardwarePlatformDetector.cs').read_text()
qwen=(root/'src/wds.resone.api/VocalSinging/QwenTtsServiceManager.cs').read_text()
whisper=(root/'src/wds.resone.api/WhisperCppRuntime.cs').read_text()
llama=(root/'src/wds.resone.api/LlamaEngineResolver.cs').read_text()

assert 'AiRuntimeLocation.Configure(args)' in program
assert '--ai-root' in location and 'AI_ROOT' in apiroot
assert 'root.txt' in apiroot
assert '.wds-ai-engine.json' in installer and 'Version' in installer and 'Sha256' in installer
assert 'WriteActivePointerAsync' in installer
assert 'Updating model:' in supervisor and '.wds-ai.json' in supervisor
assert 'AI_PLATFORM' in hardware and 'VEN_10DE' in hardware and 'VEN_1002' in hardware and 'VEN_8086' in hardware
assert 'ResolveEngineDirectory("tts"' in qwen
assert 'ResolveEngineDirectory("asr"' in whisper
assert 'ResolveEngineDirectory("llm"' in llama

qwenbuild=(root/'scripts/build-qwentts-nvidia-win-x64.ps1').read_text()
inference=(root/'scripts/build-inference-pack.ps1').read_text()
windows=(root/'scripts/build-windows.ps1').read_text()
assert "dependencyPins.'qwentts.cpp'" in qwenbuild
assert "dependencyPins.'llama.cpp'" in inference
assert "DependencyPin 'iPlug2'" in windows and "DependencyPin 'vst3sdk'" in windows and "DependencyPin 'vcpkg'" in windows
print('AI runtime provisioning regression: PASS')
