from pathlib import Path
root=Path(__file__).resolve().parents[1]
app=(root/'src/wds.resone.ui/resources/web/app.js').read_text()
html=(root/'src/wds.resone.ui/resources/web/index.html').read_text()
css=(root/'src/wds.resone.ui/resources/web/style.css').read_text()
resone=(root/'src/wds.resone.ui/Resone.cpp').read_text()
audio=(root/'native/src/AudioEngine.cpp').read_text()
header=(root/'native/include/AudioEngine.hpp').read_text()
contracts=(root/'src/wds.resone.api/Contracts.cs').read_text()

# Voice selector stays in the header and the vocal panel only owns lyrics/rendering.
assert 'id="soundSourceLabel">Instrument</span><select id="instrument"' in html
vocal=html.split('id="vocalPanel"',1)[1].split('</div>\n    <div class="composerActions"',1)[0]
assert 'id="vocalLyrics"' in vocal and 'id="renderVocals"' in vocal
assert 'voiceSelect' not in vocal and 'New voice' not in vocal
assert "select.add(new Option('＋ New voice…', VOCAL_SOURCE_NEW))" in app

# Timeline seeking is explicit in both UI and native host.
assert 'function seekBeat(' in app and "send('seek',{project:clone(song)" in app
assert 'op == "seek"' in resone and 'startSeconds' in resone
assert 'void play(Song song, double startSeconds = 0.0, bool paused = false)' in header
assert 'Fast-forward the synth state' in audio

# Mixer-only controls do not route through changed()/stop.
assert 'function sendLiveMixer()' in app and "send('mixer'" in app
assert 'void AudioEngine::mixer' in audio and 'fluid_synth_cc' in (root/'native/include/SoundFont.hpp').read_text()
assert "gain.oninput = () =>" in app and 'sendLiveMixer();' in app

# Piano-roll edit interactions and preferences.
for token in ['selectedNote', "['Delete','Backspace']", 'ondblclick', 'snapQuarterBeat', 'laneResizeHandle',
              "resone.pianoRoll.zoomX", "resone.pianoRoll.zoomY", "resone.pianoRoll.laneHeights"]:
    assert token in app or token in css
assert "e.ctrlKey" in app and "e.altKey" in app
assert 'noteName(n.pitch)' in app

# Key metadata survives workspace serialization and drives the colored grid / tonic badge.
assert 'LaneKeyRegion' in contracts and 'KeyRegions' in contracts
assert 'rememberNotationKey' in app and 'laneKeyRegions' in app and 'laneKeyBadge' in app
assert 'tonic' in app and "key=" in app
print('piano-roll interaction regression: ok')
