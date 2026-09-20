from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
release = json.loads((root / "config/runtime.release.json").read_text())
assert release["manifestVersion"] == 2
assert len(release["enginePacks"]) == 1
matrix = release["enginePacks"][0]
assert set(matrix) >= {"cpu", "cuda", "vulkan"}
for backend in ("cpu", "cuda", "vulkan"):
    assert set(matrix[backend]) >= {"llm", "tts", "asr"}
    for component in ("llm", "tts", "asr"):
        asset = matrix[backend][component]
        assert asset["url"].startswith("https://")
        assert asset["sha256"] == "REPLACE_WITH_64_HEX_SHA256" or (len(asset["sha256"]) == 64 and all(c in "0123456789abcdefABCDEF" for c in asset["sha256"]))
        assert "stripComponents" not in asset
    assert matrix[backend]["llm"].get("enabled", True) is True
    assert matrix[backend]["llm"]["directory"] == "engines/llm/llama-cpp-dynamic-win-x64"
    assert matrix[backend]["tts"]["directory"] == "engines/tts/qwenttscpp-nvidia-win-x64"
    assert matrix[backend]["asr"]["directory"] == "engines/asr/whispercpp-nvidia-win-x64"

assert matrix["cpu"]["tts"].get("enabled", True) is False
assert matrix["cpu"]["asr"].get("enabled", True) is False
assert matrix["cuda"]["tts"].get("enabled", True) is True
assert matrix["cuda"]["asr"].get("enabled", True) is True
assert matrix["vulkan"]["tts"].get("enabled", True) is False
assert matrix["vulkan"]["asr"].get("enabled", True) is False

assert matrix["cpu"]["asr"]["url"].endswith("whisper-bin-x64.zip")
assert matrix["cuda"]["asr"]["url"].endswith("whisper-cublas-12.4.0-bin-x64.zip")
assert matrix["vulkan"]["asr"]["url"].endswith("whisper-bin-x64.zip")
assert matrix["cuda"]["llm"]["sha256"] == "D46CB57B73F68E52D5DD142574061E32F7AD3865282C351F2ABB6C08A1279CE9"
assert "additionalArchives" not in matrix["cuda"]["llm"]

enabled_models = {m["id"] for m in release["models"] if m.get("enabled", True)}
assert enabled_models == {
    "gemma-4-e4b-q6k",
    "whisper-large-v3-turbo",
    "qwen-talker-base-0.6b-q8",
    "qwen-talker-voicedesign-1.7b-q8",
    "qwen-tokenizer-12hz-q8",
}

installer = (root / "src/wds.resone.launcher/EnginePackInstaller.cs").read_text()
hardware = (root / "src/wds.resone.launcher/AiHardwarePlatformDetector.cs").read_text()
build = (root / "scripts/build-windows.ps1").read_text()
assert "EnginePackManifestEntry" in installer
assert "CompactEnginePackAsset" in installer
assert "ExpandManifestPacks" in installer
assert "ComputeSourceFingerprint" in installer
assert "InstalledFileHashes" in installer
assert "forceFullVerification" in installer
assert "HashInstalledFilesAsync" in installer
assert 'AddCompactBackend(result, "cpu"' in installer
assert 'AddCompactBackend(result, "cuda"' in installer
assert 'AddCompactBackend(result, "vulkan"' in installer
assert 'return new("cuda", "NVIDIA display adapter / CUDA backend")' in hardware
assert 'return new("vulkan", "AMD display adapter / Vulkan backend")' in hardware
assert "$IsCompact" in build and "@('cpu','cuda','vulkan')" in build
assert "$ExpectedLlmDirectory" in build and "must declare directory explicitly" in build
assert "$ExpectedLlmModelPath" in build and "$HasConfiguredLlmModel" in build
print("engine pack matrix regression: PASS")
