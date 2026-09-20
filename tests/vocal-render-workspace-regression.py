from pathlib import Path
root=Path(__file__).resolve().parents[1]
js=(root/'src/wds.resone.ui/resources/web/app.js').read_text()
manager=(root/'src/wds.resone.api/VocalSinging/QwenTtsServiceManager.cs').read_text()

# Switching saved songs must stop the old native AudioEngine request before installing the new project.
load=js[js.index('function loadWorkspaceSong'):js.index('function createNewWorkspaceSong')]
assert "send('stop')" in load and "transport='stopped'" in load, 'workspace switch does not stop old playback immediately'
loaded=js[js.index("case 'workspaceLoaded':"):js.index("case 'workspaceSaved':")]
assert loaded.index("send('stop')") < loaded.index("send('project',song)"), 'workspace load must stop playback before replacing project'
assert "selected=song.lanes[0]?.id" in loaded, 'new workspace must reselect a lane so instrument UI/playback source follows the new song'

# Render Singing should own custom-voice service startup instead of racing a separate activity request.
render=js[js.index("$('renderVocals').onclick"):js.index('function midiVoiceNoteName')]
assert "send('renderVocals'" in render, 'render singing request missing'
assert "voiceServiceActivity" not in render, 'render singing still races a background custom-voice warmup'

# Qwen model lookup must tolerate AI_ROOT changes by falling back to the shared/source runtime resolver.
assert 'AiRuntimeRoot.ResolveAsset(configured)' in manager, 'Qwen model resolver does not use shared/source fallback'
assert 'File.Exists(primary) || Directory.Exists(primary)' in manager, 'Qwen model resolver does not prefer the configured AI root first'
print('PASS vocal render startup, Qwen model fallback, and workspace playback replacement regression')
