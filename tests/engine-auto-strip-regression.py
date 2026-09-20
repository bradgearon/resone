from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
installer = (root / 'src/wds.resone.launcher/EnginePackInstaller.cs').read_text()

# Engine archives default to automatic wrapper stripping. Explicit 0..8 still overrides it.
assert installer.count('public int StripComponents { get; set; } = -1;') >= 3
assert 'ResolveStripComponents(zip, pack.StripComponents)' in installer
assert 'parts.Length == 1 && parts[0].EndsWith(".dll"' in installer
assert 'CommonParentPrefixLength(runtimeFiles)' in installer
assert 'CommonParentPrefixLength(files)' in installer
assert 'stripComponents must be -1 (auto) or between 0 and 8.' in installer
assert 'engine-extract-auto-v1' in installer

# The production release manifest relies on auto layout rather than hard-coded upstream folder depth.
data = json.loads((root / 'config/runtime.release.json').read_text())
for entry in data.get('enginePacks', []):
    for backend in ('cpu', 'cuda', 'vulkan'):
        group = entry.get(backend, {})
        for component in ('llm', 'tts', 'asr'):
            asset = group.get(component)
            if asset is not None:
                assert 'stripComponents' not in asset, (backend, component)

print('engine auto-strip regression: PASS')
